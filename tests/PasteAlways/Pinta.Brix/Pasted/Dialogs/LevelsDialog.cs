/////////////////////////////////////////////////////////////////////////////////
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Krzysztof Marecki <marecki.krzysztof@gmail.com>         //
/////////////////////////////////////////////////////////////////////////////////

// Additional code:
//
// LevelsDialog.cs
//
// Author:
//      Krzysztof Marecki <marecki.krzysztof@gmail.com>
//
// Copyright (c) 2010 Krzysztof Marecki
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pinta.Brix.Controls;
using Pinta.Brix.Effects;
using Pinta.Brix.Engine;
using SkiaSharp;
using System.Threading.Tasks;
using Drawing = Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Dialogs;
//was previously: namespace Pinta.Effects;

/// <summary>
/// The Levels adjustment dialog: input/output histograms, gradient range
/// widgets and per-channel masking, ported from upstream's Gtk.Dialog onto a
/// floating panel so the live preview stays visible while editing.
/// </summary>
public sealed class LevelsDialog
{
    private struct ChannelsMask
    {
        public bool B;
        public bool G;
        public bool R;

        // Note the ordering is required to match the indexing of ColorBgra.
        public bool this[int index]
        {
            set
            {
                switch (index)
                {
                    case 0: B = value; break;
                    case 1: G = value; break;
                    case 2: R = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(index));
                }
            }

            readonly get => index switch
            {
                0 => B,
                1 => G,
                2 => R,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };
        }
    }

    private ChannelsMask mask = new() { R = true, G = true, B = true };

    private readonly LevelsData effect_data;
    private readonly XamlRoot xaml_root;

    private readonly CheckBox check_red;
    private readonly CheckBox check_green;
    private readonly CheckBox check_blue;
    private readonly NumberBox spin_in_low;
    private readonly NumberBox spin_in_high;
    private readonly NumberBox spin_out_low;
    private readonly NumberBox spin_out_high;
    private readonly NumberBox spin_out_gamma;
    private readonly ColorGradientWidget gradient_input;
    private readonly ColorGradientWidget gradient_output;
    private readonly Border colorpanel_in_high;
    private readonly Border colorpanel_in_low;
    private readonly Border colorpanel_out_high;
    private readonly Border colorpanel_out_mid;
    private readonly Border colorpanel_out_low;
    private readonly HistogramWidget histogram_input;
    private readonly HistogramWidget histogram_output;
    private readonly StackPanel content;

    //hack to avoid recurrent invocation of UpdateLevels
    private bool disable_updating;

    //when the user drags triangles inside a gradient widget the histogram is
    //not redrawn on every Levels change - at most every Max_skip changes.
    private const int Max_skip = 5;
    private int skip_counter = Max_skip;
    private bool button_down;

    private LevelsDialog(LevelsData effectData, XamlRoot xamlRoot)
    {
        effect_data = effectData;
        xaml_root = xamlRoot;

        check_red = CreateMaskCheck("Red", 2);
        check_green = CreateMaskCheck("Green", 1);
        check_blue = CreateMaskCheck("Blue", 0);

        spin_in_low = CreateSpin(0, 254, 0, 1);
        spin_in_high = CreateSpin(1, 255, 255, 1);
        spin_out_low = CreateSpin(0, 252, 0, 1);
        spin_out_high = CreateSpin(2, 255, 255, 1);
        spin_out_gamma = CreateSpin(0, 100, 1, 0.1);

        spin_in_low.ValueChanged += (_, _) => { if (ValidSpin(spin_in_low)) gradient_input.SetValue(0, (int)spin_in_low.Value); };
        spin_in_high.ValueChanged += (_, _) => { if (ValidSpin(spin_in_high)) gradient_input.SetValue(1, (int)spin_in_high.Value); };
        spin_out_low.ValueChanged += (_, _) => { if (ValidSpin(spin_out_low)) gradient_output.SetValue(0, (int)spin_out_low.Value); };
        spin_out_gamma.ValueChanged += (_, _) => { if (ValidSpin(spin_out_gamma)) gradient_output.SetValue(1, FromGammaValue()); };
        spin_out_high.ValueChanged += (_, _) => { if (ValidSpin(spin_out_high)) gradient_output.SetValue(2, (int)spin_out_high.Value); };

        gradient_input = new ColorGradientWidget(2) { Width = 50, Height = 254 };
        gradient_input.DragBegun += (_, _) => button_down = true;
        gradient_input.DragEnded += (_, _) => HandleGradientDragEnd();
        gradient_input.ValueChanged += (_, e) => HandleGradientInputValueChanged(e.Index);

        gradient_output = new ColorGradientWidget(3) { Width = 50, Height = 254 };
        gradient_output.DragBegun += (_, _) => button_down = true;
        gradient_output.DragEnded += (_, _) => HandleGradientDragEnd();
        gradient_output.ValueChanged += (_, e) => HandleGradientOutputValueChanged(e.Index);

        colorpanel_in_high = CreateColorPanel(pickable: true);
        colorpanel_in_low = CreateColorPanel(pickable: true);
        colorpanel_out_high = CreateColorPanel(pickable: true);
        colorpanel_out_mid = CreateColorPanel(pickable: false);
        colorpanel_out_low = CreateColorPanel(pickable: true);

        histogram_input = new HistogramWidget { Width = 130, Height = 254, FlipHorizontal = true };
        histogram_output = new HistogramWidget { Width = 130, Height = 254 };

        //Input column: high at the top, low at the bottom.
        Grid inputColumn = new() { RowSpacing = 6, VerticalAlignment = VerticalAlignment.Stretch };
        inputColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inputColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inputColumn.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        inputColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inputColumn.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddRow(inputColumn, spin_in_high, 0);
        AddRow(inputColumn, colorpanel_in_high, 1);
        AddRow(inputColumn, colorpanel_in_low, 3);
        AddRow(inputColumn, spin_in_low, 4);

        StackPanel inputBox = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
        inputBox.Children.Add(inputColumn);
        inputBox.Children.Add(gradient_input);

        Grid outputColumn = new() { RowSpacing = 6, VerticalAlignment = VerticalAlignment.Stretch };
        for (int i = 0; i < 6; i++)
        {
            outputColumn.RowDefinitions.Add(new RowDefinition
            {
                Height = (i == 2 || i == 4) ? new GridLength(1, GridUnitType.Star) : GridLength.Auto,
            });
        }
        AddRow(outputColumn, spin_out_high, 0);
        AddRow(outputColumn, colorpanel_out_high, 1);
        AddRow(outputColumn, spin_out_gamma, 2);
        AddRow(outputColumn, colorpanel_out_mid, 3);
        AddRow(outputColumn, colorpanel_out_low, 4);
        AddRow(outputColumn, spin_out_low, 5);

        StackPanel outputBox = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
        outputBox.Children.Add(gradient_output);
        outputBox.Children.Add(outputColumn);

        Grid mainBand = new() { ColumnSpacing = 10 };
        for (int i = 0; i < 4; i++)
        {
            mainBand.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }
        AddColumn(mainBand, Labelled("Input Histogram", histogram_input), 0);
        AddColumn(mainBand, Labelled("Input", inputBox), 1);
        AddColumn(mainBand, Labelled("Output", outputBox), 2);
        AddColumn(mainBand, Labelled("Output Histogram", histogram_output), 3);

        Button buttonAuto = new() { Content = "Auto", MinWidth = 80 };
        buttonAuto.Click += (_, _) => HandleButtonAutoClicked();
        Button buttonReset = new() { Content = "Reset", MinWidth = 80 };
        buttonReset.Click += (_, _) => Reset();

        StackPanel actionRow = new() { Orientation = Orientation.Horizontal, Spacing = 12 };
        actionRow.Children.Add(buttonAuto);
        actionRow.Children.Add(buttonReset);
        actionRow.Children.Add(check_red);
        actionRow.Children.Add(check_green);
        actionRow.Children.Add(check_blue);

        content = new StackPanel { Spacing = 10 };
        content.Children.Add(mainBand);
        content.Children.Add(actionRow);

        UpdateInputHistogram();
        Reset();
        UpdateLevels();
        MaskChanged();
    }

    public static async Task<bool> ShowAsync(LevelsEffect effect, XamlRoot xamlRoot)
    {
        LevelsDialog dialog = new((LevelsData)effect.EffectData, xamlRoot);
        return await FloatingDialogHost.ShowAsync("Levels Adjustment", dialog.content, xamlRoot, maxWidth: 700);
    }

    // ---- Construction helpers ----------------------------------------------

    private static void AddRow(Grid grid, FrameworkElement element, int row)
    {
        Grid.SetRow(element, row);
        grid.Children.Add(element);
    }

    private static void AddColumn(Grid grid, FrameworkElement element, int column)
    {
        Grid.SetColumn(element, column);
        grid.Children.Add(element);
    }

    private static FrameworkElement Labelled(string label, FrameworkElement widget)
    {
        StackPanel panel = new() { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12 });
        panel.Children.Add(widget);
        return panel;
    }

    private static NumberBox CreateSpin(double min, double max, double initial, double step)
        => new()
        {
            Minimum = min,
            Maximum = max,
            Value = initial,
            SmallChange = step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            MinWidth = 90,
        };

    private static bool ValidSpin(NumberBox box) => !double.IsNaN(box.Value);

    private CheckBox CreateMaskCheck(string label, int channel)
    {
        CheckBox check = new() { Content = label, IsChecked = true, MinWidth = 0 };
        check.Checked += (_, _) => { mask[channel] = true; MaskChanged(); };
        check.Unchecked += (_, _) => { mask[channel] = false; MaskChanged(); };
        return check;
    }

    private Border CreateColorPanel(bool pickable)
    {
        Border panel = new()
        {
            Height = 24,
            MinWidth = 90,
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x55, 0x55, 0x55)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0, 0, 0)),
        };

        if (pickable)
        {
            ToolTipService.SetToolTip(panel, "Double-click to pick a color");
            panel.DoubleTapped += async (_, _) => await HandleColorPanelDoubleClickAsync(panel);
        }

        return panel;
    }

    private static void SetPanelColor(Border panel, ColorBgra color)
        => panel.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, color.R, color.G, color.B));

    // ---- Levels logic (ported) ---------------------------------------------

    private UnaryPixelOps.Level Levels
    {
        get => effect_data.Levels;
        set => effect_data.Levels = value;
    }

    private void UpdateLivePreview()
        => effect_data.FirePropertyChanged(nameof(LevelsData.Levels));

    private void UpdateInputHistogram()
    {
        Document doc = PintaCore.Workspace.ActiveDocument;

        var surface = doc.Layers.CurrentUserLayer.Surface;
        RectangleI rect = doc.Selection.GetBounds().ToInt();
        histogram_input.Histogram.UpdateHistogram(surface, rect);
        UpdateOutputHistogram();
    }

    private void UpdateOutputHistogram()
        => histogram_output.Histogram.SetFromLeveledHistogram(histogram_input.Histogram, Levels);

    private void Reset()
    {
        histogram_output.ResetHistogram();

        spin_in_low.Value = 0;
        spin_in_high.Value = 255;
        spin_out_low.Value = 0;
        spin_out_gamma.Value = 1.0;
        spin_out_high.Value = 255;
    }

    private void UpdateFromLevelsOp()
    {
        disable_updating = true;

        spin_in_high.Value = MaskAvg(Levels.ColorInHigh);
        spin_in_low.Value = MaskAvg(Levels.ColorInLow);

        float gamma = MaskGamma();
        int lo = MaskAvg(Levels.ColorOutLow);
        int hi = MaskAvg(Levels.ColorOutHigh);

        spin_out_high.Value = hi;
        spin_out_gamma.Value = gamma;
        spin_out_low.Value = lo;

        disable_updating = false;
    }

    private void HandleButtonAutoClicked()
    {
        Levels = histogram_input.Histogram.MakeLevelsAuto();

        UpdateFromLevelsOp();
        UpdateLevels();
    }

    private int FromGammaValue()
    {
        int lo = gradient_output.GetValue(0);
        int hi = gradient_output.GetValue(2);
        int med = (int)(lo + (hi - lo) * Math.Pow(0.5, spin_out_gamma.Value));
        return med;
    }

    private int MaskAvg(ColorBgra before)
    {
        int count = 0, total = 0;

        for (int c = 0; c < 3; c++)
        {
            if (!mask[c]) { continue; }
            total += before[c];
            count++;
        }

        return count > 0 ? total / count : 0;
    }

    private ColorBgra UpdateByMask(ColorBgra before, byte val)
    {
        if (!(mask.R || mask.G || mask.B)) { return before; }

        ColorBgra after = before;
        int average = -1;
        int oldaverage;

        do
        {
            oldaverage = average;
            average = MaskAvg(after);

            if (average == 0) { break; }

            float factor = (float)val / average;

            for (int c = 0; c < 3; c++)
            {
                if (mask[c]) { after[c] = Utility.ClampToByte(after[c] * factor); }
            }
        } while (average != val && oldaverage != average);

        while (average != val)
        {
            average = MaskAvg(after);
            int diff = val - average;

            for (int c = 0; c < 3; c++)
            {
                if (mask[c]) { after[c] = Utility.ClampToByte(after[c] + diff); }
            }
        }

        return after.NewAlpha(255);
    }

    private float MaskGamma()
    {
        int count = 0;
        float total = 0;

        for (int c = 0; c < 3; c++)
        {
            if (!mask[c]) { continue; }
            total += Levels.GetGamma(c);
            count++;
        }

        return count > 0 ? total / count : 1;
    }

    private void UpdateGammaByMask(float val)
    {
        val = Math.Clamp(val, UnaryPixelOps.Level.MinGamma, UnaryPixelOps.Level.MaxGamma);
        if (!(mask.R || mask.G || mask.B)) { return; }

        float average;

        do
        {
            average = MaskGamma();

            float factor = val / average;

            for (int c = 0; c < 3; c++)
            {
                if (mask[c]) { Levels.SetGamma(c, factor * Levels.GetGamma(c)); }
            }
        } while (Math.Abs(val - average) > 0.001);
    }

    private ColorBgra GetOutMidColor()
        => Levels.Apply(histogram_input.Histogram.GetMeanColor());

    private void UpdateLevels()
    {
        if (disable_updating) { return; }

        disable_updating = true;

        if (skip_counter == Max_skip || !button_down)
        {
            Levels.ColorOutHigh = UpdateByMask(Levels.ColorOutHigh, (byte)spin_out_high.Value);
            Levels.ColorOutLow = UpdateByMask(Levels.ColorOutLow, (byte)spin_out_low.Value);
            UpdateGammaByMask((float)spin_out_gamma.Value);

            Levels.ColorInHigh = UpdateByMask(Levels.ColorInHigh, (byte)spin_in_high.Value);
            Levels.ColorInLow = UpdateByMask(Levels.ColorInLow, (byte)spin_in_low.Value);

            SetPanelColor(colorpanel_in_low, Levels.ColorInLow);
            SetPanelColor(colorpanel_in_high, Levels.ColorInHigh);

            SetPanelColor(colorpanel_out_low, Levels.ColorOutLow);
            SetPanelColor(colorpanel_out_mid, GetOutMidColor());
            SetPanelColor(colorpanel_out_high, Levels.ColorOutHigh);

            UpdateOutputHistogram();
            skip_counter = 0;
        }
        else
        {
            skip_counter++;
        }

        disable_updating = false;

        UpdateLivePreview();
    }

    private void HandleGradientDragEnd()
    {
        button_down = false;

        if (skip_counter != 0) { UpdateLevels(); }
    }

    private void HandleGradientInputValueChanged(int index)
    {
        int val = gradient_input.GetValue(index);

        if (index == 0) { spin_in_low.Value = val; }
        else { spin_in_high.Value = val; }

        UpdateLevels();
    }

    private void HandleGradientOutputValueChanged(int index)
    {
        if (gradient_output.ValueIndex != -1 && gradient_output.ValueIndex != index) { return; }

        int val = gradient_output.GetValue(index);
        int hi = gradient_output.GetValue(2);
        int lo = gradient_output.GetValue(0);
        int med = FromGammaValue();

        switch (index)
        {
            case 0:
                spin_out_low.Value = val;
                gradient_output.SetValue(1, med);
                break;

            case 1:
                med = gradient_output.GetValue(1);
                spin_out_gamma.Value = Math.Clamp(1 / Math.Log(0.5, (med - lo) / (float)(hi - lo)), 0.1, 10.0);
                break;

            case 2:
                spin_out_high.Value = val;
                gradient_output.SetValue(1, med);
                break;
        }

        UpdateLevels();
    }

    private void MaskChanged()
    {
        SKColor maxColor = new(
            (byte)(mask.R ? 255 : 0),
            (byte)(mask.G ? 255 : 0),
            (byte)(mask.B ? 255 : 0));
        gradient_input.MaxColor = maxColor;
        gradient_output.MaxColor = maxColor;

        for (int i = 0; i < 3; i++)
        {
            histogram_input.SetSelected(i, mask[i]);
            histogram_output.SetSelected(i, mask[i]);
        }
    }

    private async Task HandleColorPanelDoubleClickAsync(Border panel)
    {
        SolidColorBrush brush = (SolidColorBrush)panel.Background;
        Drawing.Color current = new(brush.Color.R / 255.0, brush.Color.G / 255.0, brush.Color.B / 255.0);

        Drawing.Color? chosen = await ColorPickerDialog.ShowAsync("Choose Color", current, xaml_root);

        if (chosen is null) { return; }

        ColorBgra col = chosen.Value.ToColorBgra();

        if (panel == colorpanel_in_low) { Levels.ColorInLow = col; }
        else if (panel == colorpanel_in_high) { Levels.ColorInHigh = col; }
        else if (panel == colorpanel_out_low) { Levels.ColorOutLow = col; }
        else if (panel == colorpanel_out_high) { Levels.ColorOutHigh = col; }

        UpdateFromLevelsOp();
        UpdateLevels();
    }
}
