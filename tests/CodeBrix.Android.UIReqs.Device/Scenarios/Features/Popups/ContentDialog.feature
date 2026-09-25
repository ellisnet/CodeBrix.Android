Feature: ContentDialog
	A ContentDialog is modal: it covers the application with a dimmed layer, puts its own
	surface over the middle of the panel, and hands back which of its buttons the finger
	chose - which is the answer the call that showed it has been waiting for.

Scenario: Showing a ContentDialog dims the application behind it
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property          | Value             |
		| Title             | Delete the file?  |
		| Content           | This cannot be undone. |
		| PrimaryButtonText | Delete            |
		| CloseButtonText   | Cancel            |
	When the frame is captured as "before"
	And the ContentDialog "dialog" is shown
	And the frame is captured as "showing"
	Then the ContentDialog "dialog" is showing
	And a popup is open
	And the panel behind the dialog is dimmed in frame "showing" compared to frame "before"

Scenario: Tapping the primary button of a ContentDialog answers Primary
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property            | Value            |
		| Title               | Delete the file? |
		| Content             | This cannot be undone. |
		| PrimaryButtonText   | Delete           |
		| SecondaryButtonText | Keep             |
		| CloseButtonText     | Cancel           |
	When the ContentDialog "dialog" is shown
	Then the ContentDialog "dialog" is showing
	When the primary button of the ContentDialog "dialog" is tapped
	Then the ContentDialog "dialog" returned "Primary"
	And the ContentDialog "dialog" is closed
	And no popup is open

Scenario: Tapping the secondary button of a ContentDialog answers Secondary
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property            | Value            |
		| Title               | Delete the file? |
		| PrimaryButtonText   | Delete           |
		| SecondaryButtonText | Keep             |
		| CloseButtonText     | Cancel           |
	When the ContentDialog "dialog" is shown
	And the secondary button of the ContentDialog "dialog" is tapped
	Then the ContentDialog "dialog" returned "Secondary"
	And no popup is open

Scenario: Tapping the close button of a ContentDialog answers None
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property          | Value            |
		| Title             | Delete the file? |
		| PrimaryButtonText | Delete           |
		| CloseButtonText   | Cancel           |
	When the ContentDialog "dialog" is shown
	And the close button of the ContentDialog "dialog" is tapped
	Then the ContentDialog "dialog" returned "None"
	And no popup is open

Scenario: A ContentDialog leaves the application as it found it
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property          | Value            |
		| Title             | Delete the file? |
		| PrimaryButtonText | Delete           |
		| CloseButtonText   | Cancel           |
	When the frame is captured as "before"
	And the ContentDialog "dialog" is shown
	And the close button of the ContentDialog "dialog" is tapped
	Then the ContentDialog "dialog" returned "None"
	When the frame is captured as "after"
	Then the region of "page" in frame "after" is unchanged from frame "before"
