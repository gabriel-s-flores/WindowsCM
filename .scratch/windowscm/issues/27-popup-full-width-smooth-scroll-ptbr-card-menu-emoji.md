# 27: Popup em Largura Total, Rolagem Suave, Localização PT-BR, Menu por Card e Emojis

Type: task

Status: resolved

Blocked by: 26

## What to build:
1. **Largura total da tela (Horizontal Fill)**: O popup horizontal deve ocupar a largura útil da tela (com margens laterais confortáveis de 16-24px, estilo Copyous `clipboard-position-horizontal: fill`), permitindo visualizar múltiplos cards (6-8) lado a lado na tela em vez de uma caixa fixa estreita de 880px.
2. **Rolagem horizontal suave (Smooth Scroll)**: Ao rolar com o scroll do mouse ou teclas de navegação, a rolagem dos cards deve ser animada suavemente (com interpolação ease-out em ~200ms e unidade por pixel), eliminando os saltos bruscos que incomodavam os usuários.
3. **Localização 100% em Português (PT-BR)**: Eliminar a mistura de inglês e português na interface. Todos os textos para o usuário (menus da bandeja, janela de configurações, botões, diálogos, tooltips e mensagens) devem estar unificados em português.
4. **Menu e opções dedicadas no topo de cada card**: Paridade com o Copyous no cabeçalho de cada card:
   - Botão de fixar/desafixar direto no card (`Pin`).
   - Botão de menu do card (`...`), abrindo o menu de contexto com opções do item (Copiar, Colar, Fixar, Ações, Tags, Editar título/conteúdo, Excluir).
   - Botão de exclusão rápida direto no card (`Excluir`).
   - Tempo relativo amigável no cabeçalho ("agora", "há 5m", "há 2h").
   - Abertura do menu também via clique com botão direito no card.
5. **Exibição enriquecida de Emojis e Caracteres**:
   - Para itens do tipo `Character` / Emoji, exibir um preview dedicado com fonte `Segoe UI Emoji` em tamanho grande (52-56px), centralizado vertical e horizontalmente no card, com identificação do ponto de código Unicode (ex: `U+1F680`) e subtítulo amigável ("Emoji • U+1F680").
   - Suporte a `Segoe UI Emoji` no preview geral de texto para que emojis em textos normais também renderizem coloridos e nítidos.
6. **Suíte de testes automatizados**: Atualização e expansão dos testes unitários em `WindowsCM.Core.Tests` para validar a formatação de emojis, cálculos de dimensionamento em tela cheia e ausência de regressões.

## Answer
Implementado em 2026-09-12. Todos os 5 requisitos foram concluídos e validados:
1. **Largura total (Horizontal Fill)**:
   - Métodos `CalculateHorizontalFillWidth` e `PlaceHorizontalFill` criados em `PopupPlacement.cs`.
   - `PopupWindow.xaml` e `PopupWindow.xaml.cs` agora expandem para preencher a largura útil da área de trabalho do monitor ativo com margens de 20px, permitindo visualizar 6 a 8 cards simultaneamente.
2. **Rolagem horizontal suave (Smooth Scroll)**:
   - Implementado suporte a animação contínua por pixel via propriedade anexada `AnimatedOffsetProperty` com `DoubleAnimation` e curva `QuadraticEase` (EaseOut, 200ms).
   - Acúmulo de notches da roda do mouse (`Math.Clamp`) eliminando saltos rígidos e engasgos.
3. **Localização 100% PT-BR**:
   - Menus da bandeja (`TrayMenu.cs`: Abrir, Modo anônimo, Limpar histórico, Configurações, Sair).
   - Janela de configurações (`SettingsWindow.xaml` e `.cs`: Atalhos de Teclado, Inicialização, Bandeja do Sistema, Pastas, Versões, Créditos, orientações e feedback de atalhos).
   - Diálogos e mensagens (`TextInputDialog`: Cancelar / OK; `QrWindow`: Código QR; `App.xaml.cs`: balões e avisos em português).
   - Textos de tipos e contadores (`ItemDisplayFormatter.cs`: "Texto • {len} caracteres", "Arquivo", etc.).
4. **Menu e botões dedicados por card (Paridade Copyous)**:
   - Cabeçalho de cada card no `PopupWindow.xaml` atualizado com:
     - Tempo relativo localizado (`RelativeTimeConverter`: "agora", "há 5 min", "há 2 h").
     - Botão de fixar/desafixar (`OnCardPinButtonClicked`).
     - Botão de menu `...` (`OnCardMenuButtonClicked`).
     - Botão de exclusão rápida (`OnCardDeleteButtonClicked`).
   - Clique com o botão direito no card (`OnCardMouseRightButtonUp`) e botão `...` abrem `ShowCardContextMenu`: Colar, Copiar, Fixar/Desafixar, Ações aplicáveis, Gerar código QR, Submenu de Tags (9 cores + remover tag), Editar título, Editar conteúdo e Excluir.
5. **Exibição enriquecida de Emojis**:
   - `ItemDisplayFormatter`: detecção de emojis via `IsEmoji`, formatação de ponto de código Unicode `GetUnicodeCodePoint` (ex: `U+1F680`), títulos "Emoji" e subtítulos "Emoji • U+1F680".
   - `PopupWindow.xaml`: contêiner dedicado para `CharacterPreviewVisibility` com `FontFamily="Segoe UI Emoji, Apple Color Emoji, Noto Color Emoji, Segoe UI Symbol"`, tamanho 54px centralizado e código Unicode monoespaçado.
   - Suporte a `Segoe UI Emoji` adicionado também ao preview geral de texto e código.
6. **Testes automatizados**:
   - 9 novos testes adicionados em `ItemDisplayFormatterTests` e `PopupPlacementTests`.
   - Suíte de 755 testes executada com 100% de sucesso e 0 warnings/erros.

