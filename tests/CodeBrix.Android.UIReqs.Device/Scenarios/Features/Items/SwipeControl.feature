Feature: SwipeControl
	A SwipeControl keeps commands beside its content, and a finger swiping the content aside
	uncovers them. A Reveal set stays uncovered once the swipe has gone past its threshold, however
	short or long the swipe was, until something closes it; only a flick back towards where the
	swipe started closes it on release. An Execute set invokes its item once and closes again.

Scenario: A short swipe past the threshold leaves a Reveal SwipeControl open
	Given the application shows a SwipeControl named "row" 600 by 80 painted "Silver" with a Reveal item named "reveal" painted "Lime"
	When the SwipeControl "row" is swiped 140 pixels to the right
	And the frame is captured
	Then the left edge of the SwipeControl "row" is uniformly "Lime"
	And the SwipeItem "reveal" was invoked 0 times

Scenario: A long swipe to the far edge leaves a Reveal SwipeControl open showing its item
	Given the application shows a SwipeControl named "row" 600 by 80 painted "Silver" with a Reveal item named "reveal" painted "Lime"
	When the SwipeControl "row" is swiped to its far edge
	And the frame is captured
	Then the left edge of the SwipeControl "row" is uniformly "Lime"
	And the SwipeItem "reveal" was invoked 0 times

Scenario: A swipe on an Execute SwipeControl invokes its item once and closes it
	Given the application shows a SwipeControl named "row" 600 by 80 painted "Silver" with an Execute item named "execute" painted "Blue"
	When the SwipeControl "row" is swiped 140 pixels to the right
	And the frame is captured
	Then the SwipeItem "execute" was invoked 1 time
	And the left edge of the SwipeControl "row" is uniformly "Silver"

Scenario: A flick back closes a Reveal SwipeControl on release
	Given the application shows a SwipeControl named "row" 600 by 80 painted "Silver" with a Reveal item named "reveal" painted "Lime"
	When the SwipeControl "row" is swiped 400 pixels to the right and flicked back 160 pixels
	And the frame is captured
	Then the left edge of the SwipeControl "row" is uniformly "Silver"
	And the SwipeItem "reveal" was invoked 0 times
