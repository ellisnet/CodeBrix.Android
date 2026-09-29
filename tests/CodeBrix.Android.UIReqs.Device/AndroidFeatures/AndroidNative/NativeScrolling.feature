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

# [AP8-S batch 3] Pinta.Brix: a finger drawing on the canvas inside a ScrollViewer that can scroll a little lost the stroke
# (only its first move reached the canvas; the pointer's capture was lost, no release came). WinUI's rule: a touch drag in a
# scrollable ScrollViewer is taken by direct manipulation (panning) unless an element under the finger opts out with a
# ManipulationMode other than System - which a drawing surface does. This pad opts out (ManipulationMode All).
Scenario: A real finger dragging across a pad that opts out of panning inside a ScrollViewer reaches the pad, and does not scroll
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding a pad named "pad" 440 by 340 that captures the pointer and handles its own manipulations
	When a real finger drags across the pad "pad" from 40, 40 to 300, 200
	Then the pad "pad" saw every move of the finger and its release, and no cancel
	And the ScrollViewer "scroller" is not scrolled

# [AP8-S batch 4] The other half of WinUI's rule: an element that captures the pointer but does NOT opt out of panning
# (ManipulationMode System, the default) does not keep a touch drag from a scrollable ScrollViewer - the ScrollViewer's
# direct manipulation takes the pointer over (the element loses its capture) and pans. Android lost that gesture (batch 3:
# nothing scrolled, natively or in Core; logs/ap8s3_dev_CAPdiag).
Scenario: A real finger dragging across a pad that captures the pointer without opting out of panning scrolls the ScrollViewer
	Given the application shows a ScrollViewer named "scroller" 400 by 300 holding a pad named "pad" 440 by 340 that captures the pointer
	When a real finger drags across the pad "pad" from 300, 200 to 40, 40
	Then the ScrollViewer "scroller" is scrolled
	And the native scroll position of "scroller" matches its VerticalOffset
	And the pad "pad" gave the pointer up to the ScrollViewer
