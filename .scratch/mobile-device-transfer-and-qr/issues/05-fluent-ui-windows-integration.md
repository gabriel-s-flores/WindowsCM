Status: resolved
Type: task

## Answer
Implemented in `PopupWindow.xaml`, `PopupWindow.xaml.cs`, `CompactPopupWindow.xaml`, `CompactPopupWindow.xaml.cs` and `MobileTransferWindow.xaml/.cs`:
- Added the `ReceiveMobileButton` button with a phone icon `\uE8EA` next to the incognito mode button in both interfaces.
- Created the `MobileTransferWindow` window with Fluent design, a high-quality QR Code display, the local IP, a copy-link button and a status indicator with real-time feedback when data is received from the phone.
- Enabled the "Generate QR code" option in the card context menu and the quick-action shortcut.

## Description

Add the phone-icon button to the `PopupWindow` and `CompactPopupWindow` interfaces to start receiving items from the phone, and create the `MobileTransferWindow` window with a persistent QR Code and real-time status.

## Requirements

- `PopupWindow.xaml`:
  - Next to `IncognitoButton`, add `ReceiveMobileButton` with a phone icon (`\uE8EA`), the tooltip "Send from mobile to PC" and the click event `OnReceiveMobileClicked`.
- `CompactPopupWindow.xaml`:
  - In the footer next to `IncognitoButton`, add the corresponding button.
- Creation of `MobileTransferWindow`:
  - Windows 11 Fluent look with rounded corners, dark/light background with dynamic brushes.
  - Prominent QR Code pointing to `http://<ip>:<port>/`.
  - Clear instruction: "Point the phone camera at this QR Code to send text and files to the computer."
  - Local IP selector/display with a button to copy the direct link.
  - Status list/card with real-time feedback on received items ("Waiting for connection...", "Received a moment ago: photo.jpg").
  - "Open Transfers Folder" and "Done / Close" buttons.
