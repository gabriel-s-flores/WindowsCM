# 28: Abertura Instantânea pelo Tray e Suporte ao Tema Claro do Windows

Type: task

Status: resolved

Blocked by: 27

## What to build:
1. **Abertura e resposta instantânea ao clicar no ícone da bandeja (System Tray)**:
   - Eliminar o timer de atraso de 500ms (`_clickTimer` / `DoubleClickTime`) no `TrayManager`.
   - Disparo imediato do toggle/abertura do popup em 0ms ao detectar clique do botão esquerdo no ícone.
   - Proteção contra corridas de perda de foco / *deactivation bounce* do Explorer (especialmente ao abrir da gaveta de overflow do Windows 11).
   - Coordenação entre o evento `Deactivated` do WPF e o clique na bandeja para evitar que o clique de fechamento reabra o popup.
   - Otimização do pipeline de abertura do `PopupWindow` (eliminar consultas SQLite duplicadas no `ShowAtCursor` e garantir ativação de primeiro plano via `SetForegroundWindow`).

2. **Suporte nativo ao Tema Claro do Windows (Dynamic Light & Dark Themes)**:
   - Detecção do tema do sistema operacional Windows (via registro `Personalize\AppsUseLightTheme` e eventos de mudança de preferências do usuário).
   - Integração com `ThemeSettings` e `ColorScheme.System` do Core.
   - Conjunto completo de pincéis e cores Windows 11 Fluent para Modo Claro e Modo Escuro no `PopupWindow`:
     - Fundo do popup e bordas.
     - Campo de busca, textos e ícone de lupa.
     - Botões da barra de ações superiores e botões dos cards.
     - Cards de histórico (fundo branco no modo claro com bordas sutis e estados hover/selecionado em azul Fluent).
     - Áreas de preview adaptadas (código estilo GitHub/VS Light, texto com alto contraste, caracteres/emojis e miniaturas).
   - Atualização dinâmica em tempo real quando o usuário alterar o tema nas configurações do Windows ou nas preferências do app.

3. **Testes automatizados e regressão zero**:
   - Testes unitários para o detector de tema do Windows e resolução de esquemas de cores.
   - Validação da suíte completa de testes existente (762 testes aprovados).

## Answer
Implementado e validado em 2026-09-12:

1. **Abertura e resposta instantânea da bandeja (0ms)**:
   - `TrayManager.cs`: remoção completa do `_clickTimer` (intervalo de `DoubleClickTime` de 500ms). O evento `MouseClick` com botão esquerdo invoca diretamente `_controller.OnLeftClick()` no `Dispatcher` da aplicação sem qualquer atraso artificial.
   - `TrayManager.cs`: `DoubleClick` mapeado para `_controller.OnDoubleClick()`, garantindo que o usuário que dá duplo-clique no ícone da bandeja veja o app abrir e permanecer em primeiro plano (sem alternar/fechar acidentalmente).
   - `PopupWindow.xaml.cs`: adicionada salvaguarda de transição de foco no `OnDeactivated`. Se a perda de foco for reportada nos primeiros 250ms após a abertura, o evento é ignorado (evitando que a liberação de captura do Explorer na gaveta de overflow feche o popup recém-aberto).
   - `PopupWindow.xaml.cs` e `ShellAdapters.cs`: implementada coordenação de fechamento via `WasRecentlyHidden` (350ms) no `Toggle()`. Quando o popup está aberto e o usuário clica no ícone da bandeja para fechá-lo, o clique que causou a perda de foco e o fechamento não reabre o popup por engano.
   - Chamada direta a `SetForegroundWindow` da User32 ao abrir, garantindo primeiro plano imediato.
   - Remoção de consulta SQLite redundante na abertura quando a pesquisa já está vazia.

2. **Tema Claro dinâmico integrado ao Windows**:
   - `IWindowsThemeDetector.cs` em `WindowsCM.Core/Settings/`: contrato para detecção de tema do sistema e notificação de mudança via evento `ThemeChanged`.
   - `ThemeDetector.cs` (`Win32WindowsThemeDetector`) em `WindowsCM.App`: leitor da chave do Registro do Windows `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` com verificação de alto contraste e inscrição em `SystemEvents.UserPreferenceChanged` para reagir imediatamente quando o Windows alternar entre os modos Claro e Escuro.
   - `PopupThemeBrushes.cs` em `WindowsCM.App`: gerador de dicionários de recursos com paleta completa Fluent para Windows 11 em Modo Claro e Modo Escuro:
     - Fundo da janela (`#F3F3F3` Claro, `#202020` Escuro).
     - Cards (`#FFFFFF` Claro com borda `#E2E2E2` e hover `#F8F8F8`; `#2B2B2B` Escuro).
     - Textos principais e secundários com alto contraste (`#1C1C1C`/`#5F5F64` Claro; `#FFFFFF`/`#8E8E93` Escuro).
     - Caixa de busca (`#FFFFFF` Claro com borda `#CECECE` e texto escuro).
     - Botões de cabeçalho e card com cores de hover e pressed adaptadas para cada modo.
     - Prévias de código com estilo editor claro (`#F6F8FA` com texto escuro `#24292E` e borda `#E1E4E8`).
     - Prévias de imagem, caracteres/emojis e texto estilizadas de acordo com o modo ativo.
   - `PopupWindow.xaml`: substituição de todas as cores fixas por referências `{DynamicResource}`.
   - `PopupWindow.xaml.cs`: método `ApplyTheme(bool isLight)` atualiza os recursos dinâmicos instantaneamente sem destruir nem reconstruir a árvore visual.
   - `App.xaml.cs`: inicialização do detector de tema e sincronização em tempo de execução via evento e ao fechar a janela de configurações.

3. **Validação e testes**:
   - `WindowsThemeDetectorTests.cs`: 7 novos testes unitários cobrindo simulação de sistema claro, escuro, alto contraste, evento de alteração dinâmica e precedência de configurações.
   - 762 testes automatizados executados com 100% de sucesso e zero regressões.
   - Compilação Debug e Release com 0 erros e 0 warnings.
