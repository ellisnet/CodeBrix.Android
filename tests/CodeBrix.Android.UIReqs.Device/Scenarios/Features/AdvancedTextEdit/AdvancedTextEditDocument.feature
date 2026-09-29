@needs-advancedtextedit
Feature: AdvancedTextEdit shows and edits a document
	The editor must draw the document it is given, in the face it was told to use; put what is typed
	in at the caret and draw it; refuse the keyboard when it is read-only; take a change back on Undo
	and put it again on Redo; mark the ends of the lines when it is asked to; and start afresh, with
	nothing to undo, when a whole new document is set.
	The Editor these scenarios build is the control with a real monospaced face at 20 points - its own
	default family name is the generic word "monospace", which resolves to nothing here, and the
	family forbids a system-font fallback - and with its scroll bars set to appear only when there is
	something to scroll, because the control shows both at all times by default and a bar is twelve
	pixels of grey that has nothing to do with the text.
	The caret is hidden before every frame that is compared with another: it blinks on a half-second
	timer that raises no event, so two frames taken across a tick would differ by the caret alone.
	Out of scope here, deliberately: hover (this is a touch and keyboard panel), display scale (pinned
	at 1.0), and the clipboard, which this panel has not got - so Copy, Cut and Paste are claimed
	nowhere. What the document HOLDS is asserted on the tree; how it LOOKS, on the pixels. The rope,
	the line manager and the undo stack themselves are the add-in's own host-free suite's business.

Scenario: The editor draws the document it is given, in the face that shipped beside it
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 150   |
	And the editor "editor" holds the sample "three lines"
	And the caret of "editor" is hidden
	When the frame is captured
	Then the monospaced font shipped beside the scenarios
	And the editor "editor" holds the text "one\ntwo\nthree"
	And the editor "editor" holds 3 lines
	And the region of "editor" has ink
	And the ink color of "editor" is "Black"
	And the bottommost 40 pixels of "editor" are blank

Scenario: Typing goes in at the caret, and what was typed is drawn
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 150   |
	And the editor "editor" holds the sample "three lines"
	When "editor" is tapped
	And the caret of "editor" is hidden
	And the caret of "editor" is put at offset 3
	And the frame is captured as "before"
	And the text "hello" is typed into "editor"
	And the frame is captured as "after"
	Then the editor "editor" holds the text "onehello\ntwo\nthree"
	And the caret of "editor" is at offset 8
	And the caret of "editor" is at line 1, column 9
	And the region of "editor" in frame "after" holds more ink than in frame "before"

Scenario: A read-only editor ignores what is typed at it
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 150   |
		| IsReadOnly | True  |
	And the editor "editor" holds the sample "three lines"
	When "editor" is tapped
	And the caret of "editor" is hidden
	And the frame is captured as "before"
	And the text "zzz" is typed into "editor"
	And the frame is captured as "after"
	Then the editor "editor" holds the text "one\ntwo\nthree"
	And the editor "editor" holds 3 lines
	And the region of "editor" in frame "after" is unchanged from frame "before"

Scenario: Undo puts back what a key took away, and Redo takes it away again
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 150   |
	And the editor "editor" holds the sample "three lines"
	When "editor" is tapped
	And the caret of "editor" is hidden
	And the caret of "editor" is put at offset 3
	And the frame is captured as "before"
	And the key "Back" is pressed
	And the frame is captured as "after"
	Then the editor "editor" holds the text "on\ntwo\nthree"
	And the editor "editor" can undo
	And the region of "editor" in frame "before" holds more ink than in frame "after"
	When Undo is performed on "editor"
	And the frame is captured as "undone"
	Then the editor "editor" holds the text "one\ntwo\nthree"
	And the region of "editor" in frame "undone" is unchanged from frame "before"
	When Redo is performed on "editor"
	And the frame is captured as "redone"
	Then the editor "editor" holds the text "on\ntwo\nthree"
	And the region of "editor" in frame "redone" is unchanged from frame "after"

Scenario: Marking the ends of the lines draws something that was not there before
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 150   |
	And the editor "editor" holds the sample "three lines"
	And the caret of "editor" is hidden
	When the frame is captured as "plain"
	And the ShowEndOfLine of "editor" is set to "True"
	And the frame is captured as "marked"
	Then the region of "editor" in frame "marked" holds more ink than in frame "plain"
	And the editor "editor" holds the text "one\ntwo\nthree"
	And nothing outside "editor" changed between frames "plain" and "marked"

Scenario: A new document replaces everything and leaves nothing to undo
	Given the editor font is warm
	And the application shows an Editor named "editor" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 150   |
	And the editor "editor" holds the sample "three lines"
	When "editor" is tapped
	And the caret of "editor" is hidden
	And the caret of "editor" is put at offset 3
	And the text "hello" is typed into "editor"
	And the frame is captured as "before"
	Then the editor "editor" can undo
	When the Text of "editor" is set to "brand new\ndocument"
	And the frame is captured as "after"
	Then the editor "editor" holds the text "brand new\ndocument"
	And the editor "editor" holds 2 lines
	And the caret of "editor" is at offset 0
	And the editor "editor" cannot undo
	And the region of "editor" in frame "after" differs from frame "before"
