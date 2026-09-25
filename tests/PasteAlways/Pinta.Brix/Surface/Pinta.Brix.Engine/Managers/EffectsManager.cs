// 
// EffectsManager.cs
//  
// Author:
//	Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2011 Jonathan Pobst
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

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

/// <summary>
/// Provides methods for registering and unregistering effects and adjustments.
/// </summary>
public sealed class EffectsManager
{
	private readonly Dictionary<Type, Command> adjustments;

	private readonly Dictionary<Type, Command> effects;
	private readonly Dictionary<Type, string> effects_categories;

	private readonly ChromeManager chrome_manager;
	private readonly LivePreviewManager live_preview_manager;

	// Pinta.Brix note: upstream stored commands in the app-level action
	// manager and appended toolkit menu items; the port owns the registries
	// and raises events the UI layer builds its menus from.
	private readonly List<Command> adjustment_commands = [];
	private readonly Dictionary<string, List<Command>> effect_commands_by_category = [];

	public IReadOnlyList<Command> AdjustmentCommands => adjustment_commands;

	public IReadOnlyCollection<string> EffectCategories => effect_commands_by_category.Keys;

	public IReadOnlyList<Command> GetEffectCommands (string category)
		=> effect_commands_by_category.TryGetValue (category, out var list) ? list : [];

	public event EventHandler? AdjustmentsChanged;
	public event EventHandler? EffectsChanged;

	internal EffectsManager (
		ChromeManager chromeManager,
		LivePreviewManager livePreviewManager)
	{
		adjustments = [];
		effects = [];
		effects_categories = [];

		chrome_manager = chromeManager;
		live_preview_manager = livePreviewManager;
	}

	/// <summary>
	/// Register a new adjustment with Pinta, causing it to be added to the Adjustments menu.
	/// </summary>
	/// <param name="adjustment">The adjustment to register</param>
	/// <returns>The action created for this adjustment</returns>
	public void RegisterAdjustment<T> (T adjustment) where T : BaseEffect
	{
#if false // For testing purposes to detect any missing icons. This implies more disk accesses on startup so we may not want this on by default.
		if (!GtkExtensions.GetDefaultIconTheme ().HasIcon (adjustment.Icon))
			Console.Error.WriteLine ($"Icon {adjustment.Icon} for adjustment {adjustment.Name} not found");
#endif
		Type adjustmentType = typeof (T);

		if (adjustments.ContainsKey (adjustmentType))
			throw new Exception ($"An adjustment of type {adjustmentType} is already registered");

		// Create a gtk action for each adjustment
		Command action = new (
			adjustmentType.Name,
			adjustment.Name + (adjustment.IsConfigurable ? Translations.GetString ("...") : ""),
			string.Empty,
			adjustment.Icon,
			shortcuts:
				adjustment.AdjustmentMenuKey is null
				? [] // If no key is specified, don't use an accelerated menu item
				: [adjustment.AdjustmentMenuKeyModifiers + adjustment.AdjustmentMenuKey]);

		action.Activated += (o, args) => { live_preview_manager.Start (adjustment); };

		int insert_at = adjustment_commands.FindIndex (c => string.Compare (c.Label, action.Label, StringComparison.CurrentCulture) > 0);
		adjustment_commands.Insert (insert_at < 0 ? adjustment_commands.Count : insert_at, action);

		chrome_manager.RegisterCommand (action);

		adjustments.Add (adjustmentType, action);

		AdjustmentsChanged?.Invoke (this, EventArgs.Empty);
	}

	/// <summary>
	/// Register a new effect with Pinta, causing it to be added to the Effects menu.
	/// </summary>
	/// <param name="effect">The effect to register</param>
	/// <returns>The action created for this effect</returns>
	public void RegisterEffect<T> (T effect) where T : BaseEffect
	{
#if false // For testing purposes to detect any missing icons. This implies more disk accesses on startup so we may not want this on by default.
		if (!GtkExtensions.GetDefaultIconTheme ().HasIcon (effect.Icon))
			Console.Error.WriteLine ($"Icon {effect.Icon} for effect {effect.Name} not found");
#endif
		Type effectType = typeof (T);

		if (effects.ContainsKey (effectType))
			throw new Exception ($"An effect of type {effectType} is already registered");

		// Create a gtk action and menu item for each effect
		Command action = new (
			effectType.Name,
			effect.Name + (effect.IsConfigurable ? Translations.GetString ("...") : ""),
			string.Empty,
			effect.Icon);

		chrome_manager.RegisterCommand (action);
		action.Activated += (o, args) => live_preview_manager.Start (effect);

		if (!effect_commands_by_category.TryGetValue (effect.EffectMenuCategory, out var category_list)) {
			category_list = [];
			effect_commands_by_category.Add (effect.EffectMenuCategory, category_list);
		}
		int effect_insert_at = category_list.FindIndex (c => string.Compare (c.Label, action.Label, StringComparison.CurrentCulture) > 0);
		category_list.Insert (effect_insert_at < 0 ? category_list.Count : effect_insert_at, action);

		effects.Add (effectType, action);
		effects_categories.Add (effectType, effect.EffectMenuCategory);

		EffectsChanged?.Invoke (this, EventArgs.Empty);
	}

	/// <summary>
	/// Unregister an effect with Pinta, causing it to be removed from the Effects menu.
	/// </summary>
	/// <param name="effect_type">The type of the effect to unregister</param>
	public void UnregisterInstanceOfEffect<T> () where T : BaseEffect
	{
		Type effectType = typeof (T);

		if (!effects.TryGetValue (effectType, out var action))
			return;

		string category = effects_categories[effectType];

		effects.Remove (effectType);
		if (effect_commands_by_category.TryGetValue (category, out var category_list)) {
			category_list.Remove (action);
			if (category_list.Count == 0)
				effect_commands_by_category.Remove (category);
		}
		effects_categories.Remove (effectType);

		EffectsChanged?.Invoke (this, EventArgs.Empty);
	}

	/// <summary>
	/// Unregister an effect with Pinta, causing it to be removed from the Adjustments menu.
	/// </summary>
	/// <param name="adjustment_type">The type of the adjustment to unregister</param>
	public void UnregisterInstanceOfAdjustment<T> () where T : BaseEffect
	{
		Type adjustmentType = typeof (T);

		if (!adjustments.TryGetValue (adjustmentType, out var action))
			return;

		adjustments.Remove (adjustmentType);
		adjustment_commands.Remove (action);

		AdjustmentsChanged?.Invoke (this, EventArgs.Empty);
	}
}
