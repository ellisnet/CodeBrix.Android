Feature: PasswordBox
	A PasswordBox must keep what was typed in its Password and draw its masking character
	instead - so what is on the panel says how many characters there are and nothing more.

Scenario: A PasswordBox keeps what was typed without drawing it
	Given the application shows a PasswordBox named "secret" with:
		| Property        | Value |
		| Width           | 500   |
		| Height          | 70    |
		| FontSize        | 32    |
		| Foreground      | Blue  |
		| Background      | White |
		| BorderThickness | 0     |
	When "secret" is tapped
	And the text "Passw0rd" is typed
	And the frame is captured
	Then the Password of "secret" is "Passw0rd"
	And the region of "secret" has ink

Scenario: A PasswordBox draws its masking character, not the password
	Given the application shows a PasswordBox named "secret" with:
		| Property        | Value |
		| Width           | 500   |
		| Height          | 70    |
		| FontSize        | 32    |
		| Foreground      | Blue  |
		| Background      | White |
		| BorderThickness | 0     |
	When "secret" is tapped
	And the text "abcdefgh" is typed
	And the frame is captured as "masked"
	Then the Password of "secret" is "abcdefgh"
	When the PasswordChar of "secret" is set to "#"
	And the frame is captured as "hashes"
	Then the Password of "secret" is "abcdefgh"
	And the region of "secret" in frame "hashes" differs from frame "masked"

Scenario: A Password set on the tree is drawn masked
	Given the application shows a PasswordBox named "secret" with:
		| Property        | Value   |
		| Width           | 500     |
		| Height          | 70      |
		| FontSize        | 32      |
		| Foreground      | Blue    |
		| Background      | White   |
		| BorderThickness | 0       |
		| Password        | hunter2 |
	When the frame is captured
	Then the Password of "secret" is "hunter2"
	And the region of "secret" has ink
	And the ink color of "secret" is "Blue"

Scenario: An empty PasswordBox draws nothing
	Given the application shows a PasswordBox named "secret" with:
		| Property        | Value |
		| Width           | 500   |
		| Height          | 70    |
		| FontSize        | 32    |
		| Foreground      | Blue  |
		| Background      | White |
		| BorderThickness | 0     |
	When the frame is captured
	Then the Password of "secret" is ""
	And the region of "secret" is blank
