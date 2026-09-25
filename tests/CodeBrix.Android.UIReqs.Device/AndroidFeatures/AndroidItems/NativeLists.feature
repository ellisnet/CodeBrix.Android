Feature: Native lists
	Android-only (not a copy of a CodeBrix.Platform feature). A ListView, a GridView, a FlipView and an
	ItemsRepeater are RecyclerViews on Android: only the items on screen have containers in Core's tree,
	containers are reused as the list scrolls, a real finger scrolls and taps the list natively, and the
	collection's changes reach the list one item at a time.

Scenario: A ListView is a native RecyclerView that realises only the rows on screen
	Given the application shows a native test ListView named "list" 320 by 240 with 200 rows
	When the frame is captured
	Then the native view of "list" is a RecyclerView
	And the ListView "list" holds 200 items
	And the ListView "list" has fewer than 30 realised containers
	And item 1 of the ListView "list" carries the text "Row 1"

Scenario: A real finger dragging a ListView scrolls it natively and Core follows
	Given the application shows a native test ListView named "list" 320 by 240 with 200 rows
	When a real finger drags the list "list" 150 pixels up
	Then the ListView "list" has scrolled down by at least 100 pixels
	When a real finger drags the list "list" 200 pixels up
	And a real finger drags the list "list" 200 pixels up
	Then the ListView "list" has no container for item 1
	And the ListView "list" has fewer than 30 realised containers

Scenario: Containers are reused as a ListView scrolls far
	Given the application shows a native test ListView named "list" 320 by 240 with 200 rows
	When a real finger drags the list "list" 200 pixels up
	And a real finger drags the list "list" 200 pixels up
	And a real finger drags the list "list" 200 pixels up
	Then the ListView "list" created fewer than 30 containers
	And the ListView "list" has fewer than 30 realised containers

Scenario: ScrollIntoView brings a far item into the native list and Core finds its container
	Given the application shows a native test ListView named "list" 320 by 240 with 200 rows
	When item 150 of the ListView "list" is scrolled into view
	Then the ListView "list" has a container for item 150
	And the ListView "list" has no container for item 1
	And the ListView "list" has fewer than 30 realised containers

Scenario: A real finger tapping a row selects it
	Given the application shows a native test ListView named "list" 320 by 240 with 20 rows
	When a real finger taps item 3 of the list "list"
	Then the SelectedIndex of the ListView "list" is 2
	And item 3 of the ListView "list" is selected

Scenario: Rows added to and removed from the collection appear and disappear
	Given the application shows a native test ListView named "list" 320 by 240 with 5 rows
	When the row "Inserted" is inserted at position 2 of the list "list"
	Then the ListView "list" holds 6 items
	And item 2 of the ListView "list" carries the text "Inserted"
	And item 3 of the ListView "list" carries the text "Row 2"
	When the row at position 1 of the list "list" is removed
	Then the ListView "list" holds 5 items
	And item 1 of the ListView "list" carries the text "Inserted"

Scenario: A cleared collection empties the list and a refilled one fills it again
	Given the application shows a native test ListView named "list" 320 by 240 with 5 rows
	When the collection of the list "list" is cleared
	Then the ListView "list" holds 0 items
	And the ListView "list" has fewer than 1 realised containers
	When 3 rows are added to the list "list"
	Then the ListView "list" holds 3 items
	And item 3 of the ListView "list" carries the text "Row 3"

Scenario: An ItemContainerStyle setter applies to the native rows
	Given the application shows a native test ListView named "list" 320 by 240 with 5 rows whose containers are "Orange"
	When the frame is captured
	Then item 1 of the ListView "list" is filled with "Orange"
	And item 4 of the ListView "list" is filled with "Orange"

Scenario: A ListView header is the first row of the native list
	Given the application shows a native test ListView named "list" 320 by 240 with 5 rows and a "Lime" header named "head"
	When the frame is captured
	Then the region of "head" is uniformly "Lime"
	And the header "head" of the list "list" is above item 1

Scenario: With item clicks enabled a tap raises ItemClick with the item
	Given the application shows a native test ListView named "list" 320 by 240 with 5 rows that raise ItemClick
	When item 2 of the ListView "list" is tapped
	Then the list "list" raised ItemClick 1 times, last with "Row 2"
	And the SelectedIndex of the ListView "list" is -1

Scenario: An ItemsRepeater with a UniformGridLayout lays its elements out in rows
	Given the application shows a native test ItemsRepeater named "grid" 400 by 300 with 6 tiles 80 wide in a uniform grid
	When the frame is captured
	Then the native view of "grid" is a RecyclerView
	And tile 2 of the repeater "grid" is to the right of tile 1
	And tile 6 of the repeater "grid" is below tile 1

Scenario: A WrapPanel wraps its children natively where Core put them
	Given the application shows a native test WrapPanel named "wrap" 300 wide holding 5 blocks 100 by 40
	When the frame is captured
	Then block 4 of the panel "wrap" is below block 1
	And block 2 of the panel "wrap" is to the right of block 1
	And the region of "wrap block 5" is uniformly "Navy"

Scenario: A TabView's tab row is a native Material tab strip and a real finger on a tab selects it
	Given the application shows a native test TabView named "tabs" with 3 tabs
	When the frame is captured
	Then the element "tabs" shows a native tab strip with 3 tabs
	When a real finger taps native tab 3 of "tabs"
	And the frame is captured
	Then the SelectedIndex of the TabView "tabs" is 2
	And the SelectionChanged of the TabView "tabs" was raised 1 times
	And the region of "tabs" contains "Orange"

Scenario: A Pivot's header row is a native Material tab strip and a real finger on a header selects it
	Given the application shows a native test Pivot named "sections" with 3 sections
	When a real finger taps native tab 2 of "sections"
	And the frame is captured
	Then the SelectedIndex of the Pivot "sections" is 1
	And the SelectionChanged of the Pivot "sections" was raised 1 times
	And the region of "sections" contains "Navy"
