@needs-graphics3dgl @needs-egl
Feature: Graphics3DGL GPU Skia canvas
	A SkiaGLCanvasElement is the same idea as a GLCanvasElement with the raw OpenGL taken away:
	it hands a person a GPU-backed Skia surface, drawn with the ordinary canvas API, and shows
	what was drawn on it. Its surface is the size the layout arranged the element at, and its
	origin is the TOP LEFT corner - it needs none of the vertical flip a read-back framebuffer
	does, so a square drawn at 0,0 has to appear at the top left and nowhere else.

	Deliberately out of scope here: hover (this is a touch and keyboard panel), display scale
	(pinned at 1.0), and which pixel format the GPU accepted for the surface - the element falls
	back from one to another on its own, and what a person sees is the same either way.

	FOUND AND NOW FENCES: these two scenarios could not be run at all at first, because a second
	off-screen OpenGL context in one process ended the process. A SkiaGLCanvasElement takes a
	context of its own when it is loaded and gives it back when it is unloaded, and the panel's
	host used to give one back - freeing its rendering device and closing its device descriptor -
	without ending the EGL display that was still pointing at both; the next context creation
	then walked into what was left and the driver crashed, twice in six runs, taking every other
	scenario's result with it. The host now ends its display on the way out (MEASURED 2026-09-10,
	FrameBufferNativeOpenGLWrapper.Dispose). Two SkiaGLCanvasElement scenarios in one process are
	the fence: they are the thing that provoked it, and they run in every pass from here on.

Scenario: A SkiaGLCanvasElement paints its GPU surface into the picture
	Given the application shows a SkiaGlCanvas named "gpu" with:
		| Property    | Value |
		| Width       | 400   |
		| Height      | 300   |
		| ClearColor  | Blue  |
		| MarkColor   | Lime  |
		| CircleColor | Red   |
	When the frame is captured
	Then IsGpuInitialized of "gpu" is "true"
	And the GPU canvas "gpu" has painted at least 1 times
	And "gpu" is 400 by 300 device pixels
	And the region of "gpu" contains at least 5 percent "Red"
	And the region of "gpu" contains at least 5 percent "Lime"
	And the 60 by 60 block at 170, 120 inside "gpu" is uniformly "Red"

Scenario: The origin of a SkiaGLCanvasElement's surface is its top left corner
	Given the application shows a SkiaGlCanvas named "gpu" with:
		| Property    | Value |
		| Width       | 400   |
		| Height      | 300   |
		| ClearColor  | Blue  |
		| MarkColor   | Lime  |
		| CircleColor | Red   |
	When the frame is captured
	Then IsGpuInitialized of "gpu" is "true"
	And the GPU surface of "gpu" was 400 by 300
	And the 80 by 80 block at 0, 0 inside "gpu" is uniformly "Lime"
	And the 80 by 80 block at 320, 220 inside "gpu" is uniformly "Blue"
