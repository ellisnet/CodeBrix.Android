Feature: ToggleSwitch
	A ToggleSwitch says which way it is set by where its knob sits: the knob travels from the
	left of the track to the right, the place it left no longer looks the way it did, and the
	track carries the theme's accent colour only while the switch is on.

Scenario: Switching a ToggleSwitch on moves its knob to the right of its track
	Given the application shows a ToggleSwitch named "sw"
	When the ToggleSwitch knob of "sw" is captured as "off"
	Then the ToggleSwitch "sw" is off
	And the ToggleSwitch knob of "sw" had ink in "off"
	When the IsOn of "sw" is set to "True"
	And the ToggleSwitch knob of "sw" is captured as "on"
	Then the ToggleSwitch "sw" is on
	And the ToggleSwitch knob of "sw" had ink in "on"
	And the ToggleSwitch knob of "sw" moved right from "off" to "on" by at least 10 pixels
	And where the ToggleSwitch knob of "sw" was in "off" looks different in "on"

Scenario: Switching a ToggleSwitch off brings its knob back to the left
	Given the application shows a ToggleSwitch named "sw" with:
		| Property | Value |
		| IsOn     | True  |
	When the ToggleSwitch knob of "sw" is captured as "on"
	And the IsOn of "sw" is set to "False"
	And the ToggleSwitch knob of "sw" is captured as "off"
	Then the ToggleSwitch "sw" is off
	And the ToggleSwitch knob of "sw" moved right from "off" to "on" by at least 10 pixels
	And the ToggleSwitch knob of "sw" had ink in "off"

Scenario: A ToggleSwitch fills its track with the accent colour only while it is on
	Given the application shows a ToggleSwitch named "sw"
	When the ToggleSwitch knob of "sw" is captured as "off"
	Then the ToggleSwitch track of "sw" is not filled with "Accent" in "off"
	When the IsOn of "sw" is set to "True"
	And the ToggleSwitch knob of "sw" is captured as "on"
	Then the ToggleSwitch track of "sw" is filled with "Accent" in "on"

Scenario: Tapping a ToggleSwitch turns it on and reports it once
	Given the application shows a ToggleSwitch named "sw"
	When the switch of the ToggleSwitch "sw" is tapped
	Then the ToggleSwitch "sw" is on
	And the Toggled of "sw" was raised once

Scenario: A disabled ToggleSwitch ignores a tap
	Given the application shows a ToggleSwitch named "sw" with:
		| Property  | Value |
		| IsEnabled | False |
	When the switch of the ToggleSwitch "sw" is tapped
	Then the ToggleSwitch "sw" is off
	And the Toggled of "sw" was raised 0 times
