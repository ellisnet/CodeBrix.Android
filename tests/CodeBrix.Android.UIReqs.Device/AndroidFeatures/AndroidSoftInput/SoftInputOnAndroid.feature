Feature: The soft keyboard on Android
	Android-only. How a CodeBrix app's window makes room for the soft keyboard, the MAUI way: CodeBrixApplication.
	SoftInputAdjust is Pan by default (the window pans so the focused text field is above the keyboard; the page keeps its
	size), Resize on request (Core lays the page out again above the keyboard: the root withholds the keyboard's height
	from the bottom of the window, the seam the Platform's own on-screen keyboard uses), or Unspecified. An application can
	switch it while it runs. InputPane reports the keyboard in every mode. Positions are measured on the screen (the
	native views, the keyboard's top edge from the window's IME inset); fingers are REAL MotionEvents dispatched to the
	activity. The keyboard is masked in every frame.

Scenario: By default the window pans a text field under the keyboard into view and the page keeps its size
	Given the application shows a TextBox named "low" at the bottom of the panel, below a Border named "ground" painted "Teal"
	Then the window's soft-input adjust mode is "Pan"
	When a real finger taps "low"
	Then the soft keyboard is showing
	And the window is panned up
	And the caret line of "low" is above the soft keyboard
	And the page keeps its full height
	And the native views are where Core laid them out
	And the input pane reports the soft keyboard
	When the frame is captured

Scenario: In Resize mode the page's bottom edge moves up onto the keyboard's top edge
	Given the application's soft-input mode is "Resize"
	And the application shows a TextBox named "high" at the top of the panel, above a Border named "ground" painted "Teal"
	Then the window's soft-input adjust mode is "Resize"
	When a real finger taps "high"
	Then the soft keyboard is showing
	And the window is not panned
	And the bottom edge of "ground" is on the soft keyboard's top edge
	And the input pane reports the soft keyboard
	When the frame is captured

Scenario: In Resize mode a text field at the bottom of the page is laid out above the keyboard
	Given the application's soft-input mode is "Resize"
	And the application shows a TextBox named "low" at the bottom of the panel, below a Border named "ground" painted "Teal"
	When a real finger taps "low"
	Then the soft keyboard is showing
	And the window is not panned
	And "low" is above the soft keyboard
	When the frame is captured

Scenario: In Resize mode a terminal that fills the window shows its last row above the keyboard
	Given the application's soft-input mode is "Resize"
	And the application shows a TerminalView named "term" that fills the panel, fed 90 numbered lines
	When the TerminalView "term" takes the focus
	Then the soft keyboard is showing
	And the bottom edge of "term" is on the soft keyboard's top edge
	And the TerminalView "term" has fewer rows than before the keyboard
	When the frame is captured

Scenario: Switching the mode while the app runs re-applies it
	Given the application's soft-input mode is "Resize"
	And the application shows a TextBox named "high" at the top of the panel, above a Border named "ground" painted "Teal"
	When a real finger taps "high"
	Then the soft keyboard is showing
	And the bottom edge of "ground" is on the soft keyboard's top edge
	When the application switches its soft-input mode to "Pan"
	Then the window's soft-input adjust mode is "Pan"
	And the page keeps its full height
	When the application switches its soft-input mode to "Unspecified"
	Then the window's soft-input adjust mode is "Unspecified"
	And the page keeps its full height

Scenario: In Resize mode the page gets its full height back when the keyboard goes
	Given the application's soft-input mode is "Resize"
	And the application shows a TextBox named "high" at the top of the panel, above a Border named "ground" painted "Teal"
	When a real finger taps "high"
	Then the soft keyboard is showing
	And the bottom edge of "ground" is on the soft keyboard's top edge
	When the soft keyboard is dismissed
	Then the page is back to its full height

# [AP8-S batch 2] KenneyAssetBrowser: the viewer's Back button collapsed on its click, Core moved the (pointer) focus on
# to the search box, and the soft keyboard came up although no finger touched the box. WinUI raises the touch keyboard
# for a finger on the box only.
Scenario: A text field that receives the focus because the tapped button collapsed does not raise the soft keyboard
	Given the application shows a Button named "back" that collapses itself when clicked, above a TextBox named "search"
	When a real finger taps "back"
	Then Core's focus is on "search"
	And the soft keyboard is hidden
	When a real finger taps "search"
	Then the soft keyboard is showing
	When the soft keyboard is dismissed

# [AP8-S item L] A CUSTOM text control (AdvancedTextEdit, TerminalView) holds the Android focus through the soft-keyboard
# session's focus view (CoreTextInputView). In Pan mode Android pans to the FOCUSED view's rectangle, so that view is laid
# out on the control's caret and follows it: the window pans the caret above the keyboard, as it does a TextBox's caret line.
Scenario: By default the window pans an editor's caret under the keyboard into view and follows it
	Given the application shows an AdvancedTextEdit named "code" that fills the panel, holding 80 numbered lines
	When a real finger taps the last line the AdvancedTextEdit "code" shows
	Then the soft keyboard is showing
	And the window is panned up
	And the soft keyboard's focus view is on the caret of the AdvancedTextEdit "code"
	And the caret of the AdvancedTextEdit "code" is above the soft keyboard
	And the page keeps its full height
	And the native views are where Core laid them out
	When the soft keyboard commits a line break
	Then the soft keyboard's focus view is on the caret of the AdvancedTextEdit "code"
	And the caret of the AdvancedTextEdit "code" is above the soft keyboard
	When the caret of the AdvancedTextEdit "code" is hidden
	And the frame is captured
	When the page is emptied
	Then no soft-keyboard session is open
	And the window is not panned

# [AP8-S batch 4] The TerminalView add-in lays the focus view on the cursor cell (WPE1-18: TerminalControl's platform caret
# seam). The cursor is shown (the caret), so no frame is captured: it blinks.
Scenario: By default the window pans a terminal's cursor row under the keyboard into view
	Given the application shows a TerminalView named "term" that fills the panel, fed 90 numbered lines, its cursor shown
	When the TerminalView "term" takes the focus
	Then the soft keyboard is showing
	And the window is panned up
	And the soft keyboard's focus view is on the cursor cell of the TerminalView "term"
	And the cursor row of the TerminalView "term" is above the soft keyboard
	And the page keeps its full height
	When the page is emptied
	Then no soft-keyboard session is open
	And the window is not panned

# [AP8-S batch 4] The guard above caught only a text area whose Unloaded the controller had SEEN (it had a session when it
# left). Core can report the focus of a removed text area the controller never followed out of the tree (the FIRST text
# area of a run, batch 3 traces logs/ap8s3_dev_traceNoGuard/_traceGuard). The controller now reads the tree itself: a
# control that is neither loaded nor reaches its window's content gets no session. The report stands in for Core's.
Scenario: A text area that left the page without a session gets none when its focus is reported afterwards
	Given the application shows an AdvancedTextEdit named "code" that fills the panel, holding 20 numbered lines
	When the page is emptied
	And Core reports that the text area of the AdvancedTextEdit "code" has the focus
	Then no soft-keyboard session is open
	And the soft keyboard is hidden
	And the window is not panned
