Feature: TextBox
	A TextBox must take the keyboard when it is tapped, put what is typed into its Text and
	draw it, show its PlaceholderText only while it is empty, refuse keys when it is read-only,
	stop accepting them at its MaxLength, and draw a selection a person can see.

Scenario: A TextBox draws the Text it was given
	Given the application shows a TextBox named "entry" with:
		| Property        | Value |
		| Width           | 500   |
		| Height          | 70    |
		| FontSize        | 32    |
		| Foreground      | Blue  |
		| Background      | White |
		| BorderThickness | 0     |
		| Text            | Hello |
	When the frame is captured
	Then the Text of "entry" is "Hello"
	And the region of "entry" has ink
	And the ink color of "entry" is "Blue"

Scenario: Tapping a TextBox gives it the keyboard
	Given the application shows a TextBox named "entry" with:
		| Property        | Value |
		| Width           | 500   |
		| Height          | 70    |
		| FontSize        | 32    |
		| Foreground      | Blue  |
		| Background      | White |
		| BorderThickness | 0     |
	Then "entry" does not have keyboard focus
	When "entry" is tapped
	Then "entry" has keyboard focus

Scenario: Typing into a focused TextBox puts the characters into its Text
	Given the application shows a TextBox named "entry" with:
		| Property        | Value |
		| Width           | 500   |
		| Height          | 70    |
		| FontSize        | 32    |
		| Foreground      | Blue  |
		| Background      | White |
		| BorderThickness | 0     |
	When "entry" is tapped
	And the text "Hello world" is typed
	Then the Text of "entry" is "Hello world"

Scenario: What is typed into a TextBox is drawn
	Given the application shows a TextBox named "entry" with:
		| Property        | Value |
		| Width           | 500   |
		| Height          | 70    |
		| FontSize        | 32    |
		| Foreground      | Blue  |
		| Background      | White |
		| BorderThickness | 0     |
	When the frame is captured as "empty"
	And "entry" is tapped
	And the text "Hello" is typed
	And the frame is captured as "typed"
	Then the Text of "entry" is "Hello"
	And the region of "entry" in frame "typed" differs from frame "empty"
	And the region of "entry" in frame "typed" holds more ink than in frame "empty"

Scenario: Typing goes nowhere when the TextBox has not been tapped
	Given the application shows a TextBox named "entry" with:
		| Property        | Value     |
		| Width           | 500       |
		| Height          | 70        |
		| FontSize        | 32        |
		| Foreground      | Blue      |
		| Background      | White     |
		| BorderThickness | 0         |
		| Text            | Untouched |
	When the text "XYZ" is typed
	Then the Text of "entry" is "Untouched"
	And "entry" does not have keyboard focus

Scenario: An empty TextBox with no PlaceholderText draws nothing
	Given the application shows a TextBox named "entry" with:
		| Property        | Value |
		| Width           | 500   |
		| Height          | 70    |
		| FontSize        | 32    |
		| Foreground      | Blue  |
		| Background      | White |
		| BorderThickness | 0     |
	When the frame is captured
	Then the Text of "entry" is ""
	And the region of "entry" is blank

Scenario: An empty TextBox draws its PlaceholderText
	Given the application shows a TextBox named "entry" with:
		| Property        | Value     |
		| Width           | 500       |
		| Height          | 70        |
		| FontSize        | 32        |
		| Foreground      | Blue      |
		| Background      | White     |
		| BorderThickness | 0         |
		| PlaceholderText | Type here |
	When the frame is captured
	Then the Text of "entry" is ""
	And the region of "entry" has ink

Scenario: The PlaceholderText disappears once something is typed and comes back when it is cleared
	Given the application shows a TextBox named "entry" with:
		| Property        | Value     |
		| Width           | 500       |
		| Height          | 70        |
		| FontSize        | 32        |
		| Foreground      | Blue      |
		| Background      | White     |
		| BorderThickness | 0         |
		| PlaceholderText | Type here |
	When the frame is captured as "placeholder"
	Then the region of "entry" has ink
	When "entry" is tapped
	And the text "Hi" is typed
	And the frame is captured as "typed"
	Then the Text of "entry" is "Hi"
	And the region of "entry" in frame "typed" differs from frame "placeholder"
	When the Text of "entry" is set to ""
	And the frame is captured as "cleared"
	Then the Text of "entry" is ""
	And the region of "entry" has ink

Scenario: A read-only TextBox ignores what is typed
	Given the application shows a TextBox named "entry" with:
		| Property        | Value  |
		| Width           | 500    |
		| Height          | 70     |
		| FontSize        | 32     |
		| Foreground      | Blue   |
		| Background      | White  |
		| BorderThickness | 0      |
		| Text            | Locked |
		| IsReadOnly      | true   |
	When "entry" is tapped
	Then "entry" has keyboard focus
	When the text "XYZ" is typed
	Then the Text of "entry" is "Locked"

Scenario: A TextBox stops accepting characters at its MaxLength
	Given the application shows a TextBox named "entry" with:
		| Property        | Value |
		| Width           | 500   |
		| Height          | 70    |
		| FontSize        | 32    |
		| Foreground      | Blue  |
		| Background      | White |
		| BorderThickness | 0     |
		| MaxLength       | 5     |
	When "entry" is tapped
	And the text "0123456789" is typed
	Then the Text of "entry" is "01234"

Scenario: Backspace removes the character that was typed last
	Given the application shows a TextBox named "entry" with:
		| Property        | Value |
		| Width           | 500   |
		| Height          | 70    |
		| FontSize        | 32    |
		| Foreground      | Blue  |
		| Background      | White |
		| BorderThickness | 0     |
	When "entry" is tapped
	And the text "Hello" is typed
	And the key "Back" is pressed
	Then the Text of "entry" is "Hell"

Scenario: A selection is drawn where the selected text is
	Given the application shows a TextBox named "entry" with:
		| Property        | Value    |
		| Width           | 500      |
		| Height          | 70       |
		| FontSize        | 32       |
		| Foreground      | Blue     |
		| Background      | White    |
		| BorderThickness | 0        |
		| Text            | Selected |
	When "entry" is tapped
	And the frame is captured as "no selection"
	And all the text of "entry" is selected
	And the frame is captured as "selected"
	Then the SelectedText of "entry" is "Selected"
	And the region of "entry" in frame "selected" differs from frame "no selection"
	And the region of "entry" in frame "selected" holds more ink than in frame "no selection"

Scenario: TextAlignment moves a TextBox's text inside the box
	Given the application shows a TextBox named "entry" with:
		| Property        | Value   |
		| Width           | 500     |
		| Height          | 70      |
		| FontSize        | 32      |
		| Foreground      | Blue    |
		| Background      | White   |
		| BorderThickness | 0       |
		| Padding         | 0       |
		| Text            | Aligned |
		| TextAlignment   | Left    |
	When the frame is captured as "left"
	Then the ink of "entry" sits at the left of its block
	When the TextAlignment of "entry" is set to "Right"
	And the frame is captured as "right"
	Then the ink of "entry" sits at the right of its block
	And the Text of "entry" is "Aligned"
	And the region of "entry" in frame "right" differs from frame "left"
