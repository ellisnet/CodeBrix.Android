Feature: RepeatButton
	A RepeatButton is the one control here that measures time: it acts the moment the finger
	lands, and a finger that stays down keeps it acting at its own Interval - but not until its
	Delay has passed, so a finger that comes and goes before then acts exactly once.

Scenario: A finger held on a RepeatButton makes it repeat
	Given the application shows a RepeatButton named "more" with:
		| Property | Value |
		| Content  | More  |
		| Width    | 240   |
		| Height   | 80    |
		| Delay    | 100   |
		| Interval | 100   |
	When a finger is held on "more" for 600 milliseconds
	Then the Click of "more" was raised at least 4 times

Scenario: A RepeatButton does not repeat until its Delay has passed
	Given the application shows a RepeatButton named "more" with:
		| Property | Value |
		| Content  | More  |
		| Width    | 240   |
		| Height   | 80    |
		| Delay    | 900   |
		| Interval | 100   |
	When a finger is held on "more" for 250 milliseconds
	Then the Click of "more" was raised once

Scenario: A disabled RepeatButton never repeats
	Given the application shows a RepeatButton named "more" with:
		| Property  | Value |
		| Content   | More  |
		| Width     | 240   |
		| Height    | 80    |
		| Delay     | 100   |
		| Interval  | 100   |
		| IsEnabled | False |
	When a finger is held on "more" for 600 milliseconds
	Then the Click of "more" was not raised
