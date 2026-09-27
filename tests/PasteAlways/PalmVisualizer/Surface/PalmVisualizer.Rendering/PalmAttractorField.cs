using System;
using System.Collections.Generic;

namespace PalmVisualizer.Rendering;

/// <summary>
/// The smoothing layer between palm tracking and the shader: holds up to
/// <see cref="MaxAttractors"/> attractor slots whose positions glide toward their targets
/// and whose strengths ease in when a palm appears and ease out when it closes or leaves.
/// That easing is what makes the visualization feel fluid and ethereal - tracking updates
/// arrive at webcam rate from a worker thread, while the renderer advances the field once
/// per rendered frame - and it is also what lets the visual melt back to its undisturbed
/// motion (every strength reaches zero) instead of snapping.
/// All members are thread-safe.
/// </summary>
public sealed class PalmAttractorField
{
    /// <summary>The most palms that can influence the visual at once.</summary>
    public const int MaxAttractors = 4;

    /// <summary>How quickly a slot's position glides toward its target, per second (higher = tighter chase).</summary>
    public const float PositionLerpPerSecond = 12f;

    /// <summary>How quickly a newly seen palm's influence swells in, per second.</summary>
    public const float StrengthAttackPerSecond = 4f;

    /// <summary>How quickly a closed or lost palm's influence melts away, per second.</summary>
    public const float StrengthReleasePerSecond = 2.5f;

    private const float FreeSlotStrength = 0.005f;

    private sealed class Slot
    {
        internal int Id;               //0 = free
        internal float TargetX;
        internal float TargetY;
        internal float TargetStrength;
        internal float X;
        internal float Y;
        internal float Strength;
    }

    private readonly object _lock = new object();
    private readonly Slot[] _slots;

    /// <summary>
    /// Initializes a new instance of the <see cref="PalmAttractorField"/> class with all
    /// slots free (no influence on the visual).
    /// </summary>
    public PalmAttractorField()
    {
        _slots = new Slot[MaxAttractors];
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i] = new Slot();
        }
    }

    /// <summary>
    /// Sets the palms currently attracting the visual. Palms keep their slot by id, so a
    /// moving hand drags its glow along; new ids claim free slots and fade in at the palm's
    /// position; ids no longer present begin fading out (a briefly closed hand that reopens
    /// with the same id re-attaches to its still-fading slot). When more palms arrive than
    /// there are free slots, the extras are ignored. Safe to call from any thread.
    /// </summary>
    /// <param name="palms">The attracting palms; empty (or null) releases them all.</param>
    public void SetTargets(IReadOnlyList<PalmAttractor> palms)
    {
        lock (_lock)
        {
            Span<bool> seen = stackalloc bool[MaxAttractors];

            if (palms != null)
            {
                foreach (PalmAttractor palm in palms)
                {
                    int slotIndex = FindSlot(palm.Id);
                    if (slotIndex < 0) { continue; }

                    Slot slot = _slots[slotIndex];
                    if (slot.Id != palm.Id)
                    {
                        //A freshly claimed slot starts AT the palm with no strength, so the
                        //  influence swells in place instead of sweeping over from stale state
                        slot.Id = palm.Id;
                        slot.X = palm.X;
                        slot.Y = palm.Y;
                        slot.Strength = 0f;
                    }
                    slot.TargetX = palm.X;
                    slot.TargetY = palm.Y;
                    slot.TargetStrength = 1f;
                    seen[slotIndex] = true;
                }
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!seen[i] && _slots[i].Id != 0)
                {
                    _slots[i].TargetStrength = 0f;
                }
            }
        }
    }

    /// <summary>
    /// Advances the field by one rendered frame: positions glide toward their targets and
    /// strengths ease toward full (attack) or nothing (release); fully released slots are
    /// freed. Safe to call from any thread - the renderer calls it once per frame.
    /// </summary>
    /// <param name="deltaSeconds">The seconds elapsed since the previous step.</param>
    public void Step(float deltaSeconds)
    {
        if (deltaSeconds <= 0f) { return; }

        //Exponential approach: framerate-independent, and never overshoots
        float positionBlend = 1f - (float)Math.Exp(-PositionLerpPerSecond * deltaSeconds);
        float attackBlend = 1f - (float)Math.Exp(-StrengthAttackPerSecond * deltaSeconds);
        float releaseBlend = 1f - (float)Math.Exp(-StrengthReleasePerSecond * deltaSeconds);

        lock (_lock)
        {
            foreach (Slot slot in _slots)
            {
                if (slot.Id == 0) { continue; }

                slot.X += (slot.TargetX - slot.X) * positionBlend;
                slot.Y += (slot.TargetY - slot.Y) * positionBlend;

                float strengthBlend = slot.TargetStrength > slot.Strength ? attackBlend : releaseBlend;
                slot.Strength += (slot.TargetStrength - slot.Strength) * strengthBlend;

                if (slot.TargetStrength <= 0f && slot.Strength < FreeSlotStrength)
                {
                    slot.Id = 0;
                    slot.Strength = 0f;
                }
            }
        }
    }

    /// <summary>
    /// Instantly releases every attractor (no fade-out). Called when the visualization is
    /// paused so a later resume starts from the undisturbed visual.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            foreach (Slot slot in _slots)
            {
                slot.Id = 0;
                slot.TargetStrength = 0f;
                slot.Strength = 0f;
            }
        }
    }

    /// <summary>
    /// Copies the field's current state for rendering: for each slot, three floats -
    /// normalized X, normalized Y, and strength 0..1 (0 for free slots).
    /// </summary>
    /// <param name="state">The destination, at least <see cref="MaxAttractors"/> * 3 floats long.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="state"/> is too short.</exception>
    public void CopyState(float[] state)
    {
        if (state == null || state.Length < MaxAttractors * 3)
        {
            throw new ArgumentException($"State buffer must hold at least {MaxAttractors * 3} floats.", nameof(state));
        }

        lock (_lock)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                state[i * 3] = _slots[i].X;
                state[(i * 3) + 1] = _slots[i].Y;
                state[(i * 3) + 2] = _slots[i].Strength;
            }
        }
    }

    //Returns the index of the slot already owned by this id, else a free slot, else -1.
    //  A slot mid-fade keeps its id, so a returning palm re-attaches instead of popping.
    private int FindSlot(int id)
    {
        var freeIndex = -1;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].Id == id) { return i; }
            if (freeIndex < 0 && _slots[i].Id == 0) { freeIndex = i; }
        }
        return freeIndex;
    }
}
