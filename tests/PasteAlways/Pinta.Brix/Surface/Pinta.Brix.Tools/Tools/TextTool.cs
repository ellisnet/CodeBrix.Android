/////////////////////////////////////////////////////////////////////////////////
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Olivier Dufour <olivier.duff@gmail.com>                 //
//                     Jonathan Pobst <monkey@jpobst.com>                      //
/////////////////////////////////////////////////////////////////////////////////

// Pinta.Brix notes, all deliberate divergences from upstream:
//  * Text layout runs on the CodeBrix.Platform text-layout add-in through the
//    engine's TextLayout wrapper - there is no Pango and no Pango context.
//  * There is no input-method/preedit support yet (plan G9/6.1c): characters
//    come directly from key events via a US-layout keysym-to-character map.
//    Dead keys and IME composition are not available in this V1.
//  * The font picker is a plain family combo fed by SystemFonts.Families
//    (G10); the Pango.Variant dropdown is dropped outright (G7), and the
//    weight dropdown carries the OpenType 100..900 scale only (G8).
//  * Shift/Ctrl+Insert clipboard shortcuts are not wired; Ctrl+C/X/V arrive
//    through the tool's clipboard seams instead.

using System;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using System.Threading.Tasks;

//was previously: namespace Pinta.Tools;
namespace Pinta.Brix.Tools;

public sealed class TextTool : BaseTool
{
	// Variables for dragging
	private PointD start_mouse_xy;
	private PointI start_click_point;
	private bool tracking;
	private readonly ToolCursor cursor_move = ToolCursor.FromShape (StandardCursor.Move);
	private readonly ToolCursor cursor_invalid = ToolCursor.FromShape (StandardCursor.NotAllowed);

	private PointI click_point;
	private bool is_editing;
	private RectangleI old_cursor_bounds = RectangleI.Zero;

	//This is used to temporarily store the UserLayer's and TextLayer's previous ImageSurface states.
	private ImageSurface? text_undo_surface;
	private ImageSurface? user_undo_surface;
	private TextEngine? undo_engine;
	// The selection from when editing started. This ensures that text doesn't suddenly disappear/appear
	// if the selection changes before the text is finalized.
	private DocumentSelection? selection;

	private readonly TextLayout layout = new ();

	private RectangleI CurrentTextBounds {
		get => workspace.ActiveDocument.Layers.CurrentUserLayer.TextBounds;

		set {
			workspace.ActiveDocument.Layers.CurrentUserLayer.PreviousTextBounds = workspace.ActiveDocument.Layers.CurrentUserLayer.TextBounds;
			workspace.ActiveDocument.Layers.CurrentUserLayer.TextBounds = value;
		}
	}

	private TextEngine CurrentTextEngine {
		get {
			if (!workspace.HasOpenDocuments)
				throw new InvalidOperationException ("Attempting to get CurrentTextEngine when there are no open documents");

			return workspace.ActiveDocument.Layers.CurrentUserLayer.TextEngine;
		}
	}

	private TextLayout CurrentTextLayout {
		get {
			if (layout.Engine != CurrentTextEngine)
				layout.Engine = CurrentTextEngine;
			return layout;
		}
	}

	//While this is true, text will not be finalized upon Surface.Clone calls.
	private bool ignore_clone_finalizations = false;

	//Whether or not either (or both) of the Ctrl keys are pressed.
	private bool ctrl_key = false;

	//Store the most recent mouse position.
	private PointI last_mouse_position = new (0, 0);

	public override string Name
		=> Translations.GetString ("Text");

	private static string FinalizeName
		=> Translations.GetString ("Text - Finalize");

	public override string Icon
		=> Icons.ToolText;

	public override Key ShortcutKey
		=> new (KeyConstants.KEY_T);

	public override int Priority
		=> 35;

	public override string StatusBarText
		=> Translations.GetString ("Left click to place cursor, then type desired text. Text color is primary color.");

	public override ToolCursor DefaultCursor { get; }

	protected override bool ShowAntialiasingButton => true;

	private readonly IChromeService chrome;
	private readonly IPaletteService palette;
	private readonly IWorkspaceService workspace;
	public TextTool (IServiceProvider services) : base (services)
	{
		chrome = services.GetService<IChromeService> ();
		palette = services.GetService<IPaletteService> ();
		workspace = services.GetService<IWorkspaceService> ();

		DefaultCursor = ToolCursor.FromShape (StandardCursor.IBeam);
	}

	#region ToolBar
	// NRT - Created by OnBuildToolBar
	private ToolBarLabel font_label = null!;
	private ToolBarComboBox font_combo = null!;
	private ToolBarSpinButton font_size = null!;
	private ToolBarDropDownButton weight_btn = null!;
	private ToolBarToggleButton italic_btn = null!;
	private ToolBarToggleButton underscore_btn = null!;
	private ToolBarToggleButton left_alignment_btn = null!;
	private ToolBarToggleButton center_alignment_btn = null!;
	private ToolBarToggleButton right_alignment_btn = null!;
	private ToolBarLabel fill_label = null!;
	private ToolBarDropDownButton fill_button = null!;
	private ToolBarSeparator fill_sep = null!;
	private ToolBarSeparator outline_sep = null!;
	private ToolBarSpinButton outline_width = null!;
	private ToolBarLabel outline_width_label = null!;
	private ToolBarSeparator join_sep = null!;
	private ToolBarDropDownButton join_btn = null!;

	protected override void OnBuildToolBar (ToolBar tb)
	{
		base.OnBuildToolBar (tb);

		if (font_label == null) {
			string fontText = Translations.GetString ("Font");
			font_label = new ToolBarLabel ($" {fontText}: ");
		}

		tb.Append (font_label);

		if (font_combo == null) {
			var families = SystemFonts.Families;
			font_combo = ToolBarComboBox.New (150, 0, false, [.. families]);

			string saved = Settings.GetSetting (SettingNames.TEXT_FONT, "Sans");
			int index = -1;
			for (int i = 0; i < families.Count; i++) {
				if (string.Equals (families[i], saved, StringComparison.OrdinalIgnoreCase)) {
					index = i;
					break;
				}
			}
			if (index >= 0)
				font_combo.SelectedIndex = index;
			else if (families.Count > 0)
				font_combo.SelectedIndex = 0;

			font_combo.SelectedItemChanged += (_, _) => HandleFontChanged ();
		}

		tb.Append (font_combo);

		tb.Append (new ToolBarSeparator ());

		if (font_size == null) {
			font_size = new ToolBarSpinButton (1, 2000, 1, Settings.GetSetting (SettingNames.TEXT_SIZE, 12));
			font_size.TooltipText = Translations.GetString ("Change font size.") + "\n"
				   + "\n" + Translations.GetString ("Shortcut keys:")
				   + "\n" + Translations.GetString ("Press {0} to decrease font size", "\"[\"")
				   + "\n" + Translations.GetString ("Press {0} to increase font size", "\"]\"");
			font_size.ValueChanged += (_, _) => HandleFontChanged ();
		}

		tb.Append (font_size);

		tb.Append (new ToolBarSeparator ());

		if (weight_btn == null) {
			weight_btn = ToolBarDropDownButton.New ();

			weight_btn.AddItem (Translations.GetString ("Thin") + " 100", Icons.TextExtraLight, 100);
			weight_btn.AddItem (Translations.GetString ("Ultralight") + " 200", Icons.TextExtraLight, 200);
			weight_btn.AddItem (Translations.GetString ("Light") + " 300", Icons.TextLight, 300);
			weight_btn.AddItem (Translations.GetString ("Normal") + " 400", Icons.TextNormal, 400);
			weight_btn.AddItem (Translations.GetString ("Medium") + " 500", Icons.TextNormal, 500);
			weight_btn.AddItem (Translations.GetString ("Semibold") + " 600", Icons.TextBold, 600);
			weight_btn.AddItem (Translations.GetString ("Bold") + " 700", Icons.TextBold, 700);
			weight_btn.AddItem (Translations.GetString ("Ultrabold") + " 800", Icons.TextExtraBold, 800);
			weight_btn.AddItem (Translations.GetString ("Heavy") + " 900", Icons.TextExtraBold, 900);

			weight_btn.SelectedIndex = Settings.GetSetting (SettingNames.TEXT_WEIGHT, 3);
			weight_btn.SelectedItemChanged += (_, _) => UpdateFont ();
		}

		tb.Append (weight_btn);

		if (italic_btn == null) {
			italic_btn = new ToolBarToggleButton {
				IconName = StandardIcons.FormatTextItalic,
				Label = Translations.GetString ("Italic"),
				TooltipText = Translations.GetString ("Italic"),
			};
			italic_btn.Active = Settings.GetSetting (SettingNames.TEXT_ITALIC, false);
			italic_btn.Toggled += (_, _) => UpdateFont ();
		}

		tb.Append (italic_btn);

		if (underscore_btn == null) {
			underscore_btn = new ToolBarToggleButton {
				IconName = StandardIcons.FormatTextUnderline,
				Label = Translations.GetString ("Underline"),
				TooltipText = Translations.GetString ("Underline"),
			};
			underscore_btn.Active = Settings.GetSetting (SettingNames.TEXT_UNDERLINE, false);
			underscore_btn.Toggled += (_, _) => UpdateFont ();
		}

		tb.Append (underscore_btn);

		tb.Append (new ToolBarSeparator ());

		TextAlignment alignment = (TextAlignment) Settings.GetSetting (SettingNames.TEXT_ALIGNMENT, (int) TextAlignment.Left);

		if (left_alignment_btn == null) {
			left_alignment_btn = new ToolBarToggleButton {
				IconName = StandardIcons.FormatJustifyLeft,
				Label = Translations.GetString ("Left"),
				TooltipText = Translations.GetString ("Left Align"),
			};
			left_alignment_btn.Active = alignment == TextAlignment.Left;
			left_alignment_btn.Toggled += HandleLeftAlignmentButtonToggled;
		}

		tb.Append (left_alignment_btn);

		if (center_alignment_btn == null) {
			center_alignment_btn = new ToolBarToggleButton {
				IconName = StandardIcons.FormatJustifyCenter,
				Label = Translations.GetString ("Center"),
				TooltipText = Translations.GetString ("Center Align"),
			};
			center_alignment_btn.Active = alignment == TextAlignment.Center;
			center_alignment_btn.Toggled += HandleCenterAlignmentButtonToggled;
		}

		tb.Append (center_alignment_btn);

		if (right_alignment_btn == null) {
			right_alignment_btn = new ToolBarToggleButton {
				IconName = StandardIcons.FormatJustifyRight,
				Label = Translations.GetString ("Right"),
				TooltipText = Translations.GetString ("Right Align"),
			};
			right_alignment_btn.Active = alignment == TextAlignment.Right;
			right_alignment_btn.Toggled += HandleRightAlignmentButtonToggled;
		}

		tb.Append (right_alignment_btn);

		fill_sep ??= new ToolBarSeparator ();

		tb.Append (fill_sep);

		if (fill_label == null) {
			string textStyleText = Translations.GetString ("Text Style");
			fill_label = new ToolBarLabel ($" {textStyleText}: ");
		}

		tb.Append (fill_label);

		if (fill_button == null) {
			fill_button = ToolBarDropDownButton.New ();

			fill_button.AddItem (Translations.GetString ("Normal"), Icons.FillStyleFill, 0);
			fill_button.AddItem (Translations.GetString ("Normal and Outline"), Icons.FillStyleOutlineFill, 1);
			fill_button.AddItem (Translations.GetString ("Outline"), Icons.FillStyleOutline, 2);
			fill_button.AddItem (Translations.GetString ("Fill Background"), Icons.FillStyleBackground, 3);

			fill_button.SelectedIndex = Settings.GetSetting (SettingNames.TEXT_STYLE, 0);
			fill_button.SelectedItemChanged += HandleFillButtonToggled;
		}

		tb.Append (fill_button);

		outline_sep ??= new ToolBarSeparator ();

		tb.Append (outline_sep);

		if (outline_width_label == null) {
			string outlineWidthText = Translations.GetString ("Outline width");
			outline_width_label = new ToolBarLabel ($" {outlineWidthText}: ");
		}

		tb.Append (outline_width_label);

		if (outline_width == null) {
			outline_width = new ToolBarSpinButton (1, 1e5, 1, Settings.GetSetting (SettingNames.TEXT_OUTLINE_WIDTH, 2));
			outline_width.ValueChanged += (_, _) => HandleFontChanged ();
		}

		tb.Append (outline_width);

		join_sep ??= new ToolBarSeparator ();

		tb.Append (join_sep);

		if (join_btn == null) {
			join_btn = ToolBarDropDownButton.New ();

			join_btn.AddItem (Translations.GetString ("Miter Join"), Icons.JoinMiter, LineJoin.Miter);
			join_btn.AddItem (Translations.GetString ("Round Join"), Icons.JoinRound, LineJoin.Round);
			join_btn.AddItem (Translations.GetString ("Bevel Join"), Icons.JoinBevel, LineJoin.Bevel);

			join_btn.SelectedIndex = Settings.GetSetting (SettingNames.TEXT_JOIN, 0);
			join_btn.SelectedItemChanged += HandleJoinButtonToggled;
		}

		tb.Append (join_btn);

		outline_width.Visible = outline_width_label.Visible = outline_sep.Visible = join_btn.Visible = join_sep.Visible = StrokeText;

		UpdateFont ();
	}

	protected override void OnSaveSettings (ISettingsService settings)
	{
		base.OnSaveSettings (settings);

		if (font_combo is not null)
			settings.PutSetting (SettingNames.TEXT_FONT, font_combo.Text);

		if (font_size is not null)
			settings.PutSetting (SettingNames.TEXT_SIZE, font_size.GetValueAsInt ());

		if (weight_btn is not null)
			settings.PutSetting (SettingNames.TEXT_WEIGHT, weight_btn.SelectedIndex);

		if (italic_btn is not null)
			settings.PutSetting (SettingNames.TEXT_ITALIC, italic_btn.Active);

		if (underscore_btn is not null)
			settings.PutSetting (SettingNames.TEXT_UNDERLINE, underscore_btn.Active);

		if (left_alignment_btn is not null)
			settings.PutSetting (SettingNames.TEXT_ALIGNMENT, (int) Alignment);

		if (fill_button is not null)
			settings.PutSetting (SettingNames.TEXT_STYLE, fill_button.SelectedIndex);

		if (outline_width is not null)
			settings.PutSetting (SettingNames.TEXT_OUTLINE_WIDTH, outline_width.GetValueAsInt ());

		if (join_btn is not null)
			settings.PutSetting (SettingNames.TEXT_JOIN, join_btn.SelectedIndex);
	}

	private void HandleFontChanged ()
	{
		if (workspace.HasOpenDocuments)
			workspace.ActiveDocument.Workspace.GrabFocusToCanvas ();

		UpdateFont ();
	}

	private TextAlignment Alignment {
		get {
			if (right_alignment_btn.Active)
				return TextAlignment.Right;
			else if (center_alignment_btn.Active)
				return TextAlignment.Center;
			else
				return TextAlignment.Left;
		}
	}

	private void HandlePintaCorePalettePrimaryColorChanged (object? sender, EventArgs e)
	{
		UpdateTextEngineColor ();
		if (is_editing || (workspace.HasOpenDocuments && CurrentTextEngine.State == TextMode.NotFinalized))
			RedrawText (is_editing, true);
	}

	private void HandleLeftAlignmentButtonToggled (object? sender, EventArgs e)
	{
		if (left_alignment_btn.Active) {
			right_alignment_btn.Active = false;
			center_alignment_btn.Active = false;
		} else if (!right_alignment_btn.Active && !center_alignment_btn.Active) {
			left_alignment_btn.Active = true;
		}

		UpdateFont ();
	}

	private void HandleCenterAlignmentButtonToggled (object? sender, EventArgs e)
	{
		if (center_alignment_btn.Active) {
			right_alignment_btn.Active = false;
			left_alignment_btn.Active = false;
		} else if (!right_alignment_btn.Active && !left_alignment_btn.Active) {
			center_alignment_btn.Active = true;
		}

		UpdateFont ();
	}

	private void HandleRightAlignmentButtonToggled (object? sender, EventArgs e)
	{
		if (right_alignment_btn.Active) {
			center_alignment_btn.Active = false;
			left_alignment_btn.Active = false;
		} else if (!center_alignment_btn.Active && !left_alignment_btn.Active) {
			right_alignment_btn.Active = true;
		}

		UpdateFont ();
	}

	private void HandleFillButtonToggled (object? sender, EventArgs e)
	{
		outline_width.Visible = outline_width_label.Visible = outline_sep.Visible = join_btn.Visible = join_sep.Visible = StrokeText;

		UpdateFont ();
	}

	private void HandleJoinButtonToggled (object? sender, EventArgs e)
	{
		UpdateFont ();
	}

	private void HandleSelectedLayerChanged (object? sender, EventArgs e)
	{
		UpdateFont ();
	}

	protected override void OnAntialiasingChanged ()
	{
		UpdateFont ();
	}

	private void UpdateFont ()
	{
		if (workspace.HasOpenDocuments) {
			FontDescription font = new () {
				Family = font_combo.SelectedIndex >= 0 ? font_combo.Text : "Sans",
				Size = font_size.GetValueAsInt (),
				Weight = weight_btn.SelectedItem.GetTagOrDefault (400),
				Italic = italic_btn.Active,
			};

			CurrentTextEngine.SetFont (font, Alignment, underscore_btn.Active);
		}

		if (is_editing || (workspace.HasOpenDocuments && CurrentTextEngine.State == TextMode.NotFinalized))
			RedrawText (is_editing, true);
	}

	private void UpdateTextEngineColor ()
	{
		if (!workspace.HasOpenDocuments)
			return;
		CurrentTextEngine.PrimaryColor = palette.PrimaryColor;
		CurrentTextEngine.SecondaryColor = palette.SecondaryColor;
	}

	private int OutlineWidth
		=> outline_width.GetValueAsInt ();

	private bool StrokeText
		=> fill_button.SelectedItem.GetTagOrDefault (0) >= 1 && fill_button.SelectedItem.GetTagOrDefault (0) != 3;

	private bool FillText
		=> fill_button.SelectedItem.GetTagOrDefault (0) <= 1 || fill_button.SelectedItem.GetTagOrDefault (0) == 3;

	private bool BackgroundFill
		=> fill_button.SelectedItem.GetTagOrDefault (0) == 3;

	#endregion

	#region Activation/Deactivation
	protected override void OnActivated (Document? document)
	{
		base.OnActivated (document);

		// We may need to redraw our text when the color changes
		palette.PrimaryColorChanged += HandlePintaCorePalettePrimaryColorChanged;
		palette.SecondaryColorChanged += HandlePintaCorePalettePrimaryColorChanged;

		workspace.LayerAdded += HandleSelectedLayerChanged;
		workspace.LayerRemoved += HandleSelectedLayerChanged;
		workspace.SelectedLayerChanged += HandleSelectedLayerChanged;

		// We always start off not in edit mode
		is_editing = false;
	}

	protected override void OnCommit (Document? document)
	{
		StopEditing (false);
	}

	protected override void OnDeactivated (Document? document, BaseTool? newTool)
	{
		base.OnDeactivated (document, newTool);

		// Stop listening for color change events
		palette.PrimaryColorChanged -= HandlePintaCorePalettePrimaryColorChanged;
		palette.SecondaryColorChanged -= HandlePintaCorePalettePrimaryColorChanged;

		workspace.LayerAdded -= HandleSelectedLayerChanged;
		workspace.LayerRemoved -= HandleSelectedLayerChanged;
		workspace.SelectedLayerChanged -= HandleSelectedLayerChanged;

		StopEditing (false);
	}
	#endregion

	#region Mouse Handlers
	protected override void OnMouseDown (Document document, ToolMouseEventArgs e)
	{
		ctrl_key = e.IsControlPressed;
		selection = document.Selection.Clone ();

		switch (e.MouseButton) {
			case MouseButton.Right:
				HandleRightClick (document, e);
				break;
			case MouseButton.Left:
				HandleLeftClick (document, e);
				break;
		}
	}

	private void HandleLeftClick (Document document, ToolMouseEventArgs e)
	{
		//Store the mouse position.
		PointI pt = e.Point;

		// If the user is [editing or holding down Ctrl] and clicked
		//within the text, move the cursor to the click location
		if ((is_editing || ctrl_key) && CurrentTextBounds.Contains (pt)) {
			StartEditing ();

			//Change the position of the cursor to where the mouse clicked.
			TextPosition p = CurrentTextLayout.PointToTextPosition (pt);
			CurrentTextEngine.SetCursorPosition (p, true);

			//Redraw the text with the new cursor position.
			RedrawText (true, true);

			return;
		}

		// We're already editing and the user clicked outside the text,
		// commit the user's work, and start a new edit
		switch (CurrentTextEngine.State) {
			// We were editing, save and stop
			case TextMode.Uncommitted:
				StopEditing (true);
				break;

			// We were editing, but nothing had been
			// keyed. Stop editing.
			case TextMode.Unchanged:
				StopEditing (false);
				break;
		}

		if (ctrl_key) {
			//Go through every UserLayer.
			foreach (UserLayer ul in document.Layers.UserLayers) {
				//Check each UserLayer's editable text boundaries to see if they contain the mouse position.
				if (!ul.TextBounds.Contains (pt))
					continue;

				//The mouse clicked on editable text.

				//Change the current UserLayer to the Layer that contains the text that was clicked on.
				document.Layers.SetCurrentUserLayer (ul);

				//The user is editing text now.
				is_editing = true;

				//Set the cursor in the editable text where the mouse was clicked.
				TextPosition p = CurrentTextLayout.PointToTextPosition (pt);
				CurrentTextEngine.SetCursorPosition (p, true);

				//Redraw the editable text with the cursor.
				RedrawText (true, true);

				//Don't check any more UserLayers - stop at the first UserLayer that has editable text containing the mouse position.
				return;
			}
		} else {
			if (CurrentTextEngine.State == TextMode.NotFinalized) {
				//The user is making a new text and the old text hasn't been finalized yet.
				FinalizeText ();
			}

			if (is_editing)
				return;

			// Start editing at the cursor location
			click_point = pt;
			CurrentTextEngine.Clear ();
			UpdateFont ();
			click_point = click_point with { Y = click_point.Y - (CurrentTextLayout.FontHeight / 2) };
			CurrentTextEngine.Origin = click_point;
			StartEditing ();
			RedrawText (true, true);
		}
	}

	private void HandleRightClick (Document document, ToolMouseEventArgs e)
	{
		// A right click allows you to move the text around

		//The user is dragging text with the right mouse button held down, so track the mouse as it moves.
		tracking = true;

		//Remember the position of the mouse before the text is dragged.
		start_mouse_xy = e.PointDouble;
		start_click_point = click_point;

		//Change the cursor to indicate that the text is being dragged.
		UpdateMouseCursor (document);
	}

	protected override void OnMouseMove (Document document, ToolMouseEventArgs e)
	{
		ctrl_key = e.IsControlPressed;

		last_mouse_position = e.Point;

		// If we're dragging the text around, do that
		if (tracking) {
			PointD delta = new (
				e.PointDouble.X - start_mouse_xy.X,
				e.PointDouble.Y - start_mouse_xy.Y);

			click_point = new PointI ((int) (start_click_point.X + delta.X), (int) (start_click_point.Y + delta.Y));
			CurrentTextEngine.Origin = click_point;

			RedrawText (true, true);
		} else {
			UpdateMouseCursor (document);
		}
	}

	protected override void OnMouseUp (Document document, ToolMouseEventArgs e)
	{
		// If we were dragging the text around, finish that up
		if (!tracking)
			return;

		PointD delta = new (e.PointDouble.X - start_mouse_xy.X, e.PointDouble.Y - start_mouse_xy.Y);

		click_point = new PointI ((int) (start_click_point.X + delta.X), (int) (start_click_point.Y + delta.Y));
		CurrentTextEngine.Origin = click_point;

		RedrawText (false, true);
		tracking = false;
		UpdateMouseCursor (document);
	}

	private void UpdateMouseCursor (Document document)
	{
		if (tracking) {
			SetCursor (cursor_move);
			return;
		}

		//Whether or not to show the normal text cursor.
		ToolCursor newCursor = cursor_invalid;

		if (ctrl_key && workspace.HasOpenDocuments) {
			//Go through every UserLayer.
			foreach (UserLayer ul in document.Layers.UserLayers) {
				if (!ul.TextBounds.Contains (last_mouse_position))
					continue; //Check each UserLayer's editable text boundaries to see if they contain the mouse position.
				newCursor = DefaultCursor; //The mouse is over editable text.
			}
		} else {
			newCursor = DefaultCursor;
		}

		if (newCursor != CurrentCursor) {
			SetCursor (newCursor);
			RedrawText (is_editing, true);
		}
	}
	#endregion

	#region Keyboard Handlers

	protected override bool OnKeyDown (Document document, ToolKeyEventArgs e)
	{
		if (!workspace.HasOpenDocuments)
			return false;

		// If we are dragging the text, we
		// aren't going to handle key presses
		if (tracking)
			return false;

		// Ignore anything with Alt pressed
		if (e.IsAltPressed)
			return false;

		ctrl_key = e.Key.IsControlKey ();
		UpdateMouseCursor (document);

		bool keyHandled = false;
		if (is_editing) {
			// Assume that we are going to handle the key
			keyHandled = true;

			switch (e.Key.Value) {
				case KeyConstants.KEY_BackSpace:
					CurrentTextEngine.PerformBackspace (e.IsControlPressed);
					break;

				case KeyConstants.KEY_Delete:
					CurrentTextEngine.PerformDelete ();
					break;

				case KeyConstants.KEY_KP_Enter:
				case KeyConstants.KEY_Return:
					CurrentTextEngine.PerformEnter ();
					break;

				case KeyConstants.KEY_Left:
					CurrentTextEngine.PerformLeft (e.IsControlPressed, e.IsShiftPressed);
					break;

				case KeyConstants.KEY_Right:
					CurrentTextEngine.PerformRight (e.IsControlPressed, e.IsShiftPressed);
					break;

				case KeyConstants.KEY_Up:
					CurrentTextEngine.PerformUp (e.IsShiftPressed);
					break;

				case KeyConstants.KEY_Down:
					CurrentTextEngine.PerformDown (e.IsShiftPressed);
					break;

				case KeyConstants.KEY_Home:
					CurrentTextEngine.PerformHome (e.IsControlPressed, e.IsShiftPressed);
					break;

				case KeyConstants.KEY_End:
					CurrentTextEngine.PerformEnd (e.IsControlPressed, e.IsShiftPressed);
					break;

				case KeyConstants.KEY_Next:
				case KeyConstants.KEY_Prior:
					break;

				case KeyConstants.KEY_Escape:
					StopEditing (false);
					return true;

				default:
					if (e.IsControlPressed) {
						if (e.Key.Value == KeyConstants.KEY_z) {
							//Ctrl + Z for undo while editing.
							OnHandleUndo (document);

							if (workspace.ActiveDocument.History.CanUndo)
								workspace.ActiveDocument.History.Undo ();

							return true;
						} else if (e.Key.Value == KeyConstants.KEY_i) {
							italic_btn.Toggle ();
							UpdateFont ();
						} else if (e.Key.Value == KeyConstants.KEY_b) {
							// If the current weight is Bold (700) or bolder, set to Normal (400). Otherwise, set to Bold (700).
							weight_btn.SelectedIndex = weight_btn.SelectedIndex >= 6 ? 3 : 6;
							UpdateFont ();
						} else if (e.Key.Value == KeyConstants.KEY_u) {
							underscore_btn.Toggle ();
							UpdateFont ();
						} else if (e.Key.Value == KeyConstants.KEY_a) {
							// Select all of the text.
							CurrentTextEngine.PerformHome (true, false);
							CurrentTextEngine.PerformEnd (true, true);
						} else {
							//Ignore command shortcut.
							return false;
						}
					} else {
						keyHandled = TryHandleChar (e);
					}

					break;
			}

			if (keyHandled)
				RedrawText (true, true);
		} else {
			switch (e.Key.Value) {
				case KeyConstants.KEY_bracketleft:
					font_size.Value--;
					return true;
				case KeyConstants.KEY_bracketright:
					font_size.Value++;
					return true;
			}
		}

		return keyHandled;
	}

	protected override bool OnKeyUp (Document document, ToolKeyEventArgs e)
	{
		if (!e.Key.IsControlKey () && !e.IsControlPressed)
			return false;

		ctrl_key = false;

		UpdateMouseCursor (document);
		return false;
	}

	private bool TryHandleChar (ToolKeyEventArgs e)
	{
		char? c = KeyToChar (e.Key.Value, e.IsShiftPressed);

		if (c is null)
			return false;

		CurrentTextEngine.InsertText (c.Value.ToString ());
		return true;
	}

	/// <summary>
	/// Pinta.Brix note (G9): with no input-method seam yet, printable
	/// characters are derived from the ASCII keysym plus the shift state,
	/// assuming a US layout. Dead keys and IME composition are unsupported.
	/// </summary>
	private static char? KeyToChar (uint keysym, bool shift)
	{
		if (keysym < 0x20 || keysym > 0x7e)
			return null;

		char c = (char) keysym;

		if (char.IsAsciiLetter (c))
			return shift ? char.ToUpperInvariant (c) : c;

		if (!shift)
			return c;

		return c switch {
			'1' => '!',
			'2' => '@',
			'3' => '#',
			'4' => '$',
			'5' => '%',
			'6' => '^',
			'7' => '&',
			'8' => '*',
			'9' => '(',
			'0' => ')',
			'-' => '_',
			'=' => '+',
			'[' => '{',
			']' => '}',
			'\\' => '|',
			';' => ':',
			'\'' => '"',
			',' => '<',
			'.' => '>',
			'/' => '?',
			'`' => '~',
			_ => c,
		};
	}

	#endregion

	#region Start/Stop Editing

	private void StartEditing ()
	{
		// Ensure we have an event handler added to finalize re-editable text for the document if the layer is cloned.
		workspace.ActiveDocument.LayerCloned -= FinalizeText;
		workspace.ActiveDocument.LayerCloned += FinalizeText;

		is_editing = true;

		selection ??= workspace.ActiveDocument.Selection.Clone ();

		//Start ignoring any Surface.Clone calls from this point on (so that it doesn't start to loop).
		ignore_clone_finalizations = true;

		//Store the previous state of the current UserLayer's and TextLayer's ImageSurfaces.
		user_undo_surface = workspace.ActiveDocument.Layers.CurrentUserLayer.Surface.Clone ();
		text_undo_surface = workspace.ActiveDocument.Layers.CurrentUserLayer.TextLayer.Layer.Surface.Clone ();

		//Store the previous state of the Text Engine.
		undo_engine = CurrentTextEngine.Clone ();

		//Update Text Engine to use current colors of color palette
		UpdateTextEngineColor ();

		//Stop ignoring any Surface.Clone calls from this point on.
		ignore_clone_finalizations = false;
	}

	private void StopEditing (bool finalize)
	{
		if (!workspace.HasOpenDocuments)
			return;

		if (!is_editing)
			return;

		is_editing = false;

		//Make sure that neither undo surface is null, the user is editing, and there are uncommitted changes.
		if (text_undo_surface != null && user_undo_surface != null && CurrentTextEngine.State == TextMode.Uncommitted) {
			Document doc = workspace.ActiveDocument;

			RedrawText (false, true);

			//Start ignoring any Surface.Clone calls from this point on (so that it doesn't start to loop).
			ignore_clone_finalizations = true;

			//Create a new TextHistoryItem so that the committing of text can be undone.
			doc.History.PushNewItem (
				new TextHistoryItem (
					workspace,
					Icon,
					Name,
					text_undo_surface.Clone (),
					user_undo_surface.Clone (),
					undo_engine!.Clone (), // NRT - Set in StartEditing
					doc.Layers.CurrentUserLayer
				)
			);

			//Stop ignoring any Surface.Clone calls from this point on.
			ignore_clone_finalizations = false;

			//Now that the text has been committed, change its state.
			CurrentTextEngine.State = TextMode.NotFinalized;
		}

		RedrawText (false, true);

		if (finalize) {
			FinalizeText ();
		}
	}
	#endregion

	#region Text Drawing Methods
	/// <summary>
	/// Clears the entire TextLayer and redraw the previous text boundary.
	/// </summary>
	private void ClearTextLayer ()
	{
		//Clear the TextLayer.
		workspace.ActiveDocument.Layers.CurrentUserLayer.TextLayer.Layer.Surface.Clear ();

		//Redraw the previous text boundary.
		InflateAndInvalidate (workspace.ActiveDocument.Layers.CurrentUserLayer.PreviousTextBounds);
	}

	/// <summary>
	/// Draws the text.
	/// </summary>
	/// <param name="showCursor">Whether or not to show the mouse cursor in the drawing.</param>
	/// <param name="useTextLayer">Whether or not to use the TextLayer (as opposed to the Userlayer).</param>
	private void RedrawText (bool showCursor, bool useTextLayer)
	{
		RectangleI r =
			CurrentTextLayout
			.GetLayoutBounds ()
			.Inflated (10 + OutlineWidth, 10 + OutlineWidth);

		InflateAndInvalidate (r);
		CurrentTextBounds = r;

		RectangleI cursorBounds = RectangleI.Zero;

		ImageSurface surf;

		if (!useTextLayer) {
			//Draw text on the current UserLayer's surface as finalized text.
			surf = workspace.ActiveDocument.Layers.CurrentUserLayer.Surface;
		} else {
			//Draw text on the current UserLayer's TextLayer's surface as re-editable text.
			surf = workspace.ActiveDocument.Layers.CurrentUserLayer.TextLayer.Layer.Surface;

			ClearTextLayer ();
		}

		using Context g = new (surf);

		g.Antialias = UseAntialiasing ? Antialias.Gray : Antialias.None;

		g.Save ();

		// Show selection if on text layer
		if (useTextLayer) {
			// Selected Text
			Color c = new (
				R: 0.7,
				G: 0.8,
				B: 0.9,
				A: 0.5);

			foreach (RectangleI rect in CurrentTextLayout.GetSelectionRectangles ())
				g.FillRectangle (rect.ToDouble (), c);
		}

		selection?.Clip (g);

		g.SetSourceColor (CurrentTextEngine.PrimaryColor);

		//Fill in background
		if (BackgroundFill) {
			using Context g2 = new (surf);
			selection?.Clip (g2);
			g2.FillRectangle (CurrentTextLayout.GetLayoutBounds ().ToDouble (), CurrentTextEngine.SecondaryColor);
		}

		//The text body is the outline path (already in canvas coordinates);
		//fill and stroke render it exactly the way upstream's PangoCairo
		//ShowLayout/LayoutPath pair did.
		Path textPath = CurrentTextLayout.GetOutline ();

		// Draws the text stroke
		if (StrokeText) {
			g.SetSourceColor (FillText ? CurrentTextEngine.SecondaryColor : CurrentTextEngine.PrimaryColor);
			g.LineWidth = OutlineWidth;
			g.LineJoin = (LineJoin) join_btn.SelectedItem.GetTagOrDefault (LineJoin.Miter);

			g.AppendPath (textPath);
			g.Stroke ();

			if (FillText)
				g.SetSourceColor (CurrentTextEngine.PrimaryColor);
		}

		// Draws the text fill
		if (FillText) {
			g.AppendPath (textPath);
			g.Fill ();

			//Underline rules (G2 - drawn here, the layout engine has no
			//text-decoration concept).
			foreach (RectangleD underline in CurrentTextLayout.GetUnderlineRectangles ())
				g.FillRectangle (underline, CurrentTextEngine.PrimaryColor);
		}

		if (showCursor) {

			RectangleI loc = CurrentTextLayout.GetCursorLocation ();
			Color color = CurrentTextEngine.PrimaryColor;

			g.DrawLine (
				new PointD (loc.X, loc.Y),
				new PointD (loc.X, loc.Y + loc.Height),
				color, 1);

			cursorBounds = loc;
			cursorBounds = cursorBounds.Inflated (2, 10);
		}

		g.Restore ();

		if (useTextLayer && (is_editing || ctrl_key) && !CurrentTextEngine.IsEmpty ()) {

			//Draw the text edit rectangle.

			g.Save ();

			g.Translate (.5, .5);

			g.AppendPath (g.CreateRectanglePath (CurrentTextBounds.ToDouble ()));

			g.LineWidth = 1;

			g.SetSourceColor (new Color (1, 1, 1));
			g.StrokePreserve ();

			g.SetDash ([2, 4], 0);
			g.SetSourceColor (new Color (1, .1, .2));

			g.Stroke ();

			g.Restore ();
		}

		InflateAndInvalidate (workspace.ActiveDocument.Layers.CurrentUserLayer.PreviousTextBounds);
		workspace.Invalidate (old_cursor_bounds);
		InflateAndInvalidate (r);
		workspace.Invalidate (cursorBounds);

		old_cursor_bounds = cursorBounds;
	}

	/// <summary>
	/// Finalize re-editable text (if applicable).
	/// </summary>
	public void FinalizeText ()
	{
		//If this is true, don't finalize any text - this is used to prevent the code from looping recursively.
		if (ignore_clone_finalizations)
			return;

		//Only bother finalizing text if editing.
		if (CurrentTextEngine.State == TextMode.Unchanged)
			return;

		//Start ignoring any Surface.Clone calls from this point on (so that it doesn't start to loop).
		ignore_clone_finalizations = true;
		Document doc = workspace.ActiveDocument;

		//Create a backup of everything before redrawing the text and etc.
		ImageSurface oldTextSurface = doc.Layers.CurrentUserLayer.TextLayer.Layer.Surface.Clone ();
		ImageSurface oldUserSurface = doc.Layers.CurrentUserLayer.Surface.Clone ();
		TextEngine oldTextEngine = CurrentTextEngine.Clone ();

		//Draw the text onto the UserLayer (without the cursor) rather than the TextLayer.
		RedrawText (false, false);

		//Clear the TextLayer.
		doc.Layers.CurrentUserLayer.TextLayer.Layer.Clear ();

		//Clear the text and its boundaries.
		CurrentTextEngine.Clear ();
		CurrentTextBounds = RectangleI.Zero;

		//Create a new TextHistoryItem so that the finalization of the text can be undone. Construct
		//it on the spot so that it is more memory efficient if the changes are small.
		TextHistoryItem hist = new (
			workspace,
			Icon,
			FinalizeName,
			oldTextSurface,
			oldUserSurface,
			oldTextEngine,
			doc.Layers.CurrentUserLayer);

		//Add the new TextHistoryItem.
		doc.History.PushNewItem (hist);

		//Stop ignoring any Surface.Clone calls from this point on.
		ignore_clone_finalizations = false;

		//Now that the text has been finalized, change its state.
		CurrentTextEngine.State = TextMode.Unchanged;

		selection = null;
	}

	private void InflateAndInvalidate (in RectangleI passedRectangle)
	{
		//Create a new instance to preserve the passed Rectangle.
		RectangleI r = new (
			passedRectangle.Location,
			passedRectangle.Size);

		r = r.Inflated (2, 2);

		workspace.Invalidate (r);
	}

	#endregion

	#region Undo/Redo

	protected override bool OnHandleUndo (Document document)
	{
		if (!is_editing)
			return false;

		// commit a history item to let the undo action undo text history item
		StopEditing (false);

		return false;
	}

	protected override bool OnHandleRedo (Document document)
	{
		//Rather than redoing something, if the text has been edited then simply commit and do not redo.
		if (!is_editing || CurrentTextEngine.State != TextMode.Uncommitted)
			return false;

		//Commit a new TextHistoryItem.
		StopEditing (false);

		return true;
	}

	#endregion

	#region Copy/Paste

	protected override async Task<bool> OnHandlePaste (Document document, IClipboardService cb)
	{
		if (!is_editing)
			return false;

		if (!await CurrentTextEngine.PerformPaste (cb))
			return false;

		RedrawText (true, true);
		return true;
	}

	protected override bool OnHandleCopy (Document document, IClipboardService cb)
	{
		if (!is_editing)
			return false;

		CurrentTextEngine.PerformCopy (cb);
		return true;
	}

	protected override bool OnHandleCut (Document document, IClipboardService cb)
	{
		if (!is_editing)
			return false;

		CurrentTextEngine.PerformCut (cb);
		RedrawText (true, true);
		return true;
	}

	#endregion
}
