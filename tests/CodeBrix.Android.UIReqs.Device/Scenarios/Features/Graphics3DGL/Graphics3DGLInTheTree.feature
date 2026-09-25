@needs-graphics3dgl @needs-egl
Feature: Graphics3DGL in the tree
	A GLCanvasElement is an ordinary element of the tree as well as a window onto OpenGL. XAML
	put inside it is composited above its picture; everything beside it goes on being drawn by
	the panel's own renderer, which shares the very same OpenGL context; taking it out of the
	tree gives its resources back and putting it in again sets them up afresh. And when OpenGL
	cannot be had - a shader the driver refuses, say - the element says so in words rather than
	throwing out of the framework's own load, because a blank rectangle explains nothing.

	Deliberately out of scope here: hover (this is a touch and keyboard panel), display scale
	(pinned at 1.0), and what any particular driver's compiler puts in its message - the
	requirement is that there IS one, not what it says.

Scenario: XAML inside a GLCanvasElement is composited above its picture
	Given the application shows a TriangleCanvas named "scene" with:
		| Property     | Value |
		| Width        | 400   |
		| Height       | 300   |
		| DrawTriangle | false |
		| ClearColor   | Blue  |
	And the layout "scene" holds a Border named "badge" with:
		| Property   | Value |
		| Width      | 120   |
		| Height     | 80    |
		| Background | Lime  |
	And the GL canvas "scene" has rendered at least 1 frames
	When the frame is captured
	Then IsGLInitialized of "scene" is "true"
	And "badge" is 120 by 80 device pixels
	And the region of "badge" is uniformly "Lime"
	And the corner pixels of "scene" are "Blue"

Scenario: A GLCanvasElement leaves the panel's own rendering alone
	Given the application shows a StackPanel named "board" with:
		| Property    | Value      |
		| Orientation | Horizontal |
		| Width       | 600        |
		| Height      | 300        |
	And the layout "board" holds a Border named "witness" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 300   |
		| Background | Lime  |
	And the layout "board" holds a TriangleCanvas named "scene" with:
		| Property      | Value |
		| Width         | 400   |
		| Height        | 300   |
		| ClearColor    | Blue  |
		| TriangleColor | Red   |
	And the GL canvas "scene" has rendered at least 1 frames
	And the frame is captured as "before"
	Then IsGLInitialized of "scene" is "true"
	And the region of "witness" in frame "before" is uniformly "Lime"
	When the ClearColor of "scene" is set to "Navy"
	And "scene" is invalidated
	And the frame is captured as "after"
	Then the region of "witness" is uniformly "Lime"
	And nothing outside "scene" changed between frames "before" and "after"

Scenario: A GLCanvasElement taken out of the tree gives its resources back
	Given the application shows a TriangleCanvas named "scene" with:
		| Property     | Value |
		| Width        | 400   |
		| Height       | 300   |
		| DrawTriangle | false |
		| ClearColor   | Blue  |
	And the GL canvas "scene" has rendered at least 1 frames
	And the frame is captured
	Then IsGLInitialized of "scene" is "true"
	And the GL canvas "scene" has been set up 1 time and torn down 0 times
	When the GL canvas "scene" is taken out of the tree
	And the frame is captured
	Then IsGLInitialized of "scene" is "null"
	And the GL initialisation status of "scene" is "NotYetInitialized"
	And the GL canvas "scene" has been set up 1 time and torn down 1 time
	And the panel is blank

Scenario: A GLCanvasElement put back into the tree sets OpenGL up again and draws again
	Given the application shows a TriangleCanvas named "scene" with:
		| Property     | Value |
		| Width        | 400   |
		| Height       | 300   |
		| DrawTriangle | false |
		| ClearColor   | Blue  |
	And the GL canvas "scene" has rendered at least 1 frames
	And the frame is captured
	And the GL canvas "scene" is taken out of the tree
	And the frame is captured
	Then IsGLInitialized of "scene" is "null"
	When the GL canvas "scene" is put back into the tree
	And the GL canvas "scene" has rendered at least 2 frames
	And the frame is captured
	Then IsGLInitialized of "scene" is "true"
	And the GL initialisation status of "scene" is "Initialized"
	And the GL canvas "scene" has been set up 2 times and torn down 1 time
	And the region of "scene" is uniformly "Blue"

Scenario: A shader the driver cannot compile is reported, not thrown
	Given the application shows a BrokenShaderCanvas named "broken" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| ClearColor | Blue  |
	When the frame is captured
	Then IsGLInitialized of "broken" is "false"
	And the GL initialisation status of "broken" is "InitializationFailed"
	And the GL initialisation of "broken" reports a failed reason
	And the region of "broken" is blank
