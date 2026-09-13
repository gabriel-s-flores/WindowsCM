# Spec: Pré-visualização Rica de Websites, Alternância de Temas (Escuro/Claro/Alto Contraste) e Botão de QR Code nos Cards

Status: ready-for-agent

## Problem Statement

1. **Pré-visualizações de Websites**: Atualmente, links web copiados para o histórico do WindowsCM exibem apenas um ícone de favicon e o domínio textual, sem carregar a imagem de pré-visualização (thumbnail OpenGraph / Twitter Card / YouTube) e sem exibir a descrição ou título completo da página, divergindo do Copyous original onde vídeos do YouTube (como a imagem de referência do Rick Astley) e páginas web aparecem com cartões de visualização enriquecida contendo miniatura e detalhes bem definidos. Além disso, o serviço `LinkPreviewService` existente nunca era disparado em segundo plano durante a captura.
2. **Temas (Modo Escuro, Claro e Alto Contraste)**: O WindowsCM não oferece uma opção na interface para o usuário alternar manualmente entre tema escuro e claro, tampouco possui um modo de Alto Contraste para acessibilidade visual e contraste acentuado (fundo preto `#000000`, bordas brancas `#FFFFFF` e realce de foco/seleção de alta visibilidade).
3. **QR Code no Menu Rápido dos Cards**: Para gerar um código QR de um item, o usuário precisa abrir o menu secundário ("...") ou acionar o atalho `Ctrl+Q`. Não há um botão direto de QR Code no menu rápido da barra inferior de cada card.

## Solution

1. **Serviço de Metadados e Pré-visualizações de Websites**:
   - Integrar suporte a URLs do YouTube com extração de ID do vídeo (`watch?v=`, `youtu.be/`, `shorts/`), resolução direta de thumbnail de alta definição (`https://img.youtube.com/vi/{id}/hqdefault.jpg`) e fallback/oEmbed para títulos e autores.
   - Atualizar `LinkPreviewHttpClient` com cabeçalhos de navegador modernos para evitar bloqueios em websites comuns e suportar download de imagens com headers adequados.
   - Disparar o `LinkPreviewService` em segundo plano quando links forem copiados ou exibidos, salvando a imagem em cache de disco (`LinkImageCache`) e atualizando o `MetadataJson` e `Title` no banco de dados SQLite.
2. **Suporte Completo a Alto Contraste e Alternância de Temas**:
   - Implementar paleta de Alto Contraste em `PopupThemeBrushes` (preto absoluto, bordas brancas com espessura nítida, textos brancos de alto contraste, destaque ciano/amarelo para seleção e fixados).
   - Adicionar controle de seleção de tema na tela de Configurações (`SettingsWindow`) com opções para Modo Escuro, Modo Claro, Alto Contraste e Seguir o Windows, com persistência e atualização dinâmica imediata em todas as janelas.
3. **Botão de Ação Rápida de QR Code**:
   - Incluir botão com o ícone oficial de QR Code (`\uED14`) na barra de ações rápidas de cada card no `PopupWindow` e `CompactPopupWindow`, acionando diretamente o diálogo do QR Code para itens compatíveis.
4. **Build e Empacotamento**:
   - Compilar publicação release `win-x64`, gerar arquivo zip portátil em `dist/` e compilar o instalador Inno Setup com `ISCC.exe`.

## User Stories

1. As a user, when I copy a link to a YouTube video or website, I want to see a rich preview card with a high-definition thumbnail image, title, and description, just like in Copyous.
2. As a user, I want to be able to switch between Dark mode, Light mode, and High Contrast mode in Settings, so that the app matches my visual preference and accessibility needs.
3. As a user with visual impairments, I want a High Contrast mode that provides pure black backgrounds and high-contrast white borders and text.
4. As a user, I want a direct QR code button on the quick action bar of each clipboard card, so that I can generate a QR code with a single click without opening submenus.
5. As a user, I want both the portable zip and installer executable to be updated and ready to use after the improvements are completed.
