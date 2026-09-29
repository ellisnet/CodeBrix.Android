Feature: The AdvancedTextEdit on Android
	Android-only (the Platform's AdvancedTextEdit group, copied as the AdvancedTextEdit group, covers the editor itself:
	drawing, highlighting, margins, the caret, typing, undo, search). This group fences what Android adds. The editor draws
	on the Android canvas supply of the add-in (one Canvas per drawing surface - the text view and each margin - shown by
	a native Skia view). A focused text area opens a SOFT-KEYBOARD session: CodeBrix.Android.UI's text-input view takes
	the Android focus with the "editor" profile (suggestions, composition, a multi-line editor) and the text area's TEXT
	TARGET, so the input method sees the document around the caret, composes in place under an underline the editor
	draws, edits the document through the text area's own typing path, and hears whenever the selection moves. The
	keyboard's calls are made here on the connection the view gives an input method, as an input method makes them;
	fingers are REAL MotionEvents dispatched to the activity. In the texts, \n is a line break. The keyboard is masked
	in every frame; the editor sits at the top of the panel, clear of it.

Scenario: An AdvancedTextEdit draws through the Android canvas supply
	Given the application shows an AdvancedTextEdit named "editor" holding "one\ntwo\nthree"
	Then every drawing surface of the AdvancedTextEdit "editor" is an Android editor canvas that has painted
	When the caret of the AdvancedTextEdit "editor" is hidden
	And the frame is captured
	Then the region of "editor" has ink

Scenario: A focused text area opens a soft-keyboard session with the editor profile and its text
	Given the application shows an AdvancedTextEdit named "editor" holding "hello world"
	When the text area of the AdvancedTextEdit "editor" takes the focus
	And the caret of the AdvancedTextEdit "editor" is put at offset 5
	Then the soft-keyboard session is open for the AdvancedTextEdit "editor" with the "editor" profile and its text
	And the soft keyboard is asked for suggestions in a multi-line editor
	And the soft keyboard is handed the selection 5 to 5 with "hello" before it and " world" after it
	And the soft keyboard reads "hello" before the cursor and " world" after it

Scenario: A word the soft keyboard composes is typed into the document as it grows, underlined until it is finished
	Given the application shows an AdvancedTextEdit named "editor" holding "say "
	And the input method is driven by the scenario alone
	When the text area of the AdvancedTextEdit "editor" takes the focus
	And the caret of the AdvancedTextEdit "editor" is put at offset 4
	And the soft keyboard composes "w"
	And the soft keyboard composes "wo"
	And the soft keyboard composes "word"
	Then the AdvancedTextEdit "editor" holds "say word"
	And the AdvancedTextEdit "editor" saw "w|o|rd" entered
	And the composition of the AdvancedTextEdit "editor" is underlined from 4 to 8
	When the caret of the AdvancedTextEdit "editor" is hidden
	And the frame is captured as "composing"
	And the soft keyboard finishes its composition
	And the caret of the AdvancedTextEdit "editor" is hidden
	And the frame is captured as "finished"
	Then the AdvancedTextEdit "editor" shows no composition
	And the AdvancedTextEdit "editor" holds "say word"
	And the region of "editor" in frame "composing" holds more ink than in frame "finished"

Scenario: An autocorrection replaces the composed word, and a period is entered as a typed key is
	Given the application shows an AdvancedTextEdit named "editor" holding ""
	And the input method is driven by the scenario alone
	When the text area of the AdvancedTextEdit "editor" takes the focus
	And the soft keyboard composes "teh"
	And the soft keyboard commits "the"
	And the soft keyboard commits "."
	Then the AdvancedTextEdit "editor" holds "the."
	And the caret of the AdvancedTextEdit "editor" is at offset 4
	And the AdvancedTextEdit "editor" shows no composition
	And the AdvancedTextEdit "editor" saw "teh|." entered

Scenario: The soft keyboard makes an earlier word its composition again and corrects it
	Given the application shows an AdvancedTextEdit named "editor" holding "one tow three"
	And the input method is driven by the scenario alone
	When the text area of the AdvancedTextEdit "editor" takes the focus
	And the caret of the AdvancedTextEdit "editor" is put at offset 13
	And the soft keyboard makes 4 to 7 its composition
	Then the composition of the AdvancedTextEdit "editor" is underlined from 4 to 7
	When the soft keyboard commits "two"
	Then the AdvancedTextEdit "editor" holds "one two three"
	And the caret of the AdvancedTextEdit "editor" is at offset 7

Scenario: A delete around the cursor and a line break from the soft keyboard edit the document
	Given the application shows an AdvancedTextEdit named "editor" holding "abcdef"
	And the input method is driven by the scenario alone
	When the text area of the AdvancedTextEdit "editor" takes the focus
	And the caret of the AdvancedTextEdit "editor" is put at offset 4
	And the soft keyboard deletes 2 characters before the cursor
	Then the AdvancedTextEdit "editor" holds "abef"
	When the soft keyboard commits a line break
	Then the AdvancedTextEdit "editor" holds "ab\nef"
	And the caret of the AdvancedTextEdit "editor" is at line 2, column 1

Scenario: The soft keyboard reads and moves the editor's selection, and the editor paints it
	Given the application shows an AdvancedTextEdit named "editor" holding "one\ntwo\nthree"
	And the input method is driven by the scenario alone
	When the text area of the AdvancedTextEdit "editor" takes the focus
	And the caret of the AdvancedTextEdit "editor" is put at offset 0
	And the caret of the AdvancedTextEdit "editor" is hidden
	And the frame is captured as "plain"
	And the soft keyboard selects 4 to 13
	And the caret of the AdvancedTextEdit "editor" is hidden
	And the frame is captured as "selected"
	Then the selected text of the AdvancedTextEdit "editor" is "two\nthree"
	And the soft keyboard reads "two\nthree" as the selected text
	And the soft keyboard reads "one\n" before the cursor and "" after it
	And the region of "editor" in frame "selected" differs from frame "plain"

Scenario: A hardware key during a session ends the composition, and the soft keyboard hears where the caret went
	Given the application shows an AdvancedTextEdit named "editor" holding ""
	And the input method is driven by the scenario alone
	When the text area of the AdvancedTextEdit "editor" takes the focus
	And the soft keyboard composes "ab"
	And the selection updates sent to the soft keyboard are counted from now
	And the key "X" is pressed
	Then the AdvancedTextEdit "editor" holds "abx"
	And the AdvancedTextEdit "editor" shows no composition
	And the soft keyboard was told that the selection moved

Scenario: A read-only AdvancedTextEdit opens no soft-keyboard session
	Given the application shows a read-only AdvancedTextEdit named "editor" holding "locked"
	When the text area of the AdvancedTextEdit "editor" takes the focus
	Then no soft-keyboard session is open

Scenario: Moving the focus off the editor closes the session and takes the underline away
	Given the application shows an AdvancedTextEdit named "editor" holding "", below a Button named "other"
	And the input method is driven by the scenario alone
	When the text area of the AdvancedTextEdit "editor" takes the focus
	And the soft keyboard composes "draft"
	Then the composition of the AdvancedTextEdit "editor" is underlined from 0 to 5
	When the Button "other" takes the focus
	Then the soft-keyboard session is closed
	And the AdvancedTextEdit "editor" holds "draft"
	And the text view of the AdvancedTextEdit "editor" draws no composition underline

Scenario: With the soft keyboard up, a real finger puts the caret where it lands
	Given the application shows an AdvancedTextEdit named "editor" holding "one\ntwo\nthree"
	When the text area of the AdvancedTextEdit "editor" takes the focus
	Then the soft keyboard is showing
	When a real finger taps the AdvancedTextEdit "editor" at line 3, column 3
	Then the caret of the AdvancedTextEdit "editor" is at line 3, column 3

Scenario: A real finger dragged along a line selects its text with the editor's own selection
	Given the application shows an AdvancedTextEdit named "editor" holding "alpha beta gamma"
	When a real finger drags along line 1 of the AdvancedTextEdit "editor" from column 7 to column 11
	Then the selected text of the AdvancedTextEdit "editor" is "beta"
