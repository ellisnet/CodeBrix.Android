@needs-textlayout
Feature: TextLayout paints what it measured
	The layout engine has no element of its own: it hands back shaped, measured text and paints it
	onto a Skia canvas. What it paints must be what it says it measured - the line breaks it
	reports, the alignment it was asked for, the colour each run carries, and a size that grows
	with the weight and the point size.
	Out of scope here, deliberately: hover (this is a touch and keyboard panel), display scale
	(pinned at 1.0), and Arabic, which the application's font does not carry - the right-to-left
	requirement is written in Hebrew, which it does carry, so nothing falls back to another face.
	The geometry the engine reports with no canvas at all is the add-in's own host-free suite's
	business; these scenarios are about the pixels.

Scenario: Laid-out text paints onto the canvas in the colour it is drawn with
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the frame is captured
	Then the region of "canvas" is blank
	When the layout origin is 20, 20
	And the text "Requirement" is laid out with:
		| Setting  | Value |
		| FontSize | 64    |
	And the layout is painted as Draw
	And the frame is captured
	Then the layout has 1 line
	And the region of "canvas" has ink
	And the ink color of "canvas" is "Black"
	And the ink of "canvas" lies inside the layout box
	And the ink width of "canvas" agrees with the layout width within 8 pixels

Scenario: A width makes the text wrap onto more than one line
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the text "The engine measures and paints every word of this line" is laid out with:
		| Setting  | Value |
		| FontSize | 40    |
		| MaxWidth | 300   |
	And the layout is painted as Draw
	And the frame is captured
	Then the layout has at least 2 lines
	And the region of "canvas" has ink
	And the ink of "canvas" is at least 2.0 line heights tall
	And the ink of "canvas" lies inside the layout box

Scenario: No width means no wrapping
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the text "The engine measures and paints every word of this line" is laid out with:
		| Setting  | Value |
		| FontSize | 44    |
	And the layout is painted as Draw
	And the frame is captured
	Then the layout has 1 line
	And the region of "canvas" has ink
	And the rightmost 30 pixels of "canvas" have ink

Scenario: Centre alignment moves the ink into the middle of the layout box
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the text "Requirement" is laid out with:
		| Setting   | Value |
		| FontSize  | 64    |
		| MaxWidth  | 900   |
		| Alignment | Left  |
	And the layout is painted as Draw
	And the frame is captured as "left"
	Then the ink of "canvas" sits at the left of its block
	When the text "Requirement" is laid out with:
		| Setting   | Value  |
		| FontSize  | 64     |
		| MaxWidth  | 900    |
		| Alignment | Center |
	And the frame is captured as "centred"
	Then the ink of "canvas" sits at the centre of its block
	And the ink of "canvas" in frame "centred" starts further right than in frame "left"

Scenario: Right alignment pushes the ink to the right edge and leaves nothing of the last painting
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the text "Requirement" is laid out with:
		| Setting   | Value |
		| FontSize  | 64    |
		| MaxWidth  | 900   |
		| Alignment | Left  |
	And the layout is painted as Draw
	And the frame is captured as "left"
	Then the ink of "canvas" sits at the left of its block
	When the text "Requirement" is laid out with:
		| Setting   | Value |
		| FontSize  | 64    |
		| MaxWidth  | 900   |
		| Alignment | Right |
	And the frame is captured as "right"
	Then the ink of "canvas" sits at the right of its block
	And the leftmost 300 pixels of "canvas" are blank
	And the ink of "canvas" in frame "right" starts further right than in frame "left"

Scenario: An explicit line break makes a second line below the first
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the layout origin is 0, 40
	And the text "First\nSecond" is laid out with:
		| Setting  | Value |
		| FontSize | 56    |
	And the layout is painted as Draw
	And the frame is captured
	Then the layout has 2 lines
	And line 1 of the layout sits below line 0
	And the topmost 30 pixels of "canvas" are blank
	And the region of "canvas" covering text 0 to 5 has ink
	And the region of "canvas" covering text 6 to 12 has ink

Scenario: A run's colour paints only that run
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the layout origin is 20, 20
	And the runs are laid out at size 64 with:
		| Text  | Colour |
		| Left  | Red    |
		| Right | Blue   |
	And the layout is painted as Draw
	And the frame is captured
	Then the region of "canvas" has ink
	And the region of "canvas" covering text 0 to 4 contains "Red"
	And the region of "canvas" covering text 0 to 4 does not contain "Blue"
	And the region of "canvas" covering text 4 to 9 contains "Blue"
	And the region of "canvas" covering text 4 to 9 does not contain "Red"

Scenario: Bold measures wider and paints wider
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the layout origin is 20, 20
	And the text "Requirement" is laid out with:
		| Setting  | Value  |
		| FontSize | 64     |
		| Weight   | Normal |
	And the layout is painted as Draw
	And the layout size is remembered as "regular"
	And the frame is captured as "regular"
	Then the region of "canvas" has ink
	When the text "Requirement" is laid out with:
		| Setting  | Value |
		| FontSize | 64    |
		| Weight   | Bold  |
	And the frame is captured as "bold"
	Then the layout is wider than the layout remembered as "regular"
	And the ink of "canvas" in frame "bold" is wider than in frame "regular"

Scenario: A larger font size measures and paints taller
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the layout origin is 20, 20
	And the text "Requirement" is laid out with:
		| Setting  | Value |
		| FontSize | 32    |
	And the layout is painted as Draw
	And the layout size is remembered as "small"
	And the frame is captured as "small"
	Then the region of "canvas" has ink
	When the text "Requirement" is laid out with:
		| Setting  | Value |
		| FontSize | 64    |
	And the frame is captured as "large"
	Then the layout is at least 1.9 times as tall as the layout remembered as "small"
	And the ink of "canvas" in frame "large" is at least 1.7 times as tall as in frame "small"

Scenario: A right-to-left base direction lays the text out from the right
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the layout origin is 20, 20
	And the text "world only" is laid out with:
		| Setting  | Value |
		| FontSize | 64    |
		| MaxWidth | 700   |
	And the layout is painted as Draw
	And the frame is captured
	Then the region of "canvas" covering text 6 to 10 sits right of the text 0 to 5
	When the text "שלום world" is laid out with:
		| Setting  | Value |
		| FontSize | 64    |
		| MaxWidth | 700   |
	And the frame is captured
	Then the layout reads right to left
	And the region of "canvas" has ink
	And the region of "canvas" covering text 0 to 4 has ink
	And the region of "canvas" covering text 5 to 10 has ink
	And the region of "canvas" covering text 0 to 4 sits right of the text 5 to 10
