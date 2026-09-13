// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Transfer;

// Represents a resource prepared on the PC for download/viewing by a mobile device.
public sealed record SharedItemSession(
    string Token,
    long? ItemId,
    string Title,
    string KindLabel,
    string? FilePath,
    IReadOnlyList<string>? FilePaths,
    string? TextContent,
    byte[]? RawBytes,
    string FileName,
    string ContentType,
    long FileSize,
    DateTime CreatedAt);

// File received from a mobile device and saved locally on the PC.
public sealed record IncomingFile(
    string FileName,
    string ContentType,
    string SavedPath,
    long FileSize);

// Payload received from a mobile device (text, files, or both).
public sealed record IncomingTransferPayload(
    string? Text,
    IReadOnlyList<IncomingFile> Files,
    DateTime ReceivedAtUtc);
