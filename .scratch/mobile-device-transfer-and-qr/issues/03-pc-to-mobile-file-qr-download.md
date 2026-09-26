Status: resolved
Type: task

## Answer
Implemented in `QrWindow.xaml`, `QrWindow.xaml.cs`, `PopupConverters.cs` and `App.xaml.cs`:
- QR Code enabled for all cards (`File`, `Files`, `Image`, etc.).
- Generation of ephemeral session URLs served by `MiniTransferHttpServer`.
- `QrWindow` now shows rich file metadata (title, size, icon), the local link with a copy button and a "Choose another file..." button to send any file from the disk.

## Description

Expand the QR Code policy (`QrActions`) and the `QrWindow` dialog to support items of every type (`ItemKind.File`, `ItemKind.Files`, `ItemKind.Image`, in addition to the text types), generating local URLs reachable from the phone and also allowing any arbitrary file from the disk to be shared.

## Requirements

- Update `QrActions.IsSupported`:
  - Return `true` for every `ItemKind` that has valid content (including `File`, `Files` and `Image`).
- Share URL generator:
  - For files/images: register an ephemeral token or ID in `LocalTransferServer` and generate the corresponding URL: `http://<ip>:<port>/d/{token}`.
  - For short text: keep the option of a QR code with plain text and/or a transfer URL, for ease of reading.
- `QrWindow` dialog update:
  - Show the QR code rendered with `QRCoder`.
  - Show friendly file information (name, extension, formatted size and thumbnail when available).
  - Show the full local URL with a "Copy Link" button.
  - Add a "Choose another file..." button to allow sending any file from the computer even if it is not already on the clipboard.
