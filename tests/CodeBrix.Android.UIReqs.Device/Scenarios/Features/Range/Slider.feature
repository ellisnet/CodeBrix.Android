Feature: Slider
	A Slider is a value a finger can set: the filled part of its track is as long as the value
	says, a tap moves the value to where the finger landed, and a finger that presses the thumb
	and drags it takes the value with it.

Scenario: A Slider draws its track, its filled part and its thumb
	Given the application shows a Slider named "level" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 50    |
	When the frame is captured
	Then the region of "HorizontalTrackRect" has ink
	And the region of "HorizontalDecreaseRect" is uniformly "Accent"
	And the region of "HorizontalThumb" has ink

Scenario: A Slider's filled part is as long as its Value says
	Given the application shows a Slider named "level" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 25    |
	Then the Slider "level" fills 25 percent of its track, within 2 pixels
	When the Value of "level" is set to "75"
	Then the Slider "level" fills 75 percent of its track, within 2 pixels

Scenario: Tapping a Slider near the right of its track raises its Value
	Given the application shows a Slider named "level" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 0     |
	When the Slider "level" is tapped at 75 percent of its track
	Then the Value of "level" is more than 60
	And the Value of "level" is less than 90
	And the ValueChanged of "level" was raised at least 1 times

Scenario: Tapping a Slider near the left of its track lowers its Value
	Given the application shows a Slider named "level" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 100   |
	When the Slider "level" is tapped at 20 percent of its track
	Then the Value of "level" is less than 40
	And the Value of "level" is more than 5

Scenario: Dragging a Slider's thumb to the right raises its Value and moves the thumb
	Given the application shows a Slider named "level" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 20    |
	When the Slider thumb of "level" is captured as "before"
	And the Slider thumb of "level" is dragged 100 pixels to the right
	And the Slider thumb of "level" is captured as "after"
	Then the Value of "level" is more than 40
	And the Value of "level" is less than 55
	And the Slider thumb of "level" had ink in "before"
	And the Slider thumb of "level" had ink in "after"
	And the Slider thumb of "level" moved right from "before" to "after" by at least 60 pixels
	And where the Slider thumb of "level" was in "before" looks different in "after"

Scenario: A disabled Slider ignores a tap on its track
	Given the application shows a Slider named "level" with:
		| Property  | Value |
		| Width     | 400   |
		| Value     | 10    |
		| IsEnabled | False |
	When the frame is captured as "before"
	And the Slider "level" is tapped at 80 percent of its track
	And the frame is captured as "after"
	Then the Value of "level" is 10
	And the region of "level" in frame "after" is unchanged from frame "before"
