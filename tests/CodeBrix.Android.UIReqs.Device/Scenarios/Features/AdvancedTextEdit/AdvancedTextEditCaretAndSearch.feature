@needs-advancedtextedit
Feature: AdvancedTextEdit moves its caret and finds what is asked for
	The editor must move the caret a line and a column at a time when the arrow keys are pressed, put
	it where a finger lands, open its search panel over the text on the find gesture, and mark every
	match of what is searched for.
	The search panel is not part of the editor until an application installs it, exactly as these
	scenarios do; nothing here types into the panel's own box, because what is being stated is that
	the panel opens and that a search marks what it found.
	Where the caret goes is asserted on the tree, never on the pixels: the caret blinks on a
	half-second timer that raises no event, so a frame taken across a tick shows it or does not. The
	one scenario that leaves the caret on show makes no claim about it - the frame is there for a
	person to look at.
	Out of scope here, deliberately: hover (this is a touch and keyboard panel), display scale (pinned
	at 1.0), and selecting by dragging a finger across the text, which the editor's own pointer
	handling would allow but which nothing has yet demonstrated on this panel.

Scenario: The arrow keys move the caret a line and a column at a time
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 150   |
	And the editor "editor" holds the sample "three lines"
	When "editor" is tapped
	And the caret of "editor" is hidden
	And the caret of "editor" is put at offset 0
	And the key "Down" is pressed
	And the key "Right" is pressed
	And the caret of "editor" is shown
	And the frame is captured
	Then the caret of "editor" is at line 2, column 2
	And the caret of "editor" is at offset 5
	And the region of "editor" has ink

Scenario: A tap puts the caret where the editor says the finger landed
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 150   |
	And the editor "editor" holds the sample "three lines"
	And the caret of "editor" is shown
	When the point 26, 35 inside "editor" is tapped
	And the frame is captured
	Then the caret of "editor" is where the editor said that point was
	And the caret of "editor" is at line 2, column 3
	And the text area of "editor" has the keyboard focus
	And the region of "editor" has ink

Scenario: The find gesture opens the search panel over the text
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 150   |
	And the editor "editor" holds the sample "three lines"
	And the search panel of "editor" is installed as "search"
	When "editor" is tapped
	And the caret of "editor" is hidden
	And the frame is captured as "plain"
	Then the search panel of "editor" is closed
	When the key "F" is pressed with the Control key held down
	And the frame is captured as "searching"
	Then the search panel of "editor" is open
	And the region of "search" has ink
	And the region of "editor" in frame "searching" differs from frame "plain"
	# THE FENCE OF A FIXED DEFECT. GotFocus bubbles, so the text area used to show its caret when
	# the search panel's own text box took the focus - a caret blinking in a document nobody is
	# typing into. The block below is the document BELOW the strip the panel occupies, at the end
	# of the last line, which is where the tap above left the caret. MEASURED on this panel: the
	# panel's own ink stops 33 pixels down the editor; the caret is one pixel wide at column 59,
	# rows 53 to 78; the last line's text ends at column 58. So the block covers the caret and
	# nothing the panel draws, and the only thing that can differ in it is the caret itself - 26
	# pixels of 3200, eight times what "the same picture" allows. The caret blinks on a
	# half-second timer that raises no event; with the fix it is never SHOWN, so this holds
	# whichever phase the timer is in - before the fix it was red whenever a blink was on.
	And the 80 by 40 block at 40, 42 inside "editor" in frame "searching" is unchanged from frame "plain"

Scenario: A search marks every match in the document
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 700   |
		| Height   | 150   |
	And the editor "editor" holds the sample "repeated words"
	And the search panel of "editor" is installed as "search"
	And the caret of "editor" is hidden
	When the frame is captured as "plain"
	Then the region of "editor" in frame "plain" does not contain "SearchMarker"
	When the search panel of "editor" searches for "alpha"
	And the caret of "editor" is hidden
	And the frame is captured as "marked"
	Then the search panel of "editor" is open
	And the region of "editor" contains "SearchMarker"
	And the region of "editor" in frame "marked" differs from frame "plain"
