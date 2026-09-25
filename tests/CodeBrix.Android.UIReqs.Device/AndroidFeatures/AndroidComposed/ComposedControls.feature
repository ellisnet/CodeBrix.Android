Feature: Native composed controls
	Android-only (AP6; not a copy of a CodeBrix.Platform feature). The composed controls the samples use are
	native: the Expander is a Material card with a native header row (text and chevron) over its Core content,
	the ColorPicker a composition of a spectrum view, Material sliders and text fields, and DatePicker /
	TimePicker open Material picker dialogs. Values travel both ways through Core.

Scenario: An Expander with a text header is a native Expander
	Given native Expanders are used
	And the application shows an Expander named "box" 500 wide headed "Details" with content named "detail" 160 tall painted "Red"
	Then "box" is shown by a native ExpanderView
	And "box" shows the native text "Details"

Scenario: A real finger on the header opens the Expander and again shuts it
	Given native Expanders are used
	And the application shows an Expander named "box" 500 wide headed "Details" with content named "detail" 160 tall painted "Red"
	When a real finger taps the header of the Expander "box"
	Then the Expander "box" is open
	When a real finger taps the header of the Expander "box"
	And the frame is captured
	Then the Expander "box" is shut
	And the region of "box" does not contain "Red"

Scenario: An opened native Expander shows its Core content under the header
	Given native Expanders are used
	And the application shows an Expander named "box" 500 wide headed "Details" with content named "detail" 160 tall painted "Red"
	When a real finger taps the header of the Expander "box"
	And the frame is captured
	Then the Expander "box" is open
	And the region of "detail" is uniformly "Red"

Scenario: Opening and shutting an Expander raises Expanding and Collapsed once each
	Given native Expanders are used
	And the application shows an Expander named "box" 500 wide headed "Details" with content named "detail" 160 tall painted "Red"
	And the Expanding and Collapsed of the Expander "box" are counted
	When a real finger taps the header of the Expander "box"
	And a real finger taps the header of the Expander "box"
	Then the Expander "box" raised Expanding 1 times and Collapsed 1 times

Scenario: An Expander whose header is an element keeps its Fluent template
	Given native Expanders are used
	And the application shows an Expander named "box" whose header is a TextBlock
	Then "box" is not shown by a native ExpanderView

Scenario: Without the native switch an Expander keeps its Fluent template
	Given the application shows an Expander named "box" 500 wide headed "Details" with content named "detail" 160 tall painted "Red"
	Then "box" is not shown by a native ExpanderView

Scenario: A ColorPicker is a native composition of Material parts
	Given the application shows a ColorPicker named "cp" with alpha and every text input
	Then "cp" is shown by a native ColorSpectrumView
	And "cp" is shown by a native Slider
	And "cp" is shown by a native TextInputLayout

Scenario: Core's Color reaches the native parts of a ColorPicker
	Given the application shows a ColorPicker named "cp" with alpha and every text input
	When the Color of the ColorPicker "cp" is set to "#800000FF"
	Then the hex field of the ColorPicker "cp" shows "#800000FF"
	And the channel fields of the ColorPicker "cp" show 0, 0, 255 and 50 percent

Scenario: A real finger on the spectrum picks a colour
	Given the application shows a ColorPicker named "cp" with alpha and every text input
	And the ColorChanged of the ColorPicker "cp" is counted
	When a real finger taps the spectrum of the ColorPicker "cp" at 25 percent across and 1 percent down
	Then the Color of the ColorPicker "cp" is near 128, 255, 0
	And the ColorPicker "cp" raised ColorChanged at least once

Scenario: Hex text entered into a ColorPicker sets its Color
	Given the application shows a ColorPicker named "cp" with alpha and every text input
	When "#80FF8000" is entered into the hex field of the ColorPicker "cp"
	Then the Color of the ColorPicker "cp" is "#80FF8000"

Scenario: Without alpha a ColorPicker hides its alpha parts and shows six hex digits
	Given the application shows a ColorPicker named "cp" without alpha
	When the Color of the ColorPicker "cp" is set to "#FF00FF00"
	Then the hex field of the ColorPicker "cp" shows "#00FF00"
	And the alpha slider of the ColorPicker "cp" is hidden

Scenario: A DatePicker opens a Material date picker on its Date and OK keeps that day
	Given the application shows a DatePicker named "dp" on 2026-09-24
	And the DateChanged of the DatePicker "dp" is counted
	When "dp" is tapped
	Then a Material date picker is showing
	When the Material picker's OK button is pressed
	Then no Material picker is showing
	And the soft keyboard is not showing
	And the DatePicker "dp" shows 2026-09-24
	And the DatePicker "dp" raised DateChanged 0 times

Scenario: A day chosen in the Material date picker becomes the DatePicker's Date
	Given the application shows a DatePicker named "dp" on 2026-09-24
	And the DateChanged of the DatePicker "dp" is counted
	When "dp" is tapped
	Then a Material date picker is showing
	When the Material date picker chooses 2026-10-05
	Then no Material picker is showing
	And the soft keyboard is not showing
	And the DatePicker "dp" shows 2026-10-05
	And the DatePicker "dp" raised DateChanged 1 times

Scenario: A TimePicker opens a Material time picker and takes the chosen time
	Given the application shows a TimePicker named "tp" at 09:30
	When "tp" is tapped
	Then a Material time picker is showing
	When the Material time picker is confirmed at 14:45
	Then no Material picker is showing
	And the soft keyboard is not showing
	And the TimePicker "tp" shows 14:45
