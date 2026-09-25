@native-overlays
Feature: Android back navigation
	Android-only (not a copy of a CodeBrix.Platform feature). The Android back button and back gesture go to
	the topmost thing that can take them - an overlay first, then a Frame that can go back - and otherwise
	stay the system's (so the predictive back-to-home animation plays). During a predictive back gesture the
	page being left previews the gesture, and springs back if the gesture is cancelled.

Scenario: The Android back button takes a Frame back one page
	Given the application shows a Frame that went from page "one" to page "two"
	Then the Android back button is taken by the app
	When the Android back button is pressed
	Then the Frame shows page "one"
	And the Frame cannot go back
	And the Android back button is left to the system

Scenario: The Android back button closes a Core flyout before it takes a Frame back
	Given the application shows a Frame that went from page "one" to page "two"
	And a Flyout named "flyout" is attached to "two" with a panel named "panel" 200 by 100 painted "Red"
	When the flyout "flyout" is shown at "two"
	And the Android back button is pressed
	Then the flyout "flyout" is closed
	And the Frame shows page "two"
	When the Android back button is pressed
	Then the Frame shows page "one"

Scenario: A predictive back gesture previews the page being left and springs back when cancelled
	Given the application shows a Frame that went from page "one" to page "two"
	When a predictive back gesture from the left edge reaches 60 percent
	Then the page being left is previewed smaller and shifted right
	When the predictive back gesture is cancelled
	Then the page being left is back at full size
	And the Frame shows page "two"

Scenario: A completed predictive back gesture takes the Frame back
	Given the application shows a Frame that went from page "one" to page "two"
	When a predictive back gesture from the left edge reaches 60 percent
	And the predictive back gesture is completed
	Then the Frame shows page "one"
	And the page that was left is at full size

Scenario: An application that handles BackRequested gets the Android back button first
	Given the application shows a Frame that went from page "one" to page "two"
	And the application handles SystemNavigationManager.BackRequested
	When the Android back button is pressed
	Then the application's BackRequested handler ran once
	And the Frame shows page "two"
