@needs-textlayout
Feature: TextLayout geometry agrees with the painting
	Everything the engine hands a consumer to draw with - the one combined outline path, each
	glyph's own outline and origin, the rectangle behind a stretch of selected text, the character
	under a point - has to describe the very picture the engine itself paints. These scenarios put
	both on the same canvas and compare them: the geometry is read from the engine, and the place
	it is checked against is read from the ink, never from a number written into the feature file.
	Out of scope here, deliberately: hover, display scale (pinned at 1.0), and the cluster
	arithmetic of scripts the application's font does not carry - the add-in's own host-free suite
	covers that with no canvas at all.

Scenario: The combined outline lands where the drawn glyphs land
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the layout origin is 20, 20
	And the text "Requirement" is laid out with:
		| Setting  | Value |
		| FontSize | 64    |
	And the layout is painted as Draw
	And the frame is captured as "drawn"
	Then the region of "canvas" has ink
	When the layout is painted as Outline
	And the frame is captured as "outlined"
	Then the region of "canvas" has ink
	And the ink bounds of "canvas" in frames "drawn" and "outlined" agree within 2 pixels

Scenario: A glyph outline is placed by the origin the engine gives it
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the layout origin is 20, 20
	And the text "Requirement" is laid out with:
		| Setting  | Value |
		| FontSize | 64    |
	And the layout is painted as FirstOutline
	And the frame is captured
	Then glyph 0 of the layout begins at the left edge of the layout
	And the region of "canvas" has ink
	And the ink of "canvas" lies inside the rectangle of text index 0
	And the region of "canvas" covering text 1 to 11 is blank

Scenario: A space has no outline of its own but still advances the pen
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the layout origin is 20, 20
	And the text "AB CD" is laid out with:
		| Setting  | Value |
		| FontSize | 96    |
	And the layout is painted as AllOutlines
	And the frame is captured
	Then glyph 2 of the layout has no outline
	And glyph 2 of the layout advances more than 5 pixels
	And the region of "canvas" has ink
	And the region of "canvas" covering text 0 to 2 has ink
	And the region of "canvas" covering text 2 to 3 is blank
	And the region of "canvas" covering text 3 to 5 has ink

Scenario: Selection rectangles cover exactly the text they were asked for
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the layout origin is 20, 20
	And the text "Requirement" is laid out with:
		| Setting  | Value |
		| FontSize | 64    |
	And the selection covers text 0 to 3
	And the layout is painted as Selection
	And the frame is captured
	Then the region of "canvas" has ink
	And the region of "canvas" covering text 0 to 3 contains "Yellow"
	And the region of "canvas" covering text 3 to 11 does not contain "Yellow"
	And the region of "canvas" covering text 3 to 11 has ink

Scenario: Hit-testing agrees with the painted ink
	Given the text engine is warm
	And the application shows a TextLayoutSurface named "canvas" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	When the layout origin is 20, 20
	And the text "Requirement" is laid out with:
		| Setting  | Value |
		| FontSize | 64    |
	And the layout is painted as Draw
	And the frame is captured
	Then the region of "canvas" has ink
	And the ink of "canvas" starts at the first character of the layout
	And a point past the ink of "canvas" is outside the text
	And the nearest index to a point past the ink of "canvas" is the end of the text
