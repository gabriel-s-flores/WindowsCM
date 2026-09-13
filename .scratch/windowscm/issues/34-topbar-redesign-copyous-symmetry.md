# Issue 34: Redesign da Top Bar com paridade de simetria e elegância do Copyous

Status: resolved
Type: task
Blocked by: 33

## Contexto

Atualmente no WindowsCM:
1. **Assimetria e Falta de Harmonia**:
   - A barra superior (`Top Bar`) posiciona a caixa de pesquisa alinhada à esquerda com largura fixa de 320px (`SearchBox`), enquanto os 4 botões de ação (Fixados, Incognito, Limpar e Configurações) ficam todos amontoados no extremo direito.
   - Entre a barra de pesquisa e os botões de ação existe um enorme vazio visual desbalanceado, quebrando a estética e a densidade visual.
2. **Comparação com o Copyous (GNOME)**:
   - No Copyous original, a barra superior possui uma distribuição harmônica de 3 seções:
     - **Esquerda**: Controles de sistema/utilidades circulares/arredondados (Configurações `⚙` e Modo Anônimo / Pausar captura `🔕`).
     - **Centro**: Cápsula de busca centralizada e elegante (`[ 🔍▾  Digite para pesquisar...   📌 ]`), com seletor/dropdown de tipos no ícone de lupa e botão de alternar visualização de fixados (`📌`) embutido na própria cápsula de busca.
     - **Direita**: Botão pílula dedicado de ação "Limpar" (`Clear`).
   - Essa estrutura de 3 colunas com colunas laterais de peso visual equivalente garante que a cápsula de busca fique perfeitamente centralizada e o conjunto apresente alta elegância, simetria e usabilidade rápida.

## Escopo e Requisitos

1. **Layout de 3 Colunas com Centralização Perfeita**:
   - Reestruturar o container superior do `PopupWindow.xaml` utilizando um `Grid` com 3 colunas (`1*`, `Auto`, `1*`), garantindo centralização matemática da cápsula de busca independente da resolução ou escala DPI.
2. **Seção Esquerda (Configurações e Modo Anônimo)**:
   - Botão de Configurações (`⚙` - `\uE713`) com formato circular/pílula (`CornerRadius="16"`, 32x32px) e tooltip explicativo.
   - Botão de Modo Anônimo / Pausar Captura (`🔕` - `\uE727`) com formato circular/pílula, destacando visualmente quando o modo anônimo estiver ativo.
3. **Seção Central (Cápsula de Busca Centralizada)**:
   - Cápsula com bordas arredondadas no estilo pílula (`CornerRadius="16"` ou `"17"`, altura 34px, largura adaptativa ~380-440px).
   - Lado esquerdo da cápsula: Ícone de pesquisa com indicador de menu dropdown (`🔍 ▾` - `\uE721` e `\uE70D`), permitindo abrir o menu de filtro por tipo (Todos os tipos, Links, Códigos, Arquivos, Imagens, Emojis, Cores, Textos).
   - Centro da cápsula: Campo de texto de busca (`SearchBox`) com texto watermark / placeholder "Digite para pesquisar..." quando vazio.
   - Lado direito da cápsula: Botão de fixados (`📌` - `\uE718`) integrado à cápsula de busca, alternando o filtro de itens favoritados com feedback visual nítido (ativo/inativo).
4. **Seção Direita (Botão Limpar)**:
   - Botão em formato de pílula (`CornerRadius="16"`, altura 32px, padding horizontal generoso) com o texto "Limpar", combinando simetricamente com o peso visual do grupo esquerdo.
   - Aciona a limpeza com preservação de itens protegidos (`ClearKeepProtected`).
5. **Suporte Completo a Temas Fluent (Claro e Escuro)**:
   - Cores de fundo, borda, hover, texto e ícones perfeitamente integrados em `PopupThemeBrushes.cs` tanto no modo Claro quanto no modo Escuro.
6. **Lógica Pura e Testes Unitários (TDD)**:
   - Suporte a filtro explícito de tipos (`SetTypeFilter`, `TypeFilter`) no `PopupViewModel`.
   - Testes unitários para garantir que a busca, filtros de tipos, fixados e ações continuam 100% íntegros.

## Answer

Implementação concluída com sucesso seguindo o ciclo TDD de Matt Pocock:

1. **Lógica Pura no Core (`PopupViewModel`)**:
   - Adicionados os métodos puros `SetTypeFilter(ItemKind? kind)`, `SetTagFilter(string? tag)` e `ClearAllFilters()`.
   - Testes unitários dedicados em `PopupViewModelTests` cobrindo filtragem por tipos específicos, limpeza de filtros e manutenção do índice de seleção.
2. **Padrão de Simetria e Harmonia em 3 Colunas (`PopupWindow.xaml`)**:
   - Substituído o cabeçalho antigo por um `Grid` com colunas `1*`, `Auto`, `1*`.
   - A coluna central contendo a cápsula de busca (largura de 420px, altura de 34px e `CornerRadius="17"`) fica matematicamente alinhada ao eixo central da janela em qualquer resolução ou DPI.
   - Coluna esquerda: botões circulares elegantes para Configurações (`⚙` - `\uE713`) e Modo Anônimo (`🔕` - `\uE727`), totalizando ~72px de largura.
   - Coluna direita: botão pílula "Limpar" (`FluentPillButton`, min-width 74px), gerando equivalência perfeita de peso visual com os botões da esquerda.
3. **Cápsula de Busca Integrada (Paridade Copyous)**:
   - Seletor de tipos `🔍 ▾` na borda esquerda: abre menu de contexto Fluent com ícones e checkmarks para filtrar instantaneamente por Links, Códigos, Arquivos, Imagens, Emojis, Cores ou Textos, atualizando dinamicamente o ícone e a cor semântica do botão.
   - Marca d'água fluida (`SearchPlaceholder`) exibindo "Digite para pesquisar..." quando o campo estiver vazio, desaparecendo imediatamente ao digitar.
   - Botão de Fixados (`📌` - `\uE718`) integrado na extremidade direita da cápsula de busca, com destaque em azul accent (`#0078D4`) e ícone branco quando ativo.
4. **Paleta Dinâmica Fluent (Claro e Escuro)**:
   - Adicionadas definições de brushes em `PopupThemeBrushes.cs` para `SearchPlaceholderBrush`, `SearchDropdownHoverBrush`, `SearchCapsulePinHoverBrush` e a família de cores `PillButton` (fundo, borda, texto, hover e pressed) para ambos os modos Claro e Escuro.
5. **Verificação**:
   - 887 testes unitários passando 100% verdes (`dotnet test`).
   - Compilação com 0 avisos e 0 erros (`dotnet build`).

