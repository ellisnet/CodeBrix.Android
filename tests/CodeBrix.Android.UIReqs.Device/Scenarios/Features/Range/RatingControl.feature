Feature: RatingControl
	A RatingControl draws a row of stars and takes a rating from a finger: tapping the third
	star means three.

Scenario: A RatingControl draws its stars
	Given the application shows a RatingControl named "rate"
	When the frame is captured
	Then the region of "rate" has ink
	And the region of "RatingBackgroundStackPanel" has ink
	And the Value of "rate" is -1

Scenario: Tapping the third star of a RatingControl sets its Value to three
	Given the application shows a RatingControl named "rate"
	When star 3 of the RatingControl "rate" is tapped
	Then the Value of "rate" is 3
	And the ValueChanged of "rate" was raised at least 1 times

Scenario: Tapping the first star of a RatingControl sets its Value to one
	Given the application shows a RatingControl named "rate"
	When star 1 of the RatingControl "rate" is tapped
	Then the Value of "rate" is 1

Scenario: A rated RatingControl does not look like an unrated one
	Given the application shows a RatingControl named "rate"
	When the frame is captured as "unrated"
	And star 5 of the RatingControl "rate" is tapped
	And the frame is captured as "rated"
	Then the Value of "rate" is 5
	And the region of "rate" in frame "rated" differs from frame "unrated"
