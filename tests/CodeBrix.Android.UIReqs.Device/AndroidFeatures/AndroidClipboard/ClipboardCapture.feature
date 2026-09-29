Feature: Clipboard capture isolation

Scenario: A capture waits through the clipboard overlay quiet period
	Given the system clipboard receives fresh harness text
	When the frame is captured
	Then the clipboard capture waited at least 10 seconds
