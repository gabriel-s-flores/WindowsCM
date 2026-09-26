# Spec: Bidirectional File and Text Transfer between Computer and Phone via QR Code

Status: ready-for-agent

## Problem Statement

1. **Current QR Code Limitation (Short Text Only)**: WindowsCM currently supports QR Code generation exclusively for text items (`Text`, `Code`, `Link`, `Character`, `Color`). For items of type `File`, `Files` and `Image`, the QR Code button is hidden and the `Ctrl+Q` shortcut is ignored, because binary files (audio, photos, documents, videos) and file lists exceed a QR Code's capacity for directly encoding characters.
2. **Lack of File Sending to Mobile Devices**: Users often need to send music (audio), captured images, PDF reports or miscellaneous files from the PC to the phone without relying on external cloud services (Google Drive, WhatsApp Web, Telegram, email).
3. **Lack of a Reverse Flow (Phone -> PC)**: There is no practical mechanism for the user to send text, links or files from the phone directly to the computer's clipboard and history.

## Solution

1. **Local HTTP Transfer Server (`LocalTransferServer`)**:
   - A lightweight, asynchronous HTTP/1.1 server built on `System.Net.Sockets.TcpListener` (requiring no Administrator permissions / `netsh urlacl`, unlike the Windows `HttpListener`, which raises an access-denied error for standard users).
   - Smart detection of the active network interface (Wi-Fi/Ethernet with a default gateway) to discover the machine's local IP.
   - Dynamic port management with automatic allocation if the default port is taken.
   - Secure endpoints for downloading files/text (`/d/{token}` or `/download?id={id}`) with Content-Type, Content-Disposition and efficient streaming of local files and images.
   - Receiving endpoint (`/upload`) supporting multipart/form-data and payloads with text and files.

2. **Modern, Responsive Mobile Web Interface**:
   - Locally served web page with a Fluent Design visual identity (automatic light/dark theme support).
   - **Computer -> Phone Flow**:
     - Shows the item's details (name, formatted size, type).
     - For audio: built-in `<audio controls>` player + download button.
     - For images: image preview + download button.
     - For files/documents: direct download button with the original file name.
     - For text/code: formatted viewer with a "Copy to Clipboard" button that copies to the phone's clipboard.
   - **Phone -> Computer Flow**:
     - Area to type/paste text with a "Send Text to PC" button.
     - Area for selecting and dragging files (camera/gallery photos, audio, PDFs, any file) with a "Send File to PC" button.
     - Real-time feedback with a progress bar and a success message.

3. **Integration into the WindowsCM Interface**:
   - **"Send from mobile to PC" button**: Added to the `PopupWindow` header (next to the incognito mode button) and to the `CompactPopupWindow` footer, with a phone icon (`\uE8EA`), a descriptive tooltip and a dedicated dialog (`MobileTransferWindow`).
   - **Universal QR Code Generation for Cards**:
     - Enable the QR Code button and the `Ctrl+Q` shortcut for ALL item types (`File`, `Files`, `Image`, `Text`, etc.).
     - The QR Code dialog shows the QR code for connecting the phone, the shared file's thumbnail/details, the local IP address/URL with a copy button, and an option to select any arbitrary file from the disk.
   - **Clipboard and History Integration**:
     - Text received from the phone is automatically written to the Windows clipboard and to the WindowsCM history with a subtle notification (toast).
     - Received files are saved to the user's transfers folder (`Downloads\WindowsCM Transfers`), placed on the Windows clipboard as `CF_HDROP` and added to the history with immediate thumbnails and semantic categories.
