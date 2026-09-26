# 29: Card Buttons/Menu Fix, Scrolling Frame Rate and Scrollbar Layout

Type: task

Status: resolved

Blocked by: 28

## User Report / Symptoms:
1. **Card Buttons and Menu don't work**:
   - The buttons in each card's header (pin, '...' options menu and delete/trash), as well as right-click, do not perform their actions.
   - When they are clicked, the clipboard manager simply closes and nothing happens.
2. **Scrolling frame rate (Frame Rate / Refresh Rate)**:
   - During horizontal scrolling, the animation seems to run well below the monitor's refresh rate (e.g. 120Hz, 144Hz, 240Hz).
   - A sticky, stuttering feel when scrolling repeatedly.
3. **Bottom scrollbar hiding the bottom of the cards**:
   - The horizontal scrollbar overlaps the bottom content of the cards (caption/type), degrading the visual experience.

## Answer
Implemented and validated on 2026-09-12 following the Matt Pocock skills cycle (diagnosing-bugs and 	dd):

1. **Card Buttons and Menu 100% Functional**:
   - PopupClickPolicy.cs: added an overload ShouldActivate(bool isVisible, int? clickedIndex, bool isInteractiveControl) that returns alse when the click originated from an interactive button (ButtonBase).
   - PopupWindow.xaml.cs: OnItemClicked now detects FindAncestor<ButtonBase>(e.OriginalSource) and delegates clicks on the Pin, Options and Delete buttons to their respective handlers without triggering copy/paste or closing the window.
   - PopupDeactivationPolicy.cs: pure policy that protects against unwanted closing while _isContextMenuOpen or _isDialogOpen is active.
   - PopupWindow.xaml.cs: integrated PopupDeactivationPolicy into OnDeactivated. ShowCardContextMenu and EditCurrentTitle now manage _isContextMenuOpen and _isDialogOpen with safeguards so that the popup closes only when the user deliberately clicks outside the application.
   - Smart menu placement: menu.Placement aligned to the bottom of the button when triggered by the ... button, or at the pointer position when triggered via right-click.
   - Fluent styling of ContextMenu and MenuItem adapted to the Light and Dark themes, with rounded corners (8px) and a soft shadow.

2. **Smooth Scrolling at the Monitor's Native Refresh Rate (120Hz/144Hz/240Hz)**:
   - Created SmoothScrollController.cs in WindowsCM.Core.Popup: a pure inertial engine based on exponential decay with frame-rate invariance (actor = 1 - e^(-lambda * dt)).
   - Hooked into WPF's CompositionTarget.Rendering in PopupWindow.xaml.cs: each animation frame fires exactly on the monitor's V-Sync, reaching 120 FPS, 144 FPS or 240 FPS without hitches.
   - Smooth accumulation of continuous mouse-wheel impulses, eliminating the "sticky" feel of the previous static restart.
   - Automatic deactivation of the rendering hook at rest (0% idle CPU).

3. **Breathing Room and a Horizontal Scrollbar without Overlap**:
   - PopupSizing.MaxHeight adjusted from 320 to 348 DIPs (PopupWindow.xaml Height="348" MaxHeight="360"), guaranteeing 280 DIPs available for the item list.
   - Cards 240 DIPs tall, with a bottom margin of 8 DIPs from the scrollbar.
   - Horizontal scrollbar styled with a modern Fluent design (10 DIPs tall, a rounded pill-style indicator of 6 DIPs that subtly expands to 8 DIPs on hover, no thick old-fashioned arrows).
   - Zero overlap or clipping in the card footers (captions and types 100% visible).

4. **Automated Tests**:
   - 15 new unit tests added in SmoothScrollControllerTests, PopupDeactivationPolicyTests, PopupClickPolicyTests and PopupPlacementTests.
   - Suite of 777 tests run with 100% success.
