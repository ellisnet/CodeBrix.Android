Feature: Android window size classes and the adaptive table
	Android-only (not a copy of a CodeBrix.Platform feature). A window's Material size classes come from its
	metrics in dp and are re-computed live. An Auto NavigationView is shown in the Material container the
	adaptive table picks for them - a bottom bar, a rail, a drawer - and re-maps live without its page being
	recreated; a ContentDialog with XAML content fills a Compact window. The 15-inch window is Expanded in both
	orientations; Compact and Medium come from REAL resizes of the window (the host runs "wm density" for the
	scenario and puts it back afterwards), and one scenario checks that the size-class override re-maps as a real
	resize does. A native NavigationView absorbs the system bars it overlaps (the harness hides them; those
	scenarios show them).

Scenario: The window's size classes come from its size in dp
	Then the window size classes are computed from the window's own size
	And the window is "Expanded" wide

Scenario: An Auto NavigationView in an Expanded window is a navigation drawer beside its page
	Given the application shows an Auto NavigationView named "nav" with the items "Home, Mail, Music" and a page named "page" painted "Blue"
	Then the NavigationView "nav" is shown as a "PersistentDrawer" with the destinations "Home, Mail, Music, Settings"
	And the page "page" is laid out beside the "PersistentDrawer" of "nav"
	When the frame is captured
	Then the region of "page" is uniformly "Blue"

Scenario: An Auto NavigationView re-maps live from drawer to bottom bar to rail and back, keeping its page
	Given the application shows an Auto NavigationView named "nav" with the items "Home, Mail, Music" and a page named "page" painted "Blue"
	And the native view of "page" is remembered
	When the real window is resized to 400 dp wide
	Then the window is "Compact" wide
	And the NavigationView "nav" is shown as a "BottomBar" with the destinations "Home, Mail, Music, Settings"
	And the page "page" is laid out beside the "BottomBar" of "nav"
	And "page" still has the native view it had
	And the destination icons of "nav" are drawn for the window's density
	When the real window is resized to 700 dp wide
	Then the window is "Medium" wide
	And the NavigationView "nav" is shown as a "Rail" with the destinations "Home, Mail, Music, Settings"
	And the page "page" is laid out beside the "Rail" of "nav"
	And "page" still has the native view it had
	And the destination icons of "nav" are drawn for the window's density
	When the real window size is restored
	Then the window is "Expanded" wide
	And the NavigationView "nav" is shown as a "PersistentDrawer" with the destinations "Home, Mail, Music, Settings"
	And "page" still has the native view it had
	And the destination icons of "nav" are drawn for the window's density
	When the frame is captured
	Then the region of "page" is uniformly "Blue"

Scenario: The size-class override re-maps an Auto NavigationView as a real resize does
	Given the application shows an Auto NavigationView named "nav" with the items "Home, Mail, Music" and a page named "page" painted "Blue"
	And the native view of "page" is remembered
	When the window is simulated as 400 by 800 dp
	Then the NavigationView "nav" is shown as a "BottomBar" with the destinations "Home, Mail, Music, Settings"
	And "page" still has the native view it had
	When the window is simulated as 700 by 900 dp
	Then the NavigationView "nav" is shown as a "Rail" with the destinations "Home, Mail, Music, Settings"
	When the window size classes are no longer simulated
	Then the NavigationView "nav" is shown as a "PersistentDrawer" with the destinations "Home, Mail, Music, Settings"
	And "page" still has the native view it had

Scenario: A Compact NavigationView with more destinations than a bottom bar holds uses a modal drawer
	Given the application shows an Auto NavigationView named "nav" with the items "One, Two, Three, Four, Five, Six" and a page named "page" painted "Blue"
	When the real window is resized to 400 dp wide
	Then the NavigationView "nav" is shown as a "ModalDrawer" with the destinations "One, Two, Three, Four, Five, Six, Settings"
	And the page "page" is laid out beside the "ModalDrawer" of "nav"
	And the modal drawer of "nav" is closed
	And the pane of "nav" is shut
	When the navigation button of "nav" is pressed
	Then the modal drawer of "nav" is open
	And the pane of "nav" is open
	When the destination "Three" of "nav" is picked
	Then the NavigationView "nav" has selected "Three"
	And the modal drawer of "nav" is closed

Scenario: Picking a destination of a bottom bar selects its item and tells the app
	Given the application shows an Auto NavigationView named "nav" with the items "Home, Mail, Music" and a page named "page" painted "Blue"
	And the real window is resized to 400 dp wide
	When the destination "Mail" of "nav" is picked
	Then the NavigationView "nav" has selected "Mail"
	And the NavigationView "nav" reported ItemInvoked for "Mail" and a selection change
	And the destination "Mail" of "nav" is checked

Scenario: Selecting an item from code checks its destination in the native container
	Given the application shows an Auto NavigationView named "nav" with the items "Home, Mail, Music" and a page named "page" painted "Blue"
	When the NavigationView "nav" selects "Music"
	Then the destination "Music" of "nav" is checked
	And the NavigationView "nav" has selected "Music"

Scenario: Settings in a native container is invoked as Settings
	Given the application shows an Auto NavigationView named "nav" with the items "Home, Mail" and a page named "page" painted "Blue"
	When the destination "Settings" of "nav" is picked
	Then the NavigationView "nav" reported ItemInvoked for Settings

Scenario: A NavigationView with an explicit PaneDisplayMode keeps its Fluent template at every size
	Given the application shows a NavigationView named "nav" 900 by 500 with items:
		| Name  | Label | Panel      | Colour |
		| alpha | Alpha | alphaPanel | Red    |
		| beta  | Beta  | betaPanel  | Blue   |
	Then the NavigationView "nav" is shown with its Fluent template
	When the real window is resized to 400 dp wide
	Then the NavigationView "nav" is shown with its Fluent template

Scenario: A NavigationView whose pane has a footer keeps its Fluent template
	Given the application shows an Auto NavigationView named "nav" with the items "Home, Mail" and a page named "page" painted "Blue"
	When the NavigationView "nav" gets a PaneFooter
	Then the NavigationView "nav" is shown with its Fluent template

Scenario: A ContentDialog with XAML content fills a Compact window and is a basic dialog again when the window widens
	Given the application shows a ContentDialog named "dialog" with XAML content
	When the real window is resized to 400 dp wide
	Then the ContentDialog "dialog" is shown full screen
	When the real window size is restored
	Then the ContentDialog "dialog" is shown as a basic dialog

Scenario: A native NavigationView keeps its chrome and its page clear of the system bars
	Given the system bars are shown
	And the application shows an Auto NavigationView named "nav" with the items "Home, Mail, Music" and a page named "page" painted "Blue"
	Then the NavigationView "nav" is shown as a "PersistentDrawer" with the destinations "Home, Mail, Music, Settings"
	And the destinations of "nav" are clear of the system bars
	And the page "page" is clear of the system bars
	When the window is simulated as 700 by 900 dp
	Then the NavigationView "nav" is shown as a "Rail" with the destinations "Home, Mail, Music, Settings"
	And the destinations of "nav" are clear of the system bars
	And the page "page" is clear of the system bars
	When the window is simulated as 400 by 800 dp
	Then the NavigationView "nav" is shown as a "BottomBar" with the destinations "Home, Mail, Music, Settings"
	And the destinations of "nav" are clear of the system bars
	And the page "page" is clear of the system bars
	And the page "page" is laid out beside the "BottomBar" of "nav"

Scenario: A native modal-drawer NavigationView keeps its top bar and its page clear of the system bars
	Given the system bars are shown
	And the application shows an Auto NavigationView named "nav" with the items "One, Two, Three, Four, Five, Six" and a page named "page" painted "Blue"
	When the window is simulated as 400 by 800 dp
	Then the NavigationView "nav" is shown as a "ModalDrawer" with the destinations "One, Two, Three, Four, Five, Six, Settings"
	And the destinations of "nav" are clear of the system bars
	And the page "page" is clear of the system bars
	And the page "page" is laid out beside the "ModalDrawer" of "nav"
