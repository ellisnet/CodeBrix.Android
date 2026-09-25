Feature: Calendars and the picker presenters
	Android-only (AP10-A). A single-selection month CalendarView is the platform calendar (its month grid replaces
	CalendarPanel and the CalendarViewDayItems); a multiple-selection one keeps its Fluent template. CalendarDatePicker
	is a Material text field opening a MaterialDatePicker. DatePicker / TimePicker open Material picker dialogs, which
	replace their Fluent flyout presenters (DatePickerFlyoutPresenter, DatePickerSelector, the LoopingSelector parts,
	TimePickerFlyoutPresenter). ListPickerFlyoutPresenter and PickerFlyoutPresenter are NotImplemented in the Platform;
	it has no PickerItem.

Scenario: A single-selection CalendarView is the platform calendar within its MinDate and MaxDate
	Given the application shows the AP10 sample "calendar view" named "cal"
	Then "cal" is shown by a native CalendarView
	And the Core tree of "cal" holds no CalendarPanel
	And the Core tree of "cal" holds no CalendarViewDayItem
	And the native calendar of "cal" runs from "2026-01-01" to "2026-12-31"
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

Scenario: A multiple-selection CalendarView keeps its Fluent template with a CalendarPanel of CalendarViewDayItems (CalendarViewBaseItems)
	Given the application shows the AP10 sample "multiple selection calendar view" named "cal"
	Then "cal" is not shown by a native CalendarView
	And the Core tree of "cal" holds a CalendarPanel
	And the Core tree of "cal" holds a CalendarViewDayItem

Scenario: A CalendarDatePicker is a Material text field labelled with its Header
	Given the application shows the AP10 sample "calendar date picker" named "due"
	Then "due" is shown by a native TextInputLayout
	And the field of the CalendarDatePicker "due" shows "" labelled "Due"
	When the frame is captured

Scenario: A real finger on a CalendarDatePicker opens a Material date picker whose choice becomes its Date
	Given the application shows the AP10 sample "calendar date picker" named "due"
	And the DateChanged events of "due" are counted
	When a real finger taps "due"
	Then the Material date picker of the CalendarDatePicker is showing
	And the Core property IsCalendarOpen of "due" is "True"
	When the CalendarDatePicker's Material date picker chooses "2026-10-05"
	Then no Material date picker of a CalendarDatePicker is showing
	And the soft keyboard is not showing
	And the Core property IsCalendarOpen of "due" is "False"
	And the Core property Date of "due" is "2026-10-05"
	And "due" raised DateChanged 1 times

Scenario: Opening and closing a CalendarDatePicker's Material date picker raises Opened and Closed once each
	Given the application shows the AP10 sample "calendar date picker" named "due"
	And the Opened events of "due" are counted
	And the Closed events of "due" are counted
	When a real finger taps "due"
	Then the Material date picker of the CalendarDatePicker is showing
	And "due" raised Opened 1 times
	And "due" raised Closed 0 times
	When the CalendarDatePicker's Material date picker chooses "2026-10-05"
	Then no Material date picker of a CalendarDatePicker is showing
	And "due" raised Opened 1 times
	And "due" raised Closed 1 times
	And the Core property IsCalendarOpen of "due" is "False"

Scenario: Setting IsCalendarOpen opens the CalendarDatePicker's Material date picker
	Given the application shows the AP10 sample "calendar date picker" named "due"
	When the Core property IsCalendarOpen of "due" becomes "True"
	Then the Material date picker of the CalendarDatePicker is showing
	When the CalendarDatePicker's Material date picker chooses "2026-11-11"
	Then the Core property Date of "due" is "2026-11-11"

Scenario: A DatePicker opens a Material date picker instead of its DatePickerFlyoutPresenter, DatePickerSelector and LoopingSelector
	Given the application shows the AP10 sample "date picker" named "dp"
	When "dp" is tapped
	Then a Material date picker is showing
	And no open Core popup over "dp" holds a DatePickerFlyoutPresenter
	And no open Core popup over "dp" holds a DatePickerSelector
	And no open Core popup over "dp" holds a LoopingSelector
	And no open Core popup over "dp" holds a LoopingSelectorItem
	And no open Core popup over "dp" holds a LoopingSelectorPanel
	When the Material picker's OK button is pressed
	Then no Material picker is showing

Scenario: A TimePicker opens a Material time picker instead of its TimePickerFlyoutPresenter
	Given the application shows the AP10 sample "time picker" named "tp"
	When "tp" is tapped
	Then a Material time picker is showing
	And no open Core popup over "tp" holds a TimePickerFlyoutPresenter
	When the Material picker's OK button is pressed
	Then no Material picker is showing

Scenario: ListPickerFlyoutPresenter and PickerFlyoutPresenter are NotImplemented in the Platform and it has no PickerItem
	Then the Platform marks ListPickerFlyoutPresenter as not implemented
	And the Platform marks PickerFlyoutPresenter as not implemented
	And the Platform has no public element named PickerItem
