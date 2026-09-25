Feature: Frame
	A Frame is where an application keeps the page it is showing. Navigating to a page puts that
	page's own content on the panel and remembers what was there before, and going back brings
	the earlier page - and the earlier picture - straight back.

Scenario: Navigating a Frame shows the page's own content
	Given the page "first" shows a panel named "firstPanel" painted "Red"
	And the application shows a Frame named "shell" 600 by 400
	When the Frame "shell" navigates to the page "first"
	And the frame is captured
	Then the Frame "shell" is showing the page "first"
	And the region of "firstPanel" is uniformly "Red"

Scenario: A Frame that has not navigated anywhere has nowhere to go back to
	Given the application shows a Frame named "shell" 600 by 400
	Then the Frame "shell" cannot go back
	And the back stack of "shell" holds 0 pages

Scenario: The first page a Frame navigates to is not something it can go back from
	Given the page "first" shows a panel named "firstPanel" painted "Red"
	And the application shows a Frame named "shell" 600 by 400
	When the Frame "shell" navigates to the page "first"
	Then the Frame "shell" cannot go back
	And the back stack of "shell" holds 0 pages

Scenario: Navigating to a second page replaces what the Frame was showing
	Given the page "first" shows a panel named "firstPanel" painted "Red"
	And the page "second" shows a panel named "secondPanel" painted "Blue"
	And the application shows a Frame named "shell" 600 by 400
	And the Frame "shell" navigates to the page "first"
	When the Frame "shell" navigates to the page "second"
	And the frame is captured
	Then the Frame "shell" is showing the page "second"
	And the region of "secondPanel" is uniformly "Blue"
	And the region of "secondPanel" does not contain "Red"
	And the Frame "shell" can go back
	And the back stack of "shell" holds 1 pages

Scenario: Going back brings the earlier page and its picture back
	Given the page "first" shows a panel named "firstPanel" painted "Red"
	And the page "second" shows a panel named "secondPanel" painted "Blue"
	And the application shows a Frame named "shell" 600 by 400
	And the Frame "shell" navigates to the page "first"
	When the frame is captured as "beforeSecond"
	And the Frame "shell" navigates to the page "second"
	And the frame is captured as "second"
	And the Frame "shell" goes back
	And the frame is captured as "back"
	Then the Frame "shell" is showing the page "first"
	And the region of "firstPanel" is uniformly "Red"
	And the region of "firstPanel" in frame "back" is unchanged from frame "beforeSecond"
	And the region of "firstPanel" in frame "second" differs from frame "back"

Scenario: Going back takes the page off the back stack
	Given the page "first" shows a panel named "firstPanel" painted "Red"
	And the page "second" shows a panel named "secondPanel" painted "Blue"
	And the page "third" shows a panel named "thirdPanel" painted "Green"
	And the application shows a Frame named "shell" 600 by 400
	And the Frame "shell" navigates to the page "first"
	And the Frame "shell" navigates to the page "second"
	And the Frame "shell" navigates to the page "third"
	Then the back stack of "shell" holds 2 pages
	When the Frame "shell" goes back
	Then the Frame "shell" is showing the page "second"
	And the back stack of "shell" holds 1 pages
	When the Frame "shell" goes back
	Then the Frame "shell" is showing the page "first"
	And the Frame "shell" cannot go back
	And the back stack of "shell" holds 0 pages
