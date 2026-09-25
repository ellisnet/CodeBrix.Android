Feature: ScrollBar
	A ScrollBar shows nothing until something asks it to show its indicator; once it does, it
	draws a thumb, and the thumb sits where the Value says it should.

Scenario: A ScrollBar with no indicator to show draws nothing
	Given the application shows a ScrollBar named "bar" with:
		| Property     | Value |
		| Width        | 20    |
		| Height       | 400   |
		| Maximum      | 100   |
		| ViewportSize | 20    |
	When the frame is captured
	Then the region of "bar" is blank

Scenario: A ScrollBar showing its indicator draws a thumb
	Given the application shows a ScrollBar named "bar" with:
		| Property      | Value          |
		| Width         | 20             |
		| Height        | 400            |
		| Maximum       | 100            |
		| ViewportSize  | 20             |
		| IndicatorMode | MouseIndicator |
	When the ScrollBar thumb of "bar" is captured as "top"
	Then the ScrollBar thumb of "bar" had ink in "top"
	And the region of "bar" has ink

Scenario: Raising a ScrollBar's Value moves its thumb down
	Given the application shows a ScrollBar named "bar" with:
		| Property      | Value          |
		| Width         | 20             |
		| Height        | 400            |
		| Maximum       | 100            |
		| ViewportSize  | 20             |
		| IndicatorMode | MouseIndicator |
	When the ScrollBar thumb of "bar" is captured as "top"
	And the Value of "bar" is set to "80"
	And the ScrollBar thumb of "bar" is captured as "bottom"
	Then the Value of "bar" is 80
	And the ScrollBar thumb of "bar" had ink in "bottom"
	And the ScrollBar thumb of "bar" moved down from "top" to "bottom" by at least 100 pixels
	And the region of "bar" in frame "bottom" differs from frame "top"
