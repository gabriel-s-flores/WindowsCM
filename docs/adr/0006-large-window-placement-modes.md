# 6. Modos de posicionamento da janela grande: monitor fixo e livre

Data: 2026-09-25

## Contexto

A janela grande (`PopupWindow`) só podia ser ancorada numa borda (em cima/embaixo na horizontal, esquerda/direita na vertical, ADR 0005) do monitor onde estivesse o mouse. Em mesas com vários monitores o usuário quer que ela abra sempre no mesmo monitor, independentemente de onde está o mouse; e há quem prefira uma janela solta, posicionada e dimensionada à mão.

"Modo para múltiplos monitores" foi decidido como **escolher um monitor fixo** (não estender a barra por todas as telas, nem seguir a janela ativa). O modo livre permite **arrastar e redimensionar**.

## Decisão

1. **`DialogSettings.LargePlacement`** (`FollowMouse` padrão, `FixedMonitor`, `Free`). Os dois modos ancorados continuam usando `LargeHorizontalPosition` / `LargeVerticalPosition`; no modo livre as bordas não se aplicam.
2. **Monitor fixo por `Screen.DeviceName`** (`LargeMonitor`, vazio = principal). É a identidade que o Windows mantém entre sessões; índice ou coordenadas mudariam ao reorganizar as telas. Com o monitor desconectado, o principal assume, mas a escolha fica salva e volta a valer quando ele reaparece (`MonitorLayout.Resolve`).
3. **Numeração na ordem da mesa** (esquerda→direita, depois cima→baixo, `MonitorLayout.Ordered`) na lista das Configurações e no overlay **Identificar**, que mostra o número no centro de cada monitor por 2 s (sem capturar foco nem cliques).
4. **Modo livre com um retângulo por orientação** (`LargeFreeBoundsHorizontal` / `LargeFreeBoundsVertical`, em DIPs do WPF — o mesmo espaço de `Window.Left/Top`). Uma faixa larga e uma coluna alta são formas diferentes; trocar a orientação não espreme uma na outra. Sem retângulo salvo, a janela abre flutuando no centro do monitor do mouse.
5. **Nunca fora da tela** (`PopupPlacement.PlaceFree`): o retângulo salvo é puxado inteiro para dentro da área de trabalho com a qual mais se sobrepõe (e reduzido se o monitor ficou menor); se não se sobrepõe a nenhuma (monitor removido), recomeça centralizado. Tamanho mínimo por orientação garante que o cabeçalho caiba.
6. **Redimensionar pela resposta a `WM_NCHITTEST`** (`ResizeHitTest.EdgeAt` → `HTLEFT`…`HTBOTTOMRIGHT`) só no modo livre, deixando o Windows conduzir o laço nativo de redimensionamento da janela sem borda; **arrastar com `DragMove`** a partir da alça no topo e de qualquer área que não seja controle. `WM_EXITSIZEMOVE` grava o retângulo ao soltar. Não se usa `WindowChrome`, para não alterar a janela nos modos ancorados.

## Consequências

- **Positivas:** as regras de monitor e de posição livre são puras e testadas no Core; configurações antigas carregam `FollowMouse` (comportamento de antes); nada muda para quem não mexer na opção.
- **Negativas / desafios:** a validação de arrastar/redimensionar e do overlay é manual (smoke no Windows); a precisão com escalas de DPI diferentes entre monitores depende do modo de DPI do processo (o WPF sem manifesto roda "system aware", em que os DIPs são consistentes entre telas).
