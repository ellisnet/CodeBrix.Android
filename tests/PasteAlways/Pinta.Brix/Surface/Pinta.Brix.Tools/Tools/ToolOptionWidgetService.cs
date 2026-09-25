//
// ToolOptionWidgetService.cs
//
// Author:
//       Paul Korecky <https://github.com/spaghetti22>
//
// Copyright (c) 2025 Paul Korecky
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

using System.Collections.Generic;
using Pinta.Brix.Engine;

//was previously: namespace Pinta.Tools;
namespace Pinta.Brix.Tools;

/// <summary>
/// This class manages the toolbar descriptors belonging to tool options.
/// (Upstream created GTK widgets; the port creates engine toolbar items the
/// UI layer renders.)
/// </summary>
public static class ToolOptionWidgetService
{
	private static readonly Dictionary<ToolOption, ToolBarItem> tool_option_widgets = [];

	/// <summary>
	/// For the provided tool option, either create the appropriate toolbar
	/// item, or if it has already been created, retrieve it.
	/// </summary>
	public static ToolBarItem GetWidgetForOption (ToolOption toolOption)
	{
		if (tool_option_widgets.TryGetValue (toolOption, out var widget)) {
			return widget;
		}

		ToolBarContainer box = new ();

		if (toolOption is IntegerOption integerOption) {
			ToolBarSpinButton spin_button = new (integerOption.Minimum, integerOption.Maximum, 1, integerOption.Value);
			spin_button.ValueChanged += (_, _) => integerOption.Value = spin_button.GetValueAsInt ();
			integerOption.OnValueChanged += newValue => spin_button.Value = newValue;

			box.Append (new ToolBarLabel ($" {integerOption.LabelText}: "));
			box.Append (spin_button);
		}

		tool_option_widgets[toolOption] = box;
		return box;
	}
}
