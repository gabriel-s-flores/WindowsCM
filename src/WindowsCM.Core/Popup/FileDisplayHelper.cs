// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.IO;
using WindowsCM.Core.History;
using WindowsCM.Core.Localization;

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

public sealed record AudioMetadataInfo(
    string Title,
    string Artist,
    string Album,
    TimeSpan? Duration,
    string FormattedDuration,
    long? FileSizeBytes,
    string FormattedSize)
{
    public bool HasArtistOrAlbum => !string.IsNullOrWhiteSpace(Artist) || !string.IsNullOrWhiteSpace(Album);

    public string ArtistAndAlbumSummary
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Album))
            {
                return $"{Artist} • {Album}";
            }
            if (!string.IsNullOrWhiteSpace(Artist))
            {
                return Artist;
            }
            return Album ?? "";
        }
    }

    public string DurationAndSizeSummary
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FormattedDuration) && !string.IsNullOrWhiteSpace(FormattedSize))
            {
                return $"{FormattedDuration} • {FormattedSize}";
            }
            if (!string.IsNullOrWhiteSpace(FormattedDuration))
            {
                return FormattedDuration;
            }
            return FormattedSize ?? "";
        }
    }
}

public static class FileDisplayHelper
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null || duration.Value <= TimeSpan.Zero)
        {
            return "";
        }

        var d = duration.Value;
        if (d.TotalHours >= 1)
        {
            return $"{(int)d.TotalHours}:{d.Minutes:D2}:{d.Seconds:D2}";
        }
        return $"{d.Minutes:D2}:{d.Seconds:D2}";
    }

    public static string FormatFileSize(long? bytes, bool? isPortuguese = null)
    {
        if (bytes is null || bytes < 0)
        {
            return "";
        }
        var culture = (isPortuguese ?? LocalizationManager.IsPortuguese) ? PtBr : CultureInfo.InvariantCulture;
        var b = bytes.Value;
        if (b < 1024)
        {
            return $"{b} B";
        }
        if (b < 1024 * 1024)
        {
            var kb = b / 1024.0;
            return $"{kb.ToString("0.0", culture)} KB";
        }
        if (b < 1024L * 1024L * 1024L)
        {
            var mb = b / (1024.0 * 1024.0);
            return $"{mb.ToString("0.0", culture)} MB";
        }
        var gb = b / (1024.0 * 1024.0 * 1024.0);
        return $"{gb.ToString("0.0", culture)} GB";
    }

    public static string GetFileTypeLabel(string? extension, bool? isPortuguese = null)
    {
        var ext = extension?.ToLowerInvariant() ?? "";
        var pt = isPortuguese ?? LocalizationManager.IsPortuguese;
        if (pt)
        {
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
        else
        {
            return ext switch
            {
                ".exe" => "Executable Application",
                ".msi" => "Windows Installer Package",
                ".pdf" => "PDF Document",
                ".docx" or ".doc" => "Word Document",
                ".xlsx" or ".xls" => "Excel Spreadsheet",
                ".pptx" or ".ppt" => "PowerPoint Presentation",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Compressed Archive",
                ".png" => "PNG Image",
                ".jpg" or ".jpeg" => "JPEG Image",
                ".gif" => "GIF Image",
                ".webp" => "WebP Image",
                ".bmp" => "BMP Image",
                ".svg" => "SVG Vector Image",
                ".ico" => "Windows Icon",
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => "Video",
                ".mp3" or ".wav" or ".flac" or ".ogg" => "Audio",
                ".cs" => "C# Source Code",
                ".js" => "JavaScript Source Code",
                ".ts" => "TypeScript Source Code",
                ".py" => "Python Source Code",
                ".json" => "JSON File",
                ".xml" => "XML File",
                ".html" or ".htm" => "HTML Document",
                ".css" => "CSS Stylesheet",
                ".md" => "Markdown Document",
                ".txt" => "Text Document",
                _ when !string.IsNullOrEmpty(ext) => $"{ext.TrimStart('.').ToUpperInvariant()} File",
                _ => "File"
            };
        }
    }

    public static FileDisplayDetails? GetFileDetails(
        ClipboardItem item,
        Func<string, long?>? probeFileSize = null,
        bool? isPortuguese = null)
    {
        probeFileSize ??= TryGetDiskFileSize;
        var pt = isPortuguese ?? LocalizationManager.IsPortuguese;

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
            var fallback = pt ? "Arquivo" : "File";
            return new FileDisplayDetails(
                FileName: fallback,
                Extension: "",
                TypeLabel: fallback,
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
                TypeLabel: GetFileTypeLabel(ext, pt),
                FormattedSize: FormatFileSize(size, pt));
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
            FileName: pt ? $"{lines.Count} arquivos" : $"{lines.Count} files",
            Extension: "",
            TypeLabel: pt ? $"{lines.Count} arquivos selecionados" : $"{lines.Count} selected files",
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
