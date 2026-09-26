# Issue 35: Ephemeral Incognito Mode, Full Cleanup and Unmistakable Visual Feedback

Status: resolved
Type: fix
Blocked by: 34

## Context

The incognito (anonymous) mode feature showed dysfunctional behaviors:
1. **Discarded Capture**: `CaptureService` returned `null` immediately if `IsIncognito` was active. Clips copied in incognito mode were not saved, nor could the user view/paste them.
2. **State Desynchronization**: Hotkeys and tray options forced conflicting values on the popup and the monitor.
3. **No Ephemeral Repository**: There was no isolation between temporary storage and the persistent on-disk SQLite database.
4. **Imperceptible Visual Feedback**: The only indicator was the color of a small button, with no notices, banner or highlight.

## Requirements

1. **Ephemeral In-Memory Session**:
   - Copies made during incognito mode are stored in an in-memory database (`:memory:`) and an isolated temporary directory.
   - Clips from the incognito session are available in the popup for browsing, searching and pasting.
2. **Irreversible Cleanup on Deactivation**:
   - As soon as the user turns off incognito mode, the temporary session is destroyed, the in-memory database is discarded and all ephemeral image files are deleted from disk.
   - No trace is written to the persistent SQLite database.
3. **Clear and Unmistakable Visual Feedback**:
   - Prominent top banner `INCOGNITO MODE ACTIVE` with a quick-exit button ("Exit incognito").
   - Windows 11 Fluent purple accent on the popup (border and top).
   - Highlighted button in the toolbar and an indication in the tray menu (`✓ Incognito mode (active)`).
   - A contextual empty state when no clips have been copied in the session yet.
4. **TDD Cycle**:
   - Unit tests covering isolation, ephemeral capture, full cleanup and synchronization.
