Feature: SplitView
	A SplitView holds a pane beside its content. While the pane is shut the panel shows nothing
	of it; while it is open the pane's own content is on the panel, and an inline pane takes its
	width away from the content rather than lying over it.

Scenario: A SplitView with a shut pane shows nothing of the pane
	Given the application shows a SplitView named "shell" 800 by 500 with a pane named "pane" painted "Red" and content named "body" painted "Blue"
	When the frame is captured
	Then the pane of "shell" is shut
	And the region of "shell" does not contain "Red"
	And the region of "body" is uniformly "Blue"

Scenario: Opening a SplitView's pane puts the pane's own content on the panel
	Given the application shows a SplitView named "shell" 800 by 500 with a pane named "pane" painted "Red" and content named "body" painted "Blue"
	When the frame is captured as "shut"
	And the pane of "shell" is opened
	And the frame is captured as "open"
	Then the pane of "shell" is open
	And the region of "pane" is uniformly "Red"
	And the region of "shell" in frame "open" differs from frame "shut"

Scenario: Shutting a SplitView's pane again takes it off the panel
	Given the application shows a SplitView named "shell" 800 by 500 with a pane named "pane" painted "Red" and content named "body" painted "Blue"
	And the pane of "shell" is opened
	When the frame is captured as "open"
	And the pane of "shell" is closed
	And the frame is captured as "shut"
	Then the pane of "shell" is shut
	And the region of "shell" does not contain "Red"
	And the region of "shell" in frame "shut" differs from frame "open"

Scenario: An inline pane takes its width away from the content
	Given the application shows a SplitView named "shell" 800 by 500 with a pane named "pane" painted "Red" and content named "body" painted "Blue"
	And the SplitViewDisplayMode of "shell" is set to "Inline"
	And the OpenPaneLength of "shell" is set to "240"
	When the pane of "shell" is opened
	And the frame is captured
	Then "pane" is 240 by 500 device pixels
	And "body" is 560 by 500 device pixels
	And the top left of "body" is 240, 0 inside "shell"
	And "pane" sits directly left of "body"
	And the region of "pane" is uniformly "Red"
	And the region of "body" is uniformly "Blue"

Scenario: A compact pane keeps a strip of itself showing while it is shut
	Given the application shows a SplitView named "shell" 800 by 500 with a pane named "pane" painted "Red" and content named "body" painted "Blue"
	And the CompactPaneLength of "shell" is set to "60"
	And the OpenPaneLength of "shell" is set to "240"
	And the SplitViewDisplayMode of "shell" is set to "CompactInline"
	When the frame is captured
	Then the pane of "shell" is shut
	And the leftmost 50 pixels of "shell" are uniformly "Red"
	And the top left of "body" is 60, 0 inside "shell"
	And "body" is 740 by 500 device pixels
