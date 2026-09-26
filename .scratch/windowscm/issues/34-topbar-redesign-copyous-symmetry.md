# Issue 34: Top Bar redesign with Copyous symmetry and elegance parity

Status: resolved
Type: task
Blocked by: 33

## Context

Currently in WindowsCM:
1. **Asymmetry and Lack of Harmony**:
   - The top bar (`Top Bar`) places the search box left-aligned with a fixed width of 320px (`SearchBox`), while the 4 action buttons (Pinned, Incognito, Clear and Settings) are all crowded at the far right.
   - Between the search bar and the action buttons there is a huge, unbalanced visual void, breaking the aesthetics and the visual density.
2. **Comparison with Copyous (GNOME)**:
   - In the original Copyous, the top bar has a harmonious distribution of 3 sections:
     - **Left**: Circular/rounded system/utility controls (Settings `⚙` and Incognito Mode / Pause capture `🔕`).
     - **Center**: An elegant, centered search capsule (`[ 🔍▾  Type to search...   📌 ]`), with a type selector/dropdown on the magnifier icon and a button to toggle the pinned view (`📌`) built into the search capsule itself.
     - **Right**: A dedicated "Clear" action pill button (`Clear`).
   - This 3-column structure, with side columns of equivalent visual weight, ensures that the search capsule is perfectly centered and that the whole presents high elegance, symmetry and quick usability.

## Scope and Requirements

1. **3-Column Layout with Perfect Centering**:
   - Restructure the top container of `PopupWindow.xaml` using a `Grid` with 3 columns (`1*`, `Auto`, `1*`), guaranteeing mathematical centering of the search capsule regardless of resolution or DPI scale.
2. **Left Section (Settings and Incognito Mode)**:
   - Settings button (`⚙` - `\uE713`) with a circular/pill shape (`CornerRadius="16"`, 32x32px) and an explanatory tooltip.
   - Incognito Mode / Pause Capture button (`🔕` - `\uE727`) with a circular/pill shape, visually highlighted when incognito mode is active.
3. **Center Section (Centered Search Capsule)**:
   - Capsule with pill-style rounded borders (`CornerRadius="16"` or `"17"`, 34px tall, adaptive width ~380-440px).
   - Left side of the capsule: Search icon with a dropdown menu indicator (`🔍 ▾` - `\uE721` and `\uE70D`), allowing the filter-by-type menu to be opened (All types, Links, Code, Files, Images, Emojis, Colors, Plain Text).
   - Center of the capsule: Search text field (`SearchBox`) with the watermark / placeholder text "Type to search..." when empty.
   - Right side of the capsule: Pinned button (`📌` - `\uE718`) built into the search capsule, toggling the filter for pinned items with clear visual feedback (active/inactive).
4. **Right Section (Clear Button)**:
   - Pill-shaped button (`CornerRadius="16"`, 32px tall, generous horizontal padding) with the text "Clear", symmetrically matching the visual weight of the left group.
   - Triggers the clear while preserving protected items (`ClearKeepProtected`).
5. **Full Fluent Theme Support (Light and Dark)**:
   - Background, border, hover, text and icon colors perfectly integrated into `PopupThemeBrushes.cs` in both Light mode and Dark mode.
6. **Pure Logic and Unit Tests (TDD)**:
   - Support for an explicit type filter (`SetTypeFilter`, `TypeFilter`) in `PopupViewModel`.
   - Unit tests to guarantee that search, type filters, pins and actions remain 100% intact.

## Answer

Implementation completed successfully following Matt Pocock's TDD cycle:

1. **Pure Logic in Core (`PopupViewModel`)**:
   - Added the pure methods `SetTypeFilter(ItemKind? kind)`, `SetTagFilter(string? tag)` and `ClearAllFilters()`.
   - Dedicated unit tests in `PopupViewModelTests` covering filtering by specific types, clearing filters and preserving the selection index.
2. **3-Column Symmetry and Harmony Pattern (`PopupWindow.xaml`)**:
   - Replaced the old header with a `Grid` with `1*`, `Auto`, `1*` columns.
   - The center column holding the search capsule (420px wide, 34px tall and `CornerRadius="17"`) stays mathematically aligned to the window's central axis at any resolution or DPI.
   - Left column: elegant circular buttons for Settings (`⚙` - `\uE713`) and Incognito Mode (`🔕` - `\uE727`), totaling ~72px in width.
   - Right column: "Clear" pill button (`FluentPillButton`, min-width 74px), producing a perfect visual-weight match with the buttons on the left.
3. **Integrated Search Capsule (Copyous Parity)**:
   - Type selector `🔍 ▾` on the left edge: opens a Fluent context menu with icons and checkmarks to filter instantly by Links, Code, Files, Images, Emojis, Colors or Plain Text, dynamically updating the button's icon and semantic color.
   - Fluid watermark (`SearchPlaceholder`) showing "Type to search..." when the field is empty, disappearing immediately on typing.
   - Pinned button (`📌` - `\uE718`) built into the right end of the search capsule, highlighted in accent blue (`#0078D4`) with a white icon when active.
4. **Dynamic Fluent Palette (Light and Dark)**:
   - Added brush definitions in `PopupThemeBrushes.cs` for `SearchPlaceholderBrush`, `SearchDropdownHoverBrush`, `SearchCapsulePinHoverBrush` and the `PillButton` color family (background, border, text, hover and pressed) for both Light and Dark modes.
5. **Verification**:
   - 887 unit tests passing, 100% green (`dotnet test`).
   - Build with 0 warnings and 0 errors (`dotnet build`).

