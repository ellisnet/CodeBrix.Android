using System;
using System.Collections.Generic;
using CodeBrix.Plotter;
using CodeBrix.Plotter.Axes;
using CodeBrix.Plotter.Legends;
using CodeBrix.Plotter.Series;
using PicoScope.Brix.ScopeData.Model;

namespace PicoScope.Brix.Charting;

/// <summary>
/// Turns scope data into a CodeBrix.Plotter <see cref="PlotModel"/>, and keeps
/// that model up to date as new samples arrive.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of the charting story for the sample. It owns one
/// <see cref="LineSeries"/> per channel and a rolling window of points, and it
/// understands both kinds of input: a one-shot <see cref="CaptureBlock"/> from
/// block mode, and a continuous feed of
/// <see cref="StreamingSamplesEventArgs"/> from a running stream.
/// </para>
/// <para>
/// <b>Threading.</b> A <see cref="PlotModel"/> is not thread-safe, and streaming
/// callbacks do not arrive on the UI thread. Every method here therefore
/// mutates the model under <see cref="PlotModel.SyncRoot"/> and only then
/// calls <see cref="PlotModel.InvalidatePlot"/>. The PlotterView control renders
/// under that same lock and accepts the invalidation from any thread, so
/// callers may use this class from whichever thread their data arrives on.
/// </para>
/// <para>
/// <b>Point budget.</b> A live stream produces far more samples than a chart can
/// usefully draw, so incoming data is decimated to
/// <see cref="MaxPointsPerChannel"/> points per channel. That keeps redraw cost
/// flat no matter how fast the scope runs.
/// </para>
/// </remarks>
public sealed class ScopePlot
{
    private readonly Dictionary<ChannelId, LineSeries> _series = new Dictionary<ChannelId, LineSeries>();
    private readonly Dictionary<ChannelId, Queue<DataPoint>> _points = new Dictionary<ChannelId, Queue<DataPoint>>();
    private readonly Dictionary<ChannelId, bool> _visible = new Dictionary<ChannelId, bool>();
    private LinearAxis _timeAxis;
    private LinearAxis _voltageAxis;
    private double _streamElapsedSeconds;

    //A distinct colour per channel; channel A is the conventional yellow of a
    //  bench scope, B the familiar blue.
    private static readonly PlotterColor[] ChannelColors =
    {
        PlotterColor.FromRgb(230, 200, 40),    //A - yellow
        PlotterColor.FromRgb(70, 150, 235),    //B - blue
        PlotterColor.FromRgb(230, 90, 70),     //C - red
        PlotterColor.FromRgb(110, 200, 110)    //D - green
    };

    /// <summary>
    /// Creates a plot, building the axes and legend.
    /// </summary>
    public ScopePlot()
    {
        Model = BuildModel();
    }

    /// <summary>
    /// The plot model to render. Hand this to a plot view; do not mutate it
    /// directly.
    /// </summary>
    public PlotModel Model { get; }

    /// <summary>
    /// The most points to keep per channel. Incoming data is decimated to fit.
    /// </summary>
    public int MaxPointsPerChannel { get; set; } = 2000;

    /// <summary>
    /// How many seconds of history a live stream keeps on screen.
    /// </summary>
    public double StreamWindowSeconds { get; set; } = 2.0;

    /// <summary>
    /// Whether the vertical axis rescales itself to the data. When false the
    /// axis is pinned to the widest configured channel range, which is usually
    /// what a scope user expects.
    /// </summary>
    public bool AutoScaleVoltage { get; set; }

    /// <summary>
    /// Shows or hides one channel's trace.
    /// </summary>
    /// <param name="channel">The channel to show or hide.</param>
    /// <param name="visible">True to draw it, false to hide it.</param>
    /// <remarks>
    /// This affects the chart only -- the channel keeps acquiring, and its
    /// history keeps accumulating, so toggling it back reveals the data that
    /// arrived while it was hidden. It is deliberately not the same thing as
    /// <see cref="ChannelSettings.Enabled"/>, which disables the channel on the
    /// device and changes the available sample rate and buffer depth.
    /// </remarks>
    public void SetChannelVisible(ChannelId channel, bool visible)
    {
        bool changed = false;

        lock (Model.SyncRoot)
        {
            _visible[channel] = visible;

            if (_series.TryGetValue(channel, out LineSeries series))
            {
                series.IsVisible = visible;
                series.RenderInLegend = visible;
                changed = true;
            }
        }

        //Only the view changed, so the data need not be re-read.
        if (changed) { Model.InvalidatePlot(false); }
    }

    /// <summary>
    /// Whether a channel's trace is currently drawn. Channels are visible until
    /// hidden.
    /// </summary>
    /// <param name="channel">The channel to test.</param>
    /// <returns>True when the trace is drawn.</returns>
    public bool IsChannelVisible(ChannelId channel)
    {
        lock (Model.SyncRoot)
        {
            return !_visible.TryGetValue(channel, out bool visible) || visible;
        }
    }

    /// <summary>
    /// Replaces the plot's contents with a single captured block.
    /// </summary>
    /// <param name="block">The block to display.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public void ShowBlock(CaptureBlock block)
    {
        if (block == null) { throw new ArgumentNullException(nameof(block)); }

        lock (Model.SyncRoot)
        {
            ClearCore();

            double widestVolts = 0;
            foreach (KeyValuePair<ChannelId, ChannelSamples> entry in block.Channels)
            {
                ChannelSamples samples = entry.Value;
                if (samples.Count == 0) { continue; }

                LineSeries series = GetOrCreateSeries(entry.Key, samples.Range);
                Queue<DataPoint> queue = _points[entry.Key];

                int step = Math.Max(1, samples.Count / MaxPointsPerChannel);
                for (int i = 0; i < samples.Count; i += step)
                {
                    if (samples.IsLostData(i)) { continue; }
                    queue.Enqueue(new DataPoint(block.TimeSecondsAt(i), samples.VoltsAt(i)));
                }

                series.Points.Clear();
                series.Points.AddRange(queue);

                double full = samples.Range.FullScaleInVolts();
                if (full > widestVolts) { widestVolts = full; }
            }

            _timeAxis.Minimum = 0;
            _timeAxis.Maximum = block.DurationSeconds > 0 ? block.DurationSeconds : double.NaN;
            ApplyVoltageRange(widestVolts);

            Model.Subtitle = FormatBlockSubtitle(block);
        }

        Model.InvalidatePlot(true);
    }

    /// <summary>
    /// Appends a batch of streamed samples, scrolling the time window forward.
    /// </summary>
    /// <param name="batch">The batch that just arrived.</param>
    /// <exception cref="ArgumentNullException"><paramref name="batch"/> is null.</exception>
    public void AppendStreaming(StreamingSamplesEventArgs batch)
    {
        if (batch == null) { throw new ArgumentNullException(nameof(batch)); }

        lock (Model.SyncRoot)
        {
            double intervalSeconds = batch.IntervalNanoseconds / 1e9;
            double widestVolts = 0;

            foreach (KeyValuePair<ChannelId, ChannelSamples> entry in batch.Channels)
            {
                ChannelSamples samples = entry.Value;
                if (samples.Count == 0) { continue; }

                LineSeries series = GetOrCreateSeries(entry.Key, samples.Range);
                Queue<DataPoint> queue = _points[entry.Key];

                //Decimate on the way in. A stream at 1 microsecond per sample
                //  delivers a million points a second; the chart wants a couple of
                //  thousand on screen in total.
                //
                //  samples per second        = 1 / intervalSeconds
                //  points the window affords = MaxPointsPerChannel / StreamWindowSeconds
                //  step                      = the ratio of the two
                int step = 1;
                if (intervalSeconds > 0 && MaxPointsPerChannel > 0)
                {
                    double stride = StreamWindowSeconds / (intervalSeconds * MaxPointsPerChannel);
                    step = Math.Max(1, (int)Math.Ceiling(stride));
                }

                for (int i = 0; i < samples.Count; i += step)
                {
                    if (samples.IsLostData(i)) { continue; }
                    double t = _streamElapsedSeconds + (i * intervalSeconds);
                    queue.Enqueue(new DataPoint(t, samples.VoltsAt(i)));
                }

                //Drop anything that has scrolled off the left edge.
                double cutoff = _streamElapsedSeconds + (samples.Count * intervalSeconds) - StreamWindowSeconds;
                while (queue.Count > 0 && queue.Peek().X < cutoff) { queue.Dequeue(); }
                while (queue.Count > MaxPointsPerChannel) { queue.Dequeue(); }

                series.Points.Clear();
                series.Points.AddRange(queue);

                double full = samples.Range.FullScaleInVolts();
                if (full > widestVolts) { widestVolts = full; }
            }

            _streamElapsedSeconds += batch.SampleCount * intervalSeconds;

            double windowEnd = _streamElapsedSeconds;
            _timeAxis.Minimum = Math.Max(0, windowEnd - StreamWindowSeconds);
            _timeAxis.Maximum = windowEnd > 0 ? windowEnd : double.NaN;
            ApplyVoltageRange(widestVolts);

            Model.Subtitle = FormatStreamSubtitle(batch);
        }

        Model.InvalidatePlot(true);
    }

    /// <summary>
    /// Removes all data and resets the time origin.
    /// </summary>
    public void Clear()
    {
        lock (Model.SyncRoot)
        {
            ClearCore();
        }

        Model.InvalidatePlot(true);
    }

    //Callers hold Model.SyncRoot.
    private void ClearCore()
    {
        foreach (Queue<DataPoint> queue in _points.Values) { queue.Clear(); }
        foreach (LineSeries series in _series.Values) { series.Points.Clear(); }
        _streamElapsedSeconds = 0;
    }

    private void ApplyVoltageRange(double widestVolts)
    {
        if (AutoScaleVoltage || widestVolts <= 0)
        {
            _voltageAxis.Minimum = double.NaN;
            _voltageAxis.Maximum = double.NaN;
        }
        else
        {
            _voltageAxis.Minimum = -widestVolts;
            _voltageAxis.Maximum = widestVolts;
        }
    }

    //Callers hold Model.SyncRoot.
    private LineSeries GetOrCreateSeries(ChannelId channel, VoltageRange range)
    {
        if (_series.TryGetValue(channel, out LineSeries existing))
        {
            //The legend names the range, and the range can change between
            //  batches when the user picks another one.
            string title = SeriesTitle(channel, range);
            if (existing.Title != title) { existing.Title = title; }
            return existing;
        }

        int index = (int)channel;
        bool visible = !_visible.TryGetValue(channel, out bool hidden) || hidden;
        var series = new LineSeries
        {
            Title = SeriesTitle(channel, range),
            Color = ChannelColors[index % ChannelColors.Length],
            StrokeThickness = 1.4,
            LineJoin = LineJoin.Bevel,
            //Series are created lazily, the first time a channel delivers data,
            //  which may be after the user has already hidden it.
            IsVisible = visible,
            RenderInLegend = visible
        };

        _series[channel] = series;
        _points[channel] = new Queue<DataPoint>();
        Model.Series.Add(series);
        return series;
    }

    private static string SeriesTitle(ChannelId channel, VoltageRange range)
        => $"Channel {(char)('A' + (int)channel)} ({range.ToDisplayString()})";

    private PlotModel BuildModel()
    {
        var model = new PlotModel
        {
            Title = "PicoScope.Brix",
            //Set explicitly, and not only for looks. The text and gridline
            //  colours below are chosen for a dark background; without a
            //  Background on the model an exported PNG comes out white and the
            //  title, legend and grid are all but invisible. The plot view
            //  clears to the model's background, so screen and export match.
            Background = PlotterColor.FromRgb(24, 24, 30),
            PlotAreaBorderColor = PlotterColor.FromRgb(90, 90, 100),
            TextColor = PlotterColor.FromRgb(220, 220, 225),
            TitleColor = PlotterColor.FromRgb(240, 240, 245),
            SubtitleColor = PlotterColor.FromRgb(170, 170, 180)
        };

        _timeAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Time",
            Unit = "s",
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = PlotterColor.FromRgb(60, 60, 70),
            MinorGridlineColor = PlotterColor.FromRgb(45, 45, 55),
            TicklineColor = PlotterColor.FromRgb(120, 120, 130),
            //Plain decimals, not scientific notation: a scope's time axis spans
            //  microseconds to seconds and "2.4e-3" is harder to read at a
            //  glance than "0.0024". Six decimals covers a microsecond tick.
            StringFormat = "0.######"
        };

        _voltageAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Voltage",
            Unit = "V",
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.Dot,
            MajorGridlineColor = PlotterColor.FromRgb(60, 60, 70),
            MinorGridlineColor = PlotterColor.FromRgb(45, 45, 55),
            TicklineColor = PlotterColor.FromRgb(120, 120, 130)
        };

        model.Axes.Add(_timeAxis);
        model.Axes.Add(_voltageAxis);
        model.Legends.Add(new Legend
        {
            LegendPosition = LegendPosition.RightTop,
            LegendPlacement = LegendPlacement.Inside,
            LegendTextColor = PlotterColor.FromRgb(220, 220, 225)
        });

        return model;
    }

    private static string FormatBlockSubtitle(CaptureBlock block)
    {
        double rate = block.IntervalNanoseconds > 0 ? 1e9 / block.IntervalNanoseconds : 0;
        return $"Block: {block.SampleCount} samples at {FormatRate(rate)} " +
               $"({block.IntervalNanoseconds:0.###} ns/sample)";
    }

    private static string FormatStreamSubtitle(StreamingSamplesEventArgs batch)
    {
        double rate = batch.IntervalNanoseconds > 0 ? 1e9 / batch.IntervalNanoseconds : 0;
        string text = $"Streaming at {FormatRate(rate)} - {batch.TotalSampleCount:N0} samples";
        if (batch.BufferOverrun) { text += " - BUFFER OVERRUN"; }
        return text;
    }

    private static string FormatRate(double samplesPerSecond)
    {
        if (samplesPerSecond >= 1e6) { return $"{samplesPerSecond / 1e6:0.##} MS/s"; }
        if (samplesPerSecond >= 1e3) { return $"{samplesPerSecond / 1e3:0.##} kS/s"; }
        return $"{samplesPerSecond:0.##} S/s";
    }
}
