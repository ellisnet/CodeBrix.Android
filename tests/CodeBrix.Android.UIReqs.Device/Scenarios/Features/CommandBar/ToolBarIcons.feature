@needs-commandbar
Feature: A tool bar icon takes its colour from the bar
	Artwork drawn in currentColor must be painted in the tint the button was given, artwork that
	states a colour of its own must keep it, and a button given two pieces of artwork must swap to
	the dark one the moment the theme around it goes dark.

	Deliberately out of scope here. The display scale is pinned at 1.0, so the story about
	rasterising an icon again for a different scale belongs to the add-in's own unit suite; and the
	raster (PNG) icon source is proven there too, because what a UIReqs scenario can see of it - a
	square of colour on a button - is what the vector scenarios below already show.

Scenario: A currentColor icon takes the button's tint and a stated colour keeps its own
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind       | Name   | Icon   | IconTint |
		| ToolButton | tinted | block  | #2266DD  |
		| ToolButton | stated | stated | #2266DD  |
	And the icons of "bar" are loaded
	When the frame is captured
	Then the region of "tinted" contains at least 40 percent "#2266DD"
	And the region of "tinted" does not contain "Black"
	And the region of "stated" contains at least 40 percent "#00AA00"
	And the region of "stated" does not contain "#2266DD"

Scenario: A light and dark artwork pair swaps when the theme around the bar changes
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind       | Name  | IconArtwork    | DarkIconArtwork |
		| ToolButton | theme | icon-light.svg | icon-dark.svg   |
	And the icons of "bar" are loaded
	When the frame is captured as "light"
	Then the icon of "theme" is drawn from "icon-light.svg"
	And the region of "theme" contains at least 40 percent "Blue"
	When the root theme is set to "Dark"
	And the icons of "bar" are loaded
	And the frame is captured
	Then the icon of "theme" is drawn from "icon-dark.svg"
	And the region of "theme" contains at least 40 percent "Orange"
	And the region of "theme" in frame "light" does not contain "Orange"
