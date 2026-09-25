Feature: Theme
	The theme decides what every control under the root looks like. An application cannot change
	its own RequestedTheme once it is running, so a theme is asked for on the root element - and
	what a theme resolves is not a number written in a scenario but the brush each control's
	template ends up holding, which the panel is then checked against.

	The controls sit on a page of a known solid colour, because the brushes a Fluent theme hands
	a control are deliberately translucent: they are meant to be composited over the page behind
	them, and on a white panel the dark theme's white text would be invisible rather than wrong.

Scenario: Text is drawn in the light theme's colour until the root asks for another
	Given the application shows a Grid named "page" 600 by 400 with Background "Gray"
	And the layout "page" holds a TextBlock named "label" with:
		| Property | Value |
		| Text     | Theme |
		| FontSize | 48    |
	When the frame is captured
	Then the root is drawn in the "Light" theme
	And the application theme is "Light"
	And the Foreground colour of "label" is darker than "Gray"
	And the text of "label" is drawn in its own Foreground colour

Scenario: Asking the root for the dark theme repaints the text under it
	Given the application shows a Grid named "page" 600 by 400 with Background "Gray"
	And the layout "page" holds a TextBlock named "label" with:
		| Property            | Value |
		| Text                | Theme |
		| FontSize            | 48    |
		| HorizontalAlignment | Left  |
		| VerticalAlignment   | Top   |
	When the frame is captured as "light"
	And the root theme is set to "Dark"
	And the frame is captured as "dark"
	Then the root is drawn in the "Dark" theme
	And the application theme is "Dark"
	And the Foreground colour of "label" is lighter than "Gray"
	And the text of "label" is drawn in its own Foreground colour
	And the region of "label" in frame "dark" differs from frame "light"

Scenario: A control's own fill is resolved from the theme as well as its text
	Given the application shows a Grid named "page" 600 by 400 with Background "Gray"
	And the layout "page" holds a TextBox named "field" with:
		| Property | Value |
		| Width    | 300   |
		| Height   | 60    |
		| Text     | Hello |
	And the theme colours of "field" are remembered as "light"
	When the frame is captured as "light"
	And the root theme is set to "Dark"
	And the frame is captured as "dark"
	Then the theme colours of "field" changed from "light"
	And at least 90 percent of "field" is one flat colour
	And the region of "field" in frame "dark" differs from frame "light"

Scenario: Putting the root's theme back brings the light picture back
	Given the application shows a Grid named "page" 600 by 400 with Background "Gray"
	And the layout "page" holds a TextBlock named "label" with:
		| Property            | Value |
		| Text                | Theme |
		| FontSize            | 48    |
		| HorizontalAlignment | Left  |
		| VerticalAlignment   | Top   |
	When the frame is captured as "light"
	And the root theme is set to "Dark"
	And the frame is captured as "dark"
	And the root theme is set to "Light"
	And the frame is captured as "restored"
	Then the root is drawn in the "Light" theme
	And the application theme is "Light"
	And the region of "label" in frame "dark" differs from frame "light"
	And the region of "label" in frame "restored" is unchanged from frame "light"

Scenario: The theme reaches every control under the root, not only the one that asked
	Given the application shows a Grid named "page" 600 by 400 with Background "Gray"
	And the layout "page" holds a TextBlock named "label" with:
		| Property | Value |
		| Text     | Theme |
		| FontSize | 48    |
	When the root theme is set to "Dark"
	And the frame is captured
	Then the root is drawn in the "Dark" theme
	And "page" is drawn in the "Dark" theme
	And "label" is drawn in the "Dark" theme
	And the text of "label" is drawn in its own Foreground colour
