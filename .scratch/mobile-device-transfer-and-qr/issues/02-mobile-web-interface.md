Status: resolved
Type: task

## Answer
Implemented in `WindowsCM.Core.Transfer.MobileWebTemplate`:
- Responsive interface with Fluent design for phones.
- Download section with dedicated support for audio (HTML5 player), images (preview), videos and documents.
- Upload section with tabs for text (with a paste button) and files (drag and drop, camera and gallery with a progress bar).
- Fully self-contained, with no requests to external servers.

## Description

Create the mobile web page served by the local server, to be shown on the phone (iOS and Android) when the QR Code is scanned.

## Requirements

- Clean design following the Windows 11 Fluent style (rounded corners, modern palette, detection of the browser's native dark/light mode via `@media (prefers-color-scheme)`).
- **Download View (PC -> Phone)**:
  - Card with the item to be downloaded:
    - If Audio: song/audio title, size in MB, HTML5 audio player `<audio controls src="..." style="width: 100%">` and a styled "Download Audio" button.
    - If Image: high-resolution image viewer with a "Download Image" button.
    - If File/Document: file type icon, name, size and a "Download File" button.
    - If Text/Code/Link: elegant highlighted text box and a "Copy Text" button (using `navigator.clipboard.writeText` with a fallback and an on-screen confirmation toast).
- **Upload View (Phone -> PC)**:
  - "Send to PC" Tab / Section:
    - Text box to paste or type messages/links, with a "Send Text" button.
    - File picker and drop area supporting multiple files, camera photos, gallery, audio and documents.
    - "Send File" button with a visual upload progress bar and immediate send feedback ("Successfully sent to computer!").
- Fully self-contained (no external CDNs, working 100% offline on local networks without internet access).
