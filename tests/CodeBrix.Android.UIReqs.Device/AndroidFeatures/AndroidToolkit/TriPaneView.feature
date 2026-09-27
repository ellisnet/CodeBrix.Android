Feature: The Toolkit TriPaneView on Android
	Android-only (not a copy of a CodeBrix.Platform feature: the Platform's src/UIReqs has no TriPaneView group). The
	TriPaneView keeps its template and Core's engine; its Android handler adds the adaptive form by the window's width
	size class - Compact = one pane, Medium = the side pane and one stacked pane, Expanded = three panes - reached through
	the engine's weights (a region the window has no room for is minimized with its restore grip, and its weight comes back
	when the window widens), and takes a finger on a divider within the 48-dp Material touch target, driving the drag
	through the control's drag entry points. The 15-inch window is Expanded; Compact and Medium come from REAL resizes of
	the window. The fingers and the mouse are REAL MotionEvents dispatched to the activity.

Scenario: A TriPaneView in an Expanded window shows its three panes through its Android handler
	Given the application shows a TriPaneView named "tri" with its panes painted "Red", "Lime" and "Blue"
	Then the TriPaneView "tri" is shown by its Android handler
	And the TriPaneView "tri" shows the panes "side, upper, lower"
	When the frame is captured
	Then the region of "tri.side" is uniformly "Red"
	And the region of "tri.upper" is uniformly "Lime"
	And the region of "tri.lower" is uniformly "Blue"

Scenario: A finger beside the side divider, inside its touch target, drags the divider
	Given the application shows a TriPaneView named "tri" with its panes painted "Red", "Lime" and "Blue"
	When a real finger drags the side divider of "tri" by 150 dp, starting 18 dp beside it
	Then the side pane of "tri" is wider than 40 percent
	And the TriPaneView "tri" took 1 divider drag natively
	And the TriPaneView "tri" raised DividerDragCompleted 1 time

Scenario: A finger beside the stack divider, inside its touch target, drags the divider
	Given the application shows a TriPaneView named "tri" with its panes painted "Red", "Lime" and "Blue"
	When a real finger drags the stack divider of "tri" by -120 dp, starting 18 dp beside it
	Then the upper pane of "tri" is narrower than 45 percent
	And the TriPaneView "tri" took 1 divider drag natively
	And the TriPaneView "tri" raised DividerDragCompleted 1 time

Scenario: A finger outside the touch target leaves the divider alone
	Given the application shows a TriPaneView named "tri" with its panes painted "Red", "Lime" and "Blue"
	When a real finger drags the side divider of "tri" by 150 dp, starting 40 dp beside it
	Then the TriPaneView "tri" took 0 divider drags natively
	And the TriPaneView "tri" raised DividerDragCompleted 0 times

Scenario: A finger drags the lower pane shut and a tap on its restore grip opens it again
	Given the application shows a TriPaneView named "tri" with its panes painted "Red", "Lime" and "Blue"
	When a real finger drags the stack divider of "tri" to the bottom of "tri"
	Then the TriPaneView "tri" shows the panes "side, upper"
	And the stack divider of "tri" is a restore grip
	When a real finger taps the stack divider of "tri"
	Then the TriPaneView "tri" shows the panes "side, upper, lower"
	And the TriPaneView "tri" raised DividerDragCompleted 2 times

Scenario: A mouse on a divider is left to Core's own divider handling
	Given the application shows a TriPaneView named "tri" with its panes painted "Red", "Lime" and "Blue"
	When a real mouse drags the side divider of "tri" by 150 dp
	Then the side pane of "tri" is wider than 40 percent
	And the TriPaneView "tri" took 0 divider drags natively
	And the TriPaneView "tri" raised DividerDragCompleted 1 time

Scenario: A Compact window shows one pane, and the restore grips switch panes
	Given the application shows a TriPaneView named "tri" with its panes painted "Red", "Lime" and "Blue"
	When the real window is resized to 400 dp wide
	Then the window is "Compact" wide
	And the TriPaneView "tri" shows the panes "upper"
	And the side divider of "tri" is a restore grip
	And the stack divider of "tri" is a restore grip
	When a real finger taps the side divider of "tri"
	Then the TriPaneView "tri" shows the panes "side"
	When a real finger taps the side divider of "tri"
	Then the TriPaneView "tri" shows the panes "upper"
	When a real finger taps the stack divider of "tri"
	Then the TriPaneView "tri" shows the panes "lower"
	When the real window size is restored
	Then the TriPaneView "tri" shows the panes "side, upper, lower"
	And the weights of "tri" are 33.3, 66.7, 50 and 50

Scenario: A Medium window shows the side pane and one stacked pane
	Given the application shows a TriPaneView named "tri" with its panes painted "Red", "Lime" and "Blue"
	When the real window is resized to 700 dp wide
	Then the window is "Medium" wide
	And the TriPaneView "tri" shows the panes "side, upper"
	When the real window size is restored
	Then the TriPaneView "tri" shows the panes "side, upper, lower"
	And the weights of "tri" are 33.3, 66.7, 50 and 50

Scenario: With the adaptive form switched off a Compact window keeps the three panes
	Given the adaptive TriPaneView form is switched off
	And the application shows a TriPaneView named "tri" with its panes painted "Red", "Lime" and "Blue"
	When the real window is resized to 400 dp wide
	Then the TriPaneView "tri" shows the panes "side, upper, lower"
	And the weights of "tri" are 33.3, 66.7, 50 and 50
