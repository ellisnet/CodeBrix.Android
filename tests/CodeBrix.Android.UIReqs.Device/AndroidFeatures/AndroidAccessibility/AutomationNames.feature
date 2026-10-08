Feature: Automation names on Android
	Android-only (AP9-3; not a copy of a CodeBrix.Platform feature). AutomationProperties.Name is what TalkBack speaks and
	what UI Automator matches as the content description: every handler that owns a native view puts the element's
	automation name on the content description of its accessibility view (the focusable widget: the Material button, the
	text field's editor, an overlaid native widget), when the element enters the tree and at every later change; a cleared
	name clears it (TalkBack falls back to the view's own text) and a handler that labels its widget itself puts its own
	label back. AutomationProperties.AutomationId becomes the view's tag. A content description draws nothing.

Scenario: A Button with an automation name shows it as the content description of its Material button
	Given the application shows a Button named "save" with the automation name "Save the document" and:
		| Property | Value |
		| Content  | Save  |
		| Width    | 240   |
		| Height   | 80    |
	Then the accessibility view of "save" is a native MaterialButton
	And the native MaterialButton of "save" has the content description "Save the document"
	And "save" shows the native text "Save"
	When the frame is captured
	Then the region of "save" has ink

Scenario: Changing the automation name at run time changes the content description
	Given the application shows a Button named "save" with the automation name "Save the document" and:
		| Property | Value |
		| Content  | Save  |
		| Width    | 240   |
		| Height   | 80    |
	When the automation name of "save" is set to "Save a copy"
	Then the native MaterialButton of "save" has the content description "Save a copy"
	When the automation name of "save" is set to "  Save as  "
	Then the native MaterialButton of "save" has the content description "Save as"

Scenario: Clearing the automation name clears the content description
	Given the application shows a Button named "save" with the automation name "Save the document" and:
		| Property | Value |
		| Content  | Save  |
		| Width    | 240   |
		| Height   | 80    |
	When the automation name of "save" is cleared
	Then the native MaterialButton of "save" has no content description
	And "save" shows the native text "Save"
	When the automation name of "save" is set to "Save again"
	Then the native MaterialButton of "save" has the content description "Save again"
	When the automation name of "save" is set to "   "
	Then the native MaterialButton of "save" has no content description

Scenario: A name set on a live Button with no name reaches its Material button
	Given the application shows a Button named "go" with:
		| Property | Value |
		| Content  | Go    |
		| Width    | 240   |
		| Height   | 80    |
	Then the native MaterialButton of "go" has no content description
	When the automation name of "go" is set to "Start the transfer"
	Then the native MaterialButton of "go" has the content description "Start the transfer"

Scenario: The automation name follows a Button whose content becomes element content
	Given the application shows a Button named "save" with the automation name "Save the document" and:
		| Property | Value |
		| Content  | Save  |
		| Width    | 240   |
		| Height   | 120   |
	When the Button "save" is given element content
	Then the accessibility view of "save" is a native ButtonHostView
	And the native ButtonHostView of "save" has the content description "Save the document"
	And "save" shows the native text "First"

Scenario: A TextBox with an automation name and an automation id names its native editor
	Given the application shows a TextBox named "email" with the automation name "Email address", the automation id "EmailBox" and:
		| Property | Value               |
		| Width    | 400                 |
		| Text     | someone@example.com |
	Then the accessibility view of "email" is a native EditText
	And the native EditText of "email" has the content description "Email address"
	And the native EditText of "email" has the tag "EmailBox"
	When the frame is captured
	Then the region of "email" has ink

Scenario: The app's automation name wins over the label a handler gives its widget, which comes back when the name is cleared
	Given the application shows the AP10B sample "rating control" named "rate"
	Then the native AppCompatRatingBar of "rate" has the content description "Rating 0 of 5"
	When the automation name of "rate" is set to "Customer rating"
	Then the native AppCompatRatingBar of "rate" has the content description "Customer rating"
	When the Core property Value of "rate" becomes "2"
	Then the native AppCompatRatingBar of "rate" has the content description "Customer rating"
	When the automation name of "rate" is cleared
	Then the native AppCompatRatingBar of "rate" has the content description "Rating 2 of 5"
