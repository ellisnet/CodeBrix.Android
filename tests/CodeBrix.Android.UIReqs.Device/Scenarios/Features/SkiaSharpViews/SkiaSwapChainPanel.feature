@needs-skiasharpviews
Feature: SKSwapChainPanel
	SKSwapChainPanel is a placeholder on this head: it exists so that code written for a GPU
	swap chain compiles, and it never paints. An application that puts one on a page must be
	told so at once rather than left with an element that silently shows nothing, so building
	one is refused. An application that has deliberately opted out of the refusal gets the
	silent element instead - and it is empty: no surface, no graphics device, and a handler
	that is never called.

	The opt-out is a switch of the whole process, so a scenario that turns it off puts it back
	afterwards; the refusal is what every other scenario of this assembly runs against.

Scenario: Building an SKSwapChainPanel is refused on this head
	Given the application has not opted out of the SKSwapChainPanel refusal
	And the application shows nothing
	When an SKSwapChainPanel is constructed
	Then the construction was refused with "not supported"

Scenario: An SKSwapChainPanel that has opted out of the refusal is an empty element
	Given the application has opted out of the SKSwapChainPanel refusal
	And the application shows a SkiaSwapChainPanel named "panel" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 300   |
	When the frame is captured as "shown"
	Then "panel" is 400 by 300 device pixels
	And the region of "panel" is blank
	And the canvas size of "panel" is empty
	And the graphics context of "panel" is none
	When "panel" is invalidated
	And the frame is captured as "invalidated"
	Then the panel is blank
	And the PaintSurface of "panel" was never raised
	And the region of "panel" in frame "invalidated" is unchanged from frame "shown"
