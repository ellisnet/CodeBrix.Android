// Command.cs
//
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
//
// Copyright (c) 2010 Jonathan Pobst
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
using System.Collections.Immutable;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

// Pinta.Brix note: upstream wrapped a toolkit action object; the port keeps
// the same public surface (name, labels, shortcuts, Activated event) as a
// plain class, and the UI layer maps commands onto menu items and keyboard
// accelerators.
public class Command
{
	private bool sensitive = true;

	public string Name { get; }

	public event EventHandler? Activated;
	public event EventHandler? SensitiveChanged;

	public void Activate ()
	{
		Activated?.Invoke (this, EventArgs.Empty);
	}

	public string Label { get; }
	public string? ShortLabel { get; init; }
	public string? Tooltip { get; }
	public string? IconName { get; }
	public string FullName => $"app.{Name}";
	public bool IsImportant { get; } = false;

	public bool Sensitive {
		get => sensitive;
		set {
			if (sensitive == value)
				return;
			sensitive = value;
			SensitiveChanged?.Invoke (this, EventArgs.Empty);
		}
	}

	public ImmutableArray<string> Shortcuts { get; }

	public Command (
		string name,
		string label,
		string? tooltip,
		string? icon_name,
		IReadOnlyList<string>? shortcuts = null)
	{
		Name = name;
		Label = label;
		Tooltip = tooltip;
		IconName = icon_name;

		Shortcuts =
			shortcuts is null
			? []
			: [.. shortcuts];
	}
}

public sealed class ToggleCommand : Command
{
	private bool value;

	public delegate void ToggledHandler (bool value, bool interactive);
	public event ToggledHandler? Toggled;

	public ToggleCommand (
		string name,
		string label,
		string? tooltip,
		string? stock_id,
		IReadOnlyList<string>? shortcuts = null
	)
		: base (name, label, tooltip, stock_id, shortcuts)
	{
		Activated += (_, _) => {
			value = !value;
			Toggled?.Invoke (value, interactive: true);
		};
	}

	public bool Value {
		get => value;
		set {
			if (value == this.value)
				return;
			this.value = value;
			Toggled?.Invoke (value, interactive: false);
		}
	}
}
