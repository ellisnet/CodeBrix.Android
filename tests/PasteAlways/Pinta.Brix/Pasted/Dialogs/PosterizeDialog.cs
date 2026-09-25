//
// PosterizeDialog.cs
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
using Pinta.Brix.Controls;
using Pinta.Brix.Effects;

namespace Pinta.Brix.Dialogs;
//was previously: namespace Pinta.Effects;

public static class PosterizeDialog
{
    private sealed class ChannelRow
    {
        public required StackPanel Root { get; init; }
        public required Slider Slider { get; init; }
        public required NumberBox Spin { get; init; }

        public int Value => (int) Math.Round(Slider.Value);

        public void SetValue(double value)
        {
            Slider.Value = value;
            Spin.Value = Math.Round(value);
        }
    }

    public static async Task<bool> ShowAsync(PosterizeEffect effect, XamlRoot xamlRoot)
    {
        PosterizeData data = effect.Data;

        //Upstream: three linked channel spin boxes, 2..64, starting at 16,
        //with a "Linked" check (default on) that keeps them in step.
        const int initialChannelValue = 16;

        CheckBox linkButton = new() { Content = "Linked", IsChecked = true };
        List<ChannelRow> rows = [];
        bool updating = false;

        void UpdateEffectData()
        {
            data.Red = rows[0].Value;
            data.Green = rows[1].Value;
            data.Blue = rows[2].Value;

            //Only fire once, even when the link changed all three channels.
            data.FirePropertyChanged("_all_");
        }

        void HandleValueChanged(ChannelRow changed, double newValue)
        {
            if (updating) { return; }

            updating = true;
            changed.SetValue(newValue);
            if (linkButton.IsChecked == true)
            {
                foreach (ChannelRow row in rows) { row.SetValue(newValue); }
            }
            updating = false;

            UpdateEffectData();
        }

        ChannelRow CreateChannelRow(string label)
        {
            Slider slider = new()
            {
                Minimum = 2,
                Maximum = 64,
                StepFrequency = 1,
                Value = initialChannelValue,
            };
            NumberBox spin = new()
            {
                Minimum = 2,
                Maximum = 64,
                SmallChange = 1,
                Value = initialChannelValue,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                MinWidth = 72,
                Margin = new Thickness(8, 0, 0, 0),
            };

            Grid grid = new();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(slider, 0);
            Grid.SetColumn(spin, 1);
            grid.Children.Add(slider);
            grid.Children.Add(spin);

            StackPanel root = new() { Spacing = 2 };
            root.Children.Add(new TextBlock { Text = label });
            root.Children.Add(grid);

            ChannelRow row = new() { Root = root, Slider = slider, Spin = spin };
            slider.ValueChanged += (_, e) => HandleValueChanged(row, e.NewValue);
            spin.ValueChanged += (_, _) =>
            {
                if (!double.IsNaN(spin.Value)) { HandleValueChanged(row, spin.Value); }
            };
            return row;
        }

        rows.Add(CreateChannelRow("Red"));
        rows.Add(CreateChannelRow("Green"));
        rows.Add(CreateChannelRow("Blue"));

        StackPanel panel = new() { Spacing = 8, MinWidth = 360 };
        foreach (ChannelRow row in rows) { panel.Children.Add(row.Root); }
        panel.Children.Add(linkButton);

        //Apply the initial values so the first preview renders immediately.
        UpdateEffectData();

        return await FloatingDialogHost.ShowAsync("Posterize", panel, xamlRoot);
    }
}
