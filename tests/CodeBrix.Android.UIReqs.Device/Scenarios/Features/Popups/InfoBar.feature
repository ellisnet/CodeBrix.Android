Feature: InfoBar
	An InfoBar is a message inside the page rather than over it: it takes no room until it is
	opened, it is painted in the colour of the severity it is carrying, and the finger that
	closes it takes it away again.

Scenario: An InfoBar that is not open takes no room on the panel
	Given the application shows an InfoBar named "bar" with:
		| Property | Value             |
		| Title    | Saved             |
		| Message  | The file is safe. |
	When the frame is captured
	Then the InfoBar "bar" is closed
	And the panel is blank

Scenario: An open informational InfoBar is painted its informational colour
	Given the application shows an InfoBar named "bar" with:
		| Property | Value             |
		| Title    | Saved             |
		| Message  | The file is safe. |
		| IsOpen   | True              |
	When the frame is captured
	Then the InfoBar "bar" is open
	And the region of "bar" has ink
	And the region of "bar" contains "InfoBarInformationalFill"

Scenario: An InfoBar reporting an error is painted its error colour instead
	Given the application shows an InfoBar named "bar" with:
		| Property | Value              |
		| Title    | Failed             |
		| Message  | The file is gone.  |
		| Severity | Error              |
		| IsOpen   | True               |
	When the frame is captured
	Then the InfoBar "bar" is open
	And the region of "bar" contains "InfoBarErrorFill"
	And the region of "bar" contains at least 0.5 percent "InfoBarErrorIcon"
	And the region of "bar" does not contain "InfoBarInformationalFill"

Scenario: Changing an InfoBar's severity repaints it
	Given the application shows an InfoBar named "bar" with:
		| Property | Value             |
		| Title    | Saved             |
		| Message  | The file is safe. |
		| IsOpen   | True              |
	When the frame is captured as "informational"
	And the Severity of "bar" is set to "Error"
	And the frame is captured as "error"
	Then the region of "bar" in frame "error" differs from frame "informational"
	And the region of "bar" in frame "error" does not contain "InfoBarInformationalFill"

Scenario: Tapping an InfoBar's close button takes it off the panel
	Given the application shows an InfoBar named "bar" with:
		| Property | Value             |
		| Title    | Saved             |
		| Message  | The file is safe. |
		| IsOpen   | True              |
	When the close button of the InfoBar "bar" is tapped
	And the frame is captured
	Then the InfoBar "bar" is closed
	And the CloseButtonClick of the InfoBar "bar" was raised 1 times
	And the panel is blank
