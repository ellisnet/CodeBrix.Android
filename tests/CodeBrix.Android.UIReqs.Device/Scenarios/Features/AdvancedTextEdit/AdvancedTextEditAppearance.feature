@needs-advancedtextedit
Feature: AdvancedTextEdit draws its margins, its colours and its lines
	The editor must number its lines down a margin when it is asked to and leave that edge to the text
	when it is not; colour a keyword, a comment and plain code differently once it is given a
	highlighting definition; paint the selection brush behind what is selected; break a line that is
	wider than it is onto more rows when word wrap is on; and, given a document far taller than
	itself, draw the lines that fit and scroll to the ones that do not.
	Where a scenario claims a colour for a piece of text it names the line and the columns, and the
	rectangle those cover comes from the editor's own layout - so the claim is about the keyword a
	person sees, not about a pixel someone guessed.
	The caret is hidden before every frame: it blinks on a half-second timer that raises no event.
	Out of scope here, deliberately: hover (this is a touch and keyboard panel), display scale (pinned
	at 1.0), and every highlighting definition but the one C# document these scenarios open - the
	highlighting manager is process-wide, so only the definitions that ship inside the add-in are used
	and none is registered.

Scenario: Line numbers are drawn down the left edge only when they are asked for
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 150   |
	And the editor "editor" holds the sample "three lines"
	And the caret of "editor" is hidden
	When the frame is captured as "no numbers"
	Then the line number margin of "editor" is not showing
	And the region of "editor" in frame "no numbers" does not contain "Gray"
	When the ShowLineNumbers of "editor" is set to "True"
	And the frame is captured as "numbered"
	Then the line number margin of "editor" is showing
	And the line numbers of "editor" are drawn in "Gray"
	And the region of "editor" in frame "numbered" differs from frame "no numbers"

Scenario: A highlighting definition colours a keyword, a comment and plain code apart
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 200   |
	And the editor "editor" holds the sample "a C# snippet"
	And the caret of "editor" is hidden
	When the frame is captured as "plain"
	Then the text at line 1, columns 1 to 3 of "editor" is drawn in "Black"
	And the text at line 3, columns 5 to 16 of "editor" is drawn in "Black"
	When the SyntaxHighlighting of "editor" is set to "C#"
	And the frame is captured as "highlighted"
	Then the editor "editor" is highlighted as "C#"
	And the text at line 1, columns 1 to 3 of "editor" is drawn in "Blue"
	And the text at line 3, columns 5 to 16 of "editor" is drawn in "Green"
	And the text at line 4, columns 5 to 10 of "editor" is drawn in "Black"

Scenario: Selecting text paints the selection brush behind it
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 150   |
	And the editor "editor" holds the sample "three lines"
	And the caret of "editor" is hidden
	When the frame is captured as "plain"
	Then the region of "editor" in frame "plain" does not contain "EditorSelection"
	When 3 characters from offset 0 are selected in "editor"
	And the caret of "editor" is hidden
	And the frame is captured as "part selected"
	Then the selection of "editor" is 3 characters long
	And the selected text of "editor" is "one"
	And the region of "editor" in frame "part selected" differs from frame "plain"
	When SelectAll is performed on "editor"
	And the caret of "editor" is hidden
	And the frame is captured as "all selected"
	Then the selection of "editor" is 13 characters long
	And the region of "editor" contains "EditorSelection"

Scenario: Word wrap breaks a line that is wider than the editor onto more rows
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property                      | Value  |
		| Width                         | 400    |
		| Height                        | 200    |
		| HorizontalScrollBarVisibility | Hidden |
		| VerticalScrollBarVisibility   | Hidden |
	And the editor "editor" holds the sample "one long line"
	And the caret of "editor" is hidden
	When the frame is captured as "unwrapped"
	Then the region of "editor" has ink
	And the bottom half of the region of "editor" is blank
	When the WordWrap of "editor" is set to "True"
	And the caret of "editor" is hidden
	And the frame is captured as "wrapped"
	Then the bottom half of the region of "editor" has ink
	And the ink of "editor" in frame "wrapped" is at least 3.0 times as tall as in frame "unwrapped"

Scenario: A document taller than the editor draws the lines that fit, and scrolls to the others
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property        | Value |
		| Width           | 400   |
		| Height          | 200   |
		| ShowLineNumbers | True  |
	And the editor "editor" holds the sample "five hundred lines"
	And the caret of "editor" is hidden
	When the frame is captured as "at the top"
	Then the editor "editor" holds 500 lines
	And the editor "editor" holds more than it can show
	And the region of "editor" has ink
	And the line numbers of "editor" are drawn in "Gray"
	When the editor "editor" is scrolled to line 250
	And the caret of "editor" is hidden
	And the frame is captured as "scrolled"
	Then the editor "editor" is scrolled away from the top
	And the region of "editor" in frame "scrolled" differs from frame "at the top"
