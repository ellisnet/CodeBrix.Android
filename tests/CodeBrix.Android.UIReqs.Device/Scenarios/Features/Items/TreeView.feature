Feature: TreeView
	A TreeView shows the top of a hierarchy and keeps the rest of it out of sight until a
	finger asks for it: the chevron on a row with children opens that row, and closes it again.

Scenario: A TreeView shows its root nodes and none of their children
	Given the application shows a TreeView named "tree" with the nodes:
		| Node      | Children              |
		| Fruit     | Apple, Pear, Plum     |
		| Vegetable | Carrot, Leek          |
	When the frame is captured
	Then the TreeView "tree" shows 2 rows
	And row 1 of the TreeView "tree" is not expanded
	And the region of "tree" has ink

Scenario: Tapping the chevron of a TreeView row shows the children of that row
	Given the application shows a TreeView named "tree" with the nodes:
		| Node      | Children          |
		| Fruit     | Apple, Pear, Plum |
		| Vegetable | Carrot, Leek      |
	When the frame is captured as "collapsed"
	And the expand chevron of row 1 of the TreeView "tree" is tapped
	And the frame is captured as "expanded"
	Then row 1 of the TreeView "tree" is expanded
	And the TreeView "tree" shows 5 rows
	And row 2 of the TreeView "tree" carries the text "Apple"
	And row 2 of the TreeView "tree" has ink
	And row 4 of the TreeView "tree" has ink

Scenario: Tapping the chevron of an expanded TreeView row hides its children again
	Given the application shows a TreeView named "tree" with the nodes:
		| Node      | Children          |
		| Fruit     | Apple, Pear, Plum |
		| Vegetable | Carrot, Leek      |
	When the expand chevron of row 1 of the TreeView "tree" is tapped
	Then the TreeView "tree" shows 5 rows
	When the expand chevron of row 1 of the TreeView "tree" is tapped
	Then row 1 of the TreeView "tree" is not expanded
	And the TreeView "tree" shows 2 rows
