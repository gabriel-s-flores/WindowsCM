# Issue 36: Resolução de Conflitos do Modo Anônimo (Tray, Atalhos e Proteção de Sessão)

Status: resolved
Type: fix
Blocked by: 35

## Contexto

O usuário identificou comportamentos conflitantes e perda de dados acidental no modo anônimo:
1. Ao clicar na bandeja do sistema (system tray) após ativar o modo anônimo, o modo era desligado silenciosamente e todo o histórico efêmero era perdido.
2. Ao usar o atalho do modo anônimo (`Ctrl+Shift+Alt+V`) com o popup aberto para fechá-lo (toggle), o app chamava `SetIncognito(false)`, destruindo os clipes.
3. Ao usar o atalho normal (`Ctrl+Shift+V`), o popup não fechava em toggle, e não havia meio de alternar entre histórico normal e anônimo sem perder a sessão.

## Requisitos

1. **Invariante de Não-Destrutividade por Visibilidade**:
   - `Show`, `Hide`, `Toggle`, cliques no ícone da bandeja e atalhos de fechar janela NUNCA devem desativar o modo anônimo nem limpar dados.
   - Apenas ações explícitas (`[Sair do anônimo]`, toggle intencional no menu/cabeçalho ou shutdown) podem destruir a sessão.
2. **Correção do Tray e Adapters**:
   - `ShellPopup.Toggle()` e `TrayController.OnMenu(TrayMenuItem.Open)` devem respeitar o modo ativo (`IsIncognito`) em vez de forçar `incognito: false`.
3. **PopupViewModel Puro**:
   - `PopupViewModel.Show` não deve sofrer o efeito colateral destrutivo de chamar `SetIncognito`.
   - Suporte a alternância de visualização entre histórico normal e sessão anônima sem encerrar a sessão efêmera.
4. **Atalhos Globais Previsíveis**:
   - `Ctrl+Shift+V` e `Ctrl+Shift+Alt+V` devem fechar o popup se já estiver visível (Toggle limpo).
5. **Ciclo TDD**:
   - Testes unitários para `TrayController`, `PopupViewModel`, `IncognitoSessionCoordinator` e `App`.
