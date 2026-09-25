Feature: Native controls
	Android-only (AP3a; not a copy of a CodeBrix.Platform feature). The copied scenarios check what a
	control does through Core's own input injector and template-part names; these check that the controls
	of tier 1 ARE native Material widgets, that a REAL Android finger or key (dispatched to the activity,
	native views first) drives them exactly once, and that values travel both ways without echo.

Scenario: A Button is a Material button and a real finger taps it once
	Given the application shows a Button named "go" with:
		| Property | Value |
		| Content  | Go    |
		| Width    | 240   |
		| Height   | 80    |
	Then "go" is shown by a native MaterialButton
	When a real finger taps "go"
	Then the Click of "go" was raised once

Scenario: A Button with element content hosts that content natively
	Given the application shows a Button named "go" with a StackPanel of two TextBlocks as its content
	Then "go" is shown by a native ButtonHostView
	And "go" shows the native text "First"
	When a real finger taps "go"
	Then the Click of "go" was raised once

Scenario: A CheckBox is a Material check box that a real finger checks once
	Given the application shows a CheckBox named "agree" with:
		| Property | Value |
		| Content  | Agree |
	Then "agree" is shown by a native MaterialCheckBox
	When a real finger taps "agree"
	Then the toggle "agree" is checked
	And the native check box "agree" is checked
	And the Checked of "agree" was raised 1 times

Scenario: A ToggleSwitch is a Material switch that a real finger turns on once
	Given the application shows a ToggleSwitch named "sw"
	Then "sw" is shown by a native MaterialSwitch
	When a real finger taps the switch of "sw"
	Then the ToggleSwitch "sw" is on
	And the Toggled of "sw" was raised once

Scenario: Core's IsOn reaches the native switch
	Given the application shows a ToggleSwitch named "sw"
	When the IsOn of "sw" is set to "True"
	Then the native switch "sw" is on

Scenario: A Slider is a Material slider that a real finger sets
	Given the application shows a Slider named "level" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 0     |
	Then "level" is shown by a native Slider
	When a real finger taps "level" at 75 percent of its width
	Then the Value of "level" is more than 60
	And the Value of "level" is less than 90

Scenario: Core's Value reaches the native slider
	Given the application shows a Slider named "level" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 10    |
	When the Value of "level" is set to "70"
	Then the native slider "level" shows 70

Scenario: A ProgressBar is a Material linear indicator showing its value
	Given the application shows a ProgressBar named "loading" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 40    |
	Then "loading" is shown by a native LinearProgressIndicator
	And the native progress of "loading" is 40 percent

Scenario: A TextBox is a Material text field that real keys type into
	Given the application shows a TextBox named "entry" with:
		| Property | Value |
		| Width    | 500   |
		| Height   | 70    |
	Then "entry" is shown by a native CodeBrixEditText
	When a real finger taps "entry"
	And the text "abc" is typed
	Then the Text of "entry" is "abc"
	And the native editor of "entry" shows "abc"

Scenario: Core's Text reaches the native editor without coming back
	Given the application shows a TextBox named "entry" with:
		| Property | Value |
		| Width    | 500   |
		| Height   | 70    |
	And the TextChanged of "entry" is counted
	When the Text of "entry" is set to "from Core"
	Then the native editor of "entry" shows "from Core"
	And the TextChanged of "entry" was counted 1 times

Scenario: A PasswordBox masks its password natively
	Given the application shows a PasswordBox named "secret" with:
		| Property | Value   |
		| Width    | 500     |
		| Height   | 70      |
		| Password | hunter2 |
	Then "secret" is shown by a native CodeBrixEditText
	And the native editor of "secret" shows "hunter2"
	And the native editor of "secret" shows its PasswordChar 7 times

Scenario: An Image shows Core's decoded bitmap in an image view
	Given the application shows a two-colour Image named "pic" 400 by 200 with its left half "Red" and its right half "Blue"
	Then "pic" is shown by a native ImageView
	And the native image of "pic" is a 40 by 20 bitmap

Scenario: A native ComboBox lists its items and a chosen row selects one
	Given native ComboBoxes are used
	And the application shows a ComboBox named "picker" with:
		| Property   | Value              |
		| Width      | 260                |
		| ItemLabels | Alpha, Beta, Gamma |
	Then "picker" is shown by a native MaterialAutoCompleteTextView
	And the native drop-down of "picker" lists "Alpha, Beta, Gamma"
	When row 2 of the native drop-down of "picker" is chosen
	Then the SelectedIndex of the ComboBox "picker" is 1
	And the native editor of "picker" shows "Beta"

Scenario: A NumberBox steps its Value with its native plus button
	Given the application shows a NumberBox named "count" holding 5
	Then "count" is shown by a native CodeBrixEditText
	When a real finger taps the end icon of "count"
	Then the NumberBox "count" holds 6
	And the native editor of "count" shows "6"

Scenario: An AutoSuggestBox reports what a real keyboard types and submits the query
	Given the application shows an AutoSuggestBox named "search" that records its events
	When a real finger taps "search"
	And the text "cat" is typed
	Then the AutoSuggestBox "search" reported the user text "cat"
	When the soft keyboard's action key is used on "search"
	Then the AutoSuggestBox "search" submitted "cat"
