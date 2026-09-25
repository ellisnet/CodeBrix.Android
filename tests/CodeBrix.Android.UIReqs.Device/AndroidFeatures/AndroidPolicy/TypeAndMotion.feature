Feature: Android type scale and motion
	Android-only (not a copy of a CodeBrix.Platform feature). FontSize is in sp while IsTextScaleFactorEnabled
	(the user's font scale applies) and in dp when it is off; the framework's TextBlock styles take their Material
	type roles. Core's Storyboards run on the display's frame clock and stop asking for frames when they finish;
	with the system's animations removed, a native indeterminate indicator keeps moving only when the motion
	policy says always animate.

Scenario: A TextBlock follows the font scale while IsTextScaleFactorEnabled, and keeps its size when it is off
	Given the application shows a TextBlock named "scaled" with FontSize 20
	And the application also shows a TextBlock named "fixed" with FontSize 20 that ignores the font scale
	When the font scale is 1.5
	Then the native text size of "scaled" is 30 dp
	And the native text size of "fixed" is 20 dp
	When the font scale is the system's again
	Then the native text size of "scaled" is 20 dp

Scenario: A TextBlock with the framework's TitleTextBlockStyle gets the Material headlineSmall role
	Given the application shows a TextBlock named "title" styled "TitleTextBlockStyle"
	Then the native text size of "title" is 24 dp
	And the native line height of "title" is 32 dp

Scenario: Core animations run on the display's frames and stop asking for frames when they finish
	Given the application shows a Border named "box" 400 by 300 with Background "Red"
	When the Opacity of "box" is animated from 1 to 0 over 600 milliseconds
	Then Core animations are being ticked
	When the animation is left to finish
	Then Core animations are no longer ticked

Scenario: With the system's animations removed, an indeterminate ProgressBar keeps moving when the motion policy says always animate
	Given the motion policy is "AlwaysAnimate"
	And the application shows an indeterminate ProgressBar named "busy"
	Then the native indicator of "busy" is driven by the CodeBrix motion clock
	And the native indicator of "busy" moves within 400 milliseconds

Scenario: With the motion policy following the system, the frozen indicator is left still
	Given the motion policy is "FollowSystem"
	And the application shows an indeterminate ProgressBar named "busy"
	Then the native indicator of "busy" is not driven by the CodeBrix motion clock
