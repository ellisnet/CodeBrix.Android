Feature: Native scrolling
	Android-only (not a copy of a CodeBrix.Platform feature). A ScrollViewer scrolls NATIVELY on
	Android: a real finger dragging its content moves the native view, the offsets reach Core (the
	ScrollViewer reports them as its own), and Core sees the content where the native view shows it.

Scenario: A real finger dragging a ScrollViewer's content scrolls it natively and Core reports the offset
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding 10 blocks 90 tall, the first named "topBlock"
	When the block "topBlock" of the ScrollViewer "scroller" is captured as "before"
	And a real finger drags the ScrollViewer "scroller" 150 pixels up
	And the block "topBlock" of the ScrollViewer "scroller" is captured as "after"
	Then the VerticalOffset of "scroller" is more than 100
	And the native scroll position of "scroller" matches its VerticalOffset
	And the content of the ScrollViewer "scroller" moved up from "before" to "after" by at least 100 pixels

Scenario: A real finger cannot drag a ScrollViewer above its own top
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding 10 blocks 90 tall, the first named "topBlock"
	When a real finger drags the ScrollViewer "scroller" 200 pixels down
	And the frame is captured
	Then the VerticalOffset of "scroller" is 0
	And the region of "topBlock" is uniformly "Red"

Scenario: ChangeView scrolls the native view to the offset
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding 10 blocks 90 tall, the first named "topBlock"
	When the ScrollViewer "scroller" is scrolled to 180
	And the frame is captured
	Then the VerticalOffset of "scroller" is 180
	And the native scroll position of "scroller" matches its VerticalOffset

Scenario: A real mouse wheel notch over a ScrollViewer scrolls it
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding 10 blocks 90 tall, the first named "topBlock"
	When the real mouse wheel turns one notch down over "scroller"
	And the frame is captured
	Then the VerticalOffset of "scroller" is more than 10
	And the native scroll position of "scroller" matches its VerticalOffset

Scenario: A disabled ScrollViewer ignores a real finger
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding 10 blocks 90 tall, the first named "topBlock"
	And the VerticalScrollBarVisibility of "scroller" is set to "Disabled"
	When a real finger drags the ScrollViewer "scroller" 150 pixels up
	And the frame is captured
	Then the VerticalOffset of "scroller" is 0
	And the native scroll position of "scroller" matches its VerticalOffset
