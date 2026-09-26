# 3. Bidirectional File and Clipboard Transfer between PC and Mobile Devices via QR Code and a Local HTTP Server

Date: 2026-09-13

## Context

WindowsCM's QR Code support was strictly limited to short texts (`Text`, `Code`, `Link`, `Character`, `Color`). For file items (`File`, `Files`) and images (`Image`), the QR Code was not available because a standard optical QR code has a physical capacity limit (~2-3 KB), which makes it impossible to encode media files directly (such as MP3 audio, recordings, photos, videos or documents).

In addition, there was no direct way for the user to send data from the phone to the computer (reverse flow), forcing the use of third-party apps (WhatsApp Web, emails to oneself or external clouds) to transfer a simple text, link or file from the phone to the Windows clipboard.

In the Windows ecosystem, the native `System.Net.HttpListener` API is built on the `http.sys` kernel driver, which requires Administrator privileges (`netsh http add urlacl`) to bind ports to external network IP addresses or wildcards (`+` / `*`), throwing an `Access is denied` exception when run in standard user apps such as WindowsCM.

## Decision

1. **Asynchronous Local HTTP Server via `TcpListener`**:
   - Implementation of an ultra-lightweight, asynchronous HTTP/1.1 server in `WindowsCM.Core.Transfer` using `System.Net.Sockets.TcpListener`.
   - Needs no Administrator privileges, UAC elevation or manual firewall commands.
   - Listens on `0.0.0.0` with automatic detection of the IP of the active physical network interface (Wi-Fi or Ethernet with a Default Gateway).
   - Route handling to serve the mobile web app, file/media downloads and upload reception.

2. **Universal QR Code Generation for History Items (PC -> Phone)**:
   - Extension of `QrActions` so that `File`, `Files` and `Image` items are also eligible for QR Code generation.
   - When the user clicks the QR Code button of a card (whether audio, image, document or long text), the app generates a protected ephemeral URL on the local server (`http://<ip>:<port>/d/{token}`) and draws the corresponding QR code.
   - Scanning it with the phone opens a responsive mobile web page (Fluent Design) with a built-in audio player (for music/audio), a photo viewer (for images) or a direct file download button with the proper MIME and `Content-Disposition` headers.
   - The QR Code dialog on Windows also offers a button to choose any file on the computer for immediate sending.

3. **Reverse Flow with a "Send from mobile to PC" Button (Phone -> PC)**:
   - Addition of a dedicated button with a phone icon (`\uE8EA`) in the top bar of `PopupWindow` (right next to the Incognito Mode button) and in the footer of `CompactPopupWindow`.
   - When clicked, it opens the `MobileTransferWindow` window showing the QR Code for connecting the phone to the upload page (`http://<ip>:<port>/`).
   - On the mobile web page opened on the phone, the user can:
     - Paste or type texts and links and send them to the PC with one click.
     - Select photos, audio recordings or files from the mobile device and send them with visual progress.
   - The local server receives the content and automatically:
     - Writes the text or file to the Windows clipboard (`Win32ClipboardWriter` / `CF_UNICODETEXT` or `CF_HDROP`).
     - Inserts the item into the WindowsCM history via `CaptureService`.
     - Shows immediate feedback (a WindowsCM toast and an update in the open window).

## Consequences

- **Positive**:
  - Direct, instant and private transfer of files and text between computer and phone without depending on the internet or on external servers.
  - Full support for audio, video, images, documents and text.
  - Zero heavy external dependencies and zero need for Administrator privileges.
  - Smooth user experience on iOS and Android phones just by pointing the native camera at the QR Code.
  - Full integration with the WindowsCM history and the Windows clipboard.
- **Challenges / Limitations**:
  - Requires the computer and the phone to be connected to the same local network (Wi-Fi or LAN).

## Revision (2026-09-26): upload key, on-demand start and limits

The stability audit found the server open to the whole local network from startup, with no authentication on uploads:

- **Upload key**: `/api/upload` accepted text and files from any device on the same network (a public Wi-Fi) and put them straight onto the clipboard. Now each run of the app generates a random 128-bit key. It travels only in the "Send from mobile to PC" QR Code (`/u/{key}`), and the upload goes to `/api/upload/{key}`. Without the key, the response is 403 and the page tells the user to scan the QR again.
- **On-demand start**: the server starts on first use (QR for a file/image/long text, "Send from mobile to PC"), not at startup. Anyone who never uses the feature never sees the firewall alert and is not left with an open port. If no port is available, a balloon explains and the app carries on without the feature.
- **Truly ephemeral shares**: 128-bit tokens, valid for 24 hours (before: 32 bits and forever, with the text held in memory).
- **Limits**: received files go straight to disk (before, the whole body stayed in memory and was copied again: a 1.5 GB video reached ~3.5 GB in the process). Texts over 16 MB are rejected, idle connections are dropped after 30 s, there are at most 16 simultaneous connections and file names are sanitized for Windows (`:` became a hidden NTFS stream).
