Feature: TextBlock
	A TextBlock must draw the text it holds, in the colour, size and weight it was given,
	against the side of its own block its TextAlignment names; it must grow taller when its
	text wraps, and let each Run inside it carry its own Foreground.

Scenario: Changing the Text of a TextBlock redraws it
	Given the application shows a TextBlock named "label" with:
		| Property      | Value |
		| Width         | 700   |
		| Height        | 80    |
		| FontSize      | 40    |
		| Foreground    | Blue  |
		| TextAlignment | Left  |
		| Text          | Short |
	When the frame is captured as "short"
	Then the Text of "label" is "Short"
	And the region of "label" has ink
	When the Text of "label" is set to "A much longer requirement"
	And the frame is captured as "long"
	Then the Text of "label" is "A much longer requirement"
	And the region of "label" in frame "long" differs from frame "short"
	And the region of "label" in frame "long" holds more ink than in frame "short"

Scenario: A TextBlock draws every glyph in its Foreground colour
	Given the application shows a TextBlock named "label" with:
		| Property   | Value       |
		| FontSize   | 48          |
		| Foreground | Green       |
		| Text       | Requirement |
	When the frame is captured
	Then the region of "label" has ink
	And the ink color of "label" is "Green"
	And the region of "label" does not contain "Blue"

Scenario: An empty TextBlock draws nothing
	Given the application shows a TextBlock named "label" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 80    |
		| FontSize   | 40    |
		| Foreground | Blue  |
	When the frame is captured
	Then the Text of "label" is ""
	And the region of "label" is blank

Scenario: A bigger FontSize draws taller ink
	Given the application shows a TextBlock named "label" with:
		| Property      | Value       |
		| Width         | 700         |
		| Height        | 120         |
		| FontSize      | 32          |
		| Foreground    | Blue        |
		| TextAlignment | Left        |
		| Text          | Requirement |
	When the frame is captured as "small"
	Then the region of "label" has ink
	When the FontSize of "label" is set to "72"
	And the frame is captured as "large"
	Then the ink of "label" in frame "large" is taller than in frame "small"
	And the region of "label" in frame "large" holds more ink than in frame "small"

Scenario: A bolder FontWeight draws more ink at the same size
	Given the application shows a TextBlock named "label" with:
		| Property      | Value       |
		| Width         | 700         |
		| Height        | 120         |
		| FontSize      | 56          |
		| Foreground    | Blue        |
		| TextAlignment | Left        |
		| FontWeight    | Normal      |
		| Text          | Requirement |
	When the frame is captured as "normal"
	Then the region of "label" has ink
	When the FontWeight of "label" is set to "Bold"
	And the frame is captured as "bold"
	Then the region of "label" in frame "bold" holds more ink than in frame "normal"
	And the region of "label" in frame "bold" differs from frame "normal"

Scenario: TextAlignment Left puts the ink at the left of the block
	Given the application shows a TextBlock named "label" with:
		| Property      | Value   |
		| Width         | 700     |
		| Height        | 80      |
		| FontSize      | 40      |
		| Foreground    | Blue    |
		| TextAlignment | Left    |
		| Text          | Aligned |
	When the frame is captured
	Then the ink of "label" sits at the left of its block

Scenario: TextAlignment Center puts the ink in the middle of the block
	Given the application shows a TextBlock named "label" with:
		| Property      | Value   |
		| Width         | 700     |
		| Height        | 80      |
		| FontSize      | 40      |
		| Foreground    | Blue    |
		| TextAlignment | Center  |
		| Text          | Aligned |
	When the frame is captured
	Then the ink of "label" sits at the centre of its block

Scenario: TextAlignment Right puts the ink at the right of the block
	Given the application shows a TextBlock named "label" with:
		| Property      | Value   |
		| Width         | 700     |
		| Height        | 80      |
		| FontSize      | 40      |
		| Foreground    | Blue    |
		| TextAlignment | Right   |
		| Text          | Aligned |
	When the frame is captured
	Then the ink of "label" sits at the right of its block

Scenario: Changing the TextAlignment moves the ink without changing the Text
	Given the application shows a TextBlock named "label" with:
		| Property      | Value   |
		| Width         | 700     |
		| Height        | 80      |
		| FontSize      | 40      |
		| Foreground    | Blue    |
		| TextAlignment | Left    |
		| Text          | Aligned |
	When the frame is captured as "left"
	Then the ink of "label" sits at the left of its block
	When the TextAlignment of "label" is set to "Right"
	And the frame is captured as "right"
	Then the ink of "label" sits at the right of its block
	And the Text of "label" is "Aligned"
	And the region of "label" in frame "right" differs from frame "left"

Scenario: TextWrapping makes a long line grow taller
	Given the application shows a TextBlock named "label" with:
		| Property     | Value                                                     |
		| Width        | 300                                                       |
		| FontSize     | 32                                                        |
		| Foreground   | Blue                                                      |
		| TextWrapping | NoWrap                                                    |
		| Text         | A requirement long enough that it cannot fit on one line   |
	When the frame is captured as "one line"
	Then the region of "label" has ink
	When the TextWrapping of "label" is set to "Wrap"
	And the frame is captured as "wrapped"
	Then the ink of "label" in frame "wrapped" is taller than in frame "one line"
	And the region of "label" in frame "wrapped" holds more ink than in frame "one line"

Scenario: Each Run carries its own Foreground
	Given the application shows a TextBlock named "label" with:
		| Property      | Value |
		| Width         | 700   |
		| Height        | 80    |
		| FontSize      | 48    |
		| TextAlignment | Left  |
	And the TextBlock "label" has runs:
		| Text  | Foreground |
		| Alpha | Red        |
		| Bravo | Blue       |
	When the frame is captured as "two colours"
	Then the region of "label" contains at least 0.4 percent "Red"
	And the region of "label" contains at least 0.4 percent "Blue"
	When the TextBlock "label" has runs:
		| Text  | Foreground |
		| Alpha | Red        |
		| Bravo | Red        |
	And the frame is captured as "one colour"
	Then the region of "label" contains at least 0.8 percent "Red"
	And the region of "label" does not contain "Blue"
	And the ink color of "label" is "Red"
