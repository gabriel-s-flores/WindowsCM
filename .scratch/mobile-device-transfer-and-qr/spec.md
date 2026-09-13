# Spec: Transferência Bidirecional de Arquivos e Textos entre Computador e Celular via QR Code

Status: ready-for-agent

## Problem Statement

1. **Limitação Atual do QR Code (Apenas Texto Curto)**: O WindowsCM atualmente suporta geração de QR Code exclusivamente para itens textuais (`Text`, `Code`, `Link`, `Character`, `Color`). Para itens do tipo `File`, `Files` e `Image`, o botão de QR Code fica oculto e o atalho `Ctrl+Q` é ignorado, pois arquivos binários (áudios, fotos, documentos, vídeos) e listas de arquivos excedem a capacidade de codificação direta de caracteres de um QR Code.
2. **Ausência de Envio de Arquivos para Dispositivos Móveis**: Usuários frequentemente precisam enviar músicas (áudio), imagens capturadas, relatórios em PDF ou arquivos diversos do PC para o celular sem depender de serviços externos na nuvem (Google Drive, WhatsApp Web, Telegram, e-mail).
3. **Ausência de Fluxo Reverso (Celular -> PC)**: Não existe um mecanismo prático para o usuário enviar textos, links ou arquivos do smartphone diretamente para o clipboard e histórico do computador.

## Solution

1. **Servidor Local de Transferência HTTP (`LocalTransferServer`)**:
   - Um servidor HTTP/1.1 assíncrono e leve construído sobre `System.Net.Sockets.TcpListener` (dispensando permissões de Administrador / `netsh urlacl`, ao contrário do `HttpListener` do Windows que gera erro de acesso negado para usuários comuns).
   - Detecção inteligente da interface de rede ativa (Wi-Fi/Ethernet com gateway padrão) para descoberta do IP local da máquina.
   - Gerenciamento dinâmico de portas com alocação automática caso a porta padrão esteja ocupada.
   - Endpoints seguros para download de arquivos/textos (`/d/{token}` ou `/download?id={id}`) com Content-Type, Content-Disposition e streaming eficiente de arquivos locais e imagens.
   - Endpoint de recepção (`/upload`) com suporte a multipart/form-data e payloads com texto e arquivos.

2. **Interface Web Móvel Moderna e Responsiva**:
   - Página web servida localmente com identidade visual Fluent Design (suporte automático a tema claro/escuro).
   - **Fluxo Computador -> Celular**:
     - Exibe detalhes do item (nome, tamanho formatado, tipo).
     - Para áudio: player integrado `<audio controls>` + botão de download.
     - Para imagem: pré-visualização da imagem + botão de download.
     - Para arquivos/documentos: botão de download direto com o nome de arquivo original.
     - Para texto/código: visualizador formatado com botão "Copiar para Área de Transferência" do celular.
   - **Fluxo Celular -> Computador**:
     - Área para digitar/colar texto com botão "Enviar Texto para o PC".
     - Área de seleção e arrastar arquivos (fotos da câmera/galeria, áudios, PDFs, qualquer arquivo) com botão "Enviar Arquivo para o PC".
     - Feedback em tempo real com barra de progresso e mensagem de sucesso.

3. **Integração na Interface do WindowsCM**:
   - **Botão "Enviar do celular"**: Adicionado no cabeçalho do `PopupWindow` (ao lado do botão de modo anônimo) e no rodapé do `CompactPopupWindow`, com ícone de smartphone (`\uE8EA`), tooltip descritivo e diálogo dedicado (`MobileTransferWindow`).
   - **Geração de QR Code Universal para Cards**:
     - Habilitar o botão de QR Code e atalho `Ctrl+Q` para TODOS os tipos de itens (`File`, `Files`, `Image`, `Text`, etc.).
     - O diálogo de QR Code exibe o código QR para conexão do celular, miniatura/detalhes do arquivo compartilhado, endereço IP/URL local com botão de cópia e opção de selecionar qualquer arquivo arbitrário do disco.
   - **Integração com Clipboard e Histórico**:
     - Textos recebidos do celular são automaticamente gravados na área de transferência do Windows e no histórico do WindowsCM com notificação sutil (toast).
     - Arquivos recebidos são salvos na pasta de transferências do usuário (`Downloads\WindowsCM Transfers`), colocados no clipboard do Windows como `CF_HDROP` e adicionados ao histórico com miniaturas e categorias semânticas imediatas.
