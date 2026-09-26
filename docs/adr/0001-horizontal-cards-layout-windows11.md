# 1. Horizontal Cards Layout and Windows 11 Visual Identity

Date: 2026-09-12

## Context

WindowsCM was conceived for parity with the Copyous clipboard manager (GNOME). During the initial prototype (Variant A - Cards), the horizontal card format (250x170px) was validated as providing excellent density and preview area for inspecting large blocks of code and images. However, the first popup implementation restricted the layout to a narrow vertical window (380x520px), truncating the preview to a single 40px line and limiting the top bar buttons to literal text labels that got cut off at different resolutions.

In addition, the display of file and image items exposed absolute operating system paths (`C:\Users\...` or `file:///C:/Users/.../AppData/Local/.../hash.png`), creating visual clutter and hurting usability.

## Decision

1. **Return to the Horizontal Cards Layout (Copyous Variant A):**
   - The popup now uses a width of 880px and a height of 320px.
   - The history list shows horizontal cards (`Width="250"`, `Height="230"`) aligned side by side, with support for smooth horizontal scrolling via the mouse scroll wheel and the navigation arrows (Left/Right).

2. **Isolation of the Display Logic (`ItemDisplayFormatter`):**
   - Creation of a pure formatter in `WindowsCM.Core.Popup` to isolate and unit test:
     - Titles: hides absolute paths and internal URIs, showing only the file name (`Path.GetFileName`) for files and "Image" for direct captures.
     - Types: classifies extensions into friendly categories ("PNG Image", "MP4 Video", "MP3 Audio", "C# Code", "PDF Document").
     - Multi-line previews: allows showing 6 to 8 lines of code or text in a monospaced font (`Cascadia Code` / `Consolas`).
     - Thumbnail path resolution: supports both cached images and image files copied from Windows Explorer.

3. **Action Buttons with Segoe Fluent Icons (Windows 11):**
   - Replacement of the text buttons with compact icon buttons (32x32px) using the native `Segoe Fluent Icons` typeface (with fallback to `Segoe MDL2 Assets`).
   - Each button has a ToolTip with a description of the action and its keyboard shortcut (`Alt+P`, `Ctrl+Shift+Alt+V`, etc.).

4. **Fluent Design Visual Identity:**
   - Dark surface (`#202020`), subtle borders (`#383838`), rounded corners (`CornerRadius="8"`) and soft shadows.
   - Active highlight for card selection and filters with the system accent color (`#0078D4` / `#ED5B00`).

## Consequences

- **Positive:**
  - Copied source code and text become immediately readable in the preview before pasting.
  - The user quickly identifies files and photos by name and thumbnail, without a confusing display of system paths.
  - Interface aligned with the Windows 11 aesthetic guidelines.
  - Zero text clipping on buttons, improving international consistency and ergonomics.
  - Logic 100% covered by automated tests.
- **Negative / Challenges:**
  - The popup takes up more screen width (880px), requiring deterministic clamping near the monitor edges (kept and tested in `PopupPlacement`).
