# Popup + style prototype

Type: prototype
Status: resolved
Blocked by: 01, 03

## Question

Answer with throwaway code (branch `prototype/<name>`): does the WPF popup look/behave like Copyous?

Minimum prototype scope:
- Frameless window placed at the mouse, horizontal (250x170 cards) + vertical toggle
- Header (settings + incognito + search pill + pin + Clear), list of 8 cards (one per type), footer in vertical mode
- 9 colored tags (#3584e4,#2190a4,#3a944a,#c88800,#ed5b00,#e62d42,#d56199,#9c3cbe,#6f8396), dark/light/high-contrast
- Keyboard navigation (arrows/Tab/Home/End, Enter copies, Ctrl+Enter default action) — no real persistence (in-memory mock)

Only start when `01` and `03` are `resolved`. Call Skill `prototype`. Link the prototype as an asset when resolving.

## Answer

Verdict (user reaction, 2026-09-09): **winner A (Cards)**. Spec adjustments:
density/spacing 1:1 Copyous; **Dark theme**; link rows without vertical
dead space (prototype C-rows wasted it); popup fills horizontal space, no
narrow floating box. B/C discarded (kept as primary source only).

Asset: branch `prototype/popup-wpf` (commits `a314624` prototype + `817f0c2`
bin/obj cleanup; 8 source files; verdict in `prototype/popup-wpf/README.md`;
bin/obj git-ignored). Note: user built it locally (SDK installed, DLL
produced) — code compiles. One review-caught fix included (preview width
196 for image/color cards). Run: `dotnet run --project
prototype/popup-wpf/PopupProto.csproj`.
