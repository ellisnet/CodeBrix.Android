Feature: Material app bars for the WinUI CommandBar family
	Android-only (AP10-A; not a copy of a CodeBrix.Platform feature). A CommandBar is a Material app bar laid out where
	Core puts it (adaptive table row "CommandBar (WinUI)"): a top app bar with its Content as title in a Medium or
	Expanded window, a bottom app bar in a Compact one. AppBarButtons and AppBarToggleButtons are its actions,
	SecondaryCommands its overflow menu (the native popup replaces CommandBarOverflowPresenter; an AppBarSeparator
	there divides menu groups). Every action runs Core's Click / Command / toggle.

Scenario: A CommandBar in an Expanded window is a Material top app bar titled with its Content
	Given the application shows the AP10 sample "top command bar" named "bar"
	Then "bar" is shown by a native MaterialToolbar
	And the CommandBar "bar" is a Material MaterialToolbar titled "Mail"
	And the Core tree of "bar" holds no CommandBarOverflowPresenter
	When the frame is captured

Scenario: A CommandBar in a Compact window is a Material bottom app bar and becomes a top app bar again when the window widens
	Given the application shows the AP10 sample "top command bar" named "bar"
	When the window is simulated as 400 by 800 dp
	Then "bar" is shown by a native BottomAppBar
	When the frame is captured
	When the window size classes are no longer simulated
	Then the CommandBar "bar" is a Material MaterialToolbar titled "Mail"

Scenario: AppBarButtons and AppBarToggleButtons are the actions and SecondaryCommands the overflow menu
	Given the application shows the AP10 sample "top command bar" named "bar"
	Then the CommandBar "bar" has the actions "Add, Bold, Delete" and the overflow items "Settings, About"

Scenario: An AppBarSeparator among the SecondaryCommands divides the overflow menu into groups
	Given the application shows the AP10 sample "top command bar" named "bar"
	Then the overflow of the CommandBar "bar" has 2 groups

Scenario: A real finger on an AppBarButton action raises its Click once
	Given the application shows the AP10 sample "top command bar" named "bar"
	And the Click events of "add" are counted
	When a real finger taps the action "Add" of the CommandBar "bar"
	Then "add" raised Click 1 times

Scenario: A real finger on an AppBarToggleButton action checks it and its menu item
	Given the application shows the AP10 sample "top command bar" named "bar"
	When a real finger taps the action "Bold" of the CommandBar "bar"
	Then the Core property IsChecked of "bold" is "True"
	And the menu item "Bold" of the CommandBar "bar" is checked

Scenario: Opening a CommandBar shows its overflow menu and choosing an item runs it and shuts the bar
	Given the application shows the AP10 sample "top command bar" named "bar"
	And the Click events of "settings" are counted
	And the Opened events of "bar" are counted
	And the Closed events of "bar" are counted
	When IsOpen of "bar" is set to true
	Then the overflow menu of the CommandBar "bar" is showing
	When the overflow item "Settings" of the CommandBar "bar" is chosen
	Then the overflow menu of the CommandBar "bar" is shut
	And "settings" raised Click 1 times
	And "bar" raised Opened 1 times
	And "bar" raised Closed 1 times

Scenario: A command disabled or renamed in Core is disabled or renamed in the native menu
	Given the application shows the AP10 sample "top command bar" named "bar"
	When the Core property IsEnabled of "delete" becomes "False"
	And the Core property Label of "about" becomes "About Mail"
	Then the menu item "Delete" of the CommandBar "bar" is disabled
	And the CommandBar "bar" has a menu item "About Mail"

Scenario: A CommandBar holding an AppBarElementContainer keeps its Fluent template
	Given the application shows the AP10 sample "command bar with an element container" named "bar"
	Then "bar" is not shown by a native MaterialToolbar
	And the Core tree of "bar" holds a AppBarElementContainer

Scenario: The command row of a CommandBarFlyout keeps its Fluent template
	Given the application shows the AP10 sample "command bar flyout row" named "row"
	Then "row" is not shown by a native MaterialToolbar

Scenario: With the native CommandBar switched off a CommandBar keeps its Fluent template
	Given the native CommandBar is switched off
	And the application shows the AP10 sample "top command bar" named "bar"
	Then "bar" is not shown by a native MaterialToolbar

Scenario: An AppBar with XAML content keeps its Fluent template
	Given the application shows the AP10 sample "app bar" named "appbar"
	Then "appbar" is not shown by a native MaterialToolbar
	And "appbar" shows the native text "Plain app bar"

Scenario: A stand-alone AppBarButton is a Material button whose real tap raises Click
	Given the application shows the AP10 sample "app bar button" named "button"
	And the Click events of "button" are counted
	Then "button" is shown by a native MaterialButton
	And "button" shows the native text "Add"
	When the frame is captured
	When a real finger taps "button"
	Then "button" raised Click 1 times

Scenario: A stand-alone AppBarToggleButton is a checkable Material button that a real tap checks
	Given the application shows the AP10 sample "app bar toggle button" named "toggle"
	Then "toggle" is shown by a native MaterialButton
	When a real finger taps "toggle"
	Then the Core property IsChecked of "toggle" is "True"
