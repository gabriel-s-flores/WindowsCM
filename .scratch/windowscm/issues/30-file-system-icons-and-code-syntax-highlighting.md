# 30: System Icons for Files and Syntax Highlighting for Code

Type: task

Status: resolved

Blocked by: 29

## User Report / Requirements:
1. **Handling and rich display of copied files**:
   - Copied files currently show the raw path (`C:\...`) in the card preview, which is not intuitive.
   - Show files with their respective Windows system icons (Shell32/SHGetFileInfo).
   - Show the file's friendly name, size and type, with a layout adapted for multiple files.
   - Guarantee full copy-back (CF_HDROP for Explorer and paste compatibility).
2. **Code syntax highlighting (Copyous parity)**:
   - Copied code must have its reserved words (keywords), types, strings and comments highlighted in distinct colors in the card preview.

## Answer
Implemented and validated on 2026-09-12 following Matt Pocock's skills (codebase-design, domain-modeling and tdd):

1. **Rich Display and Windows System Icons for Files (`ItemKind.File` and `ItemKind.Files`)**:
   - Created `FileIconService.cs` in `WindowsCM.App`: a Win32 adapter that calls `SHGetFileInfoW` with safe handle destruction (`DestroyIcon`) and a thread-safe in-memory cache per extension/file. It converts the native Windows icons into frozen `BitmapSource`s (zero re-rendering overhead and 0ms latency).
   - Created `FileDisplayHelper.cs` in `WindowsCM.Core.Popup`: a pure module for formatting file sizes (`FormatFileSize`: B, KB, MB, GB), friendly names, extensions and a clean summary of multiple files.
   - Created a dedicated `FilePreviewVisibility` case in the `PopupWindow.xaml` card template: it replaces the raw path view with an elegant Fluent card showing the official Windows icon (32x32/48x48), a prominent name, size and type (`PDF Document • 2.4 MB`), plus an enumerated summary for multiple files.

2. **Code Syntax and Reserved Word Highlighting (Copyous parity)**:
   - Created `CodeSyntaxTokenizer.cs` in `WindowsCM.Core.Previews`: a fast, pure lexer that identifies reserved words (`class`, `function`, `public`, `return`, `async`, `def`, `import`, etc.), types, strings, comments, numbers and operators.
   - Added automatic detection of common languages (C#, JavaScript, Python, SQL, Rust, Go, HTML) integrated into `ItemDisplayFormatter.GetTypeLabel` ("Code (C#)", etc.).
   - Created `SyntaxHighlightHelper` in `WindowsCM.App`: an attached property that fills the `Inlines` of a `TextBlock` with `Run` elements colored dynamically according to the Windows 11 Light and Dark themes (`CodeKeywordBrush`, `CodeTypeBrush`, `CodeStringBrush`, `CodeCommentBrush`, etc.).

3. **Unit Tests (TDD)**:
   - 21 new tests added in `FileDisplayHelperTests.cs` and `CodeSyntaxTokenizerTests.cs`.
   - Suite of 798 tests run with 100% success.
