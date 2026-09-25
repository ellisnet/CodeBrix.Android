Feature: Text, surfaces, popups, maps and the remaining elements
	Android-only (AP10-B). The rich-text family (RichTextBlock, RichTextBlockOverflow, RichEditBox, Block, Paragraph,
	Glyphs, InlineUIContainer), the swap-chain panels, the legacy WebView, Hub / HubSection, SemanticZoom, ParallaxView,
	AnnotatedScrollBar and MapControl are NotImplemented in the Platform (the same Core runs every head: parity maps
	nothing); Popover and MapPresenter are not in the Platform packages at all. NativePopupBase is a Popup on the native
	popup layer. ScrollView's ScrollPresenter scrolls natively; a stand-alone ColorSpectrum is the native spectrum view;
	ColorPickerSlider is the Material slider (AP3a); Line, Polygon, Polyline and every Shape are the native shape view
	(AP6).

Scenario: The rich-text family is NotImplemented in the Platform
	Then the Platform type Microsoft.UI.Xaml.Controls.RichTextBlock is marked not implemented
	And the Platform type Microsoft.UI.Xaml.Documents.Block is marked not implemented
	And the Platform type Microsoft.UI.Xaml.Documents.Glyphs is marked not implemented
	And the Platform type Microsoft.UI.Xaml.Documents.InlineUIContainer is marked not implemented
	And every property of the Platform type Microsoft.UI.Xaml.Controls.RichEditBox is marked not implemented
	And every property of the Platform type Microsoft.UI.Xaml.Controls.RichTextBlockOverflow is marked not implemented

Scenario: The swap-chain panels and the legacy WebView are NotImplemented in the Platform
	Then the Platform type Microsoft.UI.Xaml.Controls.SwapChainPanel is marked not implemented
	And the Platform type Microsoft.UI.Xaml.Controls.SwapChainBackgroundPanel is marked not implemented
	And the Platform type Microsoft.UI.Xaml.Controls.WebView is marked not implemented

Scenario: Hub, HubSection, SemanticZoom, ParallaxView, AnnotatedScrollBar and MapControl are NotImplemented in the Platform
	Then the Platform type Microsoft.UI.Xaml.Controls.Hub is marked not implemented
	And the Platform type Microsoft.UI.Xaml.Controls.HubSection is marked not implemented
	And the Platform type Microsoft.UI.Xaml.Controls.SemanticZoom is marked not implemented
	And the Platform type Microsoft.UI.Xaml.Controls.ParallaxView is marked not implemented
	And the Platform type Microsoft.UI.Xaml.Controls.AnnotatedScrollBar is marked not implemented
	And the Platform type Microsoft.UI.Xaml.Controls.MapControl is marked not implemented

Scenario: Popover and MapPresenter are not in the Platform packages
	Then no shipped assembly has a public type named Popover
	And no shipped assembly has a public type named MapPresenter

Scenario: A Popup shows its child on the native popup layer
	Given the application shows the AP10B sample "popup" named "popup"
	When the popup "popup" is opened
	And the frame is captured
	Then a popup is open
	And the open popup has ink

Scenario: A NativePopupBase is a Popup: its child shows on the native popup layer
	Given the application shows the AP10B sample "native popup base" named "popup"
	When the popup "popup" is opened
	And the frame is captured
	Then a popup is open
	And the open popup has ink

Scenario: A ScrollView's presenter scrolls natively and Core follows a real finger
	Given the application shows the AP10B sample "scroll view" named "scroll"
	When the frame is captured
	Then the Core tree of "scroll" holds a ScrollPresenter
	When a real finger scrolls "scroll" down by 300 pixels
	Then the ScrollView "scroll" is scrolled down by at least 60 DIPs in Core and natively

Scenario: A ScrollView scrolled from Core moves its native scroller
	Given the application shows the AP10B sample "scroll view" named "scroll"
	When the ScrollView "scroll" scrolls to 400 in Core
	Then the native scroller of "scroll" is at 400

Scenario: A ColorSpectrum is the native spectrum and a real finger picks a colour
	Given the application shows the AP10B sample "color spectrum" named "spectrum"
	And the ColorChanged events of "spectrum" are tallied
	When the frame is captured
	Then "spectrum" is shown by a native ColorSpectrumView
	When a real finger picks "spectrum" at 50 percent across and 50 percent down
	Then "spectrum" raised ColorChanged 1 times in all

Scenario: A ColorPickerSlider is the Material slider
	Given the application shows the AP10B sample "color picker slider" named "slider"
	Then "slider" is shown by a native Slider
	And the native slider "slider" shows 120

Scenario: Line, Polygon, Polyline and Rectangle are the native shape view
	Given the application shows the AP10B sample "line" named "line"
	Then "line" is shown by a native ShapeView
	Given the application shows the AP10B sample "polygon" named "polygon"
	Then "polygon" is shown by a native ShapeView
	Given the application shows the AP10B sample "polyline" named "polyline"
	Then "polyline" is shown by a native ShapeView
	Given the application shows the AP10B sample "rectangle" named "rectangle"
	Then "rectangle" is shown by a native ShapeView

Scenario: A MidiPlayer plays through the AudioPlayer add-in
	Given the application shows the AP10B sample "midi player" named "midi"
	Then "midi" is shown by a native MidiPlayerView
