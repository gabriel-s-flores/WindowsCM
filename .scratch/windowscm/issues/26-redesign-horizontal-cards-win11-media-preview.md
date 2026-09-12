# 26: Redesign com Cards Horizontais, Identidade Windows 11 e Previews Expandidos

**Type:** task

**Status:** resolved

**Blocked by:** 21, 23

**What to build:**
1. Formatação limpa de itens de arquivo e mídia: extrair apenas o nome do arquivo (`Path.GetFileName`) e tipo descritivo ("Imagem PNG", "Vídeo MP4", "Código C#"), nunca expondo o caminho absoluto ou `file:///...`.
2. Habilitar carregamento de miniaturas para arquivos de imagem copiados do Explorer (`ItemKind.File`).
3. Layout horizontal com cards (250x230px) no popup (880x320px) em container de rolagem horizontal com suporte a scroll do mouse e setas do teclado.
4. Preview ampliado para código e texto com fonte monoespaçada (`Cascadia Code` / `Consolas`) permitindo visualização de 6 a 8 linhas.
5. Botões de ação da barra superior com ícones nativos `Segoe Fluent Icons` / `Segoe MDL2 Assets` (32x32px), estados acentuados para Pinned e Incognito, e ToolTips informativos com atalhos.
6. Atualização dos testes unitários de sizing, placement e criação de novos testes para o `ItemDisplayFormatter`.

- [x] Formatação de arquivos/mídias sem caminhos absolutos e com miniaturas ativas
- [x] Previews de código e texto expandidos em cards horizontais
- [x] Botões da barra com glifos Segoe Fluent Icons e sem cortes de texto
- [x] Identidade visual Windows 11 Fluent Design
- [x] Suíte de testes unitários 100% verde

## Answer
Implementado em 2026-09-12. Todos os requisitos atendidos:
1. `ItemDisplayFormatter` criado em `WindowsCM.Core.Popup` com lógica pura e 100% testada (10 novos testes): extração de nomes de arquivos, identificação de tipos/extensões (PNG, JPEG, GIF, MP4, MP3, PDF, C#, etc.), preview de múltiplas linhas e glifos do Segoe Fluent Icons.
2. `PopupConverters.cs` atualizado:
   - `TitleLineConverter` delega para `ItemDisplayFormatter.GetTitle` (nunca expõe caminhos absolutos nem `file:///...`).
   - `KindLabelConverter` exibe tipos humanizados.
   - `ImageThumbConverter` agora carrega tanto imagens salvas em cache quanto arquivos locais de imagem do Windows Explorer com resolução ampliada (`DecodePixelHeight = 180`).
   - Conversores de visibilidade e previews dedicados adicionados.
3. `PopupWindow.xaml` redesenhado para 880x320px com cards horizontais (250x240px), rolagem horizontal fluida (suporte ao scroll do mouse e setas do teclado), barra de busca Windows 11 Fluent com ícone de lupa e botões compactos de 32x32px com ícones Segoe Fluent Icons (`\uE718` Pin, `\uE727` Incognito, `\uE74D` Clear, `\uE713` Settings) e estados acentuados.
4. Testes unitários atualizados em `PopupSizingTests` (880x320) e `PopupPlacementTests` (clamping 1080p horizontal). Suíte completa com 746 testes verdes e zero warnings/erros.
5. ADR 0001 registrado em `docs/adr/0001-horizontal-cards-layout-windows11.md`.
