//
// CurvesDialog.cs
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
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pinta.Brix.Controls;
using Pinta.Brix.Effects;
using Pinta.Brix.Engine;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace Pinta.Brix.Dialogs;
//was previously: namespace Pinta.Effects;

/// <summary>
/// The Curves adjustment dialog: a 256x256 spline editor with per-channel
/// curves, ported from upstream's Gtk.DrawingArea onto a Skia-drawn canvas in
/// a floating panel so the live preview stays visible while editing.
/// </summary>
public sealed class CurvesDialog
{
    private const int SIZE = 256; // drawing area width and height
    private const int RADIUS = 6; // control point radius

    private static readonly SKColor ForegroundColor = new(0xE6, 0xE6, 0xE6);
    private static readonly SKColor RedCurveColor = new(0xE6, 0x00, 0x00);
    private static readonly SKColor GreenCurveColor = new(0x00, 0xE6, 0x00);
    private static readonly SKColor BlueCurveColor = new(0x00, 0x00, 0xE6);

    private readonly CurvesData effect_data;

    private readonly ComboBox combo_map;
    private readonly TextBlock label_point;
    private readonly SKXamlCanvas curves_drawing;
    private readonly CheckBox check_red;
    private readonly CheckBox check_green;
    private readonly CheckBox check_blue;
    private readonly StackPanel content;

    //last added control point x
    private int? last_cpx;
    private PointI last_mouse_pos = new(0, 0);
    //Keys of existing control points which cannot be overwritten by a new
    //control point during one drag.
    private readonly HashSet<int> orig_cps = [];
    private bool dragging;

    //control points per transfer mode
    private SortedList<int, int>[] luminosity_cps;
    private SortedList<int, int>[] rgb_cps;

    private CurvesDialog(CurvesData effectData)
    {
        effect_data = effectData;

        combo_map = new ComboBox();
        combo_map.Items.Add("RGB");
        combo_map.Items.Add("Luminosity");
        combo_map.SelectedIndex = 1;
        combo_map.SelectionChanged += (_, _) => HandleComboMapChanged();

        label_point = new TextBlock
        {
            Text = "(256, 256)",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };

        check_red = CreateColorCheck("Red");
        check_green = CreateColorCheck("Green");
        check_blue = CreateColorCheck("Blue");

        Button buttonReset = new() { Content = "Reset", MinWidth = 81, HorizontalAlignment = HorizontalAlignment.Right };
        buttonReset.Click += (_, _) =>
        {
            ResetControlPoints();
            curves_drawing.Invalidate();
        };

        curves_drawing = new SKXamlCanvas
        {
            Width = SIZE,
            Height = SIZE,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(8),
        };
        curves_drawing.PaintSurface += (_, e) =>
        {
            //The surface is in physical pixels; the drawing and the pointer
            //maths are in the 256-unit logical space. Scale so they agree at
            //any display scale.
            e.Surface.Canvas.Scale(e.Info.Width / (float)SIZE, e.Info.Height / (float)SIZE);
            Draw(e.Surface.Canvas);
        };
        curves_drawing.PointerMoved += OnPointerMoved;
        curves_drawing.PointerPressed += OnPointerPressed;
        curves_drawing.PointerReleased += OnPointerReleased;
        curves_drawing.PointerExited += OnPointerExited;

        Grid topRow = new() { ColumnSpacing = 6 };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        TextBlock mapLabel = new() { Text = "Transfer Map", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(mapLabel, 0);
        Grid.SetColumn(combo_map, 1);
        Grid.SetColumn(label_point, 2);
        topRow.Children.Add(mapLabel);
        topRow.Children.Add(combo_map);
        topRow.Children.Add(label_point);

        Grid bottomRow = new() { ColumnSpacing = 6 };
        for (int i = 0; i < 3; i++)
        {
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }
        bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(check_red, 0);
        Grid.SetColumn(check_green, 1);
        Grid.SetColumn(check_blue, 2);
        Grid.SetColumn(buttonReset, 3);
        bottomRow.Children.Add(check_red);
        bottomRow.Children.Add(check_green);
        bottomRow.Children.Add(check_blue);
        bottomRow.Children.Add(buttonReset);

        content = new StackPanel { Spacing = 6, MinWidth = 300 };
        content.Children.Add(topRow);
        content.Children.Add(curves_drawing);
        content.Children.Add(bottomRow);
        content.Children.Add(new TextBlock
        {
            Text = "Tip: Right-click to remove control points.",
            Opacity = 0.7,
            FontSize = 12,
        });

        ResetControlPoints();
    }

    public static async Task<bool> ShowAsync(CurvesEffect effect, XamlRoot xamlRoot)
    {
        CurvesDialog dialog = new((CurvesData)effect.EffectData);
        return await FloatingDialogHost.ShowAsync("Curves", dialog.content, xamlRoot);
    }

    private ColorTransferMode Mode =>
        combo_map.SelectedIndex == 0
        ? ColorTransferMode.Rgb
        : ColorTransferMode.Luminosity;

    private SortedList<int, int>[] ControlPoints
    {
        get => Mode == ColorTransferMode.Luminosity ? luminosity_cps : rgb_cps;
        set
        {
            if (Mode == ColorTransferMode.Luminosity) { luminosity_cps = value; }
            else { rgb_cps = value; }
        }
    }

    private CheckBox CreateColorCheck(string label)
    {
        CheckBox result = new()
        {
            Content = label,
            IsChecked = true,
            Visibility = Visibility.Collapsed,
            MinWidth = 0,
        };
        result.Checked += (_, _) => curves_drawing.Invalidate();
        result.Unchecked += (_, _) => curves_drawing.Invalidate();
        return result;
    }

    private void HandleComboMapChanged()
    {
        if (ControlPoints == null) { ResetControlPoints(); }
        else { UpdateLivePreview(nameof(CurvesData.Mode)); }

        Visibility visible = Mode == ColorTransferMode.Rgb ? Visibility.Visible : Visibility.Collapsed;
        check_red.Visibility = check_green.Visibility = check_blue.Visibility = visible;

        curves_drawing.Invalidate();
    }

    private void UpdateLivePreview(string propertyName)
    {
        effect_data.ControlPoints = ControlPoints;
        effect_data.Mode = Mode;
        effect_data.FirePropertyChanged(propertyName);
    }

    private void ResetControlPoints()
    {
        ControlPoints = ComputeControlPoints(Mode);
        UpdateLivePreview(nameof(CurvesData.ControlPoints));
    }

    private static SortedList<int, int>[] ComputeControlPoints(ColorTransferMode mode)
    {
        int channels = mode == ColorTransferMode.Luminosity ? 1 : 3;

        var result = new SortedList<int, int>[channels];
        for (int i = 0; i < channels; i++)
        {
            result[i] = new SortedList<int, int>
            {
                { 0, 0 },
                { SIZE - 1, SIZE - 1 },
            };
        }

        return result;
    }

    private IEnumerable<SortedList<int, int>> GetActiveControlPoints()
    {
        if (Mode == ColorTransferMode.Luminosity)
        {
            yield return ControlPoints[0];
            yield break;
        }

        if (check_red.IsChecked == true) { yield return ControlPoints[0]; }
        if (check_green.IsChecked == true) { yield return ControlPoints[1]; }
        if (check_blue.IsChecked == true) { yield return ControlPoints[2]; }
    }

    private void AddControlPoint(PointI cp)
    {
        foreach (SortedList<int, int> controlPoints in GetActiveControlPoints())
        {
            controlPoints[cp.X] = SIZE - 1 - cp.Y;
        }

        last_cpx = cp.X;

        UpdateLivePreview(nameof(CurvesData.ControlPoints));
    }

    private static bool CheckControlPointProximity(PointI cp, PointI pos)
        => Math.Sqrt(Math.Pow(cp.X - pos.X, 2) + Math.Pow(cp.Y - pos.Y, 2)) < RADIUS;

    private bool SnapToControlPointProximity(ref PointI pos)
    {
        foreach (SortedList<int, int> controlPoints in GetActiveControlPoints())
        {
            for (int i = 0; i < controlPoints.Count; i++)
            {
                PointI cp = new(controlPoints.Keys[i], SIZE - 1 - controlPoints.Values[i]);

                if (!CheckControlPointProximity(cp, pos)) { continue; }

                pos = cp;
                return true;
            }
        }

        return false;
    }

    // ---- Pointer handling --------------------------------------------------

    private PointI PointerPosition(PointerRoutedEventArgs e)
    {
        Windows.Foundation.Point position = e.GetCurrentPoint(curves_drawing).Position;
        return new PointI((int)position.X, (int)position.Y);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PointI pos = PointerPosition(e);
        last_mouse_pos = pos;

        var properties = e.GetCurrentPoint(curves_drawing).Properties;

        if (properties.IsLeftButtonPressed)
        {
            orig_cps.Clear();
            foreach (SortedList<int, int> controlPoints in GetActiveControlPoints())
            {
                orig_cps.UnionWith(controlPoints.Keys);
            }

            if (SnapToControlPointProximity(ref pos))
            {
                orig_cps.Remove(pos.X); //Allow dragging the snapped control point.
            }

            AddControlPoint(pos);

            dragging = true;
            curves_drawing.CapturePointer(e.Pointer);
        }
        else if (properties.IsRightButtonPressed)
        {
            foreach (SortedList<int, int> controlPoints in GetActiveControlPoints())
            {
                for (int i = 0; i < controlPoints.Count; i++)
                {
                    PointI cp = new(controlPoints.Keys[i], SIZE - 1 - controlPoints.Values[i]);

                    //the first and last control points cannot be removed
                    if (cp.X == 0 && cp.Y == SIZE - 1) { continue; }
                    if (cp.X == SIZE - 1 && cp.Y == 0) { continue; }

                    if (CheckControlPointProximity(cp, pos))
                    {
                        controlPoints.RemoveAt(i);
                        UpdateLivePreview(nameof(CurvesData.ControlPoints));
                        break;
                    }
                }
            }
        }

        e.Handled = true;
        curves_drawing.Invalidate();
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        PointI p = PointerPosition(e);
        last_mouse_pos = p;

        if (dragging)
        {
            if (p.X < 0 || p.X >= SIZE || p.Y < 0 || p.Y >= SIZE)
            {
                curves_drawing.Invalidate();
                return;
            }

            if (last_cpx is not null)
            {
                //The first and last control points cannot be removed, so also
                //forbid dragging them away.
                if (last_cpx == 0) { p = new PointI(0, p.Y); }
                else if (last_cpx == SIZE - 1) { p = new PointI(SIZE - 1, p.Y); }
                else
                {
                    //Remove the old version of the control point being edited.
                    foreach (SortedList<int, int> controlPoints in GetActiveControlPoints())
                    {
                        controlPoints.Remove(last_cpx.Value);
                    }
                }
            }

            //Don't allow overwriting any of the original control points.
            if (!orig_cps.Contains(p.X)) { AddControlPoint(p); }
            else { last_cpx = null; }
        }

        curves_drawing.Invalidate();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        dragging = false;
        last_cpx = null;
        curves_drawing.ReleasePointerCapture(e.Pointer);
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!dragging) { last_mouse_pos = new PointI(-1, -1); }
        curves_drawing.Invalidate();
    }

    // ---- Drawing -----------------------------------------------------------

    private readonly record struct ControlPointDrawingInfo(SKColor Color, bool IsActive);

    private IEnumerable<ControlPointDrawingInfo> GetDrawingInfos()
    {
        if (Mode == ColorTransferMode.Luminosity)
        {
            yield return new ControlPointDrawingInfo(ForegroundColor, true);
            yield break;
        }

        yield return new ControlPointDrawingInfo(RedCurveColor, check_red.IsChecked == true);
        yield return new ControlPointDrawingInfo(GreenCurveColor, check_green.IsChecked == true);
        yield return new ControlPointDrawingInfo(BlueCurveColor, check_blue.IsChecked == true);
    }

    private void Draw(SKCanvas canvas)
    {
        canvas.Clear(SKColors.Transparent);

        DrawBorder(canvas);
        DrawPointerCross(canvas);
        DrawSpline(canvas);
        DrawGrid(canvas);
        DrawControlPoints(canvas);
    }

    private static void DrawBorder(SKCanvas canvas)
    {
        using SKPaint paint = new()
        {
            Color = ForegroundColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
        };
        canvas.DrawRect(0, 0, SIZE - 1, SIZE - 1, paint);
    }

    private void DrawPointerCross(SKCanvas canvas)
    {
        PointI p = last_mouse_pos;

        if (p.X < 0 || p.X >= SIZE || p.Y < 0 || p.Y >= SIZE)
        {
            label_point.Text = string.Empty;
            return;
        }

        using SKPaint paint = new()
        {
            Color = ForegroundColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f,
        };
        canvas.DrawLine(p.X, 0, p.X, SIZE, paint);
        canvas.DrawLine(0, p.Y, SIZE, p.Y, paint);

        label_point.Text = $"({p.X}, {p.Y})";
    }

    private void DrawSpline(SKCanvas canvas)
    {
        using IEnumerator<ControlPointDrawingInfo> infos = GetDrawingInfos().GetEnumerator();

        foreach (SortedList<int, int> controlPoints in ControlPoints)
        {
            SplineInterpolator<double> interpolator = new();

            IList<int> xa = controlPoints.Keys;
            IList<int> ya = controlPoints.Values;

            for (int i = 0; i < controlPoints.Count; i++)
            {
                interpolator.Add(xa[i], ya[i]);
            }

            SKPathBuilder builder = new();
            builder.MoveTo(0, (float)Math.Clamp(SIZE - 1 - interpolator.Interpolate(0), 0, SIZE - 1));
            for (int i = 1; i < SIZE; i++)
            {
                builder.LineTo(i, (float)Math.Clamp(SIZE - 1 - interpolator.Interpolate(i), 0, SIZE - 1));
            }
            using SKPath path = builder.Snapshot();

            infos.MoveNext();
            ControlPointDrawingInfo info = infos.Current;

            using SKPaint paint = new()
            {
                Color = info.Color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = info.IsActive ? 2 : 1,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true,
            };
            canvas.DrawPath(path, paint);
        }
    }

    private static void DrawGrid(SKCanvas canvas)
    {
        using SKPathEffect dash = SKPathEffect.CreateDash([4, 4], 2);
        using SKPaint paint = new()
        {
            Color = ForegroundColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            PathEffect = dash,
        };

        for (int i = 1; i < 4; i++)
        {
            canvas.DrawLine(i * SIZE / 4, 0, i * SIZE / 4, SIZE, paint);
            canvas.DrawLine(0, i * SIZE / 4, SIZE, i * SIZE / 4, paint);
        }

        canvas.DrawLine(0, SIZE - 1, SIZE - 1, 0, paint);
    }

    private void DrawControlPoints(SKCanvas canvas)
    {
        PointI lastMousePos = last_mouse_pos;

        using IEnumerator<ControlPointDrawingInfo> infos = GetDrawingInfos().GetEnumerator();

        foreach (SortedList<int, int> controlPoints in ControlPoints)
        {
            infos.MoveNext();
            ControlPointDrawingInfo info = infos.Current;

            for (int i = 0; i < controlPoints.Count; i++)
            {
                PointI cp = new(controlPoints.Keys[i], SIZE - 1 - controlPoints.Values[i]);

                if (info.IsActive)
                {
                    if (CheckControlPointProximity(cp, lastMousePos))
                    {
                        using SKPaint outline = new()
                        {
                            Color = new SKColor(0x33, 0x33, 0x33),
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 2,
                            IsAntialias = true,
                        };
                        canvas.DrawOval(SKRect.Create(cp.X - (RADIUS + 2) / 2f, cp.Y - (RADIUS + 2) / 2f, RADIUS + 2, RADIUS + 2), outline);

                        using SKPaint fill = new()
                        {
                            Color = new SKColor(0xE6, 0xE6, 0xE6),
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true,
                        };
                        canvas.DrawOval(SKRect.Create(cp.X - RADIUS / 2f, cp.Y - RADIUS / 2f, RADIUS, RADIUS), fill);
                    }
                    else
                    {
                        using SKPaint outline = new()
                        {
                            Color = info.Color,
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = 2,
                            IsAntialias = true,
                        };
                        canvas.DrawOval(SKRect.Create(cp.X - RADIUS / 2f, cp.Y - RADIUS / 2f, RADIUS, RADIUS), outline);
                    }
                }

                using SKPaint innerFill = new()
                {
                    Color = info.Color,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true,
                };
                canvas.DrawOval(SKRect.Create(cp.X - (RADIUS - 2) / 2f, cp.Y - (RADIUS - 2) / 2f, RADIUS - 2, RADIUS - 2), innerFill);
            }
        }
    }
}
