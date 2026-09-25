Feature: FlipView
	A FlipView shows one item of its collection at a time, and its content follows a finger
	that drags across it. Which item it has selected is a property the application sets and
	the view honours: the item it shows is the item it has selected, and nothing else.

	A swipe turns the page it has taken the content towards, provided the finger stops short
	of the half-way mark: the view then comes to rest on the next page and selects it. The
	SelectionChanged counts below are one higher than the number of times a scenario changes
	the selection, because the handler is attached when the FlipView is built and the view
	reports the selection of its first item as the items are assigned to it.

Scenario: A FlipView shows only the item it has selected
	Given the application shows a FlipView named "pages" with:
		| Property   | Value              |
		| Width      | 400                |
		| Height     | 300                |
		| ItemColors | Lime, Navy, Orange |
	When the frame is captured
	Then the FlipView "pages" holds 3 items
	And the SelectedIndex of the FlipView "pages" is 0
	And the region of "pages" contains "Lime"
	And the region of "pages" does not contain "Navy"
	And the region of "pages" does not contain "Orange"

Scenario: A finger swiping a FlipView takes its content with it
	Given the application shows a FlipView named "pages" with:
		| Property   | Value              |
		| Width      | 400                |
		| Height     | 300                |
		| ItemColors | Lime, Navy, Orange |
	When the frame is captured as "at rest"
	And the FlipView "pages" is swiped to the left
	And the frame is captured as "swiped"
	Then the FlipView "pages" has panned sideways by at least 100 pixels
	And the region of "pages" in frame "swiped" differs from frame "at rest"

Scenario: Selecting another item of a FlipView swaps what it shows
	Given the application shows a FlipView named "pages" with:
		| Property   | Value              |
		| Width      | 400                |
		| Height     | 300                |
		| ItemColors | Lime, Navy, Orange |
	When the frame is captured as "first page"
	And the SelectedIndex of "pages" is set to "2"
	And the frame is captured as "third page"
	Then the SelectedIndex of the FlipView "pages" is 2
	And the SelectionChanged of the FlipView "pages" was raised 2 times
	And the region of "pages" in frame "third page" differs from frame "first page"
	And the region of "pages" contains "Orange"
	And the region of "pages" does not contain "Lime"

Scenario: A short swipe turns a FlipView's page
	Given the application shows a FlipView named "pages" with:
		| Property   | Value              |
		| Width      | 400                |
		| Height     | 300                |
		| ItemColors | Lime, Navy, Orange |
	When the FlipView "pages" is swiped a short way to the left
	And the frame is captured
	Then the SelectedIndex of the FlipView "pages" is 1
	And the SelectionChanged of the FlipView "pages" was raised 2 times
	And the region of "pages" contains "Navy"
	And the region of "pages" does not contain "Lime"
