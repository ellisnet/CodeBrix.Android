Feature: The TerminalView on Android
	Android-only (the Platform's TerminalView group, copied as the TerminalView group, covers the terminal itself: painting,
	keys, the finger selection, the chords, the scrollback). This group fences what Android adds. The terminal draws on the
	Android canvas supply of the add-in (a Canvas shown by a native Skia view). A focused terminal opens a SOFT-KEYBOARD
	session: CodeBrix.Android.UI's text-input view takes the Android focus with the terminal profile (no suggestions, no
	full-screen keyboard), and its input connection turns what the keyboard commits into key presses in Core, which the
	terminal encodes for its host exactly as it encodes a hardware key. The keyboard's calls are made here on the
	connection the view gives an input method, as an input method makes them. The fingers and the mouse are REAL
	MotionEvents dispatched to the activity. In the host's text, <CR> is a carriage return, <DEL> the DEL byte and <ESC>
	the escape character.

Scenario: A Terminal draws through the Android canvas supply
	Given the application shows a TerminalView named "term" that records its host traffic
	Then the drawing surface of the TerminalView "term" is the Android terminal canvas
	When the TerminalView "term" is fed "HELLO"
	Then the drawing surface of the TerminalView "term" paints again
	When the frame is captured
	Then the region of "term" contains at least 0.03 percent "Yellow"

Scenario: A focused Terminal opens a soft-keyboard session with the terminal profile
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	Then the soft-keyboard session is open for "term" with the "terminal" profile
	And the soft keyboard is asked for no suggestions and no full-screen editor

Scenario: Text the soft keyboard commits reaches the host as typed
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	And the soft keyboard commits "ls -la"
	And the soft keyboard commits " /tmp"
	And the soft keyboard performs its editor action
	Then the host of "term" received "ls -la /tmp<CR>"

Scenario: A composition stays in the soft keyboard until it is finished
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	And the soft keyboard composes "hel"
	Then the host of "term" received ""
	When the soft keyboard finishes its composition
	Then the host of "term" received "hel"

Scenario: A delete with nothing left in the keyboard's field is one DEL per character
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	And the soft keyboard deletes 2 characters before the cursor
	Then the host of "term" received "<DEL><DEL>"

Scenario: Keys the soft keyboard sends as key events reach the host
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	And the soft keyboard sends the key "Del"
	And the soft keyboard sends the key "Enter"
	And the soft keyboard sends the key "DpadUp"
	Then the host of "term" received "<DEL><CR><ESC>[A"

Scenario: Letters outside ASCII reach the host unchanged
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	And the soft keyboard commits "café 中文"
	Then the host of "term" received "café 中文"

Scenario: Moving the focus off the Terminal closes the soft-keyboard session
	Given the application shows a TerminalView named "term" that records its host traffic, beside a Button named "other"
	When the TerminalView "term" takes the focus
	Then the soft-keyboard session is open for "term" with the "terminal" profile
	When the Button "other" takes the focus
	Then the soft-keyboard session is closed

Scenario: A finger on the focused Terminal brings a dismissed soft keyboard back, a mouse does not
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	And a real finger taps the TerminalView "term"
	Then the soft keyboard is showing
	When the soft keyboard is dismissed
	And a real mouse clicks the TerminalView "term"
	Then the soft keyboard is hidden
	When a real finger taps the TerminalView "term"
	Then the soft keyboard is showing
	And the soft-keyboard session is open for "term" with the "terminal" profile

Scenario: With the soft keyboard up, a real finger lands on the point it touches
	Given the system status bar is showing
	And the application shows a TerminalView named "term" that records its host traffic
	When a real finger taps the TerminalView "term"
	Then the soft keyboard is showing
	When a real finger presses the TerminalView "term" 100 DIPs right and 40 DIPs down from its top left corner
	Then the TerminalView "term" saw the finger 100 DIPs right and 40 DIPs down from its top left corner

# [AP9-4] A custom text control summons the soft keyboard for a finger or pen PRESS only - the TextBox's rule and WinUI's
# (no touch keyboard for programmatic or keyboard focus). Focus given any other way (an application's call, a page's first
# focus) opens the session - the control has the Android focus and hears every key at once - and leaves the screen clear.
Scenario: A Terminal that takes the focus programmatically shows no soft keyboard
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	Then the soft-keyboard session is open for "term" with the "terminal" profile
	And the soft keyboard is hidden
	When the soft keyboard sends the key "Enter"
	Then the host of "term" received "<CR>"

Scenario: A finger tap on the focused Terminal shows the soft keyboard, and after a dismissal the next tap shows it again
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	Then the soft keyboard is hidden
	When a real finger taps the TerminalView "term"
	Then the soft keyboard is showing
	When the soft keyboard is dismissed
	Then the soft keyboard is hidden
	And the soft-keyboard session is open for "term" with the "terminal" profile
	When a real finger taps the TerminalView "term"
	Then the soft keyboard is showing

Scenario: A finger tap on a Terminal without the focus gives it the focus and shows the soft keyboard
	Given the application shows a TerminalView named "term" that records its host traffic, beside a Button named "other"
	When the Button "other" takes the focus
	And a real finger taps the TerminalView "term"
	Then the soft-keyboard session is open for "term" with the "terminal" profile
	And the soft keyboard is showing

Scenario: InputPane.TryShow shows the soft keyboard for the focused Terminal without a touch
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	Then the soft keyboard is hidden
	When the application calls InputPane.TryShow
	Then the soft keyboard is showing
	And the soft-keyboard session is open for "term" with the "terminal" profile
	When the application calls InputPane.TryHide
	Then the soft keyboard is hidden
	And the soft-keyboard session is open for "term" with the "terminal" profile

Scenario: Out of touch mode, the window draws no focus highlight over its content
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	And the soft keyboard sends the key "DpadUp"
	Then the window is out of touch mode
	When the application shows a Grid named "blank" painted "White" that fills the panel
	And the root layout takes the Android focus
	Then the soft keyboard is hidden
	When the frame is captured
	Then the region of "blank" is uniformly "White"

Scenario: Out of touch mode, a text box that gives up the focus leaves no highlight over the window
	Given the application shows a TerminalView named "term" that records its host traffic
	When the TerminalView "term" takes the focus
	And the soft keyboard sends the key "DpadUp"
	Then the window is out of touch mode
	When the application shows a TextBox named "entry" and a Button named "other" above a Border named "ground" painted "White"
	And the element "entry" takes the focus
	And the element "other" takes the focus
	And the soft keyboard is dismissed
	When the frame is captured
	Then the region of "ground" is uniformly "White"
