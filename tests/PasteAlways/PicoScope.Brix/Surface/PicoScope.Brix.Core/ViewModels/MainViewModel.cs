using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Simple;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PicoScope.Brix.Charting;
using PicoScope.Brix.ScopeData;
using PicoScope.Brix.ScopeData.Model;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace PicoScope.Brix.ViewModels;

/// <summary>
/// Drives the PicoScope.Brix sample: finds a scope, configures it, runs captures and
/// streams, and keeps a <see cref="ScopePlot"/> up to date for the view to draw.
/// </summary>
/// <remarks>
/// <para>
/// <b>Devices.</b> This view model never names a device implementation. Each
/// platform head registers what it can offer with
/// <see cref="ScopeDeviceFinder"/> before the application starts, and
/// <see cref="InitializeAsync"/> asks the finder for the best one -- real
/// hardware when it opens, the simulator otherwise.
/// </para>
/// <para>
/// <b>Threading.</b> Streaming batches arrive on the driver's polling thread.
/// <see cref="ScopePlot"/> mutates its model under the model's sync root, and
/// the PlotterView control renders under that same lock, so batches are applied
/// on the thread they arrive on with no dispatcher hop. Every bindable property
/// setter passes <c>notifyOnMainThread: true</c>, because a PropertyChanged
/// raised off the UI thread would break XAML binding.
/// </para>
/// </remarks>
[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel
{
    private ILogger _log = NullLogger.Instance;
    private IScopeDataDevice _scope;
    private bool _loggedFirstBatch;

    /// <summary>
    /// Creates the view model. The view calls <see cref="InitializeAsync"/> once
    /// it has loaded.
    /// </summary>
    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        _log = LogExtensionPoint.AmbientLoggerFactory.CreateLogger<MainViewModel>();
    }

    /// <summary>
    /// The chart. Its <see cref="ScopePlot.Model"/> is what the view renders.
    /// </summary>
    public ScopePlot Plot { get; } = new ScopePlot();

    /// <summary>The voltage ranges the attached scope accepts.</summary>
    public ObservableCollection<VoltageRangeOption> AvailableRanges { get; } = new ObservableCollection<VoltageRangeOption>();

    /// <summary>The waveforms the attached scope's generator can produce.</summary>
    public ObservableCollection<WaveType> AvailableWaveTypes { get; } = new ObservableCollection<WaveType>();

    #region | Bindable properties |

    private string _statusText = "Starting up...";

    /// <summary>A one-line status message for the view.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value ?? string.Empty, notifyOnMainThread: true);
    }

    private string _deviceText = string.Empty;

    /// <summary>A description of the attached device.</summary>
    public string DeviceText
    {
        get => _deviceText;
        private set => SetProperty(ref _deviceText, value ?? string.Empty, notifyOnMainThread: true);
    }

    private string _capabilityText = string.Empty;

    /// <summary>A summary of what the attached device can do.</summary>
    public string CapabilityText
    {
        get => _capabilityText;
        private set => SetProperty(ref _capabilityText, value ?? string.Empty, notifyOnMainThread: true);
    }

    private bool _isSimulated = true;

    /// <summary>Whether the active scope is a simulator rather than real hardware.</summary>
    [AffectsProperties(nameof(SimulatedBadge))]
    public bool IsSimulated
    {
        get => _isSimulated;
        private set => SetProperty(ref _isSimulated, value, notifyOnMainThread: true);
    }

    /// <summary>
    /// A badge for the status bar: "SIMULATED" when no real device is attached,
    /// otherwise empty.
    /// </summary>
    /// <remarks>
    /// Deliberately a string rather than a bool bound to Visibility: an empty
    /// string renders as nothing with no converter or trigger.
    /// </remarks>
    public string SimulatedBadge => _isSimulated ? "SIMULATED" : string.Empty;

    private bool _isReady;

    /// <summary>Whether a scope is open and ready.</summary>
    [AffectsCommands(nameof(CaptureCommand), nameof(StartStreamingCommand),
        nameof(StopStreamingCommand), nameof(SignalGeneratorCommand), nameof(FlashLedCommand))]
    public bool IsReady
    {
        get => _isReady;
        private set => SetProperty(ref _isReady, value, notifyOnMainThread: true);
    }

    private bool _isStreaming;

    /// <summary>Whether a stream is currently running.</summary>
    [AffectsCommands(nameof(CaptureCommand), nameof(StartStreamingCommand), nameof(StopStreamingCommand))]
    public bool IsStreaming
    {
        get => _isStreaming;
        private set => SetProperty(ref _isStreaming, value, notifyOnMainThread: true);
    }

    private bool _showChannelA = true;

    /// <summary>
    /// Whether channel A's trace is drawn. Hiding a channel affects the chart
    /// only -- it keeps acquiring, so unhiding reveals the history that arrived
    /// meanwhile.
    /// </summary>
    public bool ShowChannelA
    {
        get => _showChannelA;
        set
        {
            if (_showChannelA == value) { return; }
            SetProperty(ref _showChannelA, value, notifyOnMainThread: true);
            Plot.SetChannelVisible(ChannelId.ChannelA, value);
        }
    }

    private bool _showChannelB = true;

    /// <summary>Whether channel B's trace is drawn.</summary>
    public bool ShowChannelB
    {
        get => _showChannelB;
        set
        {
            if (_showChannelB == value) { return; }
            SetProperty(ref _showChannelB, value, notifyOnMainThread: true);
            Plot.SetChannelVisible(ChannelId.ChannelB, value);
        }
    }

    private VoltageRangeOption _selectedRangeOption;

    /// <summary>The combo-box item for the input range applied to both channels.</summary>
    public VoltageRangeOption SelectedRangeOption
    {
        get => _selectedRangeOption;
        set
        {
            //A combo box clears its selection while its items are replaced;
            //  a null here is that transient, not a choice.
            if (value == null || ReferenceEquals(_selectedRangeOption, value)) { return; }
            SetProperty(ref _selectedRangeOption, value, notifyOnMainThread: true);
            ApplyChannelSettings();
        }
    }

    /// <summary>The input range applied to both channels.</summary>
    public VoltageRange SelectedRange => _selectedRangeOption?.Range ?? VoltageRange.Range5V;

    private WaveType _selectedWaveType = WaveType.Sine;

    /// <summary>The waveform the signal generator produces.</summary>
    public WaveType SelectedWaveType
    {
        get => _selectedWaveType;
        set => SetEnumProperty(ref _selectedWaveType, value, notifyOnMainThread: true);
    }

    private string _generatorFrequencyText = "5000";

    /// <summary>
    /// The signal generator frequency as typed, in hertz. Parsed when the
    /// generator is toggled on, so a half-typed value never fights the text box.
    /// </summary>
    public string GeneratorFrequencyText
    {
        get => _generatorFrequencyText;
        set => SetProperty(ref _generatorFrequencyText, value ?? string.Empty, notifyOnMainThread: true);
    }

    private bool _generatorRunning;

    /// <summary>Whether the signal generator is currently driving its output.</summary>
    public bool GeneratorRunning
    {
        get => _generatorRunning;
        private set => SetProperty(ref _generatorRunning, value, notifyOnMainThread: true);
    }

    #endregion

    /// <summary>
    /// Finds the best available scope, configures it, and starts streaming so the
    /// chart is live immediately.
    /// </summary>
    /// <returns>A task that completes once the scope is ready.</returns>
    /// <remarks>
    /// <para>
    /// Each head registers its own implementations with
    /// <see cref="ScopeDeviceFinder"/> before the application starts, which is
    /// what keeps this view model free of any device-specific reference.
    /// </para>
    /// <para>
    /// The view starts this from an <c>async void</c> load handler, so nothing
    /// may escape it. Opening and interrogating an instrument has more failure
    /// modes than any one caller can enumerate, and every one of them becomes a
    /// line of status text here, the way the rest of this view model reports.
    /// </para>
    /// </remarks>
    public async Task InitializeAsync()
    {
        try
        {
            await StartUpAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Scope startup failed.");
            SetStatus("Could not start: " + ex.Message);
        }
    }

    private async Task StartUpAsync()
    {
        SetStatus("Looking for a scope...");

        //Opening a real device takes a second or two of USB traffic, which the
        //  UI thread should not sit through.
        _scope = await Task.Run(() => ScopeDeviceFinder.FindBest()).ConfigureAwait(false);

        if (_scope == null)
        {
            SetStatus("No scope available -- nothing registered with ScopeDeviceFinder.");
            return;
        }

        IsSimulated = _scope.IsSimulated;
        DeviceText = _scope.UnitInfo.ToString();

        ScopeCapabilities c = _scope.Capabilities;
        CapabilityText =
            $"{c.ChannelCount} channels | " +
            $"{c.SupportedRanges.Count} ranges ({c.SupportedRanges[0].ToDisplayString()} to " +
            $"{c.SupportedRanges[^1].ToDisplayString()}) | " +
            $"timebase {c.MinTimebaseSingleChannel}-{c.MaxTimebase} | " +
            $"{(c.SupportsFastStreaming ? "fast streaming" : "no fast streaming")} | " +
            $"{(c.SupportsEts ? $"ETS {c.EtsEffectiveIntervalPicoseconds} ps" : "no ETS")} | " +
            $"{(c.SupportsSignalGenerator ? $"siggen to {c.MaxSignalGeneratorFrequencyHz / 1000:0} kHz" : "no siggen")}";

        _log.LogInformation("Scope: {Device} via {Implementation}", DeviceText, _scope.Name);
        _log.LogInformation("Driver: {DriverPath} (driver {DriverVersion}, USB {UsbVersion}, hardware {HardwareVersion})",
            _scope.UnitInfo.DriverPath, _scope.UnitInfo.DriverVersion, _scope.UnitInfo.UsbVersion, _scope.UnitInfo.HardwareVersion);
        _log.LogInformation("Capabilities: {Capabilities}", CapabilityText);

        //The selections are fixed here, on this thread, so the channel settings
        //  below can use them at once; the bound collections are filled on the
        //  UI thread, and the selections re-announced only after the lists
        //  hold them -- a combo box cannot show an item it does not yet have.
        VoltageRangeOption[] rangeOptions = c.SupportedRanges.Select(r => new VoltageRangeOption(r)).ToArray();
        VoltageRange preferredRange = c.Supports(VoltageRange.Range5V) ? VoltageRange.Range5V : c.SupportedRanges[^1];
        _selectedRangeOption = rangeOptions.First(o => o.Range == preferredRange);
        //A model with no signal generator - a 2104 or a 2105 - reports no
        //  waveforms at all, so there is nothing to move the selection to and
        //  the default simply stays.
        if (c.SupportedWaveTypes.Count > 0 && !c.SupportedWaveTypes.Contains(_selectedWaveType))
        {
            _selectedWaveType = c.SupportedWaveTypes[0];
        }

        InvokeOnMainThread(() =>
        {
            AvailableRanges.Clear();
            foreach (VoltageRangeOption option in rangeOptions) { AvailableRanges.Add(option); }
            NotifyPropertyChanged(nameof(SelectedRangeOption));

            AvailableWaveTypes.Clear();
            foreach (WaveType wave in c.SupportedWaveTypes) { AvailableWaveTypes.Add(wave); }
            NotifyPropertyChanged(nameof(SelectedWaveType));
        });

        ApplyChannelSettings();

        _scope.SamplesAvailable += OnSamplesAvailable;
        IsReady = true;
        SetStatus(_scope.IsSimulated
            ? "Simulated scope -- no hardware needed."
            : $"Connected to {_scope.Name}.");

        //Show something immediately rather than an empty chart.
        await CaptureOnceAsync().ConfigureAwait(false);
        DoStartStreaming();
    }

    private void ApplyChannelSettings()
    {
        if (_scope == null || !_scope.IsOpen) { return; }

        try
        {
            bool wasStreaming = _scope.IsStreaming;
            if (wasStreaming) { _scope.StopStreaming(); }

            foreach (ChannelId channel in _scope.Capabilities.SupportedChannels)
            {
                _scope.SetChannel(channel, new ChannelSettings(true, Coupling.Dc, SelectedRange));
            }

            if (wasStreaming) { DoStartStreaming(); }
        }
        catch (PicoScopeException ex)
        {
            //One of the two callers is the range picker's setter, so a range the
            //  device refuses must not throw out of a bound property. The stream
            //  may have been stopped before the refusal, so the flag the buttons
            //  read is re-read from the device rather than assumed.
            IsStreaming = _scope.IsStreaming;
            SetStatus("Could not apply the channel settings: " + ex.Message);
        }
    }

    private void OnSamplesAvailable(object sender, StreamingSamplesEventArgs e)
    {
        //Arrives on the polling thread. ScopePlot locks the model while it
        //  mutates and the plot view renders under the same lock, so the batch
        //  is applied right here.
        Plot.AppendStreaming(e);

        if (!_loggedFirstBatch)
        {
            _loggedFirstBatch = true;
            _log.LogInformation("Streaming: first batch of {Count} samples at {Interval} ns/sample.",
                e.SampleCount, e.IntervalNanoseconds);
        }
    }

    #region | Commands and their implementations |

    private SimpleCommand _captureCommand;

    /// <summary>Captures a single block and displays it.</summary>
    /// <remarks>
    /// The cast picks the asynchronous overload. Without it a task-returning
    /// body binds to the synchronous one, and the command reports itself
    /// finished while the capture is still running unobserved.
    /// </remarks>
    public SimpleCommand CaptureCommand =>
        _captureCommand ??= new SimpleCommand(() => IsReady && !IsStreaming, (Func<Task>)CaptureOnceAsync);

    private async Task CaptureOnceAsync()
    {
        if (_scope == null || !_scope.IsOpen) { return; }

        try
        {
            //Roughly one microsecond per sample gives a couple of milliseconds
            //  across the block, which suits a 1-10 kHz signal nicely.
            TimebaseInfo tb = _scope.FindTimebase(1000, 2000);
            if (!tb.IsValid)
            {
                SetStatus("No usable timebase for a single capture.");
                return;
            }

            int samples = Math.Min(2000, tb.MaxSamples);
            CaptureBlock block = await _scope.RunBlockAsync(samples, tb.Timebase).ConfigureAwait(false);

            Plot.ShowBlock(block);
            SetStatus($"Captured {block.SampleCount} samples at {tb.SampleRateHz / 1000:0.#} kS/s.");
        }
        catch (PicoScopeException ex)
        {
            SetStatus("Capture failed: " + ex.Message);
        }
        catch (Exception ex)
        {
            //The command runs this body through an async void execute path, so
            //  anything the device layer did not turn into a PicoScopeException
            //  still has to stop here.
            _log.LogError(ex, "Unexpected capture failure.");
            SetStatus("Capture failed: " + ex.Message);
        }
    }

    private SimpleCommand _startStreamingCommand;

    /// <summary>Starts a live stream.</summary>
    public SimpleCommand StartStreamingCommand =>
        _startStreamingCommand ??= new SimpleCommand(() => IsReady && !IsStreaming, DoStartStreaming);

    private void DoStartStreaming()
    {
        if (_scope == null || !_scope.IsOpen || _scope.IsStreaming) { return; }

        try
        {
            Plot.Clear();
            _loggedFirstBatch = false;
            _scope.StartStreaming(new StreamingSettings
            {
                Mode = _scope.Capabilities.SupportsFastStreaming
                    ? StreamingMode.Fast
                    : StreamingMode.Compatible,
                SampleInterval = 100,
                IntervalUnits = TimeUnits.Microseconds,
                MaxSamples = 10_000_000,
                AutoStop = false,
                OverviewBufferSize = 50000,
                PollIntervalMilliseconds = 20
            });

            IsStreaming = true;
            SetStatus("Streaming.");
        }
        catch (PicoScopeException ex)
        {
            SetStatus("Could not start streaming: " + ex.Message);
        }
    }

    private SimpleCommand _stopStreamingCommand;

    /// <summary>Stops a running stream.</summary>
    public SimpleCommand StopStreamingCommand =>
        _stopStreamingCommand ??= new SimpleCommand(() => IsStreaming, DoStopStreaming);

    private void DoStopStreaming()
    {
        if (_scope == null) { return; }

        _scope.StopStreaming();
        IsStreaming = false;
        SetStatus("Stopped.");
    }

    private SimpleCommand _signalGeneratorCommand;

    /// <summary>Turns the signal generator on or off.</summary>
    public SimpleCommand SignalGeneratorCommand =>
        _signalGeneratorCommand ??= new SimpleCommand(
            () => IsReady && _scope != null && _scope.Capabilities.SupportsSignalGenerator,
            DoToggleSignalGenerator);

    private void DoToggleSignalGenerator()
    {
        if (_scope == null || !_scope.IsOpen) { return; }

        try
        {
            if (GeneratorRunning)
            {
                _scope.StopSignalGenerator();
                GeneratorRunning = false;
                SetStatus("Signal generator off.");
                return;
            }

            if (!double.TryParse(GeneratorFrequencyText, NumberStyles.Float, CultureInfo.CurrentCulture, out double frequencyHz)
                || frequencyHz < 0)
            {
                SetStatus("Enter a generator frequency in hertz.");
                return;
            }

            _scope.SetSignalGenerator(SignalGeneratorSettings.Tone(SelectedWaveType, frequencyHz, 2.0));
            GeneratorRunning = true;
            SetStatus($"Signal generator: {SelectedWaveType} at {frequencyHz:N0} Hz, 2 Vpp. " +
                      (IsSimulated
                          ? "Channel A follows it."
                          : "Loop the generator output into Channel A to see it."));
        }
        catch (PicoScopeException ex)
        {
            SetStatus("Signal generator error: " + ex.Message);
        }
    }

    private SimpleCommand _flashLedCommand;

    /// <summary>Flashes the device LED so the physical unit can be identified.</summary>
    public SimpleCommand FlashLedCommand =>
        _flashLedCommand ??= new SimpleCommand(
            () => IsReady && _scope != null && _scope.Capabilities.SupportsFlashLed,
            DoFlashLed);

    private void DoFlashLed()
    {
        if (_scope == null) { return; }

        try
        {
            _scope.FlashLed();
            SetStatus("Flashing the device LED.");
        }
        catch (PicoScopeException ex)
        {
            SetStatus("Could not flash the LED: " + ex.Message);
        }
    }

    #endregion

    /// <summary>
    /// Stops acquisition and releases the scope. Call from the view's unload or
    /// window-closed handler -- a handle left open keeps the device locked
    /// against every other process until this one exits.
    /// </summary>
    public void Shutdown()
    {
        if (_scope == null) { return; }

        _scope.SamplesAvailable -= OnSamplesAvailable;
        _scope.StopStreaming();
        _scope.StopSignalGenerator();
        ScopeDeviceFinder.Reset();
        _scope = null;
        IsReady = false;
        IsStreaming = false;
        _log.LogInformation("Scope released.");
    }

    private void SetStatus(string text)
    {
        StatusText = text;
        _log.LogInformation("{Status}", text);
    }
}
