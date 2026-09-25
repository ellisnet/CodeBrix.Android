Feature: ScrollViewer
	A ScrollViewer shows one viewport of something taller than itself. A finger that presses its
	content and drags upwards takes the content with it: the offset it reports grows and the
	blocks that were at the top are further up the panel than they were.

Scenario: A ScrollViewer holds more than it can show and starts at the top
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding 10 blocks 90 tall, the first named "topBlock"
	When the frame is captured
	Then the ScrollViewer "scroller" holds more than it can show
	And the VerticalOffset of "scroller" is 0
	And the region of "topBlock" is uniformly "Red"

Scenario: Dragging a ScrollViewer's content upwards scrolls it
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding 10 blocks 90 tall, the first named "topBlock"
	When the block "topBlock" of the ScrollViewer "scroller" is captured as "before"
	And the ScrollViewer "scroller" is dragged 150 pixels up
	And the block "topBlock" of the ScrollViewer "scroller" is captured as "after"
	Then the VerticalOffset of "scroller" is more than 100
	And the content of the ScrollViewer "scroller" moved up from "before" to "after" by at least 100 pixels
	And the ScrollViewer "scroller" shows something different in "after" than in "before"

Scenario: A ScrollViewer that has been scrolled to an offset shows the content shifted by it
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding 10 blocks 90 tall, the first named "topBlock"
	When the block "topBlock" of the ScrollViewer "scroller" is captured as "top"
	And the ScrollViewer "scroller" is scrolled to 180
	And the block "topBlock" of the ScrollViewer "scroller" is captured as "scrolled"
	Then the VerticalOffset of "scroller" is 180
	And the content of the ScrollViewer "scroller" moved up from "top" to "scrolled" by at least 175 pixels

Scenario: A ScrollViewer never scrolls above its own top
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding 10 blocks 90 tall, the first named "topBlock"
	When the ScrollViewer "scroller" is scrolled to 200
	And the ScrollViewer "scroller" is dragged 260 pixels down
	And the frame is captured
	Then the VerticalOffset of "scroller" is 0
	And the region of "topBlock" is uniformly "Red"

Scenario: A ScrollViewer that is told not to scroll ignores a finger
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding 10 blocks 90 tall, the first named "topBlock"
	And the VerticalScrollBarVisibility of "scroller" is set to "Disabled"
	When the frame is captured as "before"
	And the ScrollViewer "scroller" is dragged 150 pixels up
	And the frame is captured as "after"
	Then the VerticalOffset of "scroller" is 0
	And the region of "scroller" in frame "after" is unchanged from frame "before"
