Feature: TextBlock ink
	Text must actually be drawn. The application carries its own fonts, so a label must put ink
	of its Foreground colour inside its own rectangle; and changing that colour must repaint the
	same ink in the new colour without moving it.

Scenario: A TextBlock draws ink in its Foreground colour
	Given the application shows a TextBlock named "title" with:
		| Property   | Value       |
		| Text       | Requirement |
		| FontSize   | 48          |
		| Foreground | Blue        |
	When the frame is captured
	Then the Text of "title" is "Requirement"
	And the region of "title" has ink
	And the ink color of "title" is "Blue"

Scenario: Changing the Foreground repaints the label in the new colour
	Given the application shows a TextBlock named "title" with:
		| Property   | Value       |
		| Text       | Requirement |
		| FontSize   | 48          |
		| Foreground | Blue        |
	When the frame is captured as "A"
	Then the ink color of "title" in frame "A" is "Blue"
	When the Foreground of "title" is set to "Red"
	And the frame is captured as "B"
	Then the ink color of "title" in frame "B" is "Red"
	And the region of "title" in frame "B" does not contain "Blue"
	And the ink bounds of "title" in frames "A" and "B" agree within 1 pixel
