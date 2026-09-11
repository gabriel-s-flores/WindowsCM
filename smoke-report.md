# smoke-report (tickets 21-24: popup-1080p + paste-diagnostics + single-click + tray-gaveta automated)

- Date (UTC): 2026-09-11T04:04:41Z
- Configuration: Debug
- Temp log: `C:\Users\gabri\AppData\Local\Temp\WindowsCM-20-smoke.log` (prefix `WCM20`)
- Baseline filtered tests: 179 (expect 179 since ticket 24)
- Summary: 7 PASS / 0 FAIL / 0 SKIP

| Step | Result | Details |
| ---- | ------ | ------- |
| build | PASS | dotnet build WindowsCM.sln green, 0 warnings/errors |
| baseline-tests | PASS | filtered popup/paste/tray green, total 179 (baseline 179 since ticket 24: 175 + 4 tray-onboarding) |
| app-launch-log | PASS | pid 21284 alive, single-instance=1 tray=1 lines |
| popup-1080p (tkt 21) | PASS | 4/4 placements match cursor+12 clamp, width=380; determinism: first final=972,552 size=380x298 cursorPx=960,540 vs second final=972,552 size=380x298 cursorPx=960,540 |
| paste-diagnostics (tkt 22) | PASS | 4/4 required ok; common-paste: outcome=Pasted clipOk=True / copy-only: line=2026-09-11T04:03:56.5351043Z [WCM20:activate] result itemId=112 kind=Text shiftHeld=True runDefault=False preview=wcm22-copyonly-639246962313281936 capturedTarget=0x4100DC currentAfterHide=0x5F0690 outcome=CopiedOnly chord=<none> diagnostics=<empty> / focus-lost: line=2026-09-11T04:04:03.0220318Z [WCM20:activate] result itemId=113 kind=Text shiftHeld=False runDefault=False preview=wcm22-focuslost-639246962378231841 capturedTarget=0x2A08DC currentAfterHide=0x803A6 outcome=CopiedOnlyForegroundLost chord=<none> diagnostics=The target window lost focus before pasting, so the item was only copied. If the… / missing-item: line=2026-09-11T04:04:09.5975307Z [WCM20:activate] result itemId=114 shiftHeld=False runDefault=False outcome=MissingItem diagnostics=Item 114 is no longer in history, so nothing was copied. / elevated SKIP (no elevated window found; unit test covers the refusal) |
| single-click-paste (tkt 23) | PASS | 4/4 ok; single-click: outcome=Pasted noteOk=True / shift-click: outcome=CopiedOnly chord=<none> / keyboard-nav: activationsBefore=6 activationsAfter=6 (no paste triggered) / double-click: outcome=Pasted occurrences=1 (single paste preserved) |
| tray-gaveta (tkt 24) | PASS | 4/4 ok; tray-icon-visible: pid=23536 alive=True trayLogCount=2 tooltip=WindowsCM / copy-feedback: flashCount=22 (new=True) balloonCount=2 / onboarding-guidance: overflow=True drag=True noProgrammaticPromo=True / overflow-drawer-screenshot: hwnd=0x6E079E opened=True closed=True fullShot=True cropShot=True |

- Popup screenshot: `C:\Users\gabri\AppData\Local\Temp\WindowsCM-popup-center-1.png`
- Popup screenshot: `C:\Users\gabri\AppData\Local\Temp\WindowsCM-popup-center-2.png`
- Popup screenshot: `C:\Users\gabri\AppData\Local\Temp\WindowsCM-popup-right.png`
- Popup screenshot: `C:\Users\gabri\AppData\Local\Temp\WindowsCM-popup-bottomright.png`
- Tray screenshot: `C:\Users\gabri\AppData\Local\Temp\WindowsCM-tray-overflow.png`
- Tray screenshot: `C:\Users\gabri\AppData\Local\Temp\WindowsCM-tray-overflow-cropped.png`

## Temp log excerpt (last 40 lines)

```
2026-09-11T04:04:02.8215860Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:03.0220318Z [WCM20:activate] result itemId=113 kind=Text shiftHeld=False runDefault=False preview=wcm22-focuslost-639246962378231841 capturedTarget=0x2A08DC currentAfterHide=0x803A6 outcome=CopiedOnlyForegroundLost chord=<none> diagnostics=The target window lost focus before pasting, so the item was only copied. If theâ€¦
2026-09-11T04:04:03.0225396Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:03.0232090Z [WCM20:tray] balloon title=WindowsCM text=The target window lost focus before pasting, so the item was only copied. If theâ€¦
2026-09-11T04:04:03.5752441Z [WCM20:single-instance] outcome=Forwarded startHidden=False
2026-09-11T04:04:05.7064673Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:08.0030051Z [WCM20:activate] capture slot=Open capturedTarget=0x25D0796
2026-09-11T04:04:08.0116431Z [WCM20:popup-open] incognito=False cursorPx=1880,1000 cursorDip=1880.0,1000.0 workArea=0,0-1920,1032 measured=380x466 size=380x466 final=1540,566 visible=10
2026-09-11T04:04:08.7598915Z [WCM20:single-instance] outcome=Forwarded startHidden=False
2026-09-11T04:04:09.5975307Z [WCM20:activate] result itemId=114 shiftHeld=False runDefault=False outcome=MissingItem diagnostics=Item 114 is no longer in history, so nothing was copied.
2026-09-11T04:04:09.5993650Z [WCM20:tray] balloon title=WindowsCM text=Item 114 is no longer in history, so nothing was copied.
2026-09-11T04:04:10.3513398Z [WCM20:single-instance] outcome=Forwarded startHidden=False
2026-09-11T04:04:12.6010955Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:14.9102567Z [WCM20:activate] capture slot=Open capturedTarget=0x84061E
2026-09-11T04:04:14.9205764Z [WCM20:popup-open] incognito=False cursorPx=1880,1000 cursorDip=1880.0,1000.0 workArea=0,0-1920,1032 measured=380x98 size=380x98 final=1540,934 visible=1
2026-09-11T04:04:15.5872271Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:15.7879700Z [WCM20:activate] result itemId=115 kind=Text shiftHeld=False runDefault=False preview=wcm23-click-639246962510352636 capturedTarget=0x84061E currentAfterHide=0x84061E outcome=Pasted chord=CtrlV diagnostics=<empty>
2026-09-11T04:04:15.7884976Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:16.8189228Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:17.5685305Z [WCM20:single-instance] outcome=Forwarded startHidden=False
2026-09-11T04:04:19.7097359Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:22.0198546Z [WCM20:activate] capture slot=Open capturedTarget=0x10207A4
2026-09-11T04:04:22.0276501Z [WCM20:popup-open] incognito=False cursorPx=1690,1002 cursorDip=1690.0,1002.0 workArea=0,0-1920,1032 measured=380x198 size=380x198 final=1540,834 visible=3
2026-09-11T04:04:22.7386227Z [WCM20:activate] result itemId=117 kind=Text shiftHeld=True runDefault=False preview=wcm23-shift-639246962581028355 capturedTarget=0x10207A4 currentAfterHide=0x5F0690 outcome=CopiedOnly chord=<none> diagnostics=<empty>
2026-09-11T04:04:22.7392051Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:22.7405497Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:23.5416548Z [WCM20:single-instance] outcome=Forwarded startHidden=False
2026-09-11T04:04:25.9413327Z [WCM20:activate] capture slot=Open capturedTarget=0x780838
2026-09-11T04:04:25.9485532Z [WCM20:popup-open] incognito=False cursorPx=1690,902 cursorDip=1690.0,902.0 workArea=0,0-1920,1032 measured=380x198 size=380x198 final=1540,834 visible=3
2026-09-11T04:04:28.5251920Z [WCM20:single-instance] outcome=Forwarded startHidden=False
2026-09-11T04:04:30.6409913Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:32.9410881Z [WCM20:activate] capture slot=Open capturedTarget=0xC807F6
2026-09-11T04:04:32.9520267Z [WCM20:popup-open] incognito=False cursorPx=1690,902 cursorDip=1690.0,902.0 workArea=0,0-1920,1032 measured=380x248 size=380x248 final=1540,784 visible=4
2026-09-11T04:04:33.5943295Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:33.7946106Z [WCM20:activate] result itemId=118 kind=Text shiftHeld=False runDefault=False preview=wcm23-double-639246962690612220 capturedTarget=0xC807F6 currentAfterHide=0xC807F6 outcome=Pasted chord=CtrlV diagnostics=<empty>
2026-09-11T04:04:33.7950802Z [WCM20:tray] flash times=3 intervalMs=65
2026-09-11T04:04:35.6688590Z [WCM20:single-instance] outcome=Forwarded startHidden=False
2026-09-11T04:04:36.4927170Z [WCM20:single-instance] outcome=IsPrimary startHidden=True
2026-09-11T04:04:36.8382021Z [WCM20:tray] visible=True tooltip=WindowsCM
2026-09-11T04:04:38.4815164Z [WCM20:tray] flash times=3 intervalMs=65
```

_Skeleton: all tickets 21-24 automated; temp log + this script are removed in ticket 25._
