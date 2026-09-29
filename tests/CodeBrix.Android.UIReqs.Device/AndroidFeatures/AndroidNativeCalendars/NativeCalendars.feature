Feature: Native month calendars
	Run in a separate app process per group. Drawing a native month calendar can change
	later edge antialiasing in the Android renderer; the group restart isolates that state.

Scenario: A single-selection CalendarView is the platform calendar within its MinDate and MaxDate
	Given the application shows the AP10 sample "calendar view on a fixed day" named "cal"
	Then "cal" is shown by a native CalendarView
	And the Core tree of "cal" holds no CalendarPanel
	And the Core tree of "cal" holds no CalendarViewDayItem
	And the native calendar of "cal" runs from "2026-01-01" to "2026-12-31"
	And the native calendar of "cal" shows "2026-03-14"
	When the frame is captured

Scenario: A date selected in Core is the day the native calendar shows
	Given the application shows the AP10 sample "calendar view" named "cal"
	When the selected date of "cal" is set to "2026-03-14"
	Then the native calendar of "cal" shows "2026-03-14"

Scenario: A day picked on the native calendar becomes the one selected date and Core raises SelectedDatesChanged
	Given the application shows the AP10 sample "calendar view" named "cal"
	And the SelectedDatesChanged events of "cal" are counted
	When the day "2026-05-20" is picked on the native calendar of "cal"
	Then the selected dates of "cal" are "2026-05-20"
	And "cal" raised SelectedDatesChanged 1 times

