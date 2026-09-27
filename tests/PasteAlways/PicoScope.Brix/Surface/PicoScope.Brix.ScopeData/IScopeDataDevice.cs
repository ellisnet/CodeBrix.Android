using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PicoScope.Brix.ScopeData.Model;

namespace PicoScope.Brix.ScopeData;

/// <summary>
/// A PicoScope 2000-series oscilloscope: everything the ps2000 driver exposes,
/// as a single abstraction that both a real device and a simulator implement.
/// </summary>
/// <remarks>
/// <para>
/// This interface is deliberately complete rather than minimal. It is intended
/// as reference material: if the driver can do something, it appears here, named
/// and documented, so that the way to do it can be looked up without reading
/// <c>ps2000.h</c>.
/// </para>
/// <para>
/// <b>Lifecycle.</b> A scope must be opened with <see cref="OpenScope"/> before
/// anything else will work, and closed with <see cref="CloseScope"/> when
/// finished. Only one process may hold a device open at a time -- if the
/// PicoScope desktop application is running, opening will fail. Implementations
/// also implement <see cref="IDisposable"/> as a safety net, because a handle
/// left open keeps the device locked until the process exits.
/// </para>
/// <para>
/// <b>Capabilities.</b> The driver serves every model in the series, so the
/// enumerations it defines are a union and any individual unit implements a
/// subset. Consult <see cref="Capabilities"/> rather than assuming: a
/// PicoScope 2204A has two channels, no external trigger, and accepts ranges
/// from 50 mV to 20 V only.
/// </para>
/// <para>
/// <b>Threading.</b> Implementations are not thread-safe. Drive one scope from
/// one thread at a time. Streaming callbacks arrive on the polling thread, not
/// the UI thread, so a view must marshal them itself.
/// </para>
/// </remarks>
public interface IScopeDataDevice : IDisposable
{
    // ---------------------------------------------------------------- identity

    /// <summary>
    /// A short human-readable name for this scope, suitable for a device picker.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this implementation talks to real hardware. False for a simulator.
    /// </summary>
    bool IsSimulated { get; }

    /// <summary>Whether the scope is currently open.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// What this device can do. Populated when the scope opens; null before that.
    /// </summary>
    ScopeCapabilities Capabilities { get; }

    /// <summary>
    /// What the device reports about itself. Meaningful only once open.
    /// </summary>
    UnitInfo UnitInfo { get; }

    // --------------------------------------------------------------- lifecycle

    /// <summary>
    /// Opens the scope, making it ready for use, and populates
    /// <see cref="Capabilities"/> and <see cref="UnitInfo"/>.
    /// </summary>
    /// <returns>True when the scope opened; false when no device was found.</returns>
    /// <remarks>
    /// Returns false rather than throwing when there is simply no device to
    /// open, since that is an ordinary condition an application handles by
    /// falling back to a simulator. It throws only when a device is present but
    /// something went genuinely wrong.
    /// </remarks>
    /// <exception cref="PicoScopeException">A device was present but could not be opened.</exception>
    bool OpenScope();

    /// <summary>
    /// Closes the scope, stopping any acquisition in progress and releasing the
    /// device so another process can use it. Safe to call when already closed.
    /// </summary>
    void CloseScope();

    /// <summary>
    /// Checks that the device is still present and responding.
    /// </summary>
    /// <returns>True when the device answered.</returns>
    bool Ping();

    /// <summary>
    /// Flashes the device's LED so it can be identified physically. The LED
    /// flashes until the next call to another driver function.
    /// </summary>
    /// <remarks>
    /// Only meaningful when <see cref="ScopeCapabilities.SupportsFlashLed"/> is
    /// set. Note that the related <c>set_led</c> and <c>set_light</c> driver
    /// calls exist but are rejected by a PicoScope 2204A, so they are not
    /// surfaced here.
    /// </remarks>
    void FlashLed();

    /// <summary>
    /// Reads the state of the device's front-panel button, on models that have
    /// one. A PicoScope 2204A does not, and always reports no press.
    /// </summary>
    /// <returns>0 for no press, 1 for a short press, 2 for a long press.</returns>
    int GetLastButtonPress();

    /// <summary>
    /// Reads one line of the device's self-reported information directly.
    /// </summary>
    /// <param name="line">Which line to read.</param>
    /// <returns>The line's text, or an empty string when unavailable.</returns>
    string GetUnitInfoLine(UnitInfoLine line);

    // ---------------------------------------------------------------- channels

    /// <summary>
    /// The current configuration of every channel the device has.
    /// </summary>
    IReadOnlyDictionary<ChannelId, ChannelSettings> Channels { get; }

    /// <summary>
    /// Configures one input channel.
    /// </summary>
    /// <param name="channel">The channel to configure.</param>
    /// <param name="settings">The coupling, range and enabled state to apply.</param>
    /// <exception cref="ScopeNotOpenException">The scope is not open.</exception>
    /// <exception cref="ScopeCapabilityException">The channel or range is not supported by this device.</exception>
    void SetChannel(ChannelId channel, ChannelSettings settings);

    /// <summary>
    /// The channels that are currently enabled.
    /// </summary>
    /// <returns>The enabled channels, in channel order.</returns>
    IReadOnlyList<ChannelId> GetEnabledChannels();

    // ---------------------------------------------------------------- timebase

    /// <summary>
    /// Asks the device what a particular timebase means for the current channel
    /// configuration.
    /// </summary>
    /// <param name="timebase">
    /// The timebase index. The sampling interval is <c>10 * 2^timebase</c>
    /// nanoseconds.
    /// </param>
    /// <param name="sampleCount">How many samples the capture would collect.</param>
    /// <param name="oversample">
    /// The oversampling factor. Above 1 the device averages that many readings
    /// per sample, trading rate for resolution. A PicoScope 2204A accepts up to 4.
    /// </param>
    /// <returns>
    /// The interval, timestamp units, and maximum block size, with
    /// <see cref="TimebaseInfo.IsValid"/> false when the device rejects the
    /// combination.
    /// </returns>
    /// <exception cref="ScopeNotOpenException">The scope is not open.</exception>
    TimebaseInfo GetTimebase(int timebase, int sampleCount, int oversample = 1);

    /// <summary>
    /// Finds the fastest timebase whose sampling interval is at least the one
    /// requested -- the usual way to turn "I want 1 microsecond per sample" into
    /// a timebase index.
    /// </summary>
    /// <param name="desiredIntervalNanoseconds">The sampling interval wanted, in nanoseconds.</param>
    /// <param name="sampleCount">How many samples the capture would collect.</param>
    /// <param name="oversample">The oversampling factor.</param>
    /// <returns>
    /// The closest valid timebase, or a result with
    /// <see cref="TimebaseInfo.IsValid"/> false when none fits.
    /// </returns>
    TimebaseInfo FindTimebase(double desiredIntervalNanoseconds, int sampleCount, int oversample = 1);

    // ----------------------------------------------------------------- trigger

    /// <summary>
    /// Sets a simple edge trigger.
    /// </summary>
    /// <param name="trigger">The source, level and edge to trigger on.</param>
    /// <exception cref="ScopeNotOpenException">The scope is not open.</exception>
    /// <exception cref="ScopeCapabilityException">The trigger source is not supported.</exception>
    void SetTrigger(TriggerSettings trigger);

    /// <summary>
    /// Disables triggering, so captures begin immediately.
    /// </summary>
    void DisableTrigger();

    /// <summary>
    /// Sets an advanced trigger: per-channel thresholds combined by a logical
    /// condition, optionally with window comparisons and hysteresis.
    /// </summary>
    /// <param name="settings">The advanced trigger configuration.</param>
    /// <exception cref="ScopeCapabilityException">The device does not support advanced triggering.</exception>
    void SetAdvancedTrigger(AdvancedTriggerSettings settings);

    /// <summary>
    /// Sets a pulse-width qualifier, requiring a trigger condition to persist
    /// for a particular length of time before it counts.
    /// </summary>
    /// <param name="qualifier">The pulse-width condition.</param>
    void SetPulseWidthQualifier(PulseWidthQualifier qualifier);

    // --------------------------------------------------------------------- ETS

    /// <summary>
    /// Configures equivalent-time sampling, which reconstructs a repetitive
    /// waveform at an effective resolution far beyond the real sampling rate.
    /// </summary>
    /// <param name="mode">The ETS mode, or <see cref="EtsMode.Off"/> to disable.</param>
    /// <param name="cycles">How many captures to interleave.</param>
    /// <param name="interleave">How many sub-sample offsets to step through.</param>
    /// <returns>The effective interval achieved, or a result reporting no support.</returns>
    /// <remarks>
    /// ETS only makes sense on a repeating signal; on a single-shot event it
    /// produces nonsense, because it is stitching together separate captures.
    /// </remarks>
    EtsResult SetEts(EtsMode mode, int cycles, int interleave);

    // ------------------------------------------------------------- block mode

    /// <summary>
    /// Captures one block of samples and waits for it to complete.
    /// </summary>
    /// <param name="sampleCount">How many samples to capture on each enabled channel.</param>
    /// <param name="timebase">The timebase index to sample at.</param>
    /// <param name="oversample">The oversampling factor.</param>
    /// <param name="includeTimestamps">
    /// Whether to also collect a per-sample timestamp array. Slightly slower, and
    /// usually unnecessary since the interval is constant and known.
    /// </param>
    /// <param name="cancellationToken">Cancels the wait for the capture to complete.</param>
    /// <returns>The captured block.</returns>
    /// <exception cref="ScopeNotOpenException">The scope is not open.</exception>
    /// <exception cref="PicoScopeException">The capture failed.</exception>
    Task<CaptureBlock> RunBlockAsync(
        int sampleCount,
        int timebase,
        int oversample = 1,
        bool includeTimestamps = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops any acquisition in progress, whether a block or a stream.
    /// </summary>
    /// <remarks>
    /// Always call this before reconfiguring the device. The driver will
    /// misbehave if channels or timebase are changed mid-capture.
    /// </remarks>
    void Stop();

    // --------------------------------------------------------------- streaming

    /// <summary>
    /// Whether a stream is currently running.
    /// </summary>
    bool IsStreaming { get; }

    /// <summary>
    /// Raised as batches of samples arrive from a running stream.
    /// </summary>
    /// <remarks>
    /// Raised on the polling thread, never the UI thread. A view must marshal
    /// to its own thread before touching UI state or a plot model.
    /// </remarks>
    event EventHandler<StreamingSamplesEventArgs> SamplesAvailable;

    /// <summary>
    /// Starts a streaming acquisition, which runs until
    /// <see cref="StopStreaming"/> is called or the sample limit is reached.
    /// </summary>
    /// <param name="settings">The streaming configuration.</param>
    /// <exception cref="ScopeNotOpenException">The scope is not open.</exception>
    /// <exception cref="ScopeCapabilityException">The requested streaming mode is not supported.</exception>
    void StartStreaming(StreamingSettings settings);

    /// <summary>
    /// Stops a running stream. Safe to call when not streaming.
    /// </summary>
    void StopStreaming();

    // -------------------------------------------------------- signal generator

    /// <summary>
    /// Configures the built-in signal generator and starts it.
    /// </summary>
    /// <param name="settings">The waveform, amplitude, frequency and sweep to produce.</param>
    /// <exception cref="ScopeCapabilityException">
    /// The device has no signal generator, or the frequency or amplitude exceeds
    /// <see cref="ScopeCapabilities.MaxSignalGeneratorFrequencyHz"/> or
    /// <see cref="ScopeCapabilities.MaxSignalGeneratorAmplitudeMicrovolts"/>.
    /// </exception>
    /// <remarks>
    /// <b>The device refuses generator calls while an acquisition is running</b> --
    /// both streaming and an in-flight block capture -- and reports no reason for
    /// the refusal. An implementation is therefore expected to stop the
    /// acquisition, apply the change, and restart it, so that callers do not have
    /// to. A running stream continues afterwards, with a brief gap in the data
    /// and its sample total carried across.
    /// </remarks>
    void SetSignalGenerator(SignalGeneratorSettings settings);

    /// <summary>
    /// Loads an arbitrary waveform into the generator and starts it.
    /// </summary>
    /// <param name="settings">The waveform samples and playback parameters.</param>
    /// <exception cref="ScopeCapabilityException">
    /// The device has no AWG, or the waveform exceeds
    /// <see cref="ScopeCapabilities.MaxArbitraryWaveformSize"/>.
    /// </exception>
    void SetArbitraryWaveform(ArbitraryWaveformSettings settings);

    /// <summary>
    /// Silences the signal generator by holding its output at 0 V DC.
    /// </summary>
    void StopSignalGenerator();
}
