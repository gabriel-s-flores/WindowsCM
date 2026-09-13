# 5. Orientação Configurável do Clipboard, Posicionamento na Tela e Fluxo de Itens

Data: 2026-09-13

## Contexto

Originalmente, o WindowsCM implementou a Área de Transferência Grande (`PopupWindow`) como uma faixa exclusivamente horizontal ancorada na parte inferior da tela (`Bottom`), com itens ordenados obrigatoriamente da esquerda para a direita (mais recentes à esquerda). O Menu Compacto (`CompactPopupWindow`) foi introduzido como um popup vertical estreito sob o cursor do mouse (320x480px), sempre exibindo os itens mais recentes de cima para baixo.

Embora esse comportamento padrão atenda à maioria dos fluxos, diversos usuários requerem:
1. **Visualização vertical ampla**: em monitores ultrawide ou verticais (código/documentos), uma barra lateral esquerda ou direita aproveita melhor o espaço vertical sem cobrir linhas de trabalho na parte inferior.
2. **Alternância de posicionamento na tela**: ancoragem no topo da tela para ambientes com barra de tarefas no topo, e seleção de lateral (esquerda vs direita) para destros ou canhotos.
3. **Controle de fluxo temporal**: alguns usuários preferem a pilha natural onde novos itens chegam à direita ou na base inferior (bottom-to-top), enquanto outros preferem a convenção ocidental de leitura (left-to-right ou top-to-bottom).
4. **Layout horizontal compacto**: usuários ágeis que desejam um menu leve sob o cursor, porém em formato de cards horizontais compactos.

## Decisão

1. **Separação de Políticas e Módulos Puros (`WindowsCM.Core.Popup`):**
   - Criação de `ItemOrderingPolicy`: módulo puro e testável responsável por inverter a sequência temporal quando a ordenação ativa exigir itens recentes no final (`RecentOnRight` ou `RecentOnBottom`), e determinar deterministicamente o índice inicial de seleção.
   - Expansão de `PopupPlacement`: funções puras `PlaceLargePopup` e `PlaceCompactPopup` cobrindo o cálculo de coordenadas de tela com margens e clamping em múltiplos monitores DPI-aware.
   - Enums explícitos no Core (`LargeHorizontalPosition`, `LargeVerticalPosition`, `HorizontalItemOrder`, `VerticalItemOrder`).

2. **Adaptabilidade da Janela Principal (`PopupWindow`):**
   - Modo Horizontal: mantém largura em tela cheia (`Fill`), altura fixa (348px) e top bar em 3 colunas, ancorando em `Bottom` ou `Top`.
   - Modo Vertical: adota largura compacta (~380px), altura preenchendo a tela de trabalho (`Fill`) e top bar adaptada para coluna estreita, ancorando em `Left` ou `Right`.
   - O scroll do mouse e a navegação por teclado se adaptam de forma transparente à orientação ativa.

3. **Flexibilidade do Menu Compacto (`CompactPopupWindow`):**
   - Suporte a `CompactOrientation.Vertical` (320x480) e `CompactOrientation.Horizontal` (540x240) sob o cursor do mouse.
   - Suporte à inversão de fluxo (de baixo para cima na vertical, da direita para a esquerda na horizontal).

4. **Experiência Visual Interativa no Painel de Configurações:**
   - Adição da aba "Layout & Posicionamento" em `SettingsWindow` com moldura estilizada de monitor e desktop.
   - Pré-visualização animada e em tempo real com dados mockados de smoke test, demonstrando visualmente onde o clipboard se posicionará e em qual sentido os itens fluirão antes da aplicação.

## Consequências

- **Positivas:**
  - Total liberdade ergonômica para qualquer perfil de monitor e preferência de uso.
  - O Core permanece 100% puro e testável sem dependências do pipeline visual do WPF.
  - Compatibilidade retroativa completa: configurações anteriores continuam com os valores padrão atuais.
- **Negativas / Desafios:**
  - `PopupWindow` requer adaptação responsiva no cabeçalho ao alternar entre layout horizontal amplo e vertical estreito.
