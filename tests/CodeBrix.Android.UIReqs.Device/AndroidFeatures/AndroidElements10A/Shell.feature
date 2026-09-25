Feature: Navigation and shell elements
	Android-only (AP10-A). NavigationView's items, headers and separators are the rows of its native Material
	navigation container (their NavigationViewItemPresenter template part is never built); SplitView keeps its
	Fluent template with the adaptive table's Compact form (an Inline pane shown as an Overlay pane); TwoPaneView keeps
	its template (its own width rules are the size-class behaviour); TitleBar is NotImplemented in the Platform.

Scenario: A NavigationView's items, header and separator (every NavigationViewItemBase) are rows of its native navigation drawer
	Given the application shows the AP10 sample "navigation view" named "nav"
	Then "nav" is shown by a native NavigationView
	And "nav" shows the native text "Inbox"
	And "nav" shows the native text "Sent"
	And "nav" shows the native text "Mail"
	And the Core tree of "nav" holds no NavigationViewItemPresenter

Scenario: A real finger on a native NavigationView item selects that NavigationViewItem
	Given the application shows the AP10 sample "navigation view" named "nav"
	When a real finger taps the native "Sent" of "nav"
	Then the Core property IsSelected of "sent" is "True"

Scenario: An open Inline SplitView pane sits beside the content in an Expanded window
	Given the application shows the AP10 sample "inline split view" named "split"
	Then the SplitView "split" shows the state OpenInlineLeft
	And the native view of "pane" is left of the native view of "content"

Scenario: In a Compact window an open Inline SplitView pane is shown as an Overlay pane that a tap beside it shuts
	Given the application shows the AP10 sample "inline split view" named "split"
	And the PaneClosed events of "split" are counted
	When the window is simulated as 400 by 800 dp
	Then the SplitView "split" shows the state OpenOverlayLeft
	And the Core property DisplayMode of "split" is "Inline"
	When the frame is captured
	When a real finger taps "split" at 90 percent across and 50 percent down
	Then the Core property IsPaneOpen of "split" is "False"
	And "split" raised PaneClosed 1 times

Scenario: A SplitView goes back to its declared Inline form when the window widens again
	Given the application shows the AP10 sample "inline split view" named "split"
	When the window is simulated as 400 by 800 dp
	Then the SplitView "split" shows the state OpenOverlayLeft
	When the window size classes are no longer simulated
	Then the SplitView "split" shows the state OpenInlineLeft

Scenario: TitleBar is NotImplemented in the Platform and takes no room on Android either
	Then the Platform marks TitleBar as not implemented
	Given the application shows the AP10 sample "title bar" named "title"
	Then "title" takes no room

Scenario: A TwoPaneView wide enough for both panes keeps its template and puts them side by side
	Given the application shows the AP10 sample "two pane view" named "two"
	Then "two" is not shown by a native NavigationView
	And the Core property Mode of "two" is "Wide"
	And the native view of "pane1" is left of the native view of "pane2"
