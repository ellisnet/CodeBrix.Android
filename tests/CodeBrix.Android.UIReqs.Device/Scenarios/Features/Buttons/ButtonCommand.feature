Feature: Button command
	A Button bound to a Command must run that command when it is tapped, and must take the
	command's own word for whether it can be run at all: a command that says it cannot execute
	leaves its button disabled and looking disabled, and the button must follow the command the
	moment that answer changes.

Scenario: Tapping a Button runs its Command
	Given the command "save" can execute
	And the application shows a Button named "go" with:
		| Property | Value        |
		| Content  | Save changes |
		| Width    | 400          |
		| Height   | 120          |
		| Command  | save         |
	Then the button "go" is enabled
	When "go" is tapped
	Then the command "save" was executed once
	And the Click of "go" was raised once

Scenario: A Button whose Command cannot execute is disabled and looks disabled
	Given the command "save" cannot execute
	And the application shows a Button named "go" with:
		| Property | Value        |
		| Content  | Save changes |
		| Width    | 500          |
		| Height   | 140          |
		| FontSize | 48           |
		| Command  | save         |
	When the frame is captured as "refused"
	Then the button "go" is disabled
	When "go" is tapped
	Then the command "save" was not executed
	When the command "save" becomes executable
	And the frame is captured as "allowed"
	Then the button "go" is enabled
	And the region of "go" in frame "allowed" differs from frame "refused"

Scenario: A Command that becomes executable lets its Button be tapped
	Given the command "save" cannot execute
	And the application shows a Button named "go" with:
		| Property | Value        |
		| Content  | Save changes |
		| Width    | 400          |
		| Height   | 120          |
		| Command  | save         |
	When "go" is tapped
	Then the command "save" was not executed
	When the command "save" becomes executable
	And "go" is tapped
	Then the command "save" was executed once

Scenario: A Command that stops being executable disables its Button again
	Given the command "save" can execute
	And the application shows a Button named "go" with:
		| Property | Value        |
		| Content  | Save changes |
		| Width    | 400          |
		| Height   | 120          |
		| Command  | save         |
	When "go" is tapped
	Then the command "save" was executed once
	When the command "save" stops being executable
	Then the button "go" is disabled
	When "go" is tapped
	Then the command "save" was executed once
