Feature: Expander
	An Expander is a header with something folded away underneath it. A new one is shut and its
	content takes up no room at all; opening it - by asking, or by tapping the header a finger
	would reach for - puts the content on the panel underneath the header.

Scenario: A new Expander is shut and its content takes up no room
	Given the application shows an Expander named "box" 500 wide headed "Details" with content named "detail" 160 tall painted "Red"
	When the frame is captured
	Then the Expander "box" is shut
	And the content of the Expander "box" is not showing
	And the region of "box" does not contain "Red"

Scenario: Opening an Expander puts its content on the panel under the header
	Given the application shows an Expander named "box" 500 wide headed "Details" with content named "detail" 160 tall painted "Red"
	When the frame is captured as "shut"
	And the Expander "box" is expanded
	And the content of the Expander "box" is captured as "open"
	Then the Expander "box" is open
	And the content of the Expander "box" is showing
	And the region of "detail" is uniformly "Red"
	And "ExpanderHeader" sits directly above "ExpanderContentClip"
	And where the content of the Expander "box" is in "open" is blank in frame "shut"

Scenario: Tapping an Expander's header opens it
	Given the application shows an Expander named "box" 500 wide headed "Details" with content named "detail" 160 tall painted "Red"
	When the header of the Expander "box" is tapped
	And the frame is captured
	Then the Expander "box" is open
	And the content of the Expander "box" is showing
	And the region of "detail" is uniformly "Red"

Scenario: Tapping the header of an open Expander shuts it again
	Given the application shows an Expander named "box" 500 wide headed "Details" with content named "detail" 160 tall painted "Red"
	And the Expander "box" is expanded
	When the header of the Expander "box" is tapped
	And the frame is captured
	Then the Expander "box" is shut
	And the content of the Expander "box" is not showing
	And the region of "box" does not contain "Red"

Scenario: Collapsing an Expander takes its content back off the panel
	Given the application shows an Expander named "box" 500 wide headed "Details" with content named "detail" 160 tall painted "Red"
	And the Expander "box" is expanded
	When the content of the Expander "box" is captured as "open"
	And the Expander "box" is collapsed
	And the frame is captured as "shut"
	Then the Expander "box" is shut
	And the content of the Expander "box" is not showing
	And where the content of the Expander "box" is in "open" is blank in frame "shut"
