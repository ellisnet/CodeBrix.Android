// AlignmentDialog.cs
//
// The Align Object configuration dialog: a 3x3 grid of anchor toggle buttons.
// Ported from upstream's Gtk.Dialog onto the FloatingDialogHost so the live
// preview stays visible while a position is chosen.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Pinta.Brix.Controls;
using Pinta.Brix.Effects;
using Pinta.Brix.Engine;

namespace Pinta.Brix.Dialogs;
//was previously: namespace Pinta.Effects;

public static class AlignmentDialog
{
    public static async Task<bool> ShowAsync(AlignObjectEffect effect, XamlRoot xamlRoot)
    {
        AlignObjectEffect.AlignObjectData data = effect.Data;

        (AlignPosition position, string icon, string tooltip)[,] cells =
        {
            { (AlignPosition.TopLeft, Icons.ResizeCanvasNW, "Top Left"),
              (AlignPosition.TopCenter, Icons.ResizeCanvasUp, "Top Center"),
              (AlignPosition.TopRight, Icons.ResizeCanvasNE, "Top Right") },
            { (AlignPosition.CenterLeft, Icons.ResizeCanvasLeft, "Center Left"),
              (AlignPosition.Center, Icons.ResizeCanvasBase, "Center"),
              (AlignPosition.CenterRight, Icons.ResizeCanvasRight, "Center Right") },
            { (AlignPosition.BottomLeft, Icons.ResizeCanvasSW, "Bottom Left"),
              (AlignPosition.BottomCenter, Icons.ResizeCanvasDown, "Bottom Center"),
              (AlignPosition.BottomRight, Icons.ResizeCanvasSE, "Bottom Right") },
        };

        List<(ToggleButton button, AlignPosition position)> buttons = [];

        void Select(AlignPosition position)
        {
            data.Position = position;
            foreach ((ToggleButton button, AlignPosition buttonPosition) in buttons)
            {
                button.IsChecked = buttonPosition == position;
            }
        }

        Grid grid = new()
        {
            RowSpacing = 6,
            ColumnSpacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        for (int i = 0; i < 3; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                (AlignPosition position, string icon, string tooltip) = cells[row, column];

                ToggleButton button = new()
                {
                    MinWidth = 48,
                    MinHeight = 40,
                    Padding = new Thickness(4),
                };
                if (IconImageSource.Create(icon, 24) is { } source)
                {
                    button.Content = new Image { Source = source, Width = 24, Height = 24 };
                }
                else
                {
                    button.Content = new TextBlock { Text = tooltip };
                }
                ToolTipService.SetToolTip(button, tooltip);

                button.Click += (_, _) => Select(position);

                Grid.SetRow(button, row);
                Grid.SetColumn(button, column);
                buttons.Add((button, position));
                grid.Children.Add(button);
            }
        }

        //Upstream selects Center on open, which also renders the first
        //preview immediately.
        Select(AlignPosition.Center);

        return await FloatingDialogHost.ShowAsync("Align Object", grid, xamlRoot);
    }
}
