@needs-wpe
Feature: A page shown in a WebView
	A WebView shows a web page inside a rectangle of the panel. The engine that draws the page runs
	outside the application - it is a system engine with processes of its own, and it renders off
	screen - so what these scenarios are really about is the whole journey: the page is handed over,
	the engine lays it out, the picture comes back as a buffer, and the framework composites that
	buffer into the panel like any other content. Every page here is flat colour on whole-pixel
	boundaries, so where a colour lands says exactly what the page and the control between them did.

	Every scenario starts the engine first and waits for a known picture. The first scenario of a
	run pays for the engine's thread and its web process there, which is why that budget is large;
	after it, the engine is up and the same step costs one navigation. Nothing tells the harness when
	a web frame has been composited - a navigation reports completion when the LOAD ends, which is
	before the picture arrives - so every visual claim is a bounded wait with its budget written down.

	Deliberately out of scope here: hover (the panel is touch and keyboard), display scale (pinned at
	1.0), anything that would have to be fetched over a network, and downloads, which would write
	files onto the machine running the scenarios. The pages come from this project: as text, which
	the control hands over as a data document, or as one of the HTML files that ship beside it.

	One scenario here found a defect in the add-in and now fences it: refusing a navigation as it was
	announced did not stop it, because the announcement came from a signal the engine emits once it
	has already committed to the load - the refusal chased a page that was on its way. A navigation
	is now announced where the engine asks whether it may go ahead, and the refusal is the answer to
	that question, so the page a scenario says no to is not the page it ends up showing.

Scenario: A WebView shows the page it was given
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	When "browser" shows the page "Solid"
	Then the navigation of "browser" completes within 5000 milliseconds
	And the region of "browser" shows at least 99 percent "Red" within 5000 milliseconds
	And "browser" is 800 by 600 device pixels

Scenario: A navigation is announced as the page it was given and reports that it finished
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	When "browser" shows the page "Solid"
	Then the navigation of "browser" completes within 5000 milliseconds
	And the navigation of "browser" was announced as "data:text/html"
	And the navigation of "browser" reported success
	And the NavigationStarting of "browser" was raised at least once
	And the NavigationCompleted of "browser" was raised at least once
	And the region of "browser" shows at least 99 percent "Red" within 5000 milliseconds

Scenario: The page's own layout lands where the page put it
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	When "browser" shows the page "Halves"
	Then the navigation of "browser" completes within 5000 milliseconds
	And the 360 by 560 block at 20, 20 inside "browser" becomes uniformly "Red" within 5000 milliseconds
	And the rightmost 380 pixels of "browser" are uniformly "Blue"

Scenario: A WebView is composited like any other content, not a hole in the panel
	Given the application shows a Grid named "stage" with:
		| Property | Value |
		| Width    | 600   |
		| Height   | 400   |
	And the layout "stage" holds a WebView named "browser" with:
		| Property | Value |
		| Width    | 600   |
		| Height   | 400   |
	And the web engine of "browser" has started within 20000 milliseconds
	And the layout "stage" holds a Border named "cover" with:
		| Property            | Value  |
		| Width               | 300    |
		| Height              | 400    |
		| HorizontalAlignment | Right  |
		| Background          | Yellow |
	When "browser" shows the page "Solid"
	Then the navigation of "browser" completes within 5000 milliseconds
	And the 280 by 380 block at 10, 10 inside "browser" becomes uniformly "Red" within 5000 milliseconds
	And the rightmost 280 pixels of "browser" are uniformly "Yellow"

Scenario: Refusing a navigation leaves the page where it was
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	And "browser" shows the page "Solid"
	And the navigation of "browser" completes within 5000 milliseconds
	And the region of "browser" shows at least 99 percent "Red" within 5000 milliseconds
	And the recorded events are forgotten
	When the navigation of "browser" to the page "Halves" is refused within 5000 milliseconds
	And the frame is captured
	Then the NavigationSucceeded of "browser" was never raised
	And the region of "browser" is uniformly "Red"
	And the region of "browser" does not contain "Blue"

Scenario: Going back returns the WebView to the page before
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	And "browser" shows the local page "page.html"
	And the navigation of "browser" completes within 10000 milliseconds
	And the region of "browser" shows at least 99 percent "Purple" within 5000 milliseconds
	And "browser" shows the local page "second.html"
	And the navigation of "browser" completes within 10000 milliseconds
	And the region of "browser" shows at least 99 percent "Teal" within 5000 milliseconds
	Then "browser" can go back
	When "browser" goes back
	Then the navigation of "browser" completes within 10000 milliseconds
	And the region of "browser" shows at least 99 percent "Purple" within 5000 milliseconds

Scenario: A WebView that is made narrower draws the page again at the new size
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	And "browser" shows the page "Bar"
	And the navigation of "browser" completes within 5000 milliseconds
	And the 40 by 400 block at 30, 100 inside "browser" becomes uniformly "Red" within 5000 milliseconds
	When the Width of "browser" is set to "500"
	Then "browser" is 500 by 600 device pixels
	And the 20 by 400 block at 70, 100 inside "browser" becomes uniformly "Red" within 5000 milliseconds
	And the 20 by 400 block at 120, 100 inside "browser" is uniformly "Blue"

Scenario: A page that lives in a file on this machine loads
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	When "browser" shows the local page "page.html"
	Then the navigation of "browser" completes within 10000 milliseconds
	And the region of "browser" shows at least 99 percent "Purple" within 5000 milliseconds
	And the address of "browser" begins with "file://"
	And the navigation of "browser" reported success
