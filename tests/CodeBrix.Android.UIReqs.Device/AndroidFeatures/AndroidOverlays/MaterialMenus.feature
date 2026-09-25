@native-overlays
Feature: Material menus
	Android-only (not a copy of a CodeBrix.Platform feature). A MenuFlyout of standard items is shown as a
	Material popup menu anchored at its target in a Medium or Expanded window, and as a modal bottom sheet in
	a Compact window. Choosing an item runs the item's own Click and puts the flyout away; dismissing the menu
	puts the flyout away and runs nothing. The flyout's IsOpen, Opening and Closed stay Core's.

Scenario: A MenuFlyout is shown as a Material popup menu in a wide window
	Given the application shows a Border named "anchor" 300 by 200 with Background "Silver"
	And a MenuFlyout named "menu" is attached to "anchor" with the items:
		| Item   |
		| Open   |
		| Save   |
		| Delete |
	When the flyout "menu" is shown at "anchor"
	Then the flyout "menu" is open
	And a Material popup menu is showing with the items "Open, Save, Delete"
	And no popup is open

Scenario: Choosing an item of the Material popup menu runs it and puts the flyout away
	Given the application shows a Border named "anchor" 300 by 200 with Background "Silver"
	And a MenuFlyout named "menu" is attached to "anchor" with the items:
		| Item   |
		| Open   |
		| Save   |
		| Delete |
	When the flyout "menu" is shown at "anchor"
	And the Material menu item "Save" is chosen
	Then the Click of "Save" was raised once
	And the Click of "Open" was not raised
	And the flyout "menu" is closed
	And no Material menu is showing

Scenario: Dismissing the Material popup menu puts the flyout away and runs nothing
	Given the application shows a Border named "anchor" 300 by 200 with Background "Silver"
	And a MenuFlyout named "menu" is attached to "anchor" with the items:
		| Item   |
		| Open   |
		| Save   |
	When the flyout "menu" is shown at "anchor"
	And the Material menu is dismissed
	Then the flyout "menu" is closed
	And the Click of "Save" was not raised
	And no Material menu is showing

Scenario: The Android back button puts a Material bottom sheet away
	Given overlays are laid out for a Compact window
	And the application shows a Border named "anchor" 300 by 200 with Background "Silver"
	And a MenuFlyout named "menu" is attached to "anchor" with the items:
		| Item   |
		| Open   |
		| Save   |
	When the flyout "menu" is shown at "anchor"
	And the Android back button is pressed
	Then the flyout "menu" is closed
	And no Material menu is showing

Scenario: Toggle, radio, separator and sub-menu items keep their meaning in the Material popup menu
	Given the application shows a Border named "anchor" 300 by 200 with Background "Silver"
	And a MenuFlyout named "menu" is attached to "anchor" with a toggle, a radio group, a separator and a sub-menu
	When the flyout "menu" is shown at "anchor"
	Then the Material menu item "Bold" is checked
	And the Material menu item "Large" is checked
	And the Material menu item "Small" is not checked
	And the Material menu has the sub-menu "More" with the items "Help, About"
	When the Material menu item "Bold" is chosen
	Then the ToggleMenuFlyoutItem "Bold" is unchecked
	And the flyout "menu" is closed

Scenario: Choosing a sub-menu item of the Material popup menu runs it
	Given the application shows a Border named "anchor" 300 by 200 with Background "Silver"
	And a MenuFlyout named "menu" is attached to "anchor" with a toggle, a radio group, a separator and a sub-menu
	When the flyout "menu" is shown at "anchor"
	And the Material menu item "About" is chosen
	Then the Click of "About" was raised once
	And the flyout "menu" is closed

Scenario: In a Compact window a MenuFlyout is a Material bottom sheet
	Given overlays are laid out for a Compact window
	And the application shows a Border named "anchor" 300 by 200 with Background "Silver"
	And a MenuFlyout named "menu" is attached to "anchor" with the items:
		| Item   |
		| Open   |
		| Save   |
		| Delete |
	When the flyout "menu" is shown at "anchor"
	Then a Material bottom sheet is showing with the items "Open, Save, Delete"
	When the Material menu item "Delete" is chosen
	Then the Click of "Delete" was raised once
	And the flyout "menu" is closed
	And no Material menu is showing

Scenario: A Material bottom sheet opens a sub-menu in place
	Given overlays are laid out for a Compact window
	And the application shows a Border named "anchor" 300 by 200 with Background "Silver"
	And a MenuFlyout named "menu" is attached to "anchor" with a toggle, a radio group, a separator and a sub-menu
	When the flyout "menu" is shown at "anchor"
	And the Material menu item "Help" is chosen
	Then the Click of "Help" was raised once
	And the flyout "menu" is closed
