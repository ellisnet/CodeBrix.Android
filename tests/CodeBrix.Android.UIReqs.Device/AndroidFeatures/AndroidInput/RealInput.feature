Feature: Real Android input
	Android-only (not a copy of a CodeBrix.Platform feature). The copied scenarios drive the panel
	through Core's own input injector; these send REAL Android events instead - MotionEvents and
	KeyEvents dispatched to the activity, the way a finger, a mouse and a hardware keyboard arrive -
	so the window's pointer and keyboard input sources are what is being checked.

Scenario: A finger on a Grid with a Background raises its pointer events and a Tapped
	Given the application shows a Grid named "pad" 400 by 300 painted "Red" that records its pointer events
	When a real finger taps "pad"
	Then "pad" recorded 1 "PointerPressed"
	And "pad" recorded 1 "PointerReleased"
	And "pad" recorded 1 "Tapped"

Scenario: A finger that lands away from a Grid leaves it alone
	Given the application shows a Grid named "pad" 400 by 300 painted "Red" that records its pointer events
	When a real finger taps the panel far from "pad"
	Then "pad" recorded 0 "PointerPressed"
	And "pad" recorded 0 "Tapped"

Scenario: A real mouse right click raises RightTapped
	Given the application shows a Grid named "pad" 400 by 300 painted "Red" that records its pointer events
	When a real mouse right-clicks "pad"
	Then "pad" recorded 1 "RightTapped"

Scenario: A mouse wheel notch over a Grid raises PointerWheelChanged with the notch delta
	Given the application shows a Grid named "pad" 400 by 300 painted "Red" that records its pointer events
	When the real mouse wheel turns one notch up over "pad"
	Then "pad" recorded 1 "PointerWheelChanged"
	And the last wheel delta "pad" recorded is 120

Scenario: A mouse moving across a Grid enters it and leaves it
	Given the application shows a Grid named "pad" 400 by 300 painted "Red" that records its pointer events
	When a real mouse hovers across "pad"
	Then "pad" recorded 1 "PointerEntered"
	And "pad" recorded 1 "PointerExited"

Scenario: A key reaches the KeyDown handler of the focused page
	Given the application shows a Page named "page" that records its keys
	And the page "page" has the keyboard focus
	When the real key "A" is pressed
	Then "page" recorded the key "A"

Scenario: Alt held with a key invokes the page's Alt keyboard accelerator
	Given the application shows a Page named "page" that records its keys
	And the page "page" has a keyboard accelerator "Menu" + "A"
	And the page "page" has the keyboard focus
	When the real key "A" is pressed with Alt held
	Then "page" recorded 1 "AcceleratorInvoked"

Scenario: A keyboard's arrow keys are arrows, not gamepad buttons
	Given the application shows a Page named "page" that records its keys
	And the page "page" has the keyboard focus
	When the real key "Down" is pressed
	Then "page" recorded the key "Down"
