@needs-svg
Feature: An SVG drawn in an Image
	An Image whose Source is an SvgImageSource draws a vector document instead of a bitmap. The
	document is redrawn at whatever size the Image is arranged to, so its colours land where the
	document put them at every size, and enlarging the Image gives a bigger picture rather than a
	bigger version of a small one. An ImageIcon shows the same artwork through an Image of its own.
	The documents here are two flat colours on whole-pixel boundaries, made at run time, so where
	each colour ends up on the panel says exactly what the Image did with the picture.
	Deliberately out of scope: hover (the panel is touch and keyboard), display scale (pinned at
	1.0) and any document that would have to be fetched over a network.

Scenario: An SVG loaded from a stream appears in the Image
	Given the application shows an SvgImage named "pic" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 200   |
		| Stretch  | Fill  |
	When the SVG "TwoHalves" of "pic" is loaded
	And the frame is captured
	Then the SVG of "pic" loaded with status "Success"
	And "pic" is 400 by 200 device pixels
	And the region of "pic" has ink

Scenario: The SVG's colours land where the document put them
	Given the application shows an SvgImage named "pic" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 200   |
		| Stretch  | Fill  |
	And the SVG "TwoHalves" of "pic" is loaded
	When the frame is captured
	Then the leftmost 190 pixels of "pic" are uniformly "Red"
	And the rightmost 190 pixels of "pic" are uniformly "Blue"
	And the region of "pic" does not contain "White"

Scenario: An SVG is drawn at the size its Image was arranged to, whatever that size is
	Given the application shows a StackPanel named "board" with:
		| Property    | Value      |
		| Width       | 700        |
		| Height      | 200        |
		| Orientation | Horizontal |
	And the layout "board" holds a SvgImage named "big" with:
		| Property          | Value |
		| Width             | 400   |
		| Height            | 200   |
		| Stretch           | Fill  |
		| VerticalAlignment | Top   |
	And the layout "board" holds a SvgImage named "small" with:
		| Property          | Value |
		| Width             | 100   |
		| Height            | 50    |
		| Stretch           | Fill  |
		| VerticalAlignment | Top   |
	And the SVG "TwoHalves" of "big" is loaded
	And the SVG "TwoHalves" of "small" is loaded
	When the frame is captured
	Then "big" is 400 by 200 device pixels
	And the leftmost 190 pixels of "big" are uniformly "Red"
	And the rightmost 190 pixels of "big" are uniformly "Blue"
	And "small" is 100 by 50 device pixels
	And the leftmost 45 pixels of "small" are uniformly "Red"
	And the rightmost 45 pixels of "small" are uniformly "Blue"

Scenario: Uniform keeps the SVG's shape where Fill squashes it
	Given the application shows an SvgImage named "pic" with:
		| Property | Value   |
		| Width    | 400     |
		| Height   | 300     |
		| Stretch  | Uniform |
	And the SVG "TwoHalves" of "pic" is loaded
	When the frame is captured as "uniform"
	Then the picture inside "pic" is 400 by 200 pixels
	When the Stretch of "pic" is set to "Fill"
	And the frame is captured as "filled"
	Then the picture inside "pic" is 400 by 300 pixels
	And the leftmost 190 pixels of "pic" are uniformly "Red"
	And the rightmost 190 pixels of "pic" are uniformly "Blue"

Scenario: Enlarging the Image redraws the SVG rather than stretching a picture of it
	Given the application shows an SvgImage named "pic" with:
		| Property | Value |
		| Width    | 200   |
		| Height   | 100   |
		| Stretch  | Fill  |
	And the SVG "TwoHalves" of "pic" is loaded
	When the frame is captured as "small"
	Then the leftmost 90 pixels of "pic" are uniformly "Red"
	When the Width of "pic" is set to "800"
	And the Height of "pic" is set to "400"
	And the frame is captured as "large"
	Then "pic" is 800 by 400 device pixels
	And the leftmost 380 pixels of "pic" are uniformly "Red"
	And the rightmost 380 pixels of "pic" are uniformly "Blue"
	And the region of "pic" does not contain "Purple"

Scenario: Loading a second document swaps the picture
	Given the application shows an SvgImage named "pic" with:
		| Property | Value |
		| Width    | 200   |
		| Height   | 200   |
		| Stretch  | Fill  |
	And the SVG "TwoHalves" of "pic" is loaded
	When the frame is captured as "first"
	Then the left half of the region of "pic" is uniformly "Red"
	When the SVG "TallGreen" of "pic" is loaded
	And the frame is captured as "second"
	Then the SVG of "pic" loaded with status "Success"
	And the top half of the region of "pic" is uniformly "Green"
	And the region of "pic" does not contain "Red"
	And the region of "pic" in frame "second" differs from frame "first"

Scenario: Clearing the Source empties the Image and leaves the rest of the panel alone
	Given the application shows an SvgImage named "pic" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 200   |
		| Stretch  | Fill  |
	And the SVG "TwoHalves" of "pic" is loaded
	When the frame is captured as "showing"
	Then the region of "pic" has ink
	When the Source of "pic" is set to "None"
	And the frame is captured as "cleared"
	Then the region of "pic" is blank
	And nothing outside "pic" changed between frames "cleared" and "showing"

Scenario: An ImageIcon shows SVG artwork
	Given the application shows an SvgImageIcon named "icon" with:
		| Property | Value |
		| Width    | 120   |
		| Height   | 120   |
	And the SVG "TwoHalves" of "icon" is loaded
	When the frame is captured
	Then the SVG of "icon" loaded with status "Success"
	And the ImageIcon "icon" draws its SVG through an Image
	And the region of "icon" has ink
	And the region of "icon" contains "Red"
	And the region of "icon" contains "Blue"
