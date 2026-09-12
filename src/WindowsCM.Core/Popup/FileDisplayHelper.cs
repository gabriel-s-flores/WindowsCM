// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.IO;
using WindowsCM.Core.History;

namespace WindowsCM.Core.Popup;

public sealed record FileItemDetail(
    string FullPath,
    string FileName,
    string Extension,
    string TypeLabel,
    string FormattedSize);

public sealed record FileDisplayDetails(
    string FileName,
    string Extension,
    string TypeLabel,
    string FormattedSize,
    string DirectoryPath,
    bool IsMultiple,
    int FileCount,
    IReadOnlyList<FileItemDetail> Items);

public static class FileDisplayHelper
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string FormatFileSize(long? bytes)
    {
        if (bytes is null || bytes < 0)
        {
            return "";
        }
        var b = bytes.Value;
        if (b < 1024)
        {
            return $"{b} B";
        }
        if (b < 1024 * 1024)
        {
            var kb = b / 1024.0;
            return $"{kb.ToString("0.0", PtBr)} KB";
        }
        if (b < 1024L * 1024L * 1024L)
        {
            var mb = b / (1024.0 * 1024.0);
            return $"{mb.ToString("0.0", PtBr)} MB";
        }
        var gb = b / (1024.0 * 1024.0 * 1024.0);
        return $"{gb.ToString("0.0", PtBr)} GB";
    }

    public static string GetFileTypeLabel(string? extension)
    {
        var ext = extension?.ToLowerInvariant() ?? "";
        return ext switch
        {
            ".exe" => "Aplicativo Executável",
            ".msi" => "Pacote de Instalação",
            ".pdf" => "Documento PDF",
            ".docx" or ".doc" => "Documento Word",
            ".xlsx" or ".xls" => "Planilha Excel",
            ".pptx" or ".ppt" => "Apresentação PowerPoint",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Arquivo Compactado",
            ".png" => "Imagem PNG",
            ".jpg" or ".jpeg" => "Imagem JPEG",
            ".gif" => "Imagem GIF",
            ".webp" => "Imagem WebP",
            ".bmp" => "Imagem BMP",
            ".svg" => "Imagem Vetorial SVG",
            ".ico" => "Ícone do Windows",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => "Vídeo",
            ".mp3" or ".wav" or ".flac" or ".ogg" => "Áudio",
            ".cs" => "Código C#",
            ".js" => "Código JavaScript",
            ".ts" => "Código TypeScript",
            ".py" => "Código Python",
            ".json" => "Arquivo JSON",
            ".xml" => "Arquivo XML",
            ".html" or ".htm" => "Documento HTML",
            ".css" => "Folha de Estilos CSS",
            ".md" => "Documento Markdown",
            ".txt" => "Documento de Texto",
            _ when !string.IsNullOrEmpty(ext) => $"Arquivo {ext.TrimStart('.').ToUpperInvariant()}",
            _ => "Arquivo"
        };
    }

    public static FileDisplayDetails? GetFileDetails(
        ClipboardItem item,
        Func<string, long?>? probeFileSize = null)
    {
        probeFileSize ??= TryGetDiskFileSize;

        if (item.Kind != ItemKind.File && item.Kind != ItemKind.Files)
        {
            return null;
        }

        var lines = (item.Content ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizePath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (lines.Count == 0)
        {
            return new FileDisplayDetails(
                FileName: "Arquivo",
                Extension: "",
                TypeLabel: "Arquivo",
                FormattedSize: "",
                DirectoryPath: "",
                IsMultiple: false,
                FileCount: 0,
                Items: Array.Empty<FileItemDetail>());
        }

        var detailsList = lines.Select(path =>
        {
            var name = Path.GetFileName(path);
            var ext = Path.GetExtension(path);
            var size = probeFileSize(path);
            return new FileItemDetail(
                FullPath: path,
                FileName: string.IsNullOrWhiteSpace(name) ? path : name,
                Extension: ext,
                TypeLabel: GetFileTypeLabel(ext),
                FormattedSize: FormatFileSize(size));
        }).ToList();

        if (lines.Count == 1)
        {
            var single = detailsList[0];
            var dir = Path.GetDirectoryName(single.FullPath) ?? "";
            return new FileDisplayDetails(
                FileName: single.FileName,
                Extension: single.Extension,
                TypeLabel: single.TypeLabel,
                FormattedSize: single.FormattedSize,
                DirectoryPath: dir,
                IsMultiple: false,
                FileCount: 1,
                Items: detailsList);
        }

        return new FileDisplayDetails(
            FileName: $"{lines.Count} arquivos",
            Extension: "",
            TypeLabel: $"{lines.Count} arquivos selecionados",
            FormattedSize: "",
            DirectoryPath: Path.GetDirectoryName(detailsList[0].FullPath) ?? "",
            IsMultiple: true,
            FileCount: lines.Count,
            Items: detailsList);
    }

    private static long? TryGetDiskFileSize(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }
        }
        catch
        {
        }
        return null;
    }

    public static string NormalizePath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return new Uri(trimmed).LocalPath;
            }
            catch
            {
                return trimmed;
            }
        }
        return trimmed;
    }
}
