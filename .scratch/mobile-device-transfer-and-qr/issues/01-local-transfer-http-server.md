Status: resolved
Type: task

## Answer
Implemented in `WindowsCM.Core.Transfer`:
- `LocalNetworkResolver`: smart local IP resolution that prioritizes physical Wi-Fi/Ethernet with an active default gateway over virtual adapters.
- `MiniTransferHttpServer`: asynchronous HTTP/1.1 server on top of `TcpListener`, removing the need for administrator privileges or `netsh urlacl`.
- Covered by tests in `TransferServerTests.cs`.

## Description

Implement the `LocalTransferServer` server in `WindowsCM.Core.Transfer` using `System.Net.Sockets.TcpListener` (to run without administrator privileges on Windows), and `LocalNetworkResolver` to detect the preferred local network IP (Wi-Fi/Ethernet).

## Requirements

- `LocalNetworkResolver`:
  - Enumerates active interfaces (`OperationalStatus == Up`, non-loopback, non-link-local).
  - Prioritizes physical adapters with a configured Default Gateway (e.g. Wi-Fi or Ethernet) over virtual adapters (VirtualBox/VMware/VPN).
  - Provides a list of all valid local IPs in case the user wants to switch networks.
- `LocalTransferServer`:
  - Listens on `IPAddress.Any` on the default port (e.g. 58921) or looks for an available free port.
  - Processes HTTP/1.1 requests fully asynchronously.
  - Supports simple REST routes: `GET /`, `GET /download`, `GET /d/{token}`, `POST /api/upload`, `GET /api/ping`.
  - Supports secure streaming of local files with path validation to prevent directory traversal (`Path.GetFullPath` compared against the allowed root).
  - Determines the correct MIME types for audio (`audio/mpeg`, `audio/wav`, `audio/ogg`, etc.), images (`image/png`, `image/jpeg`, etc.), videos and documents.
  - Fully testable through unit tests with `HttpClient`.
