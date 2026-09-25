using System;
using System.IO;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Reqnroll;
using FlexAlignContent = CodeBrix.Platform.UI.FlexPanel.FlexAlignContent;
using FlexAlignItems = CodeBrix.Platform.UI.FlexPanel.FlexAlignItems;
using FlexAlignSelf = CodeBrix.Platform.UI.FlexPanel.FlexAlignSelf;
using FlexBasis = CodeBrix.Platform.UI.FlexPanel.FlexBasis;
using FlexDirection = CodeBrix.Platform.UI.FlexPanel.FlexDirection;
using FlexJustify = CodeBrix.Platform.UI.FlexPanel.FlexJustify;
using FlexPanelElement = CodeBrix.Platform.UI.FlexPanel.FlexPanel;
using FlexWrap = CodeBrix.Platform.UI.FlexPanel.FlexWrap;

namespace CodeBrix.Platform.UI.AddIn.FlexPanel.UIReqs.Steps;

/// <summary>
/// The FlexPanel group's vocabulary: the panel itself, the properties that decide how its line
/// is laid out, and the five values a child carries about its own place in that line.
/// <para>
/// Everything else a FlexPanel scenario says - showing the panel, putting a child in it, where
/// something sits, how big it is, what colour it is - is the core harness's own vocabulary,
/// reached through this project's reqnroll.json binding assemblies. Nothing from the core
/// project is duplicated here.
/// </para>
/// </summary>
[Binding]
public sealed class FlexPanelSteps
{
	/// <summary>The name a feature file builds the panel with.</summary>
	public const string FlexPanelKind = "FlexPanel";

	/// <summary>
	/// The prerequisite name both feature files declare with a <c>@needs-flexpanel</c> tag: the
	/// add-in assembly itself. Every machine that can build this project has it, so nothing is
	/// ever skipped for it here - but a run whose add-in could not be loaded reports "skipped:
	/// the FlexPanel add-in is not usable" instead of failing fifteen times over an element kind
	/// the factory does not know.
	/// </summary>
	public const string FlexPanelPrerequisite = "flexpanel";

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	/// <summary>
	/// Teaches the element factory this add-in's nouns, before the first scenario.
	/// <para>
	/// An add-in registers from its OWN hook rather than through a partial method of the core
	/// factory: a partial method admits exactly one implementation, and every add-in adds to the
	/// same two tables. The panel's own properties go in as TYPED setters, so that "Padding"
	/// means this panel's Padding on a FlexPanel and goes on meaning a Border's or a Control's
	/// Padding everywhere else - a Panel has none in the framework, so the general setter of the
	/// core harness cannot apply one.
	/// </para>
	/// </summary>
	[BeforeTestRun(Order = 10)]
	public static void Register_the_FlexPanel_vocabulary()
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
				Prerequisite.Missing(FlexPanelPrerequisite,
					$"the FlexPanel add-in could not be used ({failure.GetType().Name}: {failure.Message})");
			}
		}
	}

	private static void RegisterTheNouns()
	{
		ElementFactory.RegisterKind(FlexPanelKind, () => new FlexPanelElement());

		ElementFactory.RegisterProperty<FlexPanelElement>("Direction",
			(panel, value) => panel.Direction = GherkinValue.ToEnum<FlexDirection>(value));
		ElementFactory.RegisterProperty<FlexPanelElement>("JustifyContent",
			(panel, value) => panel.JustifyContent = GherkinValue.ToEnum<FlexJustify>(value));
		ElementFactory.RegisterProperty<FlexPanelElement>("AlignItems",
			(panel, value) => panel.AlignItems = GherkinValue.ToEnum<FlexAlignItems>(value));
		ElementFactory.RegisterProperty<FlexPanelElement>("AlignContent",
			(panel, value) => panel.AlignContent = GherkinValue.ToEnum<FlexAlignContent>(value));
		ElementFactory.RegisterProperty<FlexPanelElement>("Wrap",
			(panel, value) => panel.Wrap = GherkinValue.ToEnum<FlexWrap>(value));
		ElementFactory.RegisterProperty<FlexPanelElement>("Padding",
			(panel, value) => panel.Padding = GherkinValue.ToThickness(value));
	}

	/// <summary>
	/// Sets one of the five values a child of a FlexPanel carries: Order, Grow, Shrink, Basis or
	/// AlignSelf. They are attached properties - they live on the child but mean something to the
	/// panel - so they are not properties of the child the element factory could set.
	/// </summary>
	/// <param name="childName">The Gherkin name of the child.</param>
	/// <param name="panelName">The Gherkin name of the FlexPanel it is in.</param>
	/// <param name="property">Order, Grow, Shrink, Basis or AlignSelf.</param>
	/// <param name="value">The value to set, as the feature file wrote it.</param>
	/// <returns>A task that completes once the UI thread has applied the change and laid out.</returns>
	[Given("the child {string} of {string} has {word} {string}")]
	[When("the child {string} of {string} has {word} {string}")]
	public async Task Given_the_child_of_has(string childName, string panelName, string property, string value)
	{
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			var panel = PanelOf(panelName);
			var child = ElementRegistry.Resolve(childName);
			var text = GherkinValue.Unquote(value);

			switch (property.ToUpperInvariant())
			{
				case "ORDER":
					FlexPanelElement.SetOrder(child, ToInt(text));
					break;
				case "GROW":
					FlexPanelElement.SetGrow(child, ToFloat(text));
					break;
				case "SHRINK":
					FlexPanelElement.SetShrink(child, ToFloat(text));
					break;
				case "BASIS":
					// "Auto", an absolute length or a percentage - the same grammar the XAML
					// attribute takes, read by the add-in's own converter.
					FlexPanelElement.SetBasis(child, FlexBasis.CreateFromString(text));
					break;
				case "ALIGNSELF":
					FlexPanelElement.SetAlignSelf(child, GherkinValue.ToEnum<FlexAlignSelf>(text));
					break;
				default:
					throw new NotSupportedException(
						$"A child of a FlexPanel carries no \"{property}\". Write Order, Grow, Shrink, "
						+ "Basis or AlignSelf.");
			}

			// The attached value changed AFTER the child was added, which is the harder case: the
			// panel has to re-lay out for a value that changed nothing about the child itself.
			panel.UpdateLayout();
		}).ConfigureAwait(false);
	}

	private static FlexPanelElement PanelOf(string name) =>
		ElementRegistry.Resolve(name) as FlexPanelElement
		?? throw new NotSupportedException(
			$"\"{name}\" is not a FlexPanel, so its children carry none of the flex values.");

	private static int ToInt(string value) => (int) Math.Round(GherkinValue.ToDouble(value));

	private static float ToFloat(string value) => (float) GherkinValue.ToDouble(value);
}
