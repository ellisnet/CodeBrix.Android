Feature: Status and feedback elements
	Android-only (AP10-B). InfoBar, RatingControl: a native Material widget drawn over the Fluent template (which keeps
	every part and behaviour; InfoBarPanel is the template's title/message panel, drawn by the card). InfoBadge and
	PersonPicture are native views. RefreshContainer keeps its template inside a native SwipeRefreshLayout whose
	indicator replaces the template's RefreshVisualizer. ProgressRing is the Material circular indicator (AP3a).
	SwipeControl keeps its template: Core's own swipe follows a real finger and its items are native buttons.
	TeachingTip opens a Core popup on the native popup layer.

Scenario: An open InfoBar is a native Material card over its template
	Given the application shows the AP10B sample "info bar" named "bar"
	When the frame is captured
	Then "bar" is shown by a native InfoBarCardView
	And the native overlay of "bar" is shown
	And "bar" shows the native text "Saved   The file is safe."
	And the Core tree of "bar" holds a InfoBarPanel
	And the InfoBar card of "bar" is filled with the theme brush "InfoBarInformationalSeverityBackgroundBrush"

Scenario: An InfoBar's severity fills its native card
	Given the application shows the AP10B sample "error info bar" named "bar"
	Then the InfoBar card of "bar" is filled with the theme brush "InfoBarErrorSeverityBackgroundBrush"
	When the Core property Severity of "bar" becomes "Success"
	Then the InfoBar card of "bar" is filled with the theme brush "InfoBarSuccessSeverityBackgroundBrush"

Scenario: A real finger on the native close button closes the InfoBar once
	Given the application shows the AP10B sample "info bar" named "bar"
	And the CloseButtonClick events of "bar" are tallied
	And the Closed events of "bar" are tallied
	When a real finger taps the native "Close" of "bar"
	Then the Core property IsOpen of "bar" is "False"
	And "bar" raised CloseButtonClick 1 times in all
	And "bar" raised Closed 1 times in all
	And the native overlay of "bar" is hidden

Scenario: A closed InfoBar takes no room and shows no card
	Given the application shows the AP10B sample "closed info bar" named "bar"
	Then the native overlay of "bar" is hidden
	And the Core size of "bar" is 400 by 0
	When the Core property IsOpen of "bar" becomes "True"
	Then the native overlay of "bar" is shown

Scenario: An InfoBar with an ActionButton keeps its template visible
	Given the application shows the AP10B sample "info bar with an action" named "bar"
	Then the native overlay of "bar" is hidden
	And "action" is shown by a native MaterialButton

Scenario: An InfoBadge with a value is a native Material badge
	Given the application shows the AP10B sample "info badge" named "badge"
	When the frame is captured
	Then "badge" is shown by a native InfoBadgeView
	And the InfoBadge "badge" shows a Value badge "5"
	And the Core size of "badge" is 16 by 16

Scenario: An InfoBadge without a value is a Material dot and takes a value later
	Given the application shows the AP10B sample "dot info badge" named "badge"
	Then the InfoBadge "badge" shows a Dot badge ""
	And the Core size of "badge" is 6 by 6
	When the Core property Value of "badge" becomes "12"
	Then the InfoBadge "badge" shows a Value badge "12"

Scenario: An InfoBadge with an icon shows the icon
	Given the application shows the AP10B sample "icon info badge" named "badge"
	Then the InfoBadge "badge" shows a Icon badge ""
	And the Core size of "badge" is 16 by 16

Scenario: A PersonPicture is a native circle with the initials of its DisplayName
	Given the application shows the AP10B sample "person picture" named "person"
	When the frame is captured
	Then "person" is shown by a native PersonPictureView
	And "person" shows the native text "AL"
	And the Core size of "person" is 96 by 96

Scenario: A PersonPicture shows its Initials and its badge number
	Given the application shows the AP10B sample "person picture with initials and a badge" named "person"
	Then "person" shows the native text "JE"
	And "person" shows the native text "3"

Scenario: A group PersonPicture shows the group glyph
	Given the application shows the AP10B sample "group person picture" named "person"
	Then the PersonPicture "person" shows the group glyph

Scenario: A PersonPicture shows the bitmap Core opened for its ProfilePicture
	Given the application shows the AP10B sample "person picture with a profile picture" named "person"
	Then the PersonPicture "person" shows its profile picture

Scenario: A RatingControl's stars are a native rating bar over its template
	Given the application shows the AP10B sample "rating control" named "rate"
	When the frame is captured
	Then "rate" is shown by a native AppCompatRatingBar
	And the native overlay of "rate" is shown
	And the rating bar of "rate" shows 0 of 5 stars
	And the Core tree of "rate" holds a StackPanel

Scenario: A real finger on the fourth star rates four and Core raises ValueChanged once
	Given the application shows the AP10B sample "rating control" named "rate"
	And the ValueChanged events of "rate" are tallied
	When a real finger taps star 4 of the rating bar "rate"
	Then the Core property Value of "rate" is "4"
	And "rate" raised ValueChanged 1 times in all
	And the rating bar of "rate" shows 4 of 5 stars

Scenario: A Value set in Core moves the native stars
	Given the application shows the AP10B sample "rating control" named "rate"
	When the Core property Value of "rate" becomes "2"
	Then the rating bar of "rate" shows 2 of 5 stars
	When the Core property MaxRating of "rate" becomes "7"
	Then the rating bar of "rate" shows 2 of 7 stars

Scenario: A read-only RatingControl ignores a real finger
	Given the application shows the AP10B sample "read-only rating control" named "rate"
	When a real finger taps star 5 of the rating bar "rate"
	Then the Core property Value of "rate" is "2"
	And the rating bar of "rate" shows 2 of 5 stars

Scenario: A ProgressRing is the Material circular indicator, spinning or determinate
	Given the application shows the AP10B sample "determinate progress ring" named "ring"
	Then "ring" is shown by a native CircularProgressIndicator
	And the native progress of "ring" is 40 percent
	When the Core property Value of "ring" becomes "75"
	Then the native progress of "ring" is 75 percent

Scenario: A RefreshContainer is a native SwipeRefreshLayout around its template
	Given the application shows the AP10B sample "refresh container" named "refresh"
	When the frame is captured
	Then "refresh" is shown by a native SwipeRefreshLayout
	And the Core tree of "refresh" holds a RefreshVisualizer
	And the native refresh indicator of "refresh" is idle

Scenario: A real finger pulling a RefreshContainer down raises RefreshRequested and the indicator follows the deferral
	Given the application shows the AP10B sample "refresh container" named "refresh"
	And the RefreshRequested events of "refresh" are tallied
	When a real finger drags "refresh" by 700 pixels down
	Then "refresh" raised RefreshRequested 1 times in all
	And the native refresh indicator of "refresh" is idle

Scenario: RequestRefresh from Core shows the native indicator until the deferral completes
	Given the application shows the AP10B sample "refresh container" named "refresh"
	And the RefreshRequested events of "refresh" are tallied
	When RequestRefresh is called on "refresh"
	Then the native refresh indicator of "refresh" is refreshing
	And "refresh" raised RefreshRequested 1 times in all
	And the native refresh indicator of "refresh" is idle

Scenario: A real finger swiping a SwipeControl's content left moves it with the finger and reveals its right items
	Given the application shows the AP10B sample "swipe control" named "swipe"
	And the input of the part "ContentRoot" of "swipe" is tallied
	When a real finger drags "swipe" by 300 pixels left
	Then the content of the SwipeControl "swipe" followed the finger left by at least 100 DIPs
	And the Core tree of "swipe" holds a AppBarButton

Scenario: A short real-finger swipe leaves a Reveal SwipeControl open showing its items
	Given the application shows the AP10B sample "swipe control" named "swipe"
	And the release of a swipe on "swipe" is measured
	When a real finger swipes "swipe" 150 pixels left and rests before lifting
	Then the SwipeControl "swipe" rests open showing its items on the right
	And the release velocity of the swipe points left

Scenario: A long real-finger swipe to the far edge leaves a Reveal SwipeControl open showing its items
	Given the application shows the AP10B sample "swipe control" named "swipe"
	And the release of a swipe on "swipe" is measured
	When a real finger swipes "swipe" 300 pixels left and rests before lifting
	And the frame is captured
	Then the SwipeControl "swipe" rests open showing its items on the right
	And the release velocity of the swipe points left

Scenario: A real finger that flicks a Reveal SwipeControl back before lifting shuts it
	Given the application shows the AP10B sample "swipe control" named "swipe"
	And the release of a swipe on "swipe" is measured
	When a real finger swipes "swipe" 250 pixels left and flicks 160 pixels back before lifting
	Then the SwipeControl "swipe" rests shut
	And the release velocity of the swipe points right

Scenario: A real-finger swipe on an Execute SwipeControl invokes its item once and shuts it
	Given the application shows the AP10B sample "execute swipe control" named "swipe"
	And the release of a swipe on "swipe" is measured
	When a real finger swipes "swipe" 200 pixels left and rests before lifting
	Then the Execute item was invoked 1 times
	And the SwipeControl "swipe" rests shut

Scenario: An opened TeachingTip is a Core popup on the native popup layer
	Given the application shows the AP10B sample "teaching tip" named "tip"
	When the TeachingTip "tip" is opened
	And the frame is captured
	Then the TeachingTip "tip" is open
	And a popup is open
	And the open popup has ink
