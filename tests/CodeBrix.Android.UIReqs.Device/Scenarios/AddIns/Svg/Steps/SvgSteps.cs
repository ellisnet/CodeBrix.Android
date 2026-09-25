using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.AddIn.Svg.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Platform.UI.Xaml.Media.Imaging.Svg;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Reqnroll;
using SilverAssertions;
using ElementFactory = CodeBrix.Platform.UI.Core.UIReqs.Support.ElementFactory;
using Size = Windows.Foundation.Size;
using SvgProvider = CodeBrix.Platform.UI.Svg.SvgProvider;

namespace CodeBrix.Platform.UI.AddIn.Svg.UIReqs.Steps;

/// <summary>
/// The Svg group's vocabulary: the two controls that can show a vector document, the settings
/// that belong to the document rather than to the control, and the one thing the core harness
/// cannot say - that a document has finished loading, and with what result.
/// <para>
/// Everything else an SVG scenario says - showing the control, sizing it, its Stretch, where
/// its colours landed, how big the picture inside it is - is the core harness's own vocabulary,
/// reached through this project's reqnroll.json binding assemblies. Nothing from the core
/// project is duplicated here.
/// </para>
/// <para>
/// There is no sleep anywhere in this class. Parsing runs on the thread pool and the events it
/// raises may arrive off the UI thread, so the signal a step waits on is the load status
/// <c>SetSourceAsync</c> returns - which is raised after the picture exists - followed by the
/// harness's own idle-then-frame handshake, which absorbs the dispatcher hop the canvas's
/// invalidation takes.
/// </para>
/// </summary>
[Binding]
public sealed class SvgSteps
{
	/// <summary>The kind a feature file asks for to get an Image drawing an SVG document.</summary>
	public const string SvgImageKind = "SvgImage";

	/// <summary>The kind a feature file asks for to get an ImageIcon drawing an SVG document.</summary>
	public const string SvgImageIconKind = "SvgImageIcon";

	/// <summary>
	/// The prerequisite name both feature files declare with a <c>@needs-svg</c> tag: the add-in
	/// assembly and the provider registration it needs. Every machine that can build this project
	/// has both, so nothing is ever skipped for it here - but a run whose add-in could not be
	/// loaded reports "skipped: the Svg add-in is not usable" instead of failing thirteen times
	/// over a region that is merely blank.
	/// </summary>
	public const string SvgPrerequisite = "svg";

	/// <summary>The value a feature file writes to take a setting away again.</summary>
	private const string NoneValue = "None";

	private const string LoadStatusKey = "uireqs.svg.status.";

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	private readonly ScenarioContext _scenarioContext;

	/// <summary>Reqnroll builds one of these per scenario.</summary>
	/// <param name="scenarioContext">The current scenario.</param>
	public SvgSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

	/// <summary>
	/// Teaches the element factory this add-in's nouns, before the first scenario.
	/// <para>
	/// The two kinds each build their control with an empty <c>SvgImageSource</c> already in
	/// place, because the source is what carries every setting a document is parsed with: a
	/// scenario has to be able to put a stylesheet or a rasterize size on it BEFORE it names a
	/// document, and the source is the only thing those belong to.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_Svg_vocabulary()
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
				// Without the provider every SvgImageSource in the process draws nothing at all
				// and says nothing about it, so a run in that state would report thirteen blank
				// regions rather than the one thing that is actually wrong.
				if (!ApiExtensibility.IsRegistered<ISvgProvider>())
				{
					Prerequisite.Missing(SvgPrerequisite,
						"nothing registered an SVG provider in this process, so no SvgImageSource can "
						+ "parse a document (Registration.cs is the module initializer that does it)");
					return;
				}

				RegisterTheNouns();
			}
			catch (Exception failure) when (failure is TypeLoadException or FileNotFoundException
				or FileLoadException or MissingMemberException)
			{
				// The add-in itself is what these scenarios are about, so a machine that cannot
				// load it has no requirement to state - it has a report to make.
				Prerequisite.Missing(SvgPrerequisite,
					$"the Svg add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(SvgImageKind, () => new Image { Source = new SvgImageSource() });
		ElementFactory.RegisterKind(SvgImageIconKind, () => new ImageIcon { Source = new SvgImageSource() });

		// These three name settings of the image SOURCE, not of the control, and no other control
		// in this assembly spells anything that way - so they go in the general table under their
		// own names rather than taking a universal word away from somebody else.
		ElementFactory.RegisterProperty("SvgCss",
			(element, value) => SvgProvider.SetCss(SourceOf(element), Stylesheet(value)));
		ElementFactory.RegisterProperty("RasterizePixelWidth",
			(element, value) => SourceOf(element).RasterizePixelWidth = Length(value));
		ElementFactory.RegisterProperty("RasterizePixelHeight",
			(element, value) => SourceOf(element).RasterizePixelHeight = Length(value));

		// "Source" IS a universal word - a media element, a web view and an icon all have one -
		// so it goes in as a TYPED setter, which leaves the word free for every other control.
		// The only value it takes is "None": a document arrives through the load step, which is
		// the only route with a completion signal on it.
		ElementFactory.RegisterProperty<Image>("Source", (image, value) => image.Source = ToSource(value));
	}

	/// <summary>
	/// Hands a document to a control's image source and waits for the source to say what it made
	/// of it. The returned status is the completion signal: it is produced after the parse has
	/// run on the thread pool and after the picture (or the failure) exists, so a step that has
	/// awaited it is looking at a source that has finished, not at one that is still working.
	/// </summary>
	/// <param name="documentName">The name of the document in <see cref="TestSvg"/>.</param>
	/// <param name="elementName">The Gherkin name of the control showing it.</param>
	/// <returns>A task that completes once the source has loaded the document.</returns>
	[Given("the SVG {string} of {string} is loaded")]
	[When("the SVG {string} of {string} is loaded")]
	public async Task Given_the_SVG_of_is_loaded(string documentName, string elementName)
	{
		var document = GherkinValue.Unquote(documentName);
		var status = default(SvgImageSourceLoadStatus);

		await TestTargetFixture.RunOnUIThreadAsync(async () =>
		{
			var source = SourceOf(ElementRegistry.Resolve(elementName));

			// The stream is not disposed on purpose: the source keeps a clone of it, and the
			// whole thing is managed memory the collector reclaims once the source lets go.
			status = await source.SetSourceAsync(TestSvg.OpenStream(document));
		}).ConfigureAwait(false);

		_scenarioContext[LoadStatusKey + elementName] = status;

		// The canvas is invalidated from the parse, which may finish off the UI thread: one
		// dispatcher hop later the tree knows the picture's size. Draining the dispatcher here
		// means the next frame the scenario asks for is the one that shows it.
		await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
	}

	/// <summary>Asserts what a control's image source made of the document it was given.</summary>
	/// <param name="elementName">The Gherkin name of the control.</param>
	/// <param name="expected">Success, NetworkError, InvalidFormat or Other.</param>
	[Then("the SVG of {string} loaded with status {string}")]
	public void Then_the_SVG_of_loaded_with_status(string elementName, string expected)
	{
		var wanted = GherkinValue.ToEnum<SvgImageSourceLoadStatus>(expected);
		var actual = LoadStatusOf(elementName);

		actual.Should().Be(wanted,
			"the load status \"{0}\" reported for its document was asserted", elementName);
	}

	/// <summary>
	/// Asserts the size of the bitmap a source rasterized for itself. It is a fact about the
	/// source rather than about the panel, and it is in device pixels: the panel's display scale
	/// is 1.0, so a source asked for 32 logical pixels rasterizes 32 of them.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the control.</param>
	/// <param name="width">The bitmap's width in device pixels.</param>
	/// <param name="height">The bitmap's height in device pixels.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the rasterized size of {string} is {int} by {int} device pixels")]
	public async Task Then_the_rasterized_size_of_is_by_device_pixels(string elementName, int width, int height)
	{
		var size = await RasterizedSizeAsync(elementName).ConfigureAwait(false);

		((int) Math.Round(size.Width)).Should().Be(width,
			"the width of the bitmap \"{0}\" rasterized was asserted", elementName);
		((int) Math.Round(size.Height)).Should().Be(height,
			"the height of the bitmap \"{0}\" rasterized was asserted", elementName);
	}

	/// <summary>
	/// Asserts that a source rasterized no bitmap at all, which is what "drawn as vectors" means
	/// on the source's side of the picture. A source with no bitmap reports a size of nothing by
	/// nothing, which is what "empty" is here.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the control.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the rasterized size of {string} is empty")]
	public async Task Then_the_rasterized_size_of_is_empty(string elementName)
	{
		var size = await RasterizedSizeAsync(elementName).ConfigureAwait(false);
		var rasterized = size.Width > 0 || size.Height > 0;

		rasterized.Should().BeFalse(
			"\"{0}\" must have rasterized nothing, but it reports a bitmap of {1} by {2}",
			elementName, size.Width, size.Height);
	}

	/// <summary>
	/// Asserts that the Image an ImageIcon builds for itself is the one holding the scenario's
	/// image source. The icon is a control with no visible parts of its own: what a person sees
	/// inside it is drawn by that Image, and this is the tree fact behind the pixels.
	/// </summary>
	/// <param name="elementName">The Gherkin name of the ImageIcon.</param>
	/// <returns>A task that completes when the assertion has been made.</returns>
	[Then("the ImageIcon {string} draws its SVG through an Image")]
	public async Task Then_the_ImageIcon_draws_its_SVG_through_an_Image(string elementName)
	{
		var found = false;
		var images = 0;

		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var icon = ElementRegistry.Resolve(elementName);
			var source = SourceOf(icon);
			var descendants = VisualTreeSearch.FindDescendants<Image>(icon);
			images = descendants.Count;
			found = descendants.Any(image => ReferenceEquals(image.Source, source));
		}).ConfigureAwait(false);

		found.Should().BeTrue(
			"the ImageIcon \"{0}\" must show its document through an Image of its own holding that "
			+ "very source; the harness found {1} Image(s) below it",
			elementName, images);
	}

	private static async Task<Size> RasterizedSizeAsync(string elementName)
	{
		var size = default(Size);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
			size = SvgProvider.GetRasterizedPixelSize(SourceOf(ElementRegistry.Resolve(elementName))))
			.ConfigureAwait(false);

		return size;
	}

	private SvgImageSourceLoadStatus LoadStatusOf(string elementName)
	{
		if (_scenarioContext.TryGetValue(LoadStatusKey + elementName, out var stored)
			&& stored is SvgImageSourceLoadStatus status)
		{
			return status;
		}

		throw new InvalidOperationException(
			$"No document has been loaded into \"{elementName}\" in this scenario, so there is no load "
			+ "status to assert. A scenario loads one with \"the SVG \"<name>\" of \"<element>\" is loaded\".");
	}

	private static SvgImageSource SourceOf(FrameworkElement element) => element switch
	{
		Image image => AsSvgSource(image.Source, element),
		ImageIcon icon => AsSvgSource(icon.Source, element),
		_ => throw new NotSupportedException(NotAnImage(element)),
	};

	// The message is built before it is handed over: string.Create's culture overload takes an
	// interpolated-string handler by reference, and a CONCATENATION of interpolated strings
	// cannot be passed that way (CS1620).
	private static string NotAnImage(FrameworkElement element)
	{
		var subject = string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\"");

		return $"{subject} shows no image source, so it can hold no SVG document. Ask for a "
			+ $"\"{SvgImageKind}\" or a \"{SvgImageIconKind}\".";
	}

	private static SvgImageSource AsSvgSource(ImageSource? source, FrameworkElement element) =>
		source as SvgImageSource ?? throw new NotSupportedException(NotAnSvgSource(source, element));

	private static string NotAnSvgSource(ImageSource? source, FrameworkElement element)
	{
		var subject = string.Create(CultureInfo.InvariantCulture,
			$"The Source of \"{element.Name}\" is {Describe(source)}");

		return $"{subject}, not an SvgImageSource. A scenario that cleared it cannot go on to say "
			+ "anything about the document it used to hold.";
	}

	private static string Describe(ImageSource? source) =>
		source is null ? "empty" : string.Create(CultureInfo.InvariantCulture, $"a {source.GetType().Name}");

	private static ImageSource? ToSource(string value) =>
		string.Equals(value, NoneValue, StringComparison.OrdinalIgnoreCase)
			? null
			: throw new NotSupportedException(
				$"\"{value}\" is not something the harness can put in a Source. Write \"{NoneValue}\" to empty "
				+ "it, and load a document with \"the SVG \"<name>\" of \"<element>\" is loaded\".");

	private static string? Stylesheet(string value) =>
		string.Equals(value, NoneValue, StringComparison.OrdinalIgnoreCase) ? null : value;

	private static double Length(string value) =>
		string.Equals(value, NoneValue, StringComparison.OrdinalIgnoreCase)
			? double.NaN
			: GherkinValue.ToDouble(value);
}
