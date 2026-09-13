Status: resolved
Type: task

## Answer
Implementado em `WindowsCM.Core.Transfer`:
- `LocalNetworkResolver`: resolução inteligente de IP local com priorização de Wi-Fi/Ethernet físico com default gateway ativo sobre adaptadores virtuais.
- `MiniTransferHttpServer`: servidor HTTP/1.1 assíncrono sobre `TcpListener`, eliminando a necessidade de privilégios de administrador ou `netsh urlacl`.
- Coberto por testes em `TransferServerTests.cs`.

## Descrição

Implementar em `WindowsCM.Core.Transfer` o servidor `LocalTransferServer` utilizando `System.Net.Sockets.TcpListener` (para execução sem privilégios de administrador no Windows) e `LocalNetworkResolver` para detecção do IP de rede local (Wi-Fi/Ethernet) prioritário.

## Requisitos

- `LocalNetworkResolver`:
  - Enumera interfaces ativas (`OperationalStatus == Up`, não-loopback, não-link-local).
  - Prioriza adaptadores físicos com Default Gateway configurado (ex.: Wi-Fi ou Ethernet) sobre adaptadores virtuais (VirtualBox/VMware/VPN).
  - Fornece lista de todos os IPs locais válidos caso o usuário queira alternar a rede.
- `LocalTransferServer`:
  - Escuta em `IPAddress.Any` na porta padrão (ex: 58921) ou busca porta livre disponível.
  - Processa requisições HTTP/1.1 de forma totalmente assíncrona.
  - Suporta rotas REST simples: `GET /`, `GET /download`, `GET /d/{token}`, `POST /api/upload`, `GET /api/ping`.
  - Suporta streaming seguro de arquivos locais com validação de caminho para impedir directory traversal (`Path.GetFullPath` comparado à raiz permitida).
  - Determina MIME types corretos para áudio (`audio/mpeg`, `audio/wav`, `audio/ogg`, etc.), imagens (`image/png`, `image/jpeg`, etc.), vídeos e documentos.
  - Totalmente testável via testes unitários com `HttpClient`.
