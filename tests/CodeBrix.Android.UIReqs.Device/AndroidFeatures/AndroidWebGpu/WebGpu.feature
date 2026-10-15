Feature: The WebGpu render view on Android
	Android-only (the Platform's WebGpu UIReqs group is not copied: its scenarios run on a desktop software head). The
	WebGpuRenderView element - not in the WebGpu Core - is the Android add-in's own port; it renders on its own WebGPU
	device (Vulkan, through CodeBrix.WebGpu.Skia and the wgpu engine's natives in the APK), reads each finished frame back
	once (the Readback render path) and paints it on the SkiaSharp.Views add-in's canvas (a native Skia view). The view
	below shows its render path in a TextBlock, written from its RenderPathChanged event. The picture is checked in the view's
	own presented frame (its surface's capture), not on a screen capture: the copied region steps measure in device pixels
	at density 1 and colour-managed displays shift pure colours, so on the real devices (density 1.5 and 3.5) a screen
	region check would look in the wrong place - the screencaps of the run show the same frames.
	The second scenario turns the application-wide direct GPU rendering latch on for itself (what
	CodeBrixApplication.UseDirectGpuRenderingMode does at start-up; put back afterwards): the view then hosts a SurfaceView
	under its canvas and presents every frame to it (the DirectSurface render path) - nothing is read back, so its picture
	is checked in the SurfaceView's own buffer (PixelCopy).

Scenario: A WebGpu render view shows its frames on Android on the Readback path, under every frame driver
	Given direct GPU rendering is off
	And a WebGpuRenderView named "view" 320 by 240 clearing to "#FF0000" is on the panel, its render path shown in "path"
	Then the WebGpuRenderView "view" presents a frame within 15000 milliseconds
	And "view" is a WebGpuRenderView of the Android WebGpu add-in, shown on the Android Skia canvas view
	And the render path of the WebGpuRenderView "view" is "Readback"
	And the TextBlock "path" reads "RenderPath=Readback"
	And the picture of the WebGpuRenderView "view" is at least 95 percent "#FF0000" within 8000 milliseconds
	When the frame driver of the WebGpuRenderView "view" is "OnDemand"
	Then one Invalidate of the WebGpuRenderView "view" renders exactly 1 frame
	When the frame driver of the WebGpuRenderView "view" is "Manual"
	Then one Invalidate of the WebGpuRenderView "view" renders exactly 0 frames
	When a frame clearing to "#00FF00" is rendered into the WebGpuRenderView "view" from a worker thread
	Then the picture of the WebGpuRenderView "view" is at least 95 percent "#00FF00" within 8000 milliseconds
	And the WebGpuRenderView "view" reported no device loss

Scenario: With direct GPU rendering on, a WebGpu render view presents to a SurfaceView on the DirectSurface path
	Given direct GPU rendering is on
	And a WebGpuRenderView named "view" 320 by 240 clearing to "#FF0000" is on the panel, its render path shown in "path"
	Then the WebGpuRenderView "view" presents a frame within 15000 milliseconds
	And "view" is a WebGpuRenderView of the Android WebGpu add-in, shown on a SurfaceView under the Android Skia canvas view
	And the render path of the WebGpuRenderView "view" is "DirectSurface"
	And the TextBlock "path" reads "RenderPath=DirectSurface"
	And the window surface of the WebGpuRenderView "view" is at least 95 percent "#FF0000" within 8000 milliseconds
	When the frame driver of the WebGpuRenderView "view" is "OnDemand"
	Then one Invalidate of the WebGpuRenderView "view" renders exactly 1 frame
	When the frame driver of the WebGpuRenderView "view" is "Manual"
	Then one Invalidate of the WebGpuRenderView "view" renders exactly 0 frames
	When a frame clearing to "#00FF00" is rendered into the WebGpuRenderView "view" from a worker thread
	Then the window surface of the WebGpuRenderView "view" is at least 95 percent "#00FF00" within 8000 milliseconds
	And the WebGpuRenderView "view" reported no device loss
