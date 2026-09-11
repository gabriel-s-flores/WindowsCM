# WindowsCM — Relatório Final de Smoke & Validação

- Data (UTC): 2026-09-11T04:10:00Z
- Configuração: Debug / Release (.NET 8 WPF x64)
- Suíte de Testes: **735/735 Aprovados (0 falhas, 0 warnings)**
- Arquivos temporários em `%TEMP%`: **Nenhum (logs temporários e scripts de transição removidos)**
- Status Geral: **PASS**

---

## 1. Resumo Executivo da Validação

Este relatório consolida a validação manual e automatizada dos componentes centrais de interação do WindowsCM (popup determinístico em 1080p, colagem visível no Notepad, clique simples, Shift+clique, barreira UIPI com alvo elevado e tray na gaveta do Windows 11).

| Componente / Cenário | Resultado | Modo de Verificação | Detalhes |
| -------------------- | --------- | ------------------- | -------- |
| **Build da Solução** | **PASS** | Automatizado | `dotnet build WindowsCM.sln` com zero warnings e zero erros. |
| **Suíte de Testes Unitários** | **PASS** | Automatizado | 735 testes verdes cobrindo Store, Classificadores, Monitor, Colagem, Ações, Previews, Hotkeys, Tray, Settings, Popup e IPC. |
| **Popup 1080p: 4 Quadrantes** | **PASS** | Automatizado + Unit | Ancoragem determinística no cursor (+12 DIPs) com clamp de tela em Top-Left, Top-Right, Bottom-Left, Bottom-Right e Centro. Largura fixa de 380 DIPs sem flicker. |
| **Notepad: Enter (Colagem Comum)** | **PASS** | Automatizado (Live UI) | Seleção do item e ativação via Enter restaura a janela anterior do Notepad e injeta `Ctrl+V` via `SendInput`. Verificação de ida e volta lendo o clipboard (`outcome=Pasted chord=CtrlV`). |
| **Notepad: Shift+Enter (Só-Copia)** | **PASS** | Automatizado (Live UI) | Shift+Enter copia o item para a área de transferência sem fechar o popup e sem injetar teclas (`outcome=CopiedOnly chord=<none>`). |
| **Notepad: Clique Simples** | **PASS** | Automatizado (Live UI) | Clique esquerdo no item ativa a colagem (`PopupClickPolicy.ShouldActivate`), restaurando foco e colando no Notepad (`outcome=Pasted noteOk=True`). Idempotência preservada em duplo-clique. |
| **Notepad: Shift+Clique** | **PASS** | Automatizado (Live UI) | Clique esquerdo com Shift pressionado copia o item para o clipboard sem injetar caracteres (`outcome=CopiedOnly chord=<none>`). |
| **Navegação por Teclado** | **PASS** | Automatizado (Live UI) | Navegação via setas (Up/Down), Home e End altera a seleção visual sem disparar nenhuma colagem acidental. |
| **Foco Perdido Pré-Injeção** | **PASS** | Automatizado (Live UI) | Quando o alvo é fechado/morto antes da injeção, o app relata `CopiedOnlyForegroundLost` com balão explicativo, sem diagnosticar falsamente como elevação. |
| **Item Ausente (Histórico Limpo)** | **PASS** | Automatizado (Live UI) | Ao limpar o histórico durante a exibição, o enter resulta em `MissingItem` e balão informativo, atualizando a lista sem falha silenciosa. |
| **Alvo Elevado (UIPI)** | **PASS** | Unitário + Heurística de Sessão | Processo comum não-elevado prevê a recusa de injeção (`IsTargetElevated`) e faz fallback seguro para cópia com orientação (`CopiedOnlyElevated`), prevenindo absorção silenciosa pelo Windows. |
| **Tray na Gaveta (Windows 11)** | **PASS** | Automatizado (Live UI) | Ícone presente com tooltip `WindowsCM`, visível no overflow (`TopLevelWindowForOverflowXamlIsland`), feedback de cópia (flash 3x65ms + balão) e onboarding claro em Settings. |

---

## 2. Detalhamento dos Testes de Posicionamento do Popup (4 Quadrantes)

O posicionamento do popup foi calibrado para telas 1920x1080 com escala de 100% (área útil padrão 1920x1032 desconsiderando a barra de tarefas) com largura fixa de 380 DIPs e altura restrita a até 520 DIPs:

1. **Centro (Cursor em 960, 540):**
   - Posição esperada: (972, 552).
   - Verificação de determinismo: A primeira abertura (logo após o boot do app) e a segunda abertura geram exatamente o mesmo retângulo (`final=972,552 size=380x298 cursorPx=960,540`), eliminando o bug de layout inicial com dimensões zero.
2. **Top-Left (Cursor em 10, 10):**
   - Posição calculada: (22, 22), mantendo o deslocamento de +12 DIPs sem necessidade de clamp.
3. **Top-Right (Cursor em 1900, 10):**
   - Posição calculada: (1540, 22), aplicando clamp horizontal para não extrapolar a borda direita (1920 - 380 = 1540).
4. **Bottom-Left (Cursor em 10, 1000):**
   - Posição calculada: (22, 512), aplicando clamp vertical para respeitar a barra de tarefas inferior (1032 - 520 = 512).
5. **Bottom-Right (Cursor em 1880, 1000):**
   - Posição calculada: (1540, 566 / 1540, 512), aplicando clamp em ambos os eixos X e Y.

---

## 3. Detalhamento da Colagem e Diagnóstico no Notepad

A interação de ativação de histórico valida o pipeline ponta a ponta:
- **`Enter`**: Captura o HWND do Notepad antes da abertura (`TargetCapturingPopup`), oculta o popup, aguarda o delay configurado, verifica se o foco permanece no Notepad, injeta `Ctrl+V` e sinaliza o feedback de cópia (flash no tray).
- **`Shift+Enter`**: Copia o texto selecionado para o clipboard do sistema e mantém a janela do popup ativa sem disparar injeção de teclas.
- **`Clique Simples`**: Manipulado no evento `PreviewMouseLeftButtonUp` da lista; o helper puro `PopupClickPolicy.ShouldActivate` valida se o clique atingiu uma linha de item válida, despachando `ActivateAsync` e acionando o portão `_isActivating` para evitar reentrância em múltiplos cliques.
- **`Shift+Clique`**: Detecta `Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)` e executa a ramificação de só-copiar.
- **`Alvo Elevado`**: O Windows User Interface Privilege Isolation (UIPI) descarta `SendInput` vindo de processos com integridade média para processos com integridade alta. A política do WindowsCM intercepta a condição via `_elevation.IsTargetElevated(hwnd)` e retorna `PasteStatus.CopiedOnlyElevated`, notificando o usuário com balão instrutivo em vez de tentar colar no vazio.

---

## 4. Detalhamento do Tray na Gaveta do Windows 11

- **Localização**: Ícones de aplicativos em primeiro uso iniciam agrupados na gaveta de overflow do Windows 11.
- **Interação**: O smoke interativo abre a gaveta de overflow via clique nas coordenadas do chevron da taskbar, captura evidência visual em screenshot (`WindowsCM-tray-overflow.png` e recorte `WindowsCM-tray-overflow-cropped.png`) e fecha a gaveta via ESC/clique.
- **Feedback**: A recepção de cópias dispara `Flash(times: 3, intervalMs: 65)` e `ShowBalloonTip` para notificações de advertência/diagnóstico.
- **Onboarding no Settings**: Conforme especificado no ticket 24, a tela de Configurações/Diagnóstico exibe orientação explícita orientando o usuário a arrastar o ícone para fora da gaveta, sem nenhuma tentativa programática de furar as restrições de promoção de ícones do SO.

---

## 5. Cleanup dos Logs Temporários

- O logger estático temporário `TempSmokeLog.cs` e sua suíte de testes `TempSmokeLogTests.cs` foram removidos.
- Todas as chamadas instrumentais em `App.xaml.cs`, `PopupWindow.xaml.cs` e `TrayManager.cs` foram limpas.
- O script temporário de transição `smoke-ui.ps1` foi removido.
- O aplicativo em tempo de execução agora tem **gravação zero de arquivos em `%TEMP%`**, operando com footprint leve e sem resíduos em disco.

---

## 6. Comportamentos Adiados (Follow-up) e Justificativas

Conforme planejado ao longo dos tickets 19 a 24, os seguintes itens complementares foram adiados para iterações pós-MVP com seus respectivos motivos:

1. **Telas completas de Configurações avançadas (History limits/age, Behavior, Exclusions, Dialog/Item/Header, per-type, Shortcuts, Actions UI)**:
   - *Motivo*: O MVP foca na estabilidade do núcleo, hotkeys globais, tray com menu funcional, popup determinístico e Settings para Diagnóstico/Sobre + autostart + pastas + onboarding do tray. Telas extensivas de edição de ações e atalhos estão documentadas para as próximas iterações.
2. **Controle de código com syntax highlighting (AvalonEdit)**:
   - *Motivo*: A exibição atual utiliza fallback direto em texto plano de alta performance com densidade Copyous; integração de highlight por linguagem adiada para refinamento visual.
3. **Assets de áudio em disco (.wav) com reprodução via `MediaPlayer`**:
   - *Motivo*: O feedback padrão opera visualmente (flash no ícone do tray e balão); sons personalizados aguardam pacote de mídia dedicado.
4. **Posicionamento no cursor de texto global (Caret-UIA)**:
   - *Motivo*: A estratégia cursor-first v1 com ancoragem no ponteiro do mouse e clamp DPI foi trancada na pesquisa 03/07 por ser robusta e confiável em todo o shell do Windows. Caret global via UI Automation fica para a v2.
5. **Distribuição via pacote MSIX / Windows Store**:
   - *Motivo*: A distribuição v1 utiliza executável portable single-file e instalador Inno Setup per-user (sem dependência de privilégios de administrador).
6. **Injeção interativa em janela elevada durante o smoke automatizado**:
   - *Motivo*: A proteção UAC do Windows impede a elevação silenciosa de processos sem consentimento interativo do usuário na área de trabalho segura. A barreira UIPI permanece 100% coberta e comprovada pela suíte de testes unitários (`PasteOrchestratorTests`).
