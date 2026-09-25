@needs-commandbar
Feature: A tool bar button does what it was bound to
	A button on a tool bar must run the command it was bound to when it is tapped, refuse the tap and
	grey its label when that command says it cannot execute, stay pressed when it is a toggle, show
	its label beside or under its icon when the bar asks for one, divide itself between a command and
	a menu when it is a drop-down, and hand the keyboard along the bar from item to item.

	Deliberately out of scope here. Six of the theme's eighteen visual states are the hover states,
	and this panel has a finger and a keyboard but no pointer that hovers, so they cannot be reached
	at all. The tooltip a button composes is a fact about the tree rather than about the frame for
	the same reason, and the press-and-hold mode of a drop-down button is left to the add-in's own
	unit suite, because its delay has no completion signal a scenario could wait on.

Scenario: Tapping a button runs the command it was bound to, once
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind       | Name | Icon | IconTint | Command     |
		| ToolButton | save | dot  | Navy     | saveCommand |
	And the icons of "bar" are loaded
	When "save" is tapped
	And the frame is captured
	Then the Click of "save" was raised once
	And the command "saveCommand" was executed once
	And the region of "save" contains "Navy"
	And the region of "save" does not contain "#D6D6D6"
	And the region of "save" does not contain "#D6D6D6"

Scenario: A button whose command cannot execute is faded and ignores a tap
	Given the command "saveCommand" can execute
	And the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind       | Name | Icon  | IconTint | Text | Command     |
		| ToolButton | save | block | Navy     | Save | saveCommand |
	And the icons of "bar" are loaded
	When the frame is captured as "enabled"
	Then the button "save" is enabled
	And the region of "save" contains at least 40 percent "Navy"
	When the command "saveCommand" stops being executable
	And the frame is captured as "disabled"
	Then the button "save" is disabled
	And the region of "save" does not contain "Navy"
	And the region of "save" in frame "disabled" differs from frame "enabled"
	When "save" is tapped
	Then the Click of "save" was not raised
	And the command "saveCommand" was not executed

Scenario: A toggle paints its checked background when it is tapped
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind             | Name      | Icon | IconTint |
		| ToolToggleButton | magnifier | dot  | Navy     |
	And the icons of "bar" are loaded
	When the frame is captured as "off"
	Then the ToolToggleButton "magnifier" is not checked
	And the region of "magnifier" in frame "off" does not contain "#CCE0F5"
	When "magnifier" is tapped
	And the frame is captured as "on"
	Then the ToolToggleButton "magnifier" is checked
	And the IsCheckedChanged of "magnifier" was raised at least once
	And the region of "magnifier" contains at least 40 percent "#CCE0F5"
	And the region of "magnifier" in frame "on" differs from frame "off"

Scenario: IconAndText puts the label beside the icon and widens the button
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind       | Name  | Icon | IconTint | Text  |
		| ToolButton | print | dot  | Navy     | Print |
	And the icons of "bar" are loaded
	When the frame is captured as "icon only"
	Then "print" is 36 by 32 device pixels
	And the region of "print" in frame "icon only" does not contain "#1B1B1B"
	When the LabelMode of "bar" is set to "IconAndText"
	And the frame is captured as "both"
	Then "print" is 69 by 32 device pixels
	And the left half of the region of "print" contains at least 10 percent "Navy"
	And the right half of the region of "print" contains at most 0.5 percent "Navy"
	And the right half of the region of "print" contains at most 95 percent "#F3F3F3"

Scenario: LabelPosition Bottom stacks the label under the icon
	Given the application shows a ToolBar named "bar" with:
		| Property  | Value       |
		| Width     | 400         |
		| LabelMode | IconAndText |
	And the bar "bar" holds:
		| Kind       | Name  | Icon | IconTint | Text  |
		| ToolButton | print | dot  | Navy     | Print |
	And the icons of "bar" are loaded
	When the frame is captured as "beside"
	Then "print" is 69 by 32 device pixels
	When the LabelPosition of "bar" is set to "Bottom"
	And the frame is captured
	Then "print" is 39 by 55 device pixels
	And the top half of the region of "print" contains at least 5 percent "Navy"
	And the bottom half of the region of "print" contains at most 0.5 percent "Navy"
	And the bottom half of the region of "print" contains at most 95 percent "#F3F3F3"

Scenario: A MenuButton drop-down runs its command from one part and opens its menu from the other
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind               | Name   | Icon | IconTint | Command       | PopupMode  |
		| ToolDropDownButton | styles | dot  | Navy     | stylesCommand | MenuButton |
	And the drop-down "styles" has a menu panel named "menu" 160 by 80 painted "Lime"
	And the icons of "bar" are loaded
	When the main part of "styles" is tapped
	Then the command "stylesCommand" was executed once
	And the menu of "styles" is closed
	When the arrow part of "styles" is tapped
	Then the menu of "styles" is open
	And the command "stylesCommand" was executed once
	When the frame is captured
	Then the open popup has ink
	And a popup is open

Scenario: The arrow keys walk along the bar and Enter invokes the focused button
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind       | Name | Icon | IconTint |
		| ToolButton | new  | dot  | Navy     |
		| ToolButton | open | dot  | Navy     |
	And the icons of "bar" are loaded
	And the keyboard focus is given to "new"
	When the key "Right" is pressed
	Then "open" has keyboard focus
	And "new" does not have keyboard focus
	When the key "Enter" is pressed
	Then the Click of "open" was raised once
	And the Click of "new" was not raised

# This scenario found a framework defect on 2026-09-10: a button that had been tapped once kept its
# hover colour for the rest of its life - checked or not, with nothing pointing at it. The button
# was still in its PointerOver visual state, because the dependency property that publishes the
# over state was never written by the pointer plumbing, so the change callback a control registers
# on it to recompute its visual state was never called and the control never heard that the finger
# had gone. Fixed at the source (the over state now writes that property, so every binding and
# callback on it is true); this scenario is the fence.
Scenario: Tapping a toggle a second time turns it off and leaves no colour behind
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind             | Name      | Icon | IconTint |
		| ToolToggleButton | magnifier | dot  | Navy     |
	And the icons of "bar" are loaded
	When the frame is captured as "off"
	And "magnifier" is tapped
	And the frame is captured as "on"
	And "magnifier" is tapped
	And the frame is captured as "off again"
	Then the ToolToggleButton "magnifier" is not checked
	And the region of "magnifier" does not contain "#E4E4E4"
	And the region of "magnifier" in frame "off again" is unchanged from frame "off"
