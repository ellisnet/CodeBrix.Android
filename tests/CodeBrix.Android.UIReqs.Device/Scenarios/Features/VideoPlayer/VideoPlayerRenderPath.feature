@needs-videoplayer
Feature: VideoPlayer render path and failures
	A VideoPlayer composes its picture on the graphics device wherever the running head can supply
	an off-screen context, and on the processor everywhere else. RenderPath says which is wanted
	and ActiveRenderPath says which is running - a distinction worth having, because asking for the
	graphics device is permission rather than a promise. The path is chosen before a source is
	opened and may not be changed after; and a source that cannot be opened has to say so in words
	a person can act on, rather than leaving a picture that never arrives.

	Which path a player left to choose comes up on is a fact about the MACHINE, not about the
	add-in, so exactly one scenario here leaves the choice open and it accepts either answer. Every
	other scenario in this pair pins the processor path, which is the answer that is the same
	everywhere.

	Deliberately out of scope: hover, display scale, the effect chain (a grade on the processor
	path needs an opt-in these requirements do not turn on) and the GpuNoFallback refusal, which
	needs a machine with no graphics device to mean anything.

Scenario: A player pinned to the processor path says that is what is running
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player" with:
		| Property   | Value |
		| RenderPath | Cpu   |
	And the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	When "player" has presented a frame within 8000 milliseconds
	Then the ActiveRenderPath of "player" is "Cpu"
	And the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds

Scenario: A player left to choose its path comes up on one of them and says which
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player" with:
		| Property   | Value    |
		| RenderPath | GpuAuto  |
	And the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	When "player" has presented a frame within 8000 milliseconds
	Then the ActiveRenderPath of "player" is Gpu or Cpu
	And the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds

Scenario: Changing the render path while a clip is open is refused, and changes nothing
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player" with:
		| Property   | Value |
		| RenderPath | Cpu   |
	And the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	And the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds
	And the frame is captured as "before-the-attempt"
	When the RenderPath of "player" is changed to "GpuAuto" with the clip open
	And the frame is captured as "after-the-attempt"
	Then the change to the RenderPath of "player" was refused
	And the RenderPath of "player" is still "Cpu"
	And the ActiveRenderPath of "player" is "Cpu"
	And the region of "player" in frame "after-the-attempt" is unchanged from frame "before-the-attempt"

Scenario: A source that names no file reports a failure and leaves the picture blank
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player"
	When the Source of "player" is set to "no-such-clip.mkv"
	And the frame is captured
	Then the MediaFailed of "player" was raised at least once
	And the failure of "player" explains what went wrong
	And the DurationSeconds of "player" is about 0.0
	And the region of "player" is blank
