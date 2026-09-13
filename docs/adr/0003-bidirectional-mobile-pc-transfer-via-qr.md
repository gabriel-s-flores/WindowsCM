# 3. Transferência Bidirecional de Arquivos e Clipboard entre PC e Dispositivos Móveis via QR Code e Servidor HTTP Local

Data: 2026-09-13

## Contexto

O WindowsCM possuía suporte a QR Code limitado estritamente a textos curtos (`Text`, `Code`, `Link`, `Character`, `Color`). Para itens de arquivo (`File`, `Files`) e imagens (`Image`), o QR Code não estava disponível porque um código QR óptico padrão possui limite físico de capacidade (~2-3 KB), impossibilitando a codificação direta de arquivos de mídia (como áudios MP3, gravações, fotos, vídeos ou documentos).

Além disso, não existia uma forma direta de o usuário enviar dados do smartphone para o computador (fluxo reverso), forçando o uso de aplicativos terceiros (WhatsApp Web, e-mails para si mesmo ou nuvens externas) para transferir um simples texto, link ou arquivo do celular para a área de transferência do Windows.

No ecossistema Windows, a API nativa `System.Net.HttpListener` baseia-se no driver de kernel `http.sys`, o qual exige privilégios de Administrador (`netsh http add urlacl`) para vincular portas a endereços IP de rede externa ou curingas (`+` / `*`), gerando a exceção `Acesso negado` quando executado em aplicativos padrão de usuário como o WindowsCM.

## Decisão

1. **Servidor HTTP Local Assíncrono via `TcpListener`**:
   - Implementação de um servidor HTTP/1.1 ultra-leve e assíncrono em `WindowsCM.Core.Transfer` utilizando `System.Net.Sockets.TcpListener`.
   - Dispensa privilégios de Administrador, elevação UAC ou comandos manuais de firewall.
   - Escuta em `0.0.0.0` com detecção automática do IP da interface de rede física ativa (Wi-Fi ou Ethernet com Gateway Padrão).
   - Gerenciamento de rotas para servir a aplicação web móvel, downloads de arquivos/mídias e recepção de uploads.

2. **Geração Universal de QR Code para Itens de Histórico (PC -> Celular)**:
   - Extensão do `QrActions` para que itens `File`, `Files` e `Image` também sejam elegíveis para geração de QR Code.
   - Quando o usuário clica no botão de QR Code de um card (seja áudio, imagem, documento ou texto longo), o aplicativo gera uma URL efêmera protegida no servidor local (`http://<ip>:<porta>/d/{token}`) e desenha o código QR correspondente.
   - Ao escanear com o celular, abre-se uma página web móvel responsiva (Fluent Design) com player de áudio integrado (para músicas/áudios), visualizador de fotos (para imagens) ou botão direto de download do arquivo com cabeçalhos MIME e `Content-Disposition` adequados.
   - O diálogo do QR Code no Windows também oferece um botão para escolher qualquer arquivo do computador para envio imediato.

3. **Fluxo Reverso com Botão "Enviar do celular" (Celular -> PC)**:
   - Adição de um botão dedicado com ícone de celular (`\uE8EA`) na barra superior do `PopupWindow` (imediatamente ao lado do botão de Modo Anônimo) e no rodapé do `CompactPopupWindow`.
   - Ao ser clicado, abre a janela `MobileTransferWindow` exibindo o QR Code para conexão do smartphone à página de envio (`http://<ip>:<porta>/`).
   - Na página web móvel aberta no celular, o usuário pode:
     - Colar ou digitar textos e links e enviá-los ao PC com um clique.
     - Selecionar fotos, gravações de áudio ou arquivos do dispositivo móvel e enviá-los com progresso visual.
   - O servidor local recebe o conteúdo e automaticamente:
     - Grava o texto ou arquivo na área de transferência do Windows (`Win32ClipboardWriter` / `CF_UNICODETEXT` ou `CF_HDROP`).
     - Insere o registro no histórico do WindowsCM via `CaptureService`.
     - Exibe feedback imediato (toast do WindowsCM e atualização na janela aberta).

## Consequências

- **Positivas**:
  - Transferência direta, instantânea e privada de arquivos e texto entre computador e celular sem depender da internet ou de servidores externos.
  - Suporte completo a áudio, vídeo, imagens, documentos e textos.
  - Zero dependências pesadas externas e zero necessidade de privilégios de Administrador.
  - Experiência de usuário fluida em smartphones iOS e Android apenas apontando a câmera nativa para o QR Code.
  - Total integração com o histórico do WindowsCM e a área de transferência do Windows.
- **Desafios / Limitações**:
  - Requer que o computador e o smartphone estejam conectados à mesma rede local (Wi-Fi ou LAN).
