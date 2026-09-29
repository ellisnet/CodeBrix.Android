using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.AddIn.AdvancedTextEdit.UIReqs.Support;
using CodeBrix.Platform.UI.AdvancedTextEdit.Editing;
using CodeBrix.Platform.UI.AdvancedTextEdit.Highlighting;
using CodeBrix.Platform.UI.AdvancedTextEdit.Rendering;
using CodeBrix.Platform.UI.AdvancedTextEdit.Search;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Reqnroll;
using SilverAssertions;
using Windows.Foundation;
using Windows.UI;
using Editor = CodeBrix.Platform.UI.AdvancedTextEdit.AdvancedTextEdit;
using FontFamily = Microsoft.UI.Xaml.Media.FontFamily;
using ScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility;
using TextPosition = CodeBrix.Platform.UI.AdvancedTextEdit.TextViewPosition;

namespace CodeBrix.Platform.UI.AddIn.AdvancedTextEdit.UIReqs.Steps;

/// <summary>
/// The AdvancedTextEdit group's vocabulary: the editor itself, the document it holds, the caret,
/// the selection, the undo stack, the line-number margin, the highlighting colours and the
/// search panel - each said in the same sentence as the pixels it has to agree with.
/// <para>
/// Everything else a scenario says - showing the editor, tapping it, pressing a key, capturing a
/// frame, what a region looks like, where its ink sits - is the core harness's own vocabulary,
/// reached through this project's reqnroll.json binding assemblies. Nothing from the core project
/// is duplicated here.
/// </para>
/// </summary>
[Binding]
public sealed class AdvancedTextEditSteps
{
	/// <summary>The name a feature file builds the editor with.</summary>
	public const string EditorKind = "Editor";

	/// <summary>
	/// The prerequisite name every feature file declares with a <c>@needs-advancedtextedit</c>
	/// tag: the add-in assembly itself. Every machine that can build this project has it, so
	/// nothing is ever skipped for it here - but a run whose add-in could not be loaded reports
	/// "skipped: the AdvancedTextEdit add-in is not usable" instead of failing once per scenario
	/// over an element kind the factory does not know.
	/// </summary>
	public const string EditorPrerequisite = "advancedtextedit";

	/// <summary>The em size every scenario's editor draws at, big enough to read on the frame.</summary>
	public const double EditorFontSize = 20;

	/// <summary>The width an editor gets when a scenario does not ask for one.</summary>
	public const double DefaultEditorWidth = 800;

	/// <summary>The height an editor gets when a scenario does not ask for one.</summary>
	public const double DefaultEditorHeight = 400;

	/// <summary>The colour name a feature file writes for the text area's selection brush.</summary>
	public const string SelectionColorName = "EditorSelection";

	/// <summary>The colour name a feature file writes for the search panel's match marker.</summary>
	public const string SearchMarkerColorName = "SearchMarker";

	private const string TappedPositionKey = "uireqs.advancedtextedit.tappedPosition";

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public AdvancedTextEditSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory this add-in's noun and the colours its scenarios name, before
	/// the first scenario.
	/// <para>
	/// The editor is registered with a real monospaced face and a readable em size, because its
	/// own defaults are the generic family name "monospace" - which resolves to nothing here -
	/// and 13 points. Its own properties go in as TYPED setters, so that "Text" means this
	/// editor's whole document on an editor and goes on meaning a TextBlock's or a TextBox's
	/// text everywhere else.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_AdvancedTextEdit_vocabulary()
	{
		lock (RegistrationLock)
		{
			if (_registered)
			{
				return;
			}

			_registered = true;

			try
			{
				RegisterTheNouns();
			}
			catch (Exception failure) when (failure is TypeLoadException or FileNotFoundException
				or FileLoadException or MissingMemberException)
			{
				// The add-in itself is what these scenarios are about, so a machine that cannot
				// load it has no requirement to state - it has a report to make.
				Prerequisite.Missing(EditorPrerequisite,
					$"the AdvancedTextEdit add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	// ------------------------------------------------------------- the font

	/// <summary>
	/// Lays a throwaway string out in the monospaced face every scenario draws with, before any
	/// scenario measures with it. The text engine answers the first measurement through a font
	/// it has not loaded yet with an interim face and finishes loading the real one on a
	/// continuation the frame handshake cannot see, so an editor measured in that moment is laid
	/// out to advance widths that were never true. This is not a sleep: it is one real layout
	/// pass on the UI thread, awaited.
	/// </summary>
	/// <returns>A task that completes once the font has been laid out with.</returns>
	[Given("the editor font is warm")]
	public async Task Given_the_editor_font_is_warm() =>
		await FontWarmup.WarmAsync(EditorSamples.MonospaceFont).ConfigureAwait(false);

	/// <summary>
	/// Asserts that the monospaced face the editor draws with is on disk beside the scenarios,
	/// with the manifest that maps a weight to a file. This is the one fact no pixel can state
	/// on its own: the editor's default family name resolves to nothing on this machine, the
	/// family forbids a system-font fallback, and a missing face draws no text at all.
	/// </summary>
	[Then("the monospaced font shipped beside the scenarios")]
	public void Then_the_monospaced_font_shipped_beside_the_scenarios()
	{
		// ANDROID PORT (h): an APK has no folder beside its assemblies; ms-appx:/// resolves to the app's ASSETS (AP1.12,
		// IApplicationPackageFilesPlatform over the AssetManager), so the face and its manifest are looked for there, at
		// the same relative path.
		var folder = EditorSamples.MonospaceFontPackage + "/" + EditorSamples.FontsFolderName;
		static bool Exists(string folder, string file)
		{
			try
			{
				using var stream = global::Android.App.Application.Context.Assets!.Open(folder + "/" + file);
				return stream != null;
			}
			catch (Java.IO.IOException)
			{
				return false;
			}
		}

		Exists(folder, EditorSamples.MonospaceFontFile).Should().BeTrue(
			"the face \"{0}\" must be in {1}, because that is where \"{2}\" resolves to",
			EditorSamples.MonospaceFontFile, folder, EditorSamples.MonospaceFont);
		Exists(folder, EditorSamples.MonospaceFontManifest).Should().BeTrue(
			"the manifest \"{0}\" must be beside the face in {1}, because it is what resolves a weight "
			+ "to a file - without it a bold keyword is drawn with the plain face",
			EditorSamples.MonospaceFontManifest, folder);
	}

	// --------------------------------------------------------- the document

	/// <summary>Opens one of the sample documents in an editor that is already showing.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="sample">The sample's name, as <see cref="EditorSamples"/> spells it.</param>
	/// <returns>A task that completes once the editor holds the sample and has laid out.</returns>
	[Given("the editor {string} holds the sample {string}")]
	[When("the editor {string} holds the sample {string}")]
	public async Task Given_the_editor_holds_the_sample(string name, string sample)
	{
		var text = EditorSamples.Named(GherkinValue.Unquote(sample));
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var editor = EditorOf(name);
			editor.Text = text;
			editor.UpdateLayout();
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts what document an editor holds. What a control HOLDS is a fact about the tree.
	/// <para>
	/// The core's universal "the Text of ... is ..." can read an editor now (this group registers
	/// a text reader for it), and a single-line claim is better said with that sentence. This one
	/// stays for the thing the universal sentence cannot say: a Gherkin value cannot carry a line
	/// break, so a claim about a MULTI-LINE document is written with <c>\n</c> between its lines
	/// and unescaped here.
	/// </para>
	/// </summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="expected">The text it must hold, with <c>\n</c> for each line break.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the editor {string} holds the text {string}")]
	public async Task Then_the_editor_holds_the_text(string name, string expected)
	{
		var wanted = EditorSamples.Unescape(GherkinValue.Unquote(expected));
		var actual = await ReadAsync(() => EditorOf(name).Text).ConfigureAwait(false);

		EditorSamples.Escape(actual).Should().Be(EditorSamples.Escape(wanted),
			"the document \"{0}\" holds was asserted", name);
	}

	/// <summary>Asserts how many lines an editor's document has.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="lines">The number of lines it must have.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the editor {string} holds {int} lines")]
	public async Task Then_the_editor_holds_lines(string name, int lines)
	{
		var actual = await ReadAsync(() => EditorOf(name).LineCount).ConfigureAwait(false);
		actual.Should().Be(lines, "the number of lines \"{0}\" holds was asserted", name);
	}

	/// <summary>Asserts which highlighting definition an editor is colouring its text with.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="definition">The definition's name, such as "C#".</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the editor {string} is highlighted as {string}")]
	public async Task Then_the_editor_is_highlighted_as(string name, string definition)
	{
		var actual = await ReadAsync(() => EditorOf(name).SyntaxHighlighting?.Name ?? "nothing")
			.ConfigureAwait(false);
		actual.Should().Be(GherkinValue.Unquote(definition),
			"the highlighting definition \"{0}\" colours its text with was asserted", name);
	}

	// ------------------------------------------------------------ the caret

	/// <summary>
	/// Takes the caret out of the picture. The caret blinks on a half-second timer that raises
	/// no event when it ticks, so two frames taken across a tick differ by the caret alone -
	/// every scenario that compares frames says this first, and it stays hidden afterwards
	/// because only taking the focus again shows it.
	/// </summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes once the caret is hidden and the panel has settled.</returns>
	[Given("the caret of {string} is hidden")]
	[When("the caret of {string} is hidden")]
	public async Task Given_the_caret_of_is_hidden(string name)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() => EditorOf(name).TextArea.Caret.Hide())
			.ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Puts the caret back on the panel, for a scenario whose frame is meant to show a person
	/// where the caret went. Nothing is claimed about the caret's pixels afterwards: it blinks.
	/// </summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes once the caret is showing and the panel has settled.</returns>
	[Given("the caret of {string} is shown")]
	[When("the caret of {string} is shown")]
	public async Task Given_the_caret_of_is_shown(string name)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() => EditorOf(name).TextArea.Caret.Show())
			.ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Puts the caret at one offset in the document, as a tap or an arrow key would.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="offset">The offset, counted in characters from the start of the document.</param>
	/// <returns>A task that completes once the caret has moved and the panel has settled.</returns>
	[Given("the caret of {string} is put at offset {int}")]
	[When("the caret of {string} is put at offset {int}")]
	public async Task When_the_caret_of_is_put_at_offset(string name, int offset)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() => EditorOf(name).CaretOffset = offset)
			.ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Asserts where the caret is, counted in characters from the start of the document.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="offset">The offset it must be at.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the caret of {string} is at offset {int}")]
	public async Task Then_the_caret_of_is_at_offset(string name, int offset)
	{
		var actual = await ReadAsync(() => EditorOf(name).CaretOffset).ConfigureAwait(false);
		actual.Should().Be(offset, "the caret offset of \"{0}\" was asserted", name);
	}

	/// <summary>Asserts where the caret is, as the line and column a person would count.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="line">The line it must be on, counted from 1.</param>
	/// <param name="column">The column it must be at, counted from 1.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the caret of {string} is at line {int}, column {int}")]
	public async Task Then_the_caret_of_is_at_line_column(string name, int line, int column)
	{
		var (actualLine, actualColumn) = await ReadAsync(() =>
		{
			var caret = EditorOf(name).TextArea.Caret;
			return (caret.Line, caret.Column);
		}).ConfigureAwait(false);

		actualLine.Should().Be(line, "the caret line of \"{0}\" was asserted", name);
		actualColumn.Should().Be(column, "the caret column of \"{0}\" was asserted", name);
	}

	// ------------------------------------------------------------ the input

	/// <summary>
	/// Types on the panel's keyboard, into the editor. The editor reads a key press's Unicode
	/// codepoint as typed text, which is exactly what the panel's keyboard carries; an editor
	/// nobody has touched yet is tapped first, because a person types into the editor they are
	/// looking at.
	/// </summary>
	/// <param name="text">The text to type.</param>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes once the keys have been delivered and the UI thread is idle.</returns>
	[When("the text {string} is typed into {string}")]
	public async Task When_the_text_is_typed_into(string text, string name)
	{
		var focused = await ReadAsync(() => EditorOf(name).TextArea.IsKeyboardFocused).ConfigureAwait(false);
		if (!focused)
		{
			var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false);
			var (x, y) = bounds.Center;
			TestTargetFixture.Session.Tap(x, y);
			await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
		}

		TestTargetFixture.Session.TypeText(GherkinValue.Unquote(text));
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts that the editable part of an editor is the one the keyboard is talking to. The
	/// editor is not itself the focused element when it has been touched: it hands the focus
	/// straight on to its text area, which is what reads the keys - so the core harness's
	/// "&lt;name&gt; has keyboard focus" would say no about an editor a person is typing into.
	/// </summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the text area of {string} has the keyboard focus")]
	public async Task Then_the_text_area_of_has_the_keyboard_focus(string name)
	{
		var focused = await ReadAsync(() => EditorOf(name).TextArea.IsKeyboardFocused).ConfigureAwait(false);
		focused.Should().BeTrue("the text area of \"{0}\" must have the keyboard focus", name);
	}

	/// <summary>
	/// Taps one point inside an editor, and remembers where the editor itself says that point
	/// is in the document, so the next step can ask whether the caret went there.
	/// </summary>
	/// <param name="x">The x coordinate, in the editor's own pixels.</param>
	/// <param name="y">The y coordinate, in the editor's own pixels.</param>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes once the tap has been delivered and the UI thread is idle.</returns>
	[When("the point {int}, {int} inside {string} is tapped")]
	public async Task When_the_point_inside_is_tapped(int x, int y, string name)
	{
		var expected = await ReadAsync(() => EditorOf(name).GetPositionFromPoint(new Point(x, y)))
			.ConfigureAwait(false);

		if (expected is not { } position)
		{
			// A point past the last line is nowhere in the document, so there would be nothing
			// for the caret to agree with: say so here rather than two steps later.
			var where = string.Create(CultureInfo.InvariantCulture, $"({x}, {y})");
			throw new InvalidOperationException(
				$"The editor \"{name}\" says there is nothing at {where}, so a tap there puts the "
				+ "caret nowhere in particular. Tap a point that is on a line of the document.");
		}

		_scenarioContext[TappedPositionKey] = position;

		var bounds = await DeviceRect.OfAsync(ElementRegistry.Resolve(name), inset: 0).ConfigureAwait(false);
		TestTargetFixture.Session.Tap(bounds.X + x, bounds.Y + y);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts that the caret went where the editor said the tapped point was. The requirement
	/// is that the two agree, so the expected line and column come from the editor's own
	/// point-to-position answer rather than from numbers written in the feature file.
	/// </summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the caret of {string} is where the editor said that point was")]
	public async Task Then_the_caret_of_is_where_the_editor_said(string name)
	{
		if (!_scenarioContext.TryGetValue(TappedPositionKey, out var stored) || stored is not TextPosition expected)
		{
			throw new InvalidOperationException(
				"No point inside the editor has been tapped, so there is nothing for the caret to agree "
				+ "with. A scenario taps one with \"When the point x, y inside \"...\" is tapped\".");
		}

		var (line, column) = await ReadAsync(() =>
		{
			var caret = EditorOf(name).TextArea.Caret;
			return (caret.Line, caret.Column);
		}).ConfigureAwait(false);

		line.Should().Be(expected.Line,
			"the caret of \"{0}\" must be on the line the editor said the tapped point was on", name);
		column.Should().Be(expected.Column,
			"the caret of \"{0}\" must be at the column the editor said the tapped point was at", name);
	}

	// -------------------------------------------------- editing and history

	/// <summary>Undoes the most recent change, as a person pressing the undo gesture would.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes once the change has been undone and the panel has settled.</returns>
	[When("Undo is performed on {string}")]
	public async Task When_Undo_is_performed_on(string name)
	{
		var undone = await ReadAsync(() => EditorOf(name).Undo()).ConfigureAwait(false);
		undone.Should().BeTrue("\"{0}\" must have something to undo", name);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Redoes the most recently undone change.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes once the change has been redone and the panel has settled.</returns>
	[When("Redo is performed on {string}")]
	public async Task When_Redo_is_performed_on(string name)
	{
		var redone = await ReadAsync(() => EditorOf(name).Redo()).ConfigureAwait(false);
		redone.Should().BeTrue("\"{0}\" must have something to redo", name);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Selects the whole document.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes once the selection is made and the panel has settled.</returns>
	[When("SelectAll is performed on {string}")]
	public async Task When_SelectAll_is_performed_on(string name)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() => EditorOf(name).SelectAll()).ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Selects a stretch of the document, as a drag across it would.</summary>
	/// <param name="length">How many characters to select.</param>
	/// <param name="offset">Where the selection starts, counted from the start of the document.</param>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes once the selection is made and the panel has settled.</returns>
	[When("{int} characters from offset {int} are selected in {string}")]
	public async Task When_characters_from_offset_are_selected_in(int length, int offset, string name)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() => EditorOf(name).Select(offset, length))
			.ConfigureAwait(false);
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Asserts how much of the document is selected.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="length">How many characters must be selected.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the selection of {string} is {int} characters long")]
	public async Task Then_the_selection_of_is_characters_long(string name, int length)
	{
		var actual = await ReadAsync(() => EditorOf(name).SelectionLength).ConfigureAwait(false);
		actual.Should().Be(length, "the length of the selection in \"{0}\" was asserted", name);
	}

	/// <summary>Asserts what is selected.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="expected">The selected text, with <c>\n</c> for each line break.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the selected text of {string} is {string}")]
	public async Task Then_the_selected_text_of_is(string name, string expected)
	{
		var wanted = EditorSamples.Unescape(GherkinValue.Unquote(expected));
		var actual = await ReadAsync(() => EditorOf(name).SelectedText).ConfigureAwait(false);

		EditorSamples.Escape(actual).Should().Be(EditorSamples.Escape(wanted),
			"the selected text of \"{0}\" was asserted", name);
	}

	/// <summary>Asserts that an editor has a change to undo.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the editor {string} can undo")]
	public async Task Then_the_editor_can_undo(string name)
	{
		var canUndo = await ReadAsync(() => EditorOf(name).CanUndo).ConfigureAwait(false);
		canUndo.Should().BeTrue("\"{0}\" must have a change on its undo stack", name);
	}

	/// <summary>Asserts that an editor's undo stack is empty.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the editor {string} cannot undo")]
	public async Task Then_the_editor_cannot_undo(string name)
	{
		var canUndo = await ReadAsync(() => EditorOf(name).CanUndo).ConfigureAwait(false);
		canUndo.Should().BeFalse("\"{0}\" must have nothing on its undo stack", name);
	}

	// --------------------------------------------------- scrolling a long document

	/// <summary>Scrolls the editor so that a line far down the document is in view.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="line">The line to scroll to, counted from 1.</param>
	/// <returns>A task that completes once the editor has scrolled and the panel has settled.</returns>
	[When("the editor {string} is scrolled to line {int}")]
	public async Task When_the_editor_is_scrolled_to_line(string name, int line)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var editor = EditorOf(name);
			editor.ScrollToLine(line);
			editor.UpdateLayout();
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Asserts that an editor's document is taller than the part of it the editor can show, which
	/// is what makes the drawing of only the visible lines a requirement at all.
	/// </summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the editor {string} holds more than it can show")]
	public async Task Then_the_editor_holds_more_than_it_can_show(string name)
	{
		var (extent, viewport) = await ReadAsync(() =>
		{
			var editor = EditorOf(name);
			return (editor.ExtentHeight, editor.ViewportHeight);
		}).ConfigureAwait(false);

		extent.Should().BeGreaterThan(viewport,
			"the document in \"{0}\" must be taller than the editor's viewport", name);
	}

	/// <summary>Asserts that an editor is no longer showing the top of its document.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the editor {string} is scrolled away from the top")]
	public async Task Then_the_editor_is_scrolled_away_from_the_top(string name)
	{
		var offset = await ReadAsync(() => EditorOf(name).VerticalOffset).ConfigureAwait(false);
		offset.Should().BeGreaterThan(0, "\"{0}\" must have scrolled down", name);
	}

	// ------------------------------------------------------- what is drawn

	/// <summary>
	/// Asserts what colour a stretch of one line is drawn in. The rectangle comes from the
	/// editor's own layout - it is asked where the columns the scenario named are - so a
	/// highlighting requirement can name a keyword rather than a pixel.
	/// </summary>
	/// <param name="line">The line, counted from 1.</param>
	/// <param name="fromColumn">The first column of the stretch, counted from 1.</param>
	/// <param name="toColumn">The column just past the stretch.</param>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="color">The colour the stretch must be drawn in.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the text at line {int}, columns {int} to {int} of {string} is drawn in {string}")]
	public async Task Then_the_text_at_line_columns_of_is_drawn_in(int line, int fromColumn, int toColumn,
		string name, Color color)
	{
		var description = string.Create(CultureInfo.InvariantCulture,
			$"the text at line {line}, columns {fromColumn} to {toColumn} of \"{name}\"");
		var block = await ReadAsync(() => ColumnsOf(name, line, fromColumn, toColumn)).ConfigureAwait(false);
		var region = await ScenarioFrames
			.SubRegionAsync(_scenarioContext, name, block, description)
			.ConfigureAwait(false);

		region.InkColorIs(color);
	}

	/// <summary>Asserts what colour an editor's line numbers are drawn in.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="color">The colour they must be drawn in.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the line numbers of {string} are drawn in {string}")]
	public async Task Then_the_line_numbers_of_are_drawn_in(string name, Color color)
	{
		var block = await ReadAsync(() => LineNumberMarginOf(name)).ConfigureAwait(false);
		var region = await ScenarioFrames
			.SubRegionAsync(_scenarioContext, name, block, $"the line number margin of \"{name}\"")
			.ConfigureAwait(false);

		region.HasInk();
		region.InkColorIs(color);
	}

	/// <summary>Asserts that an editor has a line-number margin down its left edge.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the line number margin of {string} is showing")]
	public async Task Then_the_line_number_margin_of_is_showing(string name)
	{
		var margins = await ReadAsync(() => LeftMarginKinds(name)).ConfigureAwait(false);
		margins.Contains(nameof(LineNumberMargin), StringComparer.Ordinal).Should().BeTrue(
			"\"{0}\" must have a line-number margin; its left margins are [{1}]",
			name, string.Join(", ", margins));
	}

	/// <summary>Asserts that an editor has no line-number margin.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the line number margin of {string} is not showing")]
	public async Task Then_the_line_number_margin_of_is_not_showing(string name)
	{
		var margins = await ReadAsync(() => LeftMarginKinds(name)).ConfigureAwait(false);
		margins.Contains(nameof(LineNumberMargin), StringComparer.Ordinal).Should().BeFalse(
			"\"{0}\" must have no line-number margin; its left margins are [{1}]",
			name, string.Join(", ", margins));
	}

	// ------------------------------------------------------ the search panel

	/// <summary>
	/// Installs the search panel on an editor and gives it a name of its own, so that what it
	/// covers can be talked about like any other element. The panel is not installed by default:
	/// an application asks for it, exactly as this does.
	/// </summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="panelName">The name the scenario refers to the panel by.</param>
	/// <returns>A task that completes once the panel is installed.</returns>
	[Given("the search panel of {string} is installed as {string}")]
	[When("the search panel of {string} is installed as {string}")]
	public async Task Given_the_search_panel_of_is_installed_as(string name, string panelName)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var panel = SearchPanel.Install(EditorOf(name).TextArea);
			panel.Name = panelName;
			ElementRegistry.Register(panelName, panel);
			_scenarioContext[PanelKey(name)] = panel;
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Asserts that an editor's search panel is showing.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the search panel of {string} is open")]
	public async Task Then_the_search_panel_of_is_open(string name)
	{
		var closed = await ReadAsync(() => PanelOf(name).IsClosed).ConfigureAwait(false);
		closed.Should().BeFalse("the search panel of \"{0}\" must be open", name);
	}

	/// <summary>Asserts that an editor's search panel is not showing.</summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the search panel of {string} is closed")]
	public async Task Then_the_search_panel_of_is_closed(string name)
	{
		var closed = await ReadAsync(() => PanelOf(name).IsClosed).ConfigureAwait(false);
		closed.Should().BeTrue("the search panel of \"{0}\" must be closed", name);
	}

	/// <summary>
	/// Searches an open search panel for a pattern, as typing it into the panel's box would.
	/// </summary>
	/// <param name="name">The Gherkin name of the editor.</param>
	/// <param name="pattern">What to search for.</param>
	/// <returns>A task that completes once the search has run and the panel has settled.</returns>
	[When("the search panel of {string} searches for {string}")]
	public async Task When_the_search_panel_of_searches_for(string name, string pattern)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var panel = PanelOf(name);
			if (panel.IsClosed)
			{
				panel.Open();
			}

			panel.SearchPattern = GherkinValue.Unquote(pattern);
		}).ConfigureAwait(false);

		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	// --------------------------------------------------------------- inner

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(EditorKind, () => new Editor
		{
			// The editor's own defaults are the generic family name "monospace", which resolves
			// to nothing on this machine, and 13 points, which is small to read on a frame.
			FontFamily = new FontFamily(EditorSamples.MonospaceFont),
			FontSize = EditorFontSize,

			// An editor is a scrollable surface with no natural size of its own: unbounded, it
			// would fill the panel and no scenario could say where its text sits. A scenario
			// that needs another size says so with Width and Height.
			Width = DefaultEditorWidth,
			Height = DefaultEditorHeight,

			// The control shows BOTH scroll bars at all times by default, and a bar is twelve
			// pixels of grey track and thumb that has nothing to do with the document. A
			// requirement about what the text looks like cannot be stated over that, so the
			// editor these scenarios build shows a bar only when there is something to scroll -
			// which is itself worth seeing, and the long-document scenario is where it is seen.
			// A scenario that wants the bars gone or always there says so with these properties.
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
		});

		ElementFactory.RegisterProperty<Editor>("Text",
			(editor, value) => editor.Text = EditorSamples.Unescape(value));

		// The mirror of that setter: the core's universal "the Text of ... is ..." reads a
		// TextBlock and a TextBox on its own and would throw for an editor, so the editor teaches
		// it where its own document lives. The sentence with the ESCAPES in it - "the editor ...
		// holds the text "one\ntwo"" - stays in this class, because that is the one thing the
		// universal sentence cannot say: a feature file cannot write a line break inside a quoted
		// Gherkin value, and a multi-line document is what an editor holds.
		ElementFactory.RegisterTextReader<Editor>(editor => editor.Text);
		ElementFactory.RegisterProperty<Editor>("IsReadOnly",
			(editor, value) => editor.IsReadOnly = ToBool(value));
		ElementFactory.RegisterProperty<Editor>("ShowLineNumbers",
			(editor, value) => editor.ShowLineNumbers = ToBool(value));
		ElementFactory.RegisterProperty<Editor>("WordWrap",
			(editor, value) => editor.WordWrap = ToBool(value));
		ElementFactory.RegisterProperty<Editor>("ShowEndOfLine",
			(editor, value) => editor.Options.ShowEndOfLine = ToBool(value));
		ElementFactory.RegisterProperty<Editor>("SyntaxHighlighting",
			(editor, value) => editor.SyntaxHighlighting = Definition(value));
		ElementFactory.RegisterProperty<Editor>("HorizontalScrollBarVisibility",
			(editor, value) => editor.HorizontalScrollBarVisibility =
				GherkinValue.ToEnum<ScrollBarVisibility>(value));
		ElementFactory.RegisterProperty<Editor>("VerticalScrollBarVisibility",
			(editor, value) => editor.VerticalScrollBarVisibility =
				GherkinValue.ToEnum<ScrollBarVisibility>(value));

		// The two colours the editor mixes for itself, so that a feature file can name them
		// rather than write the hexadecimal of a brush it did not choose. The selection brush is
		// deliberately not opaque - the text stays readable through it - and the harness judges
		// a colour as it is seen, over the panel background.
		Colors.RegisterName(SelectionColorName, Color.FromArgb(0x66, 0x33, 0x99, 0xFF));
		Colors.RegisterName(SearchMarkerColorName, Color.FromArgb(0xFF, 0x90, 0xEE, 0x90));
	}

	private static IHighlightingDefinition? Definition(string value)
	{
		var name = GherkinValue.Unquote(value);
		if (name.Length == 0 || string.Equals(name, "none", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		// Only the definitions that ship inside the add-in are used: the highlighting manager is
		// process-wide, so a definition registered by one scenario would outlive it.
		return HighlightingManager.Instance.GetDefinition(name)
			?? throw new NotSupportedException(
				$"The add-in ships no highlighting definition called \"{name}\". It ships: "
				+ string.Join(", ", HighlightingManager.Instance.HighlightingDefinitions
					.Select(known => known.Name)) + ".");
	}

	private static bool ToBool(string value) => bool.Parse(GherkinValue.Unquote(value));

	private static string PanelKey(string editorName) =>
		"uireqs.advancedtextedit.searchPanel." + editorName;

	private static Editor EditorOf(string name) =>
		ElementRegistry.Resolve(name) as Editor
		?? throw new NotSupportedException(
			$"\"{name}\" is not an Editor, so it holds no document.");

	private SearchPanel PanelOf(string editorName)
	{
		if (_scenarioContext.TryGetValue(PanelKey(editorName), out var stored) && stored is SearchPanel panel)
		{
			return panel;
		}

		throw new InvalidOperationException(
			$"No search panel has been installed on \"{editorName}\". A scenario installs one with "
			+ "\"Given the search panel of \"...\" is installed as \"...\"\"; the editor does not "
			+ "have one until an application asks for it.");
	}

	private static string[] LeftMarginKinds(string name) =>
		EditorOf(name).TextArea.LeftMargins.Select(margin => margin.GetType().Name).ToArray();

	/// <summary>
	/// The rectangle a stretch of one line covers, in the editor's own coordinates. Call this on
	/// the UI thread: it asks the text view, which owns its document.
	/// </summary>
	private static DeviceRect ColumnsOf(string name, int line, int fromColumn, int toColumn)
	{
		var editor = EditorOf(name);
		var view = editor.TextArea.TextView;
		view.EnsureVisualLines();

		var start = view.GetVisualPosition(new TextPosition(line, fromColumn), VisualYPosition.LineTop);
		var end = view.GetVisualPosition(new TextPosition(line, toColumn), VisualYPosition.LineBottom);
		var origin = view.TransformToVisual(editor).TransformPoint(new Point(0, 0));

		// The positions are in the document's own coordinates; what is on the panel is the
		// document scrolled by the view's offsets and moved by wherever the view sits inside the
		// editor (the left margins push it right).
		var left = start.X - view.HorizontalOffset + origin.X;
		var top = start.Y - view.VerticalOffset + origin.Y;
		var right = end.X - view.HorizontalOffset + origin.X;
		var bottom = end.Y - view.VerticalOffset + origin.Y;

		return new DeviceRect(
			(int) Math.Floor(left),
			(int) Math.Floor(top),
			(int) Math.Ceiling(right - left),
			(int) Math.Ceiling(bottom - top));
	}

	/// <summary>
	/// The rectangle the line-number margin covers, in the editor's own coordinates. Call this
	/// on the UI thread.
	/// </summary>
	private static DeviceRect LineNumberMarginOf(string name)
	{
		var editor = EditorOf(name);
		var margin = editor.TextArea.LeftMargins.OfType<LineNumberMargin>().FirstOrDefault()
			?? throw new NotSupportedException(
				$"\"{name}\" has no line-number margin, so nothing is drawn where one would be. "
				+ "A scenario asks for one by setting ShowLineNumbers.");

		var origin = margin.TransformToVisual(editor).TransformPoint(new Point(0, 0));
		return new DeviceRect(
			(int) Math.Floor(origin.X),
			(int) Math.Floor(origin.Y),
			(int) Math.Floor(margin.ActualWidth),
			(int) Math.Floor(margin.ActualHeight));
	}

	private static async Task<T> ReadAsync<T>(Func<T> read)
	{
		var value = default(T)!;
		await TestTargetFixture.RunOnUIThreadAsync(() => value = read()).ConfigureAwait(false);
		return value;
	}
}
