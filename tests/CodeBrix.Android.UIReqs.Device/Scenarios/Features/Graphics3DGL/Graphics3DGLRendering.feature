@needs-graphics3dgl @needs-egl
Feature: Graphics3DGL rendering
	A GLCanvasElement brings OpenGL up when it is shown, hands its subclass a live binding to
	draw with, and presents what the subclass drew as the picture a person sees. It draws in the
	size the layout arranged it at, the right way up, and it draws again when it is invalidated -
	once per invalidation, and not at all without one.

	Deliberately out of scope here: hover (this is a touch and keyboard panel), display scale
	(pinned at 1.0), how long a frame takes (a machine with no GPU draws in software, and no
	requirement here is about speed), the exact shape of an antialiased edge (the driver decides
	that), and the animation idiom in which a canvas invalidates itself from inside its own
	drawing - a canvas that does that is dirty forever, and no frame the harness asks for would
	ever arrive. Every canvas in these scenarios is redrawn by the scenario, never by itself.

Scenario: A GLCanvasElement initialises OpenGL on this panel
	Given the application shows a TriangleCanvas named "scene" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 300   |
	When the frame is captured
	Then IsGLInitialized of "scene" is "true"
	And the GL initialisation status of "scene" is "Initialized"
	And the GL initialisation of "scene" reports no failed reason
	And the GL canvas "scene" has rendered at least 1 frames

Scenario: A GLCanvasElement clears its surface to the colour it was asked for
	Given the application shows a TriangleCanvas named "scene" with:
		| Property     | Value |
		| Width        | 400   |
		| Height       | 300   |
		| DrawTriangle | false |
		| ClearColor   | Blue  |
	And the GL canvas "scene" has rendered at least 1 frames
	When the frame is captured
	Then IsGLInitialized of "scene" is "true"
	And "scene" is 400 by 300 device pixels
	And the region of "scene" is uniformly "Blue"
	And the corner pixels of "scene" are "Blue"

Scenario: A GLCanvasElement draws the geometry it uploaded
	Given the application shows a TriangleCanvas named "scene" with:
		| Property      | Value |
		| Width         | 400   |
		| Height        | 300   |
		| ClearColor    | Blue  |
		| TriangleColor | Red   |
	And the GL canvas "scene" has rendered at least 1 frames
	When the frame is captured
	Then IsGLInitialized of "scene" is "true"
	And the region of "scene" contains at least 15 percent "Red"
	And the corner pixels of "scene" are "Blue"

Scenario: The picture a GLCanvasElement presents is not upside down
	Given the application shows a TriangleCanvas named "scene" with:
		| Property      | Value |
		| Width         | 400   |
		| Height        | 300   |
		| ClearColor    | Blue  |
		| TriangleColor | Red   |
	And the GL canvas "scene" has rendered at least 1 frames
	When the frame is captured
	Then IsGLInitialized of "scene" is "true"
	And the 120 by 20 block at 140, 240 inside "scene" is uniformly "Red"
	And the 120 by 20 block at 140, 0 inside "scene" is uniformly "Blue"

Scenario: Invalidating a GLCanvasElement draws it exactly once more, with the state it has now
	Given the application shows a TriangleCanvas named "scene" with:
		| Property     | Value |
		| Width        | 400   |
		| Height       | 300   |
		| DrawTriangle | false |
		| ClearColor   | Blue  |
	And the GL canvas "scene" has rendered at least 1 frames
	And the frame is captured as "blue"
	Then the region of "scene" in frame "blue" is uniformly "Blue"
	When the ClearColor of "scene" is set to "Red"
	And "scene" is invalidated
	And the frame is captured as "red"
	Then the render count of "scene" has grown by exactly 1
	And the region of "scene" is uniformly "Red"
	And the region of "scene" in frame "red" differs from frame "blue"

Scenario: A GLCanvasElement nobody invalidated keeps the picture it produced
	Given the application shows a TriangleCanvas named "scene" with:
		| Property     | Value |
		| Width        | 400   |
		| Height       | 300   |
		| DrawTriangle | false |
		| ClearColor   | Blue  |
	And the GL canvas "scene" has rendered at least 1 frames
	And the frame is captured as "first"
	And the render count of "scene" is noted
	When the frame is captured as "later"
	Then the render count of "scene" has not grown
	And the region of "scene" in frame "later" is unchanged from frame "first"

Scenario: Resizing a GLCanvasElement rebuilds its framebuffer and fills the new size
	Given the application shows a TriangleCanvas named "scene" with:
		| Property     | Value |
		| Width        | 400   |
		| Height       | 300   |
		| DrawTriangle | false |
		| ClearColor   | Blue  |
	And the GL canvas "scene" has rendered at least 1 frames
	And the frame is captured as "narrow"
	Then the region of "scene" in frame "narrow" is uniformly "Blue"
	When the Width of "scene" is set to "600"
	And the GL canvas "scene" has rendered at least 2 frames
	And the frame is captured as "wide"
	Then "scene" is 600 by 300 device pixels
	And the region of "scene" is uniformly "Blue"
	And the corner pixels of "scene" are "Blue"
	And the rightmost 100 pixels of "scene" are uniformly "Blue"
