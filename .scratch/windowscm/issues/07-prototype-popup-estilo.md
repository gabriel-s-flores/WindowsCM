# Protótipo popup + estilo

Type: prototype
Status: resolved
Blocked by: 01, 03

## Question

Responder com código throwaway (branch `prototype/<nome>`): o popup WPF parece/comporta-se como o Copyous?

Escopo mínimo do protótipo:
- Janela frameless posicionada no mouse, horizontal (cards 250x170) + toggle vertical
- Header (settings + incognito + search pill + pin + Clear), lista de 8 cards (um por tipo), footer no modo vertical
- 9 tags coloridas (#3584e4,#2190a4,#3a944a,#c88800,#ed5b00,#e62d42,#d56199,#9c3cbe,#6f8396), dark/light/high-contrast
- Navegação teclado (setas/Tab/Home/End, Enter copia, Ctrl+Enter ação default) — sem persistência real (mock em memória)

Só começar quando `01` e `03` estiverem `resolved`. Chamar Skill `prototype`. Linkar o protótipo como asset ao resolver.

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
