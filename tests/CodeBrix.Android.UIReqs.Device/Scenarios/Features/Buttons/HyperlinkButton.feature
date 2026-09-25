Feature: HyperlinkButton
	A HyperlinkButton is a button that looks like a piece of text: it draws its content, it
	raises Click when a finger taps it, and when it is disabled it does neither.

Scenario: A HyperlinkButton draws its content and raises Click when it is tapped
	Given the application shows a HyperlinkButton named "link" with:
		| Property | Value   |
		| Content  | Open me |
		| FontSize | 40      |
	When the frame is captured
	Then the region of "link" has ink
	And the Click of "link" was not raised
	When "link" is tapped
	Then the Click of "link" was raised once

Scenario: A disabled HyperlinkButton ignores a tap and does not look enabled
	Given the application shows a HyperlinkButton named "link" with:
		| Property | Value   |
		| Content  | Open me |
		| FontSize | 40      |
	When the frame is captured as "enabled"
	And the IsEnabled of "link" is set to "False"
	And the frame is captured as "disabled"
	Then the button "link" is disabled
	And the region of "link" in frame "disabled" differs from frame "enabled"
	When "link" is tapped
	Then the Click of "link" was not raised
