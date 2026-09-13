# 03: QR code quick action button on clipboard item cards

**What to build:**
Add a dedicated QR Code action button (`&#xED14;`) on the bottom quick action toolbar of every card in `PopupWindow.xaml` and inline actions in `CompactPopupWindow.xaml`. Clicking the button opens `QrWindow` directly with the item payload.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Add QR code button in `PopupWindow.xaml` card template
- [x] Add QR code button in `CompactPopupWindow.xaml` item template
- [x] Implement `OnCardQrButtonClicked` handler in both windows
- [x] Add `QrButtonVisibilityConverter` ensuring button appears for supported item kinds (`Text`, `Code`, `Link`, `Character`, `Color`)

