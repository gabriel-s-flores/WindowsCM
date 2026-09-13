Status: resolved
Type: task

## Answer
Implementado em `WindowsCM.Core.Transfer.MobileWebTemplate`:
- Interface responsiva com design Fluent para celular.
- Seção de download com suporte dedicado para áudio (player HTML5), imagens (preview), vídeos e documentos.
- Seção de upload com abas para textos (com botão de colar) e arquivos (drag and drop, câmera e galeria com barra de progresso).
- Totalmente autocontida, sem requisições para servidores externos.

## Descrição

Criar a página web móvel servida pelo servidor local para exibição no smartphone (iOS e Android) quando o QR Code for escaneado.

## Requisitos

- Design limpo seguindo o estilo Windows 11 Fluent (cantos arredondados, paleta moderna, detecção de modo escuro/claro nativo do navegador via `@media (prefers-color-scheme)`).
- **Visão de Download (PC -> Celular)**:
  - Card com o item a ser baixado:
    - Se for Áudio: título da música/áudio, tamanho em MB, player de áudio HTML5 `<audio controls src="..." style="width: 100%">` e botão estilizado "Baixar Áudio".
    - Se for Imagem: visualizador da imagem em alta resolução com botão "Baixar Imagem".
    - Se for Arquivo/Documento: ícone do tipo de arquivo, nome, tamanho e botão "Baixar Arquivo".
    - Se for Texto/Código/Link: caixa de texto elegante com destaque e botão "Copiar Texto" (usando `navigator.clipboard.writeText` com fallback e toast de confirmação na tela).
- **Visão de Upload (Celular -> PC)**:
  - Aba / Seção "Enviar para o PC":
    - Caixa de texto para colar ou digitar mensagens/links com botão "Enviar Texto".
    - Seletor de arquivo e área de drop com suporte a múltiplos arquivos, fotos da câmera, galeria, áudios e documentos.
    - Botão "Enviar Arquivo" com barra de progresso visual de upload e feedback de envio imediato ("Enviado com sucesso para o computador!").
- Totalmente autocontida (sem CDNs externas, funcionando 100% offline em redes locais sem acesso à internet).
