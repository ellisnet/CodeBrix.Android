// MainPage.Menus.cs
//
// Builds the menu bar and the in-app toolbar row from the engine's action
// model. Upstream assembled equivalent Gio.Menu models in ActionManager;
// keeping the structure here means the XAML holds no command declarations and
// a new command only has to be declared once, in Pinta.Brix.Engine.Actions.

using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pinta.Brix.Controls;
using Pinta.Brix.Engine;

namespace Pinta.Brix.Views;

public sealed partial class MainPage
{
    private const int ToolbarIconSize = 20;

    private MenuBarItem adjustmentsMenu;
    private MenuBarItem effectsMenu;
    private MenuBarItem windowMenu;
    private int windowMenuFixedItemCount;

    private void BuildMenus()
    {
        ActionManager actions = PintaCore.Actions;

        MainMenuBar.Items.Clear();

        MainMenuBar.Items.Add(BuildMenu("File",
            actions.File.New,
            actions.File.NewScreenshot,
            actions.File.Open,
            null,
            actions.File.Save,
            actions.File.SaveAs,
            null,
            actions.File.Close));

        MenuBarItem edit = BuildMenu("Edit",
            actions.Edit.Undo,
            actions.Edit.Redo,
            null,
            actions.Edit.Cut,
            actions.Edit.Copy,
            actions.Edit.CopyMerged,
            actions.Edit.Paste,
            actions.Edit.PasteIntoNewLayer,
            actions.Edit.PasteIntoNewImage,
            null,
            actions.Edit.SelectAll,
            actions.Edit.Deselect,
            null,
            actions.Edit.EraseSelection,
            actions.Edit.FillSelection,
            actions.Edit.InvertSelection,
            actions.Edit.OffsetSelection,
            null);
        edit.Items.Add(CommandMenuBuilder.CreateSubmenu("Palette", new[] {
            actions.Edit.LoadPalette,
            actions.Edit.SavePalette,
            actions.Edit.ResetPalette,
            actions.Edit.ResizePalette,
        }));
        MainMenuBar.Items.Add(edit);

        MainMenuBar.Items.Add(BuildMenu("View",
            actions.View.ZoomIn,
            actions.View.ZoomOut,
            actions.View.ZoomToWindow,
            actions.View.ZoomToSelection,
            actions.View.ActualSize,
            null,
            actions.View.Fullscreen,
            null,
            actions.View.ToolBar,
            actions.View.ImageTabs,
            actions.View.MenuBar,
            actions.View.StatusBar,
            actions.View.ToolBox,
            actions.View.ToolWindows,
            actions.View.Rulers,
            null,
            actions.View.EditCanvasGrid));

        MainMenuBar.Items.Add(BuildMenu("Image",
            actions.Image.CropToSelection,
            actions.Image.AutoCrop,
            actions.Image.Resize,
            actions.Image.CanvasSize,
            null,
            actions.Image.FlipHorizontal,
            actions.Image.FlipVertical,
            null,
            actions.Image.RotateCW,
            actions.Image.RotateCCW,
            actions.Image.Rotate180,
            null,
            actions.Image.Flatten));

        MainMenuBar.Items.Add(BuildMenu("Layers",
            actions.Layers.AddNewLayer,
            actions.Layers.DeleteLayer,
            actions.Layers.DuplicateLayer,
            actions.Layers.MergeLayerDown,
            actions.Layers.ImportFromFile,
            null,
            actions.Layers.FlipHorizontal,
            actions.Layers.FlipVertical,
            actions.Layers.RotateZoom,
            null,
            actions.Layers.MoveLayerUp,
            actions.Layers.MoveLayerDown,
            null,
            actions.Layers.Properties));

        adjustmentsMenu = new MenuBarItem { Title = "Adjustments" };
        MainMenuBar.Items.Add(adjustmentsMenu);

        effectsMenu = new MenuBarItem { Title = "Effects" };
        MainMenuBar.Items.Add(effectsMenu);

        windowMenu = BuildMenu("Window",
            actions.Window.SaveAll,
            actions.Window.CloseAll,
            null);
        windowMenuFixedItemCount = windowMenu.Items.Count;
        MainMenuBar.Items.Add(windowMenu);

        MainMenuBar.Items.Add(BuildMenu("Help",
            actions.Help.Contents,
            actions.App.KeyboardShortcuts,
            null,
            actions.Help.Website,
            actions.Help.Bugs,
            actions.Help.Translate,
            null,
            actions.App.About));

        //XAML accelerators do not fire on the Skia heads (see
        //CommandAcceleratorTable), so every shortcut is dispatched from the
        //page's KeyDown handler instead. The menu items still SHOW their
        //shortcut - that part of the XAML accelerator works.
        acceleratorTable = new CommandAcceleratorTable();

        foreach (Command command in actions.AllCommands())
        {
            acceleratorTable.Register(command);
        }

        //Handled keys have to be seen too: the canvas marks most key events
        //handled, and a shortcut must still work while it has focus.
        AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnGlobalKeyDown), handledEventsToo: true);
        AddHandler(UIElement.KeyUpEvent, new KeyEventHandler(OnGlobalKeyUp), handledEventsToo: true);
    }

    private CommandAcceleratorTable acceleratorTable;

    private void OnGlobalKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (acceleratorTable is null) { return; }

        if (acceleratorTable.TrackModifier(args.Key, down: true)) { return; }

        if (acceleratorTable.TryInvoke(args.Key))
        {
            args.Handled = true;
            return;
        }

        //Unmodified keys upstream handles globally rather than as commands.
        if (TryHandlePaletteKey(args.Key))
        {
            args.Handled = true;
        }
    }

    private void OnGlobalKeyUp(object sender, KeyRoutedEventArgs args)
    {
        acceleratorTable?.TrackModifier(args.Key, down: false);
    }

    private static MenuBarItem BuildMenu(string title, params Command[] commands)
    {
        MenuBarItem menu = new() { Title = title };

        foreach (Command command in commands)
        {
            //A null entry is a separator - it keeps the call sites readable
            //next to upstream's menu-model code.
            menu.Items.Add(command is null
                ? CommandMenuBuilder.CreateSeparator()
                : CommandMenuBuilder.Create(command));
        }

        return menu;
    }

    // ---- The in-app toolbar row -------------------------------------------

    private void BuildMainToolbar()
    {
        ActionManager actions = PintaCore.Actions;

        MainToolbarPanel.Children.Clear();

        AddToolbarButton(actions.File.New);
        AddToolbarButton(actions.File.Open);
        AddToolbarButton(actions.File.Save);
        AddToolbarSeparator();
        AddToolbarButton(actions.Edit.Undo);
        AddToolbarButton(actions.Edit.Redo);
        AddToolbarSeparator();
        AddToolbarButton(actions.Edit.Cut);
        AddToolbarButton(actions.Edit.Copy);
        AddToolbarButton(actions.Edit.Paste);
        AddToolbarSeparator();
        AddToolbarButton(actions.Image.CropToSelection);
        AddToolbarButton(actions.Edit.Deselect);
    }

    private void AddToolbarButton(Command command)
    {
        Button button = new()
        {
            Padding = new Thickness(6, 4, 6, 4),
            IsEnabled = command.Sensitive,
        };

        var source = IconImageSource.Create(command.IconName, ToolbarIconSize);

        //Fall back to the label when an icon will not resolve, so a button is
        //never a blank rectangle.
        if (source is null)
        {
            button.Content = command.ShortLabel ?? command.Label;
        }
        else
        {
            button.Content = new Image
            {
                Width = ToolbarIconSize,
                Height = ToolbarIconSize,
                Source = source,
            };
        }

        ToolTipService.SetToolTip(button, ToolbarTooltip(command));
        button.Click += (_, _) => command.Activate();
        command.SensitiveChanged += (_, _) => button.IsEnabled = command.Sensitive;

        MainToolbarPanel.Children.Add(button);
    }

    private static string ToolbarTooltip(Command command)
    {
        string label = command.ShortLabel ?? command.Label;

        //Trim the trailing ellipsis that marks a dialog-opening command; a
        //tooltip does not need it.
        label = label.TrimEnd('.');

        string shortcut = command.Shortcuts.FirstOrDefault() ?? string.Empty;

        return string.IsNullOrEmpty(shortcut)
            ? label
            : $"{label} ({FormatShortcut(shortcut)})";
    }

    private static string FormatShortcut(string accelerator) => accelerator
        .Replace("<Primary>", "Ctrl+")
        .Replace("<Ctrl>", "Ctrl+")
        .Replace("<Control>", "Ctrl+")
        .Replace("<Shift>", "Shift+")
        .Replace("<Alt>", "Alt+");

    private void AddToolbarSeparator() =>
        MainToolbarPanel.Children.Add(new Border
        {
            Width = 1,
            Margin = new Thickness(4, 2, 4, 2),
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlForegroundBaseLowBrush"],
        });

    // ---- Pad toolbars ------------------------------------------------------

    private const int PadIconSize = 16;

    /// <summary>
    /// Builds the layers and history pad toolbars. Upstream's layers pad has
    /// Add / Delete / Duplicate / Merge Down / Move Up / Move Down plus a
    /// hamburger menu; the history pad has Undo and Redo.
    /// </summary>
    private void BuildPadToolbars()
    {
        ActionManager actions = PintaCore.Actions;

        LayersPadToolbar.Children.Clear();
        AddPadButton(LayersPadToolbar, actions.Layers.AddNewLayer, "+");
        AddPadButton(LayersPadToolbar, actions.Layers.DeleteLayer, "\u2212");
        AddPadButton(LayersPadToolbar, actions.Layers.DuplicateLayer, "\u29C9");
        AddPadButton(LayersPadToolbar, actions.Layers.MergeLayerDown, "\u21A7");
        AddPadButton(LayersPadToolbar, actions.Layers.MoveLayerUp, "\u25B2");
        AddPadButton(LayersPadToolbar, actions.Layers.MoveLayerDown, "\u25BC");
        AddPadButton(LayersPadToolbar, actions.Layers.Properties, "\u2699");

        HistoryPadToolbar.Children.Clear();
        AddPadButton(HistoryPadToolbar, actions.Edit.Undo, "\u21B6");
        AddPadButton(HistoryPadToolbar, actions.Edit.Redo, "\u21B7");
    }

    private static void AddPadButton(Panel host, Command command, string fallbackGlyph)
    {
        Button button = new()
        {
            Padding = new Thickness(4, 2, 4, 2),
            MinWidth = 30,
            IsEnabled = command.Sensitive,
        };

        var source = IconImageSource.Create(command.IconName, PadIconSize);

        //Most of these DO have Pinta icons; the glyph is the fallback for the
        //few that map to absent standard-icon names.
        if (source is null)
        {
            button.Content = fallbackGlyph;
        }
        else
        {
            button.Content = new Image { Width = PadIconSize, Height = PadIconSize, Source = source };
        }

        ToolTipService.SetToolTip(button, ToolbarTooltip(command));
        button.Click += (_, _) => command.Activate();
        command.SensitiveChanged += (_, _) => button.IsEnabled = command.Sensitive;

        host.Children.Add(button);
    }

    // ---- Registry-driven menus --------------------------------------------

    private void RebuildAdjustmentsMenu()
    {
        adjustmentsMenu.Items.Clear();

        foreach (Command command in PintaCore.Effects.AdjustmentCommands)
        {
            adjustmentsMenu.Items.Add(CommandMenuBuilder.Create(command));
        }
    }

    private void RebuildEffectsMenu()
    {
        effectsMenu.Items.Clear();

        foreach (string category in PintaCore.Effects.EffectCategories.OrderBy(c => c))
        {
            effectsMenu.Items.Add(CommandMenuBuilder.CreateSubmenu(
                category,
                PintaCore.Effects.GetEffectCommands(category)));
        }
    }

    /// <summary>
    /// Refreshes the Window menu's open-document list (Alt+1 .. Alt+9),
    /// leaving the fixed Save All / Close All entries in place.
    /// </summary>
    private void RebuildWindowMenu()
    {
        while (windowMenu.Items.Count > windowMenuFixedItemCount)
        {
            windowMenu.Items.RemoveAt(windowMenu.Items.Count - 1);
        }

        IReadOnlyList<Document> documents = PintaCore.Workspace.OpenDocuments;

        for (int i = 0; i < documents.Count; i++)
        {
            Document document = documents[i];

            MenuFlyoutItem item = new()
            {
                Text = document.IsDirty ? $"{document.DisplayName}*" : document.DisplayName,
                IsEnabled = true,
            };

            if (i < 9)
            {
                item.KeyboardAccelerators.Add(new Microsoft.UI.Xaml.Input.KeyboardAccelerator
                {
                    Key = Windows.System.VirtualKey.Number1 + i,
                    Modifiers = Windows.System.VirtualKeyModifiers.Menu,
                });
            }

            int index = i;
            item.Click += (_, _) => PintaCore.Workspace.SetActiveDocument(index);
            windowMenu.Items.Add(item);
        }
    }
}
