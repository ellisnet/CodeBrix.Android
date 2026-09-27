@needs-terminalview
Feature: Terminal painting
	A Terminal must paint the whole of the cell it is laid out in with its own background colour,
	fit as many whole character cells into that space as will go and say so, draw the output it is
	fed in its own foreground colour, honour the colour and attribute sequences that output carries,
	clear itself when it is reset, and re-fit its grid when the font it draws with changes.

	Every scenario here hides the cursor before it looks at anything. A terminal has no property
	that stops it: a focused one blinks twice a second with no completion signal to wait on, and an
	unfocused one draws a steady outline over whichever cell the buffer is at. The escape sequence
	the scenarios feed is the same one a host sends before it draws something to be read rather than
	typed into.

	Deliberately out of scope here: hovering (this is a touch and keyboard panel), display scale
	(pinned at 1.0), reading the buffer back (a terminal deliberately does not expose one, so every
	claim about content is a claim about pixels), and the true-colour, mouse-reporting and IME
	sequences the engine documents as unsupported.

Scenario: A Terminal paints the whole of its cell in its own background colour
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
	Then "term" is 800 by 600 device pixels
	And "term" is inset 0 pixels inside "cell"
	And the region of "term" is uniformly "Navy"
	And the corner pixels of "term" are "Navy"

Scenario: Output fed to a Terminal appears in its foreground colour
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
	And the frame is captured as "before the output"
	Then row 0 of "term" is uniformly "Navy"
	When the script "Banner" is fed to "term"
	And the frame is captured as "with the output"
	Then cell 0, 0 of "term" contains at least 5 percent "Yellow"
	And row 0 of "term" contains at least 0.5 percent "Yellow"
	And row 1 of "term" is uniformly "Navy"

Scenario: The grid is as many whole cells as fit, and the Terminal says so
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
	Then the grid of "term" is 55 columns by 18 rows
	And the grid of "term" fits the space it was given
	And the GridResized of "term" was raised at least once
	And the last GridResized of "term" reported the grid it now has
	And the scroll bar of "term" is hidden

Scenario: A colour sequence overrides the Terminal's own foreground
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
	And the script "SgrRed" is fed to "term"
	And the frame is captured
	Then cell 0, 0 of "term" contains at least 5 percent "PaletteRed"
	And row 0 of "term" does not contain "Yellow"
	And row 1 of "term" is uniformly "Navy"

Scenario: An inverse run fills its cells with the foreground colour
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
	And the script "Inverse" is fed to "term"
	And the frame is captured
	Then cell 0, 0 of "term" is uniformly "Yellow"
	And cell 3, 0 of "term" is uniformly "Yellow"
	And cell 4, 0 of "term" is uniformly "Navy"
	And row 1 of "term" is uniformly "Navy"

Scenario: A bold run is drawn in the bright twin of its colour
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
	And the script "Attributes" is fed to "term"
	And the frame is captured
	Then cell 0, 0 of "term" contains at least 5 percent "PaletteRed"
	And row 0 of "term" does not contain "PaletteBrightRed"
	And cell 0, 1 of "term" contains at least 5 percent "PaletteBrightRed"
	And row 1 of "term" does not contain "PaletteRed"

Scenario: Resetting a Terminal clears the screen
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
	And the script "SampleGlyphs" is fed to "term"
	And the frame is captured as "with the output"
	Then row 0 of "term" contains at least 1 percent "Yellow"
	When the terminal "term" is reset
	And the frame is captured as "after the reset"
	Then the region of "term" is uniformly "Navy"
	And row 0 of "term" does not contain "Yellow"
	And cell 0, 0 of "term" is uniformly "Navy"

Scenario: A larger font gives the Terminal a smaller grid
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 800   |
		| Height   | 600   |
	And the layout "cell" holds a Terminal named "term" with:
		| Property        | Value  |
		| BackgroundColor | Navy   |
		| ForegroundColor | Yellow |
	When the frame is captured
	And the cursor of "term" is hidden
	And the script "Banner" is fed to "term"
	And the frame is captured as "at the default font size"
	And the grid of "term" is remembered as "at the default font size"
	And the recorded events are forgotten
	And the TerminalFontSize of "term" is set to "28"
	And the frame is captured as "at twice the font size"
	Then the grid of "term" is smaller than in "at the default font size"
	And the grid of "term" fits the space it was given
	And the GridResized of "term" was raised at least once
	And the last GridResized of "term" reported the grid it now has
	And cell 0, 0 of "term" contains at least 5 percent "Yellow"
