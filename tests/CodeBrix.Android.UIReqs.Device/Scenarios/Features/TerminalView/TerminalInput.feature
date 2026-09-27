@needs-terminalview
Feature: Terminal input
	A Terminal must hand everything a person types to its host as VT bytes and draw none of it
	itself, encode a special key as the escape sequence the far end expects, let a finger select a
	run of text and tint what it covers, copy that selection on the terminal's own clipboard chord,
	announce the name a session gives itself without disturbing the screen, and page back through
	the scrollback and forward again.

	Every scenario here hides the cursor first, for the same reason the painting scenarios do: the
	cursor blinks twice a second while the control has the keyboard, and half of the frames would
	otherwise hold a block that no requirement is about.

	Deliberately out of scope here: pasting (it reads the system clipboard, which a test machine may
	not have), the right-click context menu (emulated touch never reports a right button, by design),
	double-click word selection (it depends on two taps landing inside four hundred milliseconds),
	and mouse-reporting protocols, which the engine documents as unsupported.

Scenario: Typing goes to the host and nothing is drawn locally
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 800   |
		| Height   | 600   |
	And the layout "cell" holds a Terminal named "term" with:
		| Property         | Value  |
		| BackgroundColor  | Navy   |
		| ForegroundColor  | Yellow |
		| TerminalFontSize | 24     |
	When the frame is captured
	And the cursor of "term" is hidden
	And "term" is given focus
	And the frame is captured as "before typing"
	Then "term" has keyboard focus
	When the text "abc" is typed
	And the frame is captured as "after typing"
	Then the InputEmitted of "term" was raised 3 times
	And the input emitted by "term" is "abc"
	And the region of "term" in frame "after typing" is unchanged from frame "before typing"

Scenario: A cursor key is handed to the host as its escape sequence
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 800   |
		| Height   | 600   |
	And the layout "cell" holds a Terminal named "term" with:
		| Property         | Value  |
		| BackgroundColor  | Navy   |
		| ForegroundColor  | Yellow |
		| TerminalFontSize | 24     |
	When the frame is captured
	And the cursor of "term" is hidden
	And "term" is given focus
	And the frame is captured as "before the key"
	And the key "Up" is pressed
	And the frame is captured as "after the key"
	Then the InputEmitted of "term" was raised once
	And the input emitted by "term" is "ESC[A"
	And the region of "term" in frame "after the key" is unchanged from frame "before the key"

Scenario: A finger dragged along a row selects it and tints what it covers
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 800   |
		| Height   | 600   |
	And the layout "cell" holds a Terminal named "term" with:
		| Property         | Value   |
		| BackgroundColor  | Navy    |
		| ForegroundColor  | Yellow  |
		| SelectionColor   | Magenta |
		| TerminalFontSize | 24      |
	When the frame is captured
	And the cursor of "term" is hidden
	And the script "SampleGlyphs" is fed to "term"
	And the frame is captured as "before the drag"
	Then row 0 of "term" does not contain "Magenta"
	When a finger drags from cell 0 to cell 10 along row 0 of "term"
	And the frame is captured as "with the selection"
	Then row 0 of "term" contains at least 5 percent "Magenta"
	And cell 0, 0 of "term" is uniformly "Magenta"
	And cell 9, 0 of "term" is uniformly "Magenta"
	And cell 10, 0 of "term" is uniformly "Navy"
	And row 1 of "term" does not contain "Magenta"

Scenario: The clipboard chord copies what the finger selected
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 800   |
		| Height   | 600   |
	And the layout "cell" holds a Terminal named "term" with:
		| Property         | Value   |
		| BackgroundColor  | Navy    |
		| ForegroundColor  | Yellow  |
		| SelectionColor   | Magenta |
		| TerminalFontSize | 24      |
	When the frame is captured
	And the cursor of "term" is hidden
	And the script "SampleGlyphs" is fed to "term"
	And the frame is captured
	And a finger drags from cell 0 to cell 10 along row 0 of "term"
	And the key "C" is pressed with the Control and Shift keys held down
	And the frame is captured as "after the chord"
	Then the CopyRequested of "term" was raised at least once
	And the text copied from "term" is "MMMMMMMMMM"
	And the InputEmitted of "term" was never raised
	And row 0 of "term" contains at least 5 percent "Magenta"

Scenario: A session that renames itself says so and leaves the screen alone
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 800   |
		| Height   | 600   |
	And the layout "cell" holds a Terminal named "term" with:
		| Property         | Value  |
		| BackgroundColor  | Navy   |
		| ForegroundColor  | Yellow |
		| TerminalFontSize | 24     |
	When the frame is captured
	And the cursor of "term" is hidden
	And the script "Banner" is fed to "term"
	And the frame is captured as "before the title"
	Then the TitleChanged of "term" was never raised
	When the script "OscTitle" is fed to "term"
	And the frame is captured as "after the title"
	Then the TitleChanged of "term" was raised at least once
	And the title reported by "term" is "Session"
	And the region of "term" in frame "after the title" is unchanged from frame "before the title"

Scenario: The scrollback chords page back through the history and forward again
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 800   |
		| Height   | 600   |
	And the layout "cell" holds a Terminal named "term" with:
		| Property         | Value  |
		| BackgroundColor  | Navy   |
		| ForegroundColor  | Yellow |
		| TerminalFontSize | 24     |
	When the frame is captured
	And the cursor of "term" is hidden
	And the frame is captured
	Then the scroll bar of "term" is hidden
	When the script "LongOutput" is fed to "term"
	And "term" is given focus
	And the frame is captured as "the live tail"
	Then the scroll bar of "term" is showing
	When the key "PageUp" is pressed with the Shift key held down
	And the frame is captured as "the history"
	Then the region of "term" in frame "the history" differs from frame "the live tail"
	When the key "PageDown" is pressed with the Shift key held down
	And the frame is captured as "back at the tail"
	Then the region of "term" in frame "back at the tail" is unchanged from frame "the live tail"
	And the InputEmitted of "term" was never raised
