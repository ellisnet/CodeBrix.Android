Feature: TwoPaneView
	A TwoPaneView decides for itself whether there is room to put its two panes side by side, one
	above the other, or only one of them at a time - and the numbers it decides from are its own,
	so the same control says the same thing on either panel.

Scenario: A TwoPaneView wide enough for both panes puts them side by side
	Given the application shows a TwoPaneView named "two" 900 by 400 with panes named "left" painted "Red" and "right" painted "Blue"
	When the frame is captured
	Then the TwoPaneView "two" is in "Wide" mode
	And the region of "left" is uniformly "Red"
	And the region of "right" is uniformly "Blue"
	And "left" sits directly left of "right"

Scenario: A TwoPaneView too narrow for both panes stacks them
	Given the application shows a TwoPaneView named "two" 900 by 400 with panes named "left" painted "Red" and "right" painted "Blue"
	And the MinWideModeWidth of "two" is set to "1200"
	And the MinTallModeHeight of "two" is set to "200"
	When the frame is captured
	Then the TwoPaneView "two" is in "Tall" mode
	And the region of "left" is uniformly "Red"
	And the region of "right" is uniformly "Blue"
	And "left" sits directly above "right"

Scenario: A TwoPaneView with room for neither shows one pane only
	Given the application shows a TwoPaneView named "two" 900 by 400 with panes named "left" painted "Red" and "right" painted "Blue"
	And the MinWideModeWidth of "two" is set to "1200"
	And the MinTallModeHeight of "two" is set to "900"
	When the frame is captured
	Then the TwoPaneView "two" is in "SinglePane" mode
	And the region of "two" is uniformly "Red"
	And the region of "two" does not contain "Blue"

Scenario: A TwoPaneView showing one pane only can be told which one it is
	Given the application shows a TwoPaneView named "two" 900 by 400 with panes named "left" painted "Red" and "right" painted "Blue"
	And the MinWideModeWidth of "two" is set to "1200"
	And the MinTallModeHeight of "two" is set to "900"
	And the PanePriority of "two" is set to "Pane2"
	When the frame is captured
	Then the TwoPaneView "two" is in "SinglePane" mode
	And the region of "two" is uniformly "Blue"
	And the region of "two" does not contain "Red"

Scenario: Widening a narrow TwoPaneView brings the second pane back beside the first
	Given the application shows a TwoPaneView named "two" 900 by 400 with panes named "left" painted "Red" and "right" painted "Blue"
	And the MinWideModeWidth of "two" is set to "1200"
	And the MinTallModeHeight of "two" is set to "900"
	When the frame is captured as "single"
	And the MinWideModeWidth of "two" is set to "600"
	And the frame is captured as "wide"
	Then the TwoPaneView "two" is in "Wide" mode
	And the region of "two" in frame "wide" differs from frame "single"
	And the region of "right" is uniformly "Blue"
	And "left" sits directly left of "right"
