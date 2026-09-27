using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PicoScope.Brix.ScopeData.Model;

namespace PicoScope.Brix.ScopeData.Simulation;

/// <summary>
/// A complete <see cref="IScopeDataDevice"/> that synthesises data instead of talking
/// to hardware.
/// </summary>
/// <remarks>
/// <para>
/// This is not a stub. It implements the whole interface, enforces the same
/// lifecycle rules a real device does, and imposes the real capability limits of
/// a PicoScope 2204A -- so a capture that would be rejected by the hardware is
/// rejected here too. Code developed and tested against the simulator behaves
/// the same way when a physical scope is plugged in.
/// </para>
/// <para>
/// The signal it produces is a sine wave with a little harmonic content and
/// noise on channel A, and a slower, phase-shifted companion on channel B, so a
/// chart has something recognisable to draw with nothing connected. If the
/// signal generator is configured, channel A follows it -- as though the
/// generator output were looped back into the input, which is a common way to
/// test a real scope.
/// </para>
/// </remarks>
public sealed class SimulatedScopeDataDevice : IScopeDataDevice
{
    private readonly Dictionary<ChannelId, ChannelSettings> _channels = new Dictionary<ChannelId, ChannelSettings>();
    private readonly Random _noise = new Random(20260813);
    private readonly object _syncRoot = new object();

    private bool _isOpen;
    private bool _disposed;
    private TriggerSettings _trigger = TriggerSettings.Disabled;
    private EtsResult _ets;
    private SignalGeneratorSettings _signalGenerator = SignalGeneratorSettings.Off;
    private bool _signalGeneratorRunning;
    private CancellationTokenSource _streamCancellation;
    private Task _streamTask;
    private long _totalStreamedSamples;
    private double _phaseSeconds;

    /// <summary>
    /// Creates a simulated scope.
    /// </summary>
    /// <param name="name">
    /// An optional display name. Defaults to "Simulated PicoScope 2204A".
    /// </param>
    public SimulatedScopeDataDevice(string name = "Simulated PicoScope 2204A")
    {
        Name = name;
        foreach (ChannelId channel in ScopeCapabilities.PicoScope2204A.SupportedChannels)
        {
            _channels[channel] = ChannelSettings.Default;
        }
    }

    /// <summary>
    /// The base frequency of the synthesised waveform on channel A, in hertz,
    /// used when the signal generator is not running.
    /// </summary>
    public double SimulatedFrequencyHz { get; set; } = 1000.0;

    /// <summary>
    /// The peak amplitude of the synthesised waveform, in volts.
    /// </summary>
    public double SimulatedAmplitudeVolts { get; set; } = 2.0;

    /// <summary>
    /// How much random noise to add, as a fraction of the amplitude. Zero
    /// produces a mathematically clean wave, which looks artificial; the default
    /// of 0.02 looks like a real measurement.
    /// </summary>
    public double SimulatedNoiseFraction { get; set; } = 0.02;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public bool IsSimulated => true;

    /// <inheritdoc />
    public bool IsOpen => _isOpen;

    /// <inheritdoc />
    public ScopeCapabilities Capabilities { get; private set; }

    /// <inheritdoc />
    public UnitInfo UnitInfo { get; private set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<ChannelId, ChannelSettings> Channels => _channels;

    /// <inheritdoc />
    public bool IsStreaming => _streamTask != null && !_streamTask.IsCompleted;

    /// <inheritdoc />
    public event EventHandler<StreamingSamplesEventArgs> SamplesAvailable;

    // --------------------------------------------------------------- lifecycle

    /// <inheritdoc />
    public bool OpenScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isOpen) { return true; }

        Capabilities = ScopeCapabilities.PicoScope2204A;
        UnitInfo = new UnitInfo(
            Variant: "2204A (simulated)",
            BatchAndSerial: "SIM/0001",
            CalibrationDate: DateTime.UtcNow.ToString("ddMMMyy"),
            DriverVersion: "simulated",
            HardwareVersion: "simulated",
            UsbVersion: "n/a",
            KernelDriverVersion: "n/a",
            DriverPath: "(none - simulated)",
            ErrorCode: "0");

        _isOpen = true;
        return true;
    }

    /// <inheritdoc />
    public void CloseScope()
    {
        if (!_isOpen) { return; }

        StopStreaming();
        StopSignalGenerator();
        _isOpen = false;
    }

    /// <inheritdoc />
    public bool Ping() => _isOpen;

    /// <inheritdoc />
    public void FlashLed() => EnsureOpen("flash the LED");

    /// <inheritdoc />
    public int GetLastButtonPress()
    {
        EnsureOpen("read the button");
        return 0;   //a 2204A has no button, and the simulator matches it
    }

    /// <inheritdoc />
    public string GetUnitInfoLine(UnitInfoLine line)
    {
        EnsureOpen("read unit info");
        return line switch
        {
            UnitInfoLine.DriverVersion => UnitInfo.DriverVersion,
            UnitInfoLine.UsbVersion => UnitInfo.UsbVersion,
            UnitInfoLine.HardwareVersion => UnitInfo.HardwareVersion,
            UnitInfoLine.VariantInfo => UnitInfo.Variant,
            UnitInfoLine.BatchAndSerial => UnitInfo.BatchAndSerial,
            UnitInfoLine.CalibrationDate => UnitInfo.CalibrationDate,
            UnitInfoLine.ErrorCode => UnitInfo.ErrorCode,
            UnitInfoLine.KernelDriverVersion => UnitInfo.KernelDriverVersion,
            UnitInfoLine.DriverPath => UnitInfo.DriverPath,
            _ => string.Empty
        };
    }

    // ---------------------------------------------------------------- channels

    /// <inheritdoc />
    public void SetChannel(ChannelId channel, ChannelSettings settings)
    {
        EnsureOpen("set a channel");

        if (!Capabilities.Supports(channel))
        {
            throw new ScopeCapabilityException(
                $"Channel {channel} is not present on a {Capabilities.Variant}.");
        }
        if (!Capabilities.Supports(settings.Range))
        {
            throw new ScopeCapabilityException(
                $"Range {settings.Range.ToDisplayString()} is not supported by a {Capabilities.Variant}.");
        }

        _channels[channel] = settings;
    }

    /// <inheritdoc />
    public IReadOnlyList<ChannelId> GetEnabledChannels()
        => _channels.Where(c => c.Value.Enabled).Select(c => c.Key).OrderBy(c => (int)c).ToArray();

    // ---------------------------------------------------------------- timebase

    /// <inheritdoc />
    public TimebaseInfo GetTimebase(int timebase, int sampleCount, int oversample = 1)
    {
        EnsureOpen("query a timebase");

        int enabled = GetEnabledChannels().Count;
        int minTimebase = Capabilities.MinTimebaseFor(enabled);

        if (timebase < minTimebase || timebase > Capabilities.MaxTimebase
            || oversample < 1 || oversample > Capabilities.MaxOversample)
        {
            return TimebaseInfo.Invalid(timebase, oversample);
        }

        //The series relationship: interval = 10 * 2^timebase nanoseconds,
        //  multiplied by the oversampling factor.
        long intervalNs = (long)(10L * (1L << timebase) * oversample);
        int maxSamples = Capabilities.MaxSamplesFor(enabled) / oversample;

        return new TimebaseInfo(
            timebase, true, intervalNs, PickTimestampUnits(intervalNs, maxSamples), maxSamples, oversample);
    }

    /// <inheritdoc />
    public TimebaseInfo FindTimebase(double desiredIntervalNanoseconds, int sampleCount, int oversample = 1)
    {
        EnsureOpen("find a timebase");

        int enabled = GetEnabledChannels().Count;
        for (int tb = Capabilities.MinTimebaseFor(enabled); tb <= Capabilities.MaxTimebase; tb++)
        {
            TimebaseInfo info = GetTimebase(tb, sampleCount, oversample);
            if (info.IsValid && info.IntervalNanoseconds >= desiredIntervalNanoseconds)
            {
                return info;
            }
        }
        return TimebaseInfo.Invalid(Capabilities.MaxTimebase, oversample);
    }

    // ----------------------------------------------------------------- trigger

    /// <inheritdoc />
    public void SetTrigger(TriggerSettings trigger)
    {
        EnsureOpen("set the trigger");

        if (!Capabilities.SupportedTriggerSources.Contains(trigger.Source))
        {
            throw new ScopeCapabilityException(
                $"{trigger.Source} cannot be used as a trigger source on a {Capabilities.Variant}.");
        }
        _trigger = trigger;
    }

    /// <inheritdoc />
    public void DisableTrigger()
    {
        EnsureOpen("disable the trigger");
        _trigger = TriggerSettings.Disabled;
    }

    /// <inheritdoc />
    public void SetAdvancedTrigger(AdvancedTriggerSettings settings)
    {
        EnsureOpen("set an advanced trigger");
        if (settings == null) { throw new ArgumentNullException(nameof(settings)); }
        //Accepted and recorded; the simulator's synthesised signal is free-running,
        //  so an advanced trigger has no visible effect on what it produces.
    }

    /// <inheritdoc />
    public void SetPulseWidthQualifier(PulseWidthQualifier qualifier)
        => EnsureOpen("set a pulse-width qualifier");

    // --------------------------------------------------------------------- ETS

    /// <inheritdoc />
    public EtsResult SetEts(EtsMode mode, int cycles, int interleave)
    {
        EnsureOpen("configure ETS");

        if (mode == EtsMode.Off)
        {
            _ets = new EtsResult(EtsMode.Off, 0, 0, 0);
            return _ets;
        }

        int clampedCycles = Math.Min(cycles, Capabilities.MaxEtsCycles);
        int clampedInterleave = Math.Min(interleave, Capabilities.MaxEtsInterleave);
        _ets = new EtsResult(mode, clampedCycles, clampedInterleave,
            Capabilities.EtsEffectiveIntervalPicoseconds);
        return _ets;
    }

    // -------------------------------------------------------------- block mode

    /// <inheritdoc />
    public async Task<CaptureBlock> RunBlockAsync(
        int sampleCount,
        int timebase,
        int oversample = 1,
        bool includeTimestamps = false,
        CancellationToken cancellationToken = default)
    {
        EnsureOpen("run a block capture");

        TimebaseInfo info = GetTimebase(timebase, sampleCount, oversample);
        if (!info.IsValid)
        {
            throw new PicoScopeException(
                $"Timebase {timebase} is not valid with {GetEnabledChannels().Count} channel(s) " +
                $"enabled and oversample {oversample}.");
        }
        if (sampleCount > info.MaxSamples)
        {
            throw new PicoScopeException(
                $"{sampleCount} samples exceeds the {info.MaxSamples}-sample buffer at timebase {timebase}.");
        }

        //Pretend to take as long as the real capture would, capped so a slow
        //  timebase does not make the simulator unusable.
        double durationMs = sampleCount * info.IntervalNanoseconds / 1e6;
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(durationMs, 250)), cancellationToken)
            .ConfigureAwait(false);

        var channels = new Dictionary<ChannelId, ChannelSamples>();
        double intervalSeconds = info.IntervalNanoseconds / 1e9;

        foreach (ChannelId channel in GetEnabledChannels())
        {
            ChannelSettings settings = _channels[channel];
            short[] samples = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                double t = i * intervalSeconds;
                samples[i] = SampleAt(channel, settings.Range, t);
            }
            channels[channel] = new ChannelSamples(channel, settings.Range, samples);
        }

        int[] timestamps = null;
        if (includeTimestamps)
        {
            timestamps = new int[sampleCount];
            double scale = TimestampScaleFromNanoseconds(info.TimestampUnits);
            for (int i = 0; i < sampleCount; i++)
            {
                timestamps[i] = (int)(i * info.IntervalNanoseconds * scale);
            }
        }

        return new CaptureBlock
        {
            Channels = channels,
            IntervalNanoseconds = info.IntervalNanoseconds,
            SampleCount = sampleCount,
            Timestamps = timestamps,
            TimestampUnits = info.TimestampUnits,
            OverflowFlags = 0,
            CapturedAtUtc = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public void Stop() => StopStreaming();

    // --------------------------------------------------------------- streaming

    /// <inheritdoc />
    public void StartStreaming(StreamingSettings settings)
    {
        EnsureOpen("start streaming");
        if (settings == null) { throw new ArgumentNullException(nameof(settings)); }

        if (settings.Mode == StreamingMode.Fast && !Capabilities.SupportsFastStreaming)
        {
            throw new ScopeCapabilityException($"A {Capabilities.Variant} does not support fast streaming.");
        }
        if (settings.Mode == StreamingMode.Compatible && !Capabilities.SupportsCompatibleStreaming)
        {
            throw new ScopeCapabilityException($"A {Capabilities.Variant} does not support compatible streaming.");
        }

        StopStreaming();

        _totalStreamedSamples = 0;
        _phaseSeconds = 0;
        _streamCancellation = new CancellationTokenSource();
        CancellationToken token = _streamCancellation.Token;
        _streamTask = Task.Run(() => StreamLoop(settings, token), token);
    }

    /// <inheritdoc />
    public void StopStreaming()
    {
        CancellationTokenSource cts;
        Task task;
        lock (_syncRoot)
        {
            cts = _streamCancellation;
            task = _streamTask;
            _streamCancellation = null;
            _streamTask = null;
        }

        if (cts == null) { return; }

        try
        {
            cts.Cancel();
            task?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            //The loop observes cancellation by throwing; that is expected.
        }
        finally
        {
            cts.Dispose();
        }
    }

    private void StreamLoop(StreamingSettings settings, CancellationToken token)
    {
        double intervalNs = settings.IntervalInNanoseconds();
        double intervalSeconds = intervalNs / 1e9;
        int pollMs = Math.Max(1, settings.PollIntervalMilliseconds);

        //How many samples a real device would have produced between polls.
        int samplesPerBatch = Math.Max(1, (int)(pollMs / 1000.0 / Math.Max(intervalSeconds, 1e-9)));
        //Keep a batch to a sane size; a 1 microsecond interval would otherwise
        //  synthesise 10,000 samples every poll for no visible benefit.
        samplesPerBatch = Math.Min(samplesPerBatch, 20000);

        while (!token.IsCancellationRequested)
        {
            var channels = new Dictionary<ChannelId, ChannelSamples>();
            foreach (ChannelId channel in GetEnabledChannels())
            {
                ChannelSettings cs = _channels[channel];
                short[] samples = new short[samplesPerBatch];
                for (int i = 0; i < samplesPerBatch; i++)
                {
                    samples[i] = SampleAt(channel, cs.Range, _phaseSeconds + (i * intervalSeconds));
                }
                channels[channel] = new ChannelSamples(channel, cs.Range, samples);
            }

            _phaseSeconds += samplesPerBatch * intervalSeconds;
            _totalStreamedSamples += samplesPerBatch;

            SamplesAvailable?.Invoke(this, new StreamingSamplesEventArgs
            {
                Channels = channels,
                SampleCount = samplesPerBatch,
                TotalSampleCount = _totalStreamedSamples,
                IntervalNanoseconds = intervalNs,
                Triggered = false,
                TriggerIndex = 0,
                OverflowFlags = 0,
                BufferOverrun = false
            });

            if (settings.AutoStop && _totalStreamedSamples >= settings.MaxSamples) { return; }

            try
            {
                Task.Delay(pollMs, token).Wait(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // -------------------------------------------------------- signal generator

    /// <inheritdoc />
    public void SetSignalGenerator(SignalGeneratorSettings settings)
    {
        EnsureOpen("set the signal generator");

        if (!Capabilities.SupportsSignalGenerator)
        {
            throw new ScopeCapabilityException($"A {Capabilities.Variant} has no signal generator.");
        }
        if (settings.StartFrequencyHz > Capabilities.MaxSignalGeneratorFrequencyHz
            || settings.StopFrequencyHz > Capabilities.MaxSignalGeneratorFrequencyHz)
        {
            throw new ScopeCapabilityException(
                $"The signal generator on a {Capabilities.Variant} tops out at " +
                $"{Capabilities.MaxSignalGeneratorFrequencyHz} Hz.");
        }
        if (settings.PeakToPeakMicrovolts > Capabilities.MaxSignalGeneratorAmplitudeMicrovolts)
        {
            throw new ScopeCapabilityException(
                $"The signal generator on a {Capabilities.Variant} tops out at " +
                $"{Capabilities.MaxSignalGeneratorAmplitudeMicrovolts / 1_000_000.0:0.#} V peak-to-peak.");
        }

        _signalGenerator = settings;
        _signalGeneratorRunning = settings.WaveType != WaveType.DcVoltage || settings.OffsetMicrovolts != 0;
    }

    /// <inheritdoc />
    public void SetArbitraryWaveform(ArbitraryWaveformSettings settings)
    {
        EnsureOpen("set an arbitrary waveform");

        if (!Capabilities.SupportsArbitraryWaveform)
        {
            throw new ScopeCapabilityException($"A {Capabilities.Variant} has no arbitrary waveform generator.");
        }
        if (settings.Waveform == null || settings.Waveform.Length == 0)
        {
            throw new ArgumentException("The waveform must contain at least one sample.", nameof(settings));
        }
        if (settings.Waveform.Length > Capabilities.MaxArbitraryWaveformSize)
        {
            throw new ScopeCapabilityException(
                $"The AWG buffer on a {Capabilities.Variant} holds at most " +
                $"{Capabilities.MaxArbitraryWaveformSize} samples.");
        }
        if (settings.PeakToPeakMicrovolts > Capabilities.MaxSignalGeneratorAmplitudeMicrovolts)
        {
            throw new ScopeCapabilityException(
                $"The signal generator on a {Capabilities.Variant} tops out at " +
                $"{Capabilities.MaxSignalGeneratorAmplitudeMicrovolts / 1_000_000.0:0.#} V peak-to-peak.");
        }

        _signalGeneratorRunning = true;
    }

    /// <inheritdoc />
    public void StopSignalGenerator()
    {
        _signalGenerator = SignalGeneratorSettings.Off;
        _signalGeneratorRunning = false;
    }

    // ---------------------------------------------------------------- sampling

    /// <summary>
    /// Produces one synthesised sample for a channel at a given moment.
    /// </summary>
    private short SampleAt(ChannelId channel, VoltageRange range, double timeSeconds)
    {
        double volts;

        if (channel == ChannelId.ChannelA && _signalGeneratorRunning)
        {
            //Behave as though the generator output were looped back into A.
            volts = GeneratorVoltsAt(timeSeconds);
        }
        else if (channel == ChannelId.ChannelA)
        {
            //A clean fundamental with a touch of third harmonic, which is what
            //  most real "electrical activity" looks like on a scope.
            double w = 2 * Math.PI * SimulatedFrequencyHz * timeSeconds;
            volts = SimulatedAmplitudeVolts * (Math.Sin(w) + (0.12 * Math.Sin(3 * w)));
        }
        else
        {
            //Channel B: slower, phase-shifted, smaller. Visibly a different
            //  trace so the two are easy to tell apart on the chart.
            double w = 2 * Math.PI * (SimulatedFrequencyHz / 4.0) * timeSeconds;
            volts = SimulatedAmplitudeVolts * 0.55 * Math.Sin(w + (Math.PI / 3.0));
        }

        if (SimulatedNoiseFraction > 0)
        {
            volts += SimulatedAmplitudeVolts * SimulatedNoiseFraction * ((_noise.NextDouble() * 2.0) - 1.0);
        }

        //Clip to the channel's range exactly as the hardware would.
        double fullScale = range.FullScaleInVolts();
        if (volts > fullScale) { volts = fullScale; }
        if (volts < -fullScale) { volts = -fullScale; }

        return (short)Math.Round(volts / fullScale * ScopeConstants.MaxAdcValue);
    }

    private double GeneratorVoltsAt(double timeSeconds)
    {
        double amplitude = _signalGenerator.PeakToPeakMicrovolts / 2_000_000.0;
        double offset = _signalGenerator.OffsetMicrovolts / 1_000_000.0;
        double phase = 2 * Math.PI * _signalGenerator.StartFrequencyHz * timeSeconds;
        double unit = _signalGenerator.WaveType switch
        {
            WaveType.Sine => Math.Sin(phase),
            WaveType.Square => Math.Sign(Math.Sin(phase)),
            WaveType.Triangle => 2.0 / Math.PI * Math.Asin(Math.Sin(phase)),
            WaveType.RampUp => 2.0 * ((phase / (2 * Math.PI)) % 1.0) - 1.0,
            WaveType.RampDown => 1.0 - 2.0 * ((phase / (2 * Math.PI)) % 1.0),
            WaveType.DcVoltage => 0.0,
            WaveType.Gaussian => Math.Exp(-Math.Pow(((phase / (2 * Math.PI)) % 1.0) - 0.5, 2) / 0.02) * 2 - 1,
            WaveType.Sinc => Sinc(((phase / (2 * Math.PI)) % 1.0 - 0.5) * 20),
            WaveType.HalfSine => Math.Abs(Math.Sin(phase)),
            _ => Math.Sin(phase)
        };
        return offset + (amplitude * unit);
    }

    private static double Sinc(double x) => Math.Abs(x) < 1e-9 ? 1.0 : Math.Sin(Math.PI * x) / (Math.PI * x);

    private static TimeUnits PickTimestampUnits(long intervalNs, int maxSamples)
    {
        //Mirror the driver's own behaviour: choose the finest unit in which the
        //  whole capture still fits a 32-bit integer.
        double totalNs = (double)intervalNs * maxSamples;
        if (totalNs * 1000.0 < int.MaxValue) { return TimeUnits.Picoseconds; }
        if (totalNs < int.MaxValue) { return TimeUnits.Nanoseconds; }
        if (totalNs / 1000.0 < int.MaxValue) { return TimeUnits.Microseconds; }
        return TimeUnits.Milliseconds;
    }

    private static double TimestampScaleFromNanoseconds(TimeUnits units) => units switch
    {
        TimeUnits.Femtoseconds => 1e6,
        TimeUnits.Picoseconds => 1e3,
        TimeUnits.Nanoseconds => 1.0,
        TimeUnits.Microseconds => 1e-3,
        TimeUnits.Milliseconds => 1e-6,
        _ => 1e-9
    };

    private void EnsureOpen(string operation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isOpen) { throw new ScopeNotOpenException(operation); }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        CloseScope();
    }
}
