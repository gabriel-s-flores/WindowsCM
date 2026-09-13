# 31: Favicons de Websites para Links Copiados e Separação Sutil por Cores de Tipo de Item

Type: task

Status: resolved

Blocked by: 30

## User Report / Requirements:
1. **Websites e Links Copiados com Favicon/Ícone na Thumbnail**:
   - Ao copiar uma URL/website, o ícone oficial (favicon) desse site deve ser exibido na thumbnail / preview do card no popup.
   - O card deve apresentar visualização rica dedicada para links (`LinkPreviewVisibility`): ícone do site em destaque, domínio limpo (ex: `github.com`), título/caminho amigável e URL formatada.
   - Resolução veloz de favicons (cache local em disco e memória, download assíncrono não bloqueante via CDN de alta disponibilidade com fallback elegante para ícone web Fluent quando offline).
2. **Separação Sutil por Cores para Cada Tipo de Item**:
   - Separar visualmente, com sutileza e elegância Fluent (Windows 11), cada tipo de item na clipboard:
     - `Link`: Azul Fluent (`#0078D4` / `#4CC2FF`)
     - `Code`: Roxo / Púrpura Fluent (`#8764B8` / `#B180F0`)
     - `File` / `Files`: Âmbar / Laranja Suave Fluent (`#D97706` / `#F59E0B`)
     - `Image`: Esmeralda / Verde Fluent (`#107C41` / `#36B66B`)
     - `Character` (Emoji/Caractere): Rosa / Coral Suave (`#E83B86` / `#F472B6`)
     - `Color`: A própria cor representada ou Magenta
     - `Text`: Cinza Ardósia Neutro / Slate (`#64748B` / `#94A3B8`)
   - Aplicação sutil:
     - Ícone de tipo no cabeçalho do card colorido com a cor de acento do tipo.
     - Indicador vertical no cabeçalho: se o item não possuir Tag manual definida pelo usuário, exibe a cor do tipo de item como indicador suave.
     - Badge / pílula de tipo no rodapé do card (`KindLabel`) com fundo translúcido sutil (tinted background ~10-14%) e bolinha indicadora na cor do tipo.
     - Suporte nativo tanto para o tema Claro quanto Escuro do Windows 11.

## Answer
Implementado e validado em 2026-09-12 seguindo as skills de Matt Pocock (`codebase-design`, `domain-modeling` e `tdd`):

1. **Favicons Oficiais de Websites em Miniaturas de Links (`ItemKind.Link`)**:
   - Criado `LinkDisplayHelper.cs` em `WindowsCM.Core.Popup`: módulo puro para extração de host/domínio limpo (`GetDomain`, removendo `www.`, portas e parâmetros), resolução canônica de endpoint de favicons em alta definição (`GetFaviconCdnUrl`), formatação de caminhos e títulos amigáveis (`GetPathOrTitle`) e exibição higienizada de URLs (`GetDisplayUrl`).
   - Criado `FaviconService.cs` em `WindowsCM.App`: gerenciador com cache em 2 níveis (RAM em `ConcurrentDictionary` com `BitmapSource` congelados para renderização em 0ms; e disco persistido em `%LocalAppData%\WindowsCM\favicons\{domain}.png`). Busca favicons de forma assíncrona em segundo plano via CDN global resiliente (Google Favicons API sz=64), sem bloquear o loop de captura ou a UI. Notifica reativamente a UI (`FaviconUpdated`) para atualizar os cards de links na hora em que o ícone estiver pronto.
   - Criado caso dedicado `LinkPreviewVisibility` no template de card em `PopupWindow.xaml`: substitui a exibição crua de texto por um container Fluent elegante de 46x46 px com cantos arredondados contendo o favicon oficial do site (ou ícone Fluent `\uE774` como fallback gracioso caso offline), domínio em semi-bold, título ou caminho da página e URL formatada.

2. **Separação Sutil por Cores por Tipo de Item (Fluent Windows 11)**:
   - Criado `ItemTypeTheme.cs` em `WindowsCM.Core.Popup`: mapeamento semântico determinístico de cores de acento e fundos translúcidos (12-16% alpha) para os 8 tipos do domínio (`Link`, `Code`, `File`/`Files`, `Image`, `Character`, `Color`, `Text`) com contraste balanceado tanto no tema Claro quanto Escuro. Itens de cor assumem dinamicamente a própria cor copiada.
   - Atualizado `PopupThemeBrushes.cs` com os brushes semânticos `KindLinkBrush`, `KindCodeBrush`, `KindFileBrush`, etc. e paleta de containers de links/favicons.
   - Criados os conversores `KindBrushConverter`, `KindBackgroundBrushConverter` e `CardIndicatorBrushConverter` em `PopupConverters.cs`.
   - No `PopupWindow.xaml`:
     - O ícone do tipo no cabeçalho do card agora recebe dinamicamente a cor de acento do seu tipo (`KindBrush`).
     - A barra indicadora lateral de 3x14 px exibe a tag manual caso o usuário tenha classificado o item; se não houver tag manual, ela exibe sutilmente a cor do tipo do item.
     - O subtítulo inferior de tipo foi transformado em uma pílula / badge Fluent com cantos arredondados (`CornerRadius="4"`), fundo suave translúcido (`KindBackgroundBrush`), bolinha indicadora e tipografia semi-bold na cor do tipo.

3. **Testes Unitários e Qualidade (TDD)**:
   - Criados `LinkDisplayHelperTests.cs` e `ItemTypeThemeTests.cs`.
   - Total de 827 testes executados com 100% de aprovação e zero falhas (`dotnet test`).
   - Compilação de `WindowsCM.App` sem nenhum erro ou warning.
