Feature: Popup
	A Popup is the plainest thing an application can put over itself: it draws its child when
	IsOpen says so, and nothing at all when it does not.

Scenario: A Popup that is not open draws nothing
	Given the application shows a Popup named "pop" with a panel named "sheet" 300 by 200 painted "Lime"
	When the frame is captured
	Then the Popup "pop" is closed
	And no popup is open
	And the panel is blank

Scenario: Opening a Popup draws its child over the application
	Given the application shows a Popup named "pop" with a panel named "sheet" 300 by 200 painted "Lime"
	When the IsOpen of "pop" is set to "True"
	And the frame is captured
	Then the Popup "pop" is open
	And a popup is open
	And the region of "sheet" is uniformly "Lime"

Scenario: Closing a Popup takes its child off the panel again
	Given the application shows a Popup named "pop" with a panel named "sheet" 300 by 200 painted "Lime"
	When the frame is captured as "closed"
	And the IsOpen of "pop" is set to "True"
	And the frame is captured as "open"
	Then a popup is open
	When the IsOpen of "pop" is set to "False"
	And the frame is captured as "closed again"
	Then the Popup "pop" is closed
	And no popup is open
	And the panel is blank
