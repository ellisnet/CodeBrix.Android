using System;
using System.Collections.Generic;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// A control whose visual states are built in C#, so that a requirement about the visual state
/// manager can be stated without any XAML anywhere. It is one coloured panel in a template of
/// its own, plus one visual state group whose states each repaint that panel; going to a state
/// is then the only thing that changes what the panel shows.
/// </summary>
public sealed partial class StateBox : Control
{
	/// <summary>The name the coloured panel inside the template is registered under.</summary>
	public const string SwatchSuffix = "Swatch";

	/// <summary>The name of the one visual state group the control carries.</summary>
	public const string StateGroupName = "SwatchStates";

	/// <summary>How long a state's colour change is given to run.</summary>
	public static readonly TimeSpan StateChangeDuration = TimeSpan.FromMilliseconds(120);

	private readonly List<KeyValuePair<string, Color>> _states = new();
	private readonly SolidColorBrush _swatchBrush;

	/// <summary>Builds the control with the colour it shows before it is put into any state.</summary>
	/// <param name="restingColor">The colour the panel is painted to begin with.</param>
	public StateBox(Color restingColor)
	{
		RestingColor = restingColor;
		_swatchBrush = new SolidColorBrush(restingColor);
		Template = new ControlTemplate(BuildTemplateRoot);
	}

	/// <summary>The colour the panel is painted before any state is entered.</summary>
	public Color RestingColor { get; }

	/// <summary>The name the coloured panel is registered under, once the control has a name.</summary>
	public string SwatchName => Name + SwatchSuffix;

	/// <summary>Adds a state the control can be put into.</summary>
	/// <param name="stateName">The name a feature file uses for the state.</param>
	/// <param name="color">The colour the panel is painted while the control is in it.</param>
	public void AddState(string stateName, Color color)
	{
		ArgumentException.ThrowIfNullOrEmpty(stateName);
		_states.Add(new KeyValuePair<string, Color>(stateName, color));
	}

	/// <summary>The names of the states the control was given.</summary>
	/// <returns>The state names.</returns>
	public IReadOnlyCollection<string> StateNames
	{
		get
		{
			var names = new List<string>(_states.Count);
			foreach (var state in _states)
			{
				names.Add(state.Key);
			}

			return names;
		}
	}

	private UIElement BuildTemplateRoot()
	{
		var swatch = new Border
		{
			Name = SwatchName,
			Background = _swatchBrush,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};

		ElementRegistry.Register(SwatchName, swatch);

		var root = new Grid();
		root.Children.Add(swatch);

		var group = new VisualStateGroup { Name = StateGroupName };
		foreach (var state in _states)
		{
			group.States.Add(BuildState(state.Key, state.Value));
		}

		// The attached property's default is an EMPTY ARRAY, which is fixed size, so the groups
		// have to be SET as a list rather than added to what is already there.
		VisualStateManager.SetVisualStateGroups(root, new List<VisualStateGroup> { group });
		return root;
	}

	private VisualState BuildState(string stateName, Color color)
	{
		// The animation targets the BRUSH rather than the Border, so the state does not have to
		// name a property path through the element tree: the brush is the thing whose colour a
		// state changes, and it is the same brush for the life of the control.
		var animation = new ColorAnimation
		{
			To = color,
			Duration = new Duration(StateChangeDuration),
			EnableDependentAnimation = true,
		};

		Storyboard.SetTarget(animation, _swatchBrush);
		Storyboard.SetTargetProperty(animation, "Color");

		var storyboard = new Storyboard();
		storyboard.Children.Add(animation);

		return new VisualState
		{
			Name = stateName,
			Storyboard = storyboard,
		};
	}
}
