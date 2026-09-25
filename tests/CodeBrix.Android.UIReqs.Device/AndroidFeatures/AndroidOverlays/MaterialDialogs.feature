@native-overlays
Feature: Material dialogs
	Android-only (not a copy of a CodeBrix.Platform feature). A ContentDialog whose title and content are
	text is shown as a Material 3 dialog (a platform window over the activity) instead of Core's own popup;
	its buttons still run Core's path - the click event, the command, Closing - and ShowAsync still answers
	which button closed it. The copied Popups scenarios run with the Material forms switched off (they look
	for Core's popup and template parts); these scenarios run with them on.

Scenario: A text ContentDialog is shown as a Material dialog over the dimmed application
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property          | Value                  |
		| Title             | Delete the file?       |
		| Content           | This cannot be undone. |
		| PrimaryButtonText | Delete                 |
		| CloseButtonText   | Cancel                 |
	When the frame is captured as "before"
	And the ContentDialog "dialog" is shown
	And the frame is captured as "showing"
	Then a Material dialog is showing with the title "Delete the file?" and the message "This cannot be undone."
	And the Material dialog shows "Delete" as its positive button
	And the Material dialog shows "Cancel" as its negative button
	And no popup is open
	And the panel behind the dialog is dimmed in frame "showing" compared to frame "before"

Scenario: Pressing the Material dialog's primary button answers Primary
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property            | Value            |
		| Title               | Delete the file? |
		| PrimaryButtonText   | Delete           |
		| SecondaryButtonText | Keep             |
		| CloseButtonText     | Cancel           |
	When the ContentDialog "dialog" is shown
	Then the Material dialog shows "Keep" as its neutral button
	When the Material dialog button "Delete" is pressed
	Then the ContentDialog "dialog" returned "Primary"
	And no Material dialog is showing

Scenario: Pressing the Material dialog's secondary button answers Secondary
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property            | Value            |
		| Title               | Delete the file? |
		| PrimaryButtonText   | Delete           |
		| SecondaryButtonText | Keep             |
	When the ContentDialog "dialog" is shown
	Then the Material dialog shows "Keep" as its negative button
	When the Material dialog button "Keep" is pressed
	Then the ContentDialog "dialog" returned "Secondary"
	And no Material dialog is showing

Scenario: Pressing the Material dialog's close button answers None
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property          | Value            |
		| Title             | Delete the file? |
		| PrimaryButtonText | Delete           |
		| CloseButtonText   | Cancel           |
	When the ContentDialog "dialog" is shown
	And the Material dialog button "Cancel" is pressed
	Then the ContentDialog "dialog" returned "None"
	And no Material dialog is showing

Scenario: The Android back button on a Material dialog answers None, as Escape does
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property          | Value            |
		| Title             | Delete the file? |
		| PrimaryButtonText | Delete           |
		| CloseButtonText   | Cancel           |
	When the ContentDialog "dialog" is shown
	And the Android back button is pressed
	Then the ContentDialog "dialog" returned "None"
	And no Material dialog is showing

Scenario: A Closing the application cancels keeps the Material dialog open
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property          | Value            |
		| Title             | Delete the file? |
		| PrimaryButtonText | Delete           |
		| CloseButtonText   | Cancel           |
	And the ContentDialog "dialog" cancels its first Closing
	When the ContentDialog "dialog" is shown
	And the Material dialog button "Delete" is pressed
	Then a Material dialog is showing with the title "Delete the file?" and the message ""
	When the Material dialog button "Delete" is pressed
	Then the ContentDialog "dialog" returned "Primary"
	And no Material dialog is showing

Scenario: A disabled primary button is disabled on the Material dialog until the application enables it
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property          | Value      |
		| Title             | Sign in    |
		| PrimaryButtonText | Continue   |
		| CloseButtonText   | Cancel     |
	And the ContentDialog "dialog" starts with its primary button disabled
	When the ContentDialog "dialog" is shown
	Then the Material dialog button "Continue" is disabled
	When the ContentDialog "dialog" enables its primary button
	Then the Material dialog button "Continue" is enabled
	When the Material dialog button "Continue" is pressed
	Then the ContentDialog "dialog" returned "Primary"

Scenario: A ContentDialog with XAML content stays in Core's own popup
	Given the application shows a Border named "page" 900 by 600 with Background "Lime"
	And the scenario has a ContentDialog named "dialog" with:
		| Property          | Value            |
		| Title             | Pick a colour    |
		| PrimaryButtonText | OK               |
		| CloseButtonText   | Cancel           |
	And the ContentDialog "dialog" shows a panel of XAML as its content
	When the ContentDialog "dialog" is shown
	Then no Material dialog is showing
	And a popup is open
	When the close button of the ContentDialog "dialog" is tapped
	Then the ContentDialog "dialog" returned "None"
	And no popup is open
