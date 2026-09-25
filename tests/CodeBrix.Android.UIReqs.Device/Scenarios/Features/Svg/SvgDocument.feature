@needs-svg
Feature: What an SvgImageSource makes of its document
	An SvgImageSource parses one SVG document and reports what it made of it. A stylesheet handed
	to it before it parses decides what a document's currentColor means, so one file can be drawn
	in any colour without being rewritten; a document the parser cannot read leaves the Image empty
	and says why rather than failing silently; and a source told to rasterize draws from a bitmap
	of exactly the size it asked for instead of from the vector picture.
	Deliberately out of scope: hover, display scale (pinned at 1.0, so a logical pixel is a device
	pixel and a source asked for 32 rasterizes 32) and any document fetched over a network.

Scenario: A stylesheet set before the document is parsed tints its currentColor
	Given the application shows an SvgImage named "pic" with:
		| Property | Value                 |
		| Width    | 200                   |
		| Height   | 200                   |
		| Stretch  | Fill                  |
		| SvgCss   | * { color: #2266DD; } |
	And the SVG "CurrentColor" of "pic" is loaded
	When the frame is captured
	Then the SVG of "pic" loaded with status "Success"
	And the region of "pic" is uniformly "#2266DD"

Scenario: Without a stylesheet a currentColor document is black
	Given the application shows an SvgImage named "pic" with:
		| Property | Value |
		| Width    | 200   |
		| Height   | 200   |
		| Stretch  | Fill  |
	And the SVG "CurrentColor" of "pic" is loaded
	When the frame is captured
	Then the region of "pic" is uniformly "Black"

Scenario: A document the parser cannot read leaves the Image empty and says why
	Given the application shows an SvgImage named "pic" with:
		| Property | Value |
		| Width    | 200   |
		| Height   | 200   |
		| Stretch  | Fill  |
	When the SVG "Malformed" of "pic" is loaded
	And the frame is captured
	Then the SVG of "pic" loaded with status "InvalidFormat"
	And "pic" is 200 by 200 device pixels
	And the region of "pic" is blank

Scenario: A source told to rasterize draws from a bitmap of exactly that size
	Given the application shows an SvgImage named "pic" with:
		| Property             | Value |
		| Width                | 32    |
		| Height               | 32    |
		| Stretch              | Fill  |
		| RasterizePixelWidth  | 32    |
		| RasterizePixelHeight | 32    |
	And the SVG "CurrentColor" of "pic" is loaded
	When the frame is captured
	Then the rasterized size of "pic" is 32 by 32 device pixels
	And the region of "pic" has ink
	And the ink color of "pic" is "Black"

Scenario: A source given only one rasterize dimension stays in vector mode
	Given the application shows an SvgImage named "pic" with:
		| Property            | Value |
		| Width               | 200   |
		| Height              | 200   |
		| Stretch             | Fill  |
		| RasterizePixelWidth | 32    |
	And the SVG "TwoHalves" of "pic" is loaded
	When the frame is captured
	Then the rasterized size of "pic" is empty
	And the leftmost 90 pixels of "pic" are uniformly "Red"
	And the rightmost 90 pixels of "pic" are uniformly "Blue"
