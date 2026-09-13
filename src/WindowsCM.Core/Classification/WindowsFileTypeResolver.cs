// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Classification;

public interface IWindowsRegistryAssociations
{
    string? GetPerceivedType(string extension);
}

public sealed class DefaultWindowsRegistryAssociations : IWindowsRegistryAssociations
{
    private static readonly IntPtr HKEY_CLASSES_ROOT = new(unchecked((int)0x80000000));
    private const int ERROR_SUCCESS = 0;
    private const int KEY_READ = 0x20019;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegOpenKeyEx(
        IntPtr hKey, string lpSubKey, int ulOptions, int samDesired, out IntPtr phkResult);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegCloseKey(IntPtr hKey);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegQueryValueEx(
        IntPtr hKey, string lpValueName, IntPtr lpReserved,
        out int lpType, StringBuilder lpData, ref int lpcbData);

    public string? GetPerceivedType(string extension)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var ext = extension.Trim();
        if (!ext.StartsWith('.'))
        {
            ext = "." + ext;
        }

        var status = RegOpenKeyEx(HKEY_CLASSES_ROOT, ext, 0, KEY_READ, out var key);
        if (status != ERROR_SUCCESS)
        {
            return null;
        }

        try
        {
            var capacity = 260;
            var sb = new StringBuilder(capacity);
            var byteCount = capacity * 2;
            status = RegQueryValueEx(key, "PerceivedType", IntPtr.Zero, out _, sb, ref byteCount);
            return status == ERROR_SUCCESS ? sb.ToString().Trim().ToLowerInvariant() : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            RegCloseKey(key);
        }
    }
}

public sealed record ResolvedFileInfo(
    string CategoryId,
    string CategoryName,
    string ColorHex,
    string DisplayLabel)
{
    public bool IsImage => string.Equals(CategoryId, "images", StringComparison.OrdinalIgnoreCase);
    public bool IsVideo => string.Equals(CategoryId, "video", StringComparison.OrdinalIgnoreCase);
    public bool IsAudio => string.Equals(CategoryId, "audio", StringComparison.OrdinalIgnoreCase);
}

public sealed class WindowsFileTypeResolver
{
    private readonly FileCategorySettings _settings;
    private readonly IWindowsRegistryAssociations _registry;

    public WindowsFileTypeResolver(
        FileCategorySettings? settings = null,
        IWindowsRegistryAssociations? registry = null)
    {
        _settings = settings ?? new FileCategorySettings();
        _registry = registry ?? new DefaultWindowsRegistryAssociations();
    }

    public ResolvedFileInfo ResolveFileInfo(string? pathOrExtension, bool? isPortuguese = null)
    {
        var isPt = isPortuguese ?? Localization.LocalizationManager.IsPortuguese;
        var fallbackName = isPt ? "Arquivo" : "File";
        if (string.IsNullOrWhiteSpace(pathOrExtension))
        {
            return new ResolvedFileInfo("file", fallbackName, "#107C41", fallbackName);
        }

        var trimmed = pathOrExtension.Trim();
        var ext = Path.GetExtension(trimmed);
        if (string.IsNullOrEmpty(ext))
        {
            ext = FileCategory.NormalizeExtension(trimmed);
        }
        else
        {
            ext = ext.ToLowerInvariant();
        }

        var displayLabel = GetFriendlyExtensionLabel(ext, isPt);

        // 1. Try matching user-configured categories first (highest priority)
        var userCat = _settings.ResolveCategory(ext);
        if (userCat != null)
        {
            return new ResolvedFileInfo(userCat.Id, userCat.Name, userCat.ColorHex, displayLabel);
        }

        // 2. Query Windows Registry PerceivedType
        var perceived = _registry.GetPerceivedType(ext);
        if (!string.IsNullOrEmpty(perceived))
        {
            var mapped = MapPerceivedType(perceived, displayLabel, isPt);
            if (mapped != null)
            {
                return mapped;
            }
        }

        // 3. Fallback: Generic file
        return new ResolvedFileInfo("file", fallbackName, "#D97706", displayLabel);
    }

    private ResolvedFileInfo? MapPerceivedType(string perceivedType, string displayLabel, bool isPt)
    {
        var normalized = perceivedType.Trim().ToLowerInvariant();
        var category = normalized switch
        {
            "image" => _settings.Categories.FirstOrDefault(c => c.Id == "images")
                ?? new FileCategory("images", isPt ? "Imagens" : "Images", "#16A34A", []),
            "audio" => _settings.Categories.FirstOrDefault(c => c.Id == "audio")
                ?? new FileCategory("audio", isPt ? "Áudio" : "Audio", "#8B5CF6", []),
            "video" => _settings.Categories.FirstOrDefault(c => c.Id == "video")
                ?? new FileCategory("video", isPt ? "Vídeos" : "Videos", "#DC2626", []),
            "document" => _settings.Categories.FirstOrDefault(c => c.Id == "documents")
                ?? new FileCategory("documents", isPt ? "Documentos" : "Documents", "#0078D4", []),
            "compressed" => _settings.Categories.FirstOrDefault(c => c.Id == "archives")
                ?? new FileCategory("archives", isPt ? "Compactados" : "Archives", "#B45309", []),
            _ => null
        };

        if (category != null)
        {
            return new ResolvedFileInfo(category.Id, category.Name, category.ColorHex, displayLabel);
        }

        return null;
    }

    public static string GetFriendlyExtensionLabel(string ext) =>
        GetFriendlyExtensionLabel(ext, Localization.LocalizationManager.IsPortuguese);

    public static string GetFriendlyExtensionLabel(string ext, bool isPortuguese)
    {
        var lower = ext.ToLowerInvariant();
        if (isPortuguese)
        {
            return lower switch
            {
                ".png" => "Imagem PNG",
                ".jpg" or ".jpeg" => "Imagem JPEG",
                ".gif" => "Imagem GIF",
                ".webp" => "Imagem WebP",
                ".bmp" => "Imagem BMP",
                ".svg" => "Imagem Vetorial SVG",
                ".ico" => "Ícone ICO",
                ".mp4" => "Vídeo MP4",
                ".mkv" => "Vídeo MKV",
                ".avi" => "Vídeo AVI",
                ".mov" => "Vídeo MOV",
                ".webm" => "Vídeo WebM",
                ".mp3" => "Áudio MP3",
                ".wav" => "Áudio WAV",
                ".flac" => "Áudio FLAC",
                ".pdf" => "Documento PDF",
                ".docx" or ".doc" => "Documento Word",
                ".xlsx" or ".xls" => "Planilha Excel",
                ".pptx" or ".ppt" => "Apresentação PowerPoint",
                ".zip" or ".rar" or ".7z" => "Arquivo Compactado",
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

        return lower switch
        {
            ".png" => "PNG Image",
            ".jpg" or ".jpeg" => "JPEG Image",
            ".gif" => "GIF Image",
            ".webp" => "WebP Image",
            ".bmp" => "BMP Image",
            ".svg" => "SVG Vector Image",
            ".ico" => "ICO Icon",
            ".mp4" => "MP4 Video",
            ".mkv" => "MKV Video",
            ".avi" => "AVI Video",
            ".mov" => "MOV Video",
            ".webm" => "WebM Video",
            ".mp3" => "MP3 Audio",
            ".wav" => "WAV Audio",
            ".flac" => "FLAC Audio",
            ".pdf" => "PDF Document",
            ".docx" or ".doc" => "Word Document",
            ".xlsx" or ".xls" => "Excel Spreadsheet",
            ".pptx" or ".ppt" => "PowerPoint Presentation",
            ".zip" or ".rar" or ".7z" => "Compressed Archive",
            ".cs" => "C# Code",
            ".js" => "JavaScript Code",
            ".ts" => "TypeScript Code",
            ".py" => "Python Code",
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
