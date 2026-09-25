@needs-wpe
Feature: A WebView and its page talking to each other
	An application that hosts a page needs three things from it: to be able to run a script in it and
	get an answer back, to see what the page draws when that script changes it, and to hear from the
	page when the page has something to say - as it loads, or because a finger landed on it. This
	feature is those three, plus the two things that finish the round trip: reloading a page runs it
	again, and what a person types on the panel reaches the text box inside the page.

	The engine answers a script with a JSON literal rather than a bare value, because a script may
	answer with any JavaScript value; a scenario states the text a person expects, and one scenario
	states the encoding as well, because an application has to know about it.

	Every scenario starts the engine first and waits for a known picture, exactly as the page
	scenarios do. Deliberately out of scope here: hover, display scale, host-to-page messages (the
	engine on this head has none - a script is the route), and anything that would reach a network.

	Two scenarios here reload a page, and the second of them found a defect in the add-in and now
	fences it: a page that had been handed over as TEXT came back as an empty document, because the
	engine reloads the URI it gave that document and the text was nowhere it could fetch it from.
	The control now hands such a page over again, so the two scenarios state the one requirement
	they share - a page that is reloaded runs again - however the page reached the control.

Scenario: A script runs in the page and its answer comes back
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	When "browser" shows the page "Title"
	Then the navigation of "browser" completes within 5000 milliseconds
	And the DocumentTitle of "browser" becomes "Hello UIReqs" within 5000 milliseconds
	And the script "document.title" in "browser" returns the text "Hello UIReqs"
	And the script "document.title" in "browser" answers with a JSON string
	And the region of "browser" shows at least 99 percent "Blue" within 5000 milliseconds

Scenario: A script that changes the page changes what is on the panel
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	And "browser" shows the page "Solid"
	And the navigation of "browser" completes within 5000 milliseconds
	And the region of "browser" shows at least 99 percent "Red" within 5000 milliseconds
	When the script "document.body.style.background='#008000'" runs in "browser"
	Then the region of "browser" shows at least 99 percent "Green" within 5000 milliseconds

Scenario: A page can talk to its host as it loads
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	When "browser" shows the page "Button"
	Then the navigation of "browser" completes within 5000 milliseconds
	And the web message "ready" reaches "browser" within 5000 milliseconds
	And the WebMessageReceived of "browser" was raised at least once
	And the region of "browser" shows at least 99 percent "Red" within 5000 milliseconds

Scenario: A tap on a WebView reaches the page inside it
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	And "browser" shows the page "Button"
	And the navigation of "browser" completes within 5000 milliseconds
	And the region of "browser" shows at least 99 percent "Red" within 5000 milliseconds
	When "browser" is tapped
	Then the web message "tapped" reaches "browser" within 5000 milliseconds
	And the region of "browser" shows at least 99 percent "Green" within 5000 milliseconds

Scenario: Reloading a WebView runs its page again
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	And "browser" shows the local page "talkative.html"
	And the navigation of "browser" completes within 10000 milliseconds
	And the region of "browser" shows at least 99 percent "Purple" within 5000 milliseconds
	And the web message "ready" reaches "browser" within 5000 milliseconds
	When "browser" is reloaded
	Then the navigation of "browser" completes within 10000 milliseconds
	And the region of "browser" shows at least 99 percent "Purple" within 5000 milliseconds
	And the web message "ready" reaches "browser" 2 times within 5000 milliseconds

Scenario: Reloading a page that was handed over as text runs that page again
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	And "browser" shows the page "Button"
	And the navigation of "browser" completes within 5000 milliseconds
	And the region of "browser" shows at least 99 percent "Red" within 5000 milliseconds
	And the web message "ready" reaches "browser" within 5000 milliseconds
	When "browser" is reloaded
	Then the navigation of "browser" completes within 5000 milliseconds
	And the region of "browser" shows at least 99 percent "Red" within 5000 milliseconds
	And the web message "ready" reaches "browser" 2 times within 5000 milliseconds

Scenario: What a person types reaches the text box inside the page
	Given the application shows a WebView named "browser" 800 by 600
	And the web engine of "browser" has started within 20000 milliseconds
	And "browser" shows the page "Typing"
	And the navigation of "browser" completes within 5000 milliseconds
	And the 100 by 100 block at 20, 20 inside "browser" becomes uniformly "Yellow" within 5000 milliseconds
	And the keyboard focus is given to "browser"
	And the script "document.getElementById('box').focus()" runs in "browser"
	When the text "abc" is typed
	Then the script "document.getElementById('box').value" in "browser" returns the text "abc" within 5000 milliseconds
