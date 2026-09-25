Feature: GL canvases draw again on their own when invalidated
	Android-only (AP7-A; not a copy of a CodeBrix.Platform feature). The copied Graphics3DGL scenarios capture a frame
	while they wait for a canvas to draw, and a capture is itself a drawing pass - so they cannot tell a canvas that
	draws because it was invalidated from one that only draws when something else redraws the window. These scenarios
	never capture a frame between the invalidation and the check: an invalidated GLCanvasElement or SkiaGLCanvasElement
	has to draw again by itself, through its own native view (FOUND in the KenneyAssetBrowser 3D viewer: an orbit drag
	invalidated the canvas and nothing was drawn until something else redrew the window).

Scenario: A GLCanvasElement is shown by the native GL picture view
	Given the application shows a TriangleCanvas named "scene" with:
		| Property     | Value |
		| Width        | 400   |
		| Height       | 300   |
		| DrawTriangle | false |
		| ClearColor   | Blue  |
	Then the GL canvas "scene" draws within 10 seconds without a frame being captured
	And "scene" is shown by the native GL picture view

Scenario: An invalidated GLCanvasElement draws again without a frame being captured
	Given the application shows a TriangleCanvas named "scene" with:
		| Property     | Value |
		| Width        | 400   |
		| Height       | 300   |
		| DrawTriangle | false |
		| ClearColor   | Blue  |
	And the GL canvas "scene" draws within 10 seconds without a frame being captured
	When the GL canvas "scene" is invalidated 3 times, a second apart, without a frame being captured
	Then the GL canvas "scene" drew once for every invalidation

Scenario: An invalidated SkiaGLCanvasElement paints again without a frame being captured
	Given the application shows a SkiaGlCanvas named "gpu" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| ClearColor | Blue  |
	And the GPU canvas "gpu" paints within 10 seconds without a frame being captured
	When the GPU canvas "gpu" is invalidated 3 times, a second apart, without a frame being captured
	Then the GPU canvas "gpu" painted once for every invalidation
	And "gpu" is shown by the native GL picture view
