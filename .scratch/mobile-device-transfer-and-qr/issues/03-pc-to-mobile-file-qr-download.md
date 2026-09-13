Status: resolved
Type: task

## Answer
Implementado em `QrWindow.xaml`, `QrWindow.xaml.cs`, `PopupConverters.cs` e `App.xaml.cs`:
- QR Code habilitado para todos os cards (`File`, `Files`, `Image`, etc.).
- Geração de URLs de sessão efêmeras servidas pelo `MiniTransferHttpServer`.
- `QrWindow` agora exibe metadados ricos do arquivo (título, tamanho, ícone), link local com botão de cópia e botão "Escolher outro arquivo..." para envio de qualquer arquivo do disco.

## Descrição

Expandir a política de QR Code (`QrActions`) e o diálogo `QrWindow` para suportar itens de todos os tipos (`ItemKind.File`, `ItemKind.Files`, `ItemKind.Image`, além dos tipos de texto), gerando URLs locais acessíveis pelo celular e permitindo também compartilhar qualquer arquivo arbitrário do disco.

## Requisitos

- Atualizar `QrActions.IsSupported`:
  - Retornar `true` para todos os `ItemKind` que possuam conteúdo válido (incluindo `File`, `Files` e `Image`).
- Gerador de URL de compartilhamento:
  - Para arquivos/imagens: registrar um token efêmero ou ID no `LocalTransferServer` e gerar a URL correspondente: `http://<ip>:<porta>/d/{token}`.
  - Para textos curtos: manter a opção de QR code com texto puro e/ou URL de transferência para facilidade de leitura.
- Atualização do diálogo `QrWindow`:
  - Exibir o código QR renderizado com `QRCoder`.
  - Exibir informações amigáveis do arquivo (nome, extensão, tamanho formatado e miniatura quando disponível).
  - Exibir a URL local completa com botão "Copiar Link".
  - Adicionar botão "Escolher outro arquivo..." para permitir o envio de qualquer arquivo do computador mesmo que não esteja previamente na área de transferência.
