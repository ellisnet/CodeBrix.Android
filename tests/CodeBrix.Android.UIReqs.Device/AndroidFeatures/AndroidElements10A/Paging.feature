Feature: Selection and paging elements
	Android-only (AP10-A). Pivot and SelectorBar show a Material tab row over their Fluent template (their header
	parts PivotHeaderPanel / PivotHeaderItem are laid out by Core, drawn by the tab row); FlipView pages through a
	native RecyclerView of FlipViewItems; PipsPager, PagerControl and BreadcrumbBar are native rows of Material parts;
	RadioButtons' items are Material radio buttons; ItemsView and grouped lists keep their Fluent template; ListBox,
	ListBoxItem and GroupItem are NotImplemented in the Platform.

Scenario: A Pivot's PivotHeaderPanel is drawn by a native tab row and a real finger on a header selects its PivotItem
	Given the application shows the AP10 sample "pivot" named "pivot"
	Then the element "pivot" shows a native tab strip with 3 tabs
	And the Core tree of "pivot" holds a PivotHeaderPanel
	And the Core tree of "pivot" holds a PivotHeaderItem
	When a real finger taps the native "Three" of "pivot"
	Then the Core property SelectedIndex of "pivot" is "2"

Scenario: A PivotPanel standing alone is a Core panel (the Fluent Pivot template does not use it)
	Given the application shows the AP10 sample "pivot panel" named "panel"
	Then "panel" is not shown by a native TabLayout
	And "panel" is shown by a native CodeBrixViewGroup

Scenario: A NativePivotPresenter standing alone is laid out by Core (the Fluent Pivot does not use it)
	Given the application shows the AP10 sample "native pivot presenter" named "presenter"
	Then "presenter" is not shown by a native TabLayout

Scenario: A SelectorBar's items are a native tab row and a real finger on a SelectorBarItem selects it
	Given the application shows the AP10 sample "selector bar" named "bar"
	Then the element "bar" shows a native tab strip with 3 tabs
	When a real finger taps the native "Shared" of "bar"
	Then the Core property IsSelected of "shared" is "True"

Scenario: A FlipView pages through its FlipViewItems in a native RecyclerView
	Given the application shows the AP10 sample "flip view" named "flip"
	Then "flip" is shown by a native RecyclerView
	And the Core tree of "flip" holds a FlipViewItem
	When the Core property SelectedIndex of "flip" becomes "2"
	Then "flip" shows the native text "three"

Scenario: A PipsPager is a native row of pips and a real finger on a pip selects its page
	Given the application shows the AP10 sample "pips pager" named "pips"
	And the SelectedIndexChanged events of "pips" are counted
	Then "pips" is shown by a native PipsPagerView
	And the PipsPager "pips" shows 5 pips from page 1 with page 1 selected
	When the frame is captured
	When a real finger taps pip 3 of the PipsPager "pips"
	Then the Core property SelectedPageIndex of "pips" is "2"
	And "pips" raised SelectedIndexChanged 1 times

Scenario: A PipsPager with more pages than MaxVisiblePips keeps the selected pip centred
	Given the application shows the AP10 sample "long pips pager" named "pips"
	Then the PipsPager "pips" shows 5 pips from page 5 with page 7 selected

Scenario: A wrapping PipsPager's next button goes from the last page to the first
	Given the application shows the AP10 sample "wrapping pips pager" named "pips"
	When the Core property SelectedPageIndex of "pips" becomes "2"
	And a real finger taps the native "Next page" of "pips"
	Then the Core property SelectedPageIndex of "pips" is "0"

Scenario: A PagerControl is a native row whose next button and page drop-down choose pages
	Given the application shows the AP10 sample "pager control" named "pager"
	And the SelectedIndexChanged events of "pager" are counted
	Then "pager" is shown by a native PagerControlView
	And the PagerControl "pager" shows a DropDown selector showing "1"
	And "pager" shows the native text "of 5"
	And "pager" has a disabled native "Previous page"
	When the frame is captured
	When a real finger taps the native "Next page" of "pager"
	Then the Core property SelectedPageIndex of "pager" is "1"
	When page 4 is chosen from the drop-down of the PagerControl "pager"
	Then the Core property SelectedPageIndex of "pager" is "3"
	And the PagerControl "pager" shows a DropDown selector showing "4"
	And "pager" raised SelectedIndexChanged 2 times

Scenario: A PagerControl in NumberBox mode takes a typed page number
	Given the application shows the AP10 sample "number box pager" named "pager"
	Then the PagerControl "pager" shows a NumberField selector showing "1"
	When "12" is typed into the number field of the PagerControl "pager"
	Then the Core property SelectedPageIndex of "pager" is "11"

Scenario: A PagerControl in ButtonPanel mode shows page-number buttons that a real finger chooses
	Given the application shows the AP10 sample "button panel pager" named "pager"
	Then the PagerControl "pager" shows a ButtonPanel selector showing ""
	When a real finger taps the native "Page 3" of "pager"
	Then the Core property SelectedPageIndex of "pager" is "2"

Scenario: A BreadcrumbBar is a native row of crumbs whose real tap raises ItemClicked with the BreadcrumbBarItem's item
	Given the application shows the AP10 sample "breadcrumb bar" named "crumbs"
	And the ItemClicked events of "crumbs" are counted
	Then "crumbs" is shown by a native BreadcrumbBarView
	And the BreadcrumbBar "crumbs" shows the crumbs "Home, Documents, Design" with 2 buttons
	When the frame is captured
	When a real finger taps the native "Documents" of "crumbs"
	Then "crumbs" raised ItemClicked 1 times
	And the last ItemClicked of "crumbs" was "1:Documents"

Scenario: A crumb added to a BreadcrumbBar's collection appears at its end
	Given the application shows the AP10 sample "breadcrumb bar" named "crumbs"
	When the crumb "Drafts" is added to "crumbs"
	Then the BreadcrumbBar "crumbs" shows the crumbs "Home, Documents, Design, Drafts" with 3 buttons

Scenario: ListBox, ListBoxItem and GroupItem are Core controls and a ListBox keeps its Fluent template
	AP1.12 (pin 1.0.270.342): WPE1-10 implemented ListBox and ListBoxItem and WPE1-8 GroupItem in the Platform Core (AP10-A
	pinned them as NotImplemented); a ListBox keeps its Core template, with a ListBoxItem per item.
	Then the Platform implements ListBox
	And the Platform implements ListBoxItem
	And the Platform implements GroupItem
	Given the application shows the AP10 sample "list box" named "list"
	Then "list" is not shown by a native RecyclerView
	And the Core tree of "list" holds a ListBoxItem
	And "list" shows the native text "a"

Scenario: An ItemsView keeps its Fluent template with an ItemContainer per item
	Given the application shows the AP10 sample "items view" named "items"
	Then the Core tree of "items" holds a ItemContainer
	And "items" shows the native text "Green"

Scenario: A grouped ListView keeps its Fluent template and shows the items of every group
	Given the application shows the AP10 sample "grouped list view" named "list"
	Then "list" is not shown by a native RecyclerView
	And "list" shows the native text "Apple"
	And "list" shows the native text "Leek"

Scenario: A grouped ListView shows a ListViewHeaderItem (a ListViewBaseHeaderItem) per group
	Given the application shows the AP10 sample "grouped list view" named "list"
	Then the Core tree of "list" holds a ListViewHeaderItem
	And "list" shows the native text "Fruit"

Scenario: A grouped GridView keeps its Fluent template and shows the items of every group
	Given the application shows the AP10 sample "grouped grid view" named "grid"
	Then "grid" is not shown by a native RecyclerView
	And "grid" shows the native text "Leek"

Scenario: A grouped GridView shows a GridViewHeaderItem per group
	Given the application shows the AP10 sample "grouped grid view" named "grid"
	Then the Core tree of "grid" holds a GridViewHeaderItem
	And "grid" shows the native text "Vegetables"

Scenario: RadioButtons shows its items as Material radio buttons and a real finger selects one
	Given the application shows the AP10 sample "radio buttons" named "radios"
	Then "radios" is shown by a native MaterialRadioButton
	When a real finger taps the native "Medium" of "radios"
	Then the Core property SelectedIndex of "radios" is "1"

Scenario: A CarouselPanel standing alone is a Core panel
	Given the application shows the AP10 sample "carousel panel" named "carousel"
	Then "carousel" is not shown by a native RecyclerView
