Feature: VisualState
	A control says which of its looks it is wearing by going to a visual state. The states here
	are built in C# - a template of one coloured panel and a state group whose states repaint it
	- so what is being checked is the visual state manager itself and nothing else.

Scenario: A state box shows its resting colour until it is put into a state
	Given the application shows a state box named "box" 400 by 240 resting "Gray" with states:
		| State | Colour |
		| Calm  | Green  |
		| Alert | Red    |
	When the frame is captured
	Then the region of "box" is uniformly "Gray"

Scenario: Going to a visual state paints the control that state's colour
	Given the application shows a state box named "box" 400 by 240 resting "Gray" with states:
		| State | Colour |
		| Calm  | Green  |
		| Alert | Red    |
	When the frame is captured as "resting"
	And the state box "box" is put into the state "Alert"
	And the frame is captured as "alert"
	Then the state box took the state
	And the region of "box" is uniformly "Red"
	And the region of "box" in frame "alert" differs from frame "resting"

Scenario: Going to another state repaints the control again
	Given the application shows a state box named "box" 400 by 240 resting "Gray" with states:
		| State | Colour |
		| Calm  | Green  |
		| Alert | Red    |
	And the state box "box" is put into the state "Alert"
	When the frame is captured as "alert"
	And the state box "box" is put into the state "Calm"
	And the frame is captured as "calm"
	Then the state box took the state
	And the region of "box" is uniformly "Green"
	And the region of "box" in frame "calm" differs from frame "alert"

Scenario: A control asked for a state it does not have is left as it was
	Given the application shows a state box named "box" 400 by 240 resting "Gray" with states:
		| State | Colour |
		| Calm  | Green  |
		| Alert | Red    |
	And the state box "box" is put into the state "Alert"
	When the frame is captured as "alert"
	And the state box "box" is put into the state "Panic"
	And the frame is captured as "after"
	Then the state box did not take the state
	And the region of "box" is uniformly "Red"
	And the region of "box" in frame "after" is unchanged from frame "alert"
