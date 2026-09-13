// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Reflection;
using WindowsCM.Core.Actions;
using WindowsCM.Core.Classification;
using WindowsCM.Core.History;
using WindowsCM.Core.Localization;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Transfer;
using WindowsCM.Core.Tray;
using Xunit;

namespace WindowsCM.Core.Tests.Localization;

public class LocalizationTests
{
    [Fact]
    public void ResolveIsPortuguese_ExplicitLanguage_OverridesSystemCulture()
    {
        var ptCulture = CultureInfo.GetCultureInfo("pt-BR");
        var enCulture = CultureInfo.GetCultureInfo("en-US");

        // Portuguese selected explicitly
        Assert.True(LocalizationManager.ResolveIsPortuguese(AppLanguage.Portuguese, enCulture));
        Assert.True(LocalizationManager.ResolveIsPortuguese(AppLanguage.Portuguese, ptCulture));

        // English selected explicitly
        Assert.False(LocalizationManager.ResolveIsPortuguese(AppLanguage.English, ptCulture));
        Assert.False(LocalizationManager.ResolveIsPortuguese(AppLanguage.English, enCulture));
    }

    [Theory]
    [InlineData("pt-BR", true)]
    [InlineData("pt-PT", true)]
    [InlineData("pt", true)]
    [InlineData("en-US", false)]
    [InlineData("en-GB", false)]
    [InlineData("es-ES", false)]
    [InlineData("fr-FR", false)]
    [InlineData("de-DE", false)]
    [InlineData("ja-JP", false)]
    [InlineData("zh-CN", false)]
    public void ResolveIsPortuguese_SystemLanguage_DefaultsToEnglishUnlessPortuguese(string cultureName, bool expectedPortuguese)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var result = LocalizationManager.ResolveIsPortuguese(AppLanguage.System, culture);
        Assert.Equal(expectedPortuguese, result);
    }

    [Fact]
    public void StringCatalogs_AllPropertiesPopulatedAndNonNull()
    {
        var pt = new PortugueseAppStrings();
        var en = new EnglishAppStrings();
        var properties = typeof(IAppStrings).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.NotEmpty(properties);

        foreach (var prop in properties)
        {
            if (prop.PropertyType == typeof(string))
            {
                var ptVal = (string?)prop.GetValue(pt);
                var enVal = (string?)prop.GetValue(en);

                Assert.False(string.IsNullOrWhiteSpace(ptVal), $"Portuguese string for {prop.Name} is null or whitespace");
                Assert.False(string.IsNullOrWhiteSpace(enVal), $"English string for {prop.Name} is null or whitespace");
            }
        }
    }

    [Fact]
    public void EnglishCatalog_DoesNotContainUntranslatedPortugueseKeywords()
    {
        var en = new EnglishAppStrings();
        var properties = typeof(IAppStrings).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Portuguese keywords that should not appear in English translations
        var ptKeywords = new[]
        {
            "Configurações",
            "Área de Transferência",
            "Histórico",
            "Fixar",
            "Remover",
            "Limpar",
            "Padrão",
            "Seguir",
            "Pesquisar",
            "Itens",
            "Excluir",
            "Atalho",
            "Dispositivo",
            "Conectar"
        };

        foreach (var prop in properties)
        {
            if (prop.PropertyType == typeof(string))
            {
                var enVal = (string)prop.GetValue(en)!;
                foreach (var kw in ptKeywords)
                {
                    Assert.False(
                        enVal.Contains(kw, StringComparison.OrdinalIgnoreCase),
                        $"English property {prop.Name} contains Portuguese keyword '{kw}': \"{enVal}\"");
                }
            }
        }
    }

    [Fact]
    public void FormatFileSize_FormatsAccordingToLanguageCulture()
    {
        long bytes = 2_500_000; // ~2.4 MB

        var ptSize = FileDisplayHelper.FormatFileSize(bytes, isPortuguese: true);
        var enSize = FileDisplayHelper.FormatFileSize(bytes, isPortuguese: false);

        Assert.Equal("2,4 MB", ptSize);
        Assert.Equal("2.4 MB", enSize);
    }

    [Fact]
    public void GetFileTypeLabel_ReturnsLocalizedLabels()
    {
        Assert.Equal("Documento PDF", FileDisplayHelper.GetFileTypeLabel(".pdf", isPortuguese: true));
        Assert.Equal("PDF Document", FileDisplayHelper.GetFileTypeLabel(".pdf", isPortuguese: false));

        Assert.Equal("Planilha Excel", FileDisplayHelper.GetFileTypeLabel(".xlsx", isPortuguese: true));
        Assert.Equal("Excel Spreadsheet", FileDisplayHelper.GetFileTypeLabel(".xlsx", isPortuguese: false));

        Assert.Equal("Imagem PNG", FileDisplayHelper.GetFileTypeLabel(".png", isPortuguese: true));
        Assert.Equal("PNG Image", FileDisplayHelper.GetFileTypeLabel(".png", isPortuguese: false));

        Assert.Equal("Arquivo", FileDisplayHelper.GetFileTypeLabel("", isPortuguese: true));
        Assert.Equal("File", FileDisplayHelper.GetFileTypeLabel("", isPortuguese: false));
    }

    [Fact]
    public void TrayMenu_ReturnsLocalizedLabels()
    {
        Assert.Equal("Abrir", TrayMenu.LabelFor(TrayMenuItem.Open, isPortuguese: true));
        Assert.Equal("Open", TrayMenu.LabelFor(TrayMenuItem.Open, isPortuguese: false));

        Assert.Equal("Modo anônimo", TrayMenu.LabelFor(TrayMenuItem.Incognito, isPortuguese: true));
        Assert.Equal("Incognito mode", TrayMenu.LabelFor(TrayMenuItem.Incognito, isPortuguese: false));

        Assert.Equal("Configurações", TrayMenu.LabelFor(TrayMenuItem.Settings, isPortuguese: true));
        Assert.Equal("Settings", TrayMenu.LabelFor(TrayMenuItem.Settings, isPortuguese: false));

        Assert.Equal("Sair", TrayMenu.LabelFor(TrayMenuItem.Exit, isPortuguese: true));
        Assert.Equal("Exit", TrayMenu.LabelFor(TrayMenuItem.Exit, isPortuguese: false));
    }

    [Fact]
    public void BuiltinActions_ReturnsLocalizedNames()
    {
        Assert.Equal("Abrir com aplicativo padrão", BuiltinActions.GetLocalizedName(BuiltinActions.OpenWithDefault, "Fallback", isPortuguese: true));
        Assert.Equal("Open with default application", BuiltinActions.GetLocalizedName(BuiltinActions.OpenWithDefault, "Fallback", isPortuguese: false));

        Assert.Equal("Abrir no Explorador de Arquivos", BuiltinActions.GetLocalizedName(BuiltinActions.OpenWithFiles, "Fallback", isPortuguese: true));
        Assert.Equal("Open in File Explorer", BuiltinActions.GetLocalizedName(BuiltinActions.OpenWithFiles, "Fallback", isPortuguese: false));

        Assert.Equal("Gerar código QR", BuiltinActions.GetLocalizedName(BuiltinActions.QrCode, "Fallback", isPortuguese: true));
        Assert.Equal("Generate QR code", BuiltinActions.GetLocalizedName(BuiltinActions.QrCode, "Fallback", isPortuguese: false));
    }

    [Fact]
    public void MobileWebTemplate_RenderDownloadPage_HonorsLanguage()
    {
        var session = new SharedItemSession(
            Token: "tok123",
            ItemId: 1,
            Title: "Test item",
            KindLabel: "Text",
            FilePath: null,
            FilePaths: null,
            TextContent: "Sample text content",
            RawBytes: null,
            FileName: "item.txt",
            ContentType: "text/plain",
            FileSize: 19,
            CreatedAt: DateTime.UtcNow);

        var ptHtml = MobileWebTemplate.RenderDownloadPage(session, "192.168.1.5:8080", isPortuguese: true);
        var enHtml = MobileWebTemplate.RenderDownloadPage(session, "192.168.1.5:8080", isPortuguese: false);

        Assert.Contains("lang=\"pt-BR\"", ptHtml);
        Assert.Contains("📋 Copiar Texto no Celular", ptHtml);
        Assert.Contains("Texto copiado!", ptHtml);

        Assert.Contains("lang=\"en\"", enHtml);
        Assert.Contains("📋 Copy Text on Mobile", enHtml);
        Assert.Contains("Text copied!", enHtml);
    }

    [Fact]
    public void MobileWebTemplate_RenderUploadPage_HonorsLanguage()
    {
        var ptHtml = MobileWebTemplate.RenderUploadPage("192.168.1.5:8080", isPortuguese: true);
        var enHtml = MobileWebTemplate.RenderUploadPage("192.168.1.5:8080", isPortuguese: false);

        Assert.Contains("lang=\"pt-BR\"", ptHtml);
        Assert.Contains("Enviar para o PC", ptHtml);
        Assert.Contains("Arquivos & Mídias", ptHtml);

        Assert.Contains("lang=\"en\"", enHtml);
        Assert.Contains("Send to PC", enHtml);
        Assert.Contains("Files & Media", enHtml);
    }

    [Fact]
    public void LocalizationManager_CurrentLanguage_UpdatesStringsDynamically()
    {
        var originalLang = LocalizationManager.CurrentLanguage;
        try
        {
            LocalizationManager.CurrentLanguage = AppLanguage.English;
            Assert.False(LocalizationManager.IsPortuguese);
            Assert.Equal("Settings", LocalizationManager.Strings.TraySettings);
            Assert.Equal("WindowsCM — Incognito mode", LocalizationManager.Strings.TrayIconTooltipIncognito);
            Assert.Equal("INCOGNITO MODE ACTIVE", LocalizationManager.Strings.PopupIncognitoBannerTitle);

            LocalizationManager.CurrentLanguage = AppLanguage.Portuguese;
            Assert.True(LocalizationManager.IsPortuguese);
            Assert.Equal("Configurações", LocalizationManager.Strings.TraySettings);
            Assert.Equal("WindowsCM — Modo anônimo", LocalizationManager.Strings.TrayIconTooltipIncognito);
            Assert.Equal("MODO ANÔNIMO ATIVO", LocalizationManager.Strings.PopupIncognitoBannerTitle);
        }
        finally
        {
            LocalizationManager.CurrentLanguage = originalLang;
        }
    }

    [Fact]
    public void LayoutPreviewAndMockData_CompleteEnglishPortugueseParity()
    {
        var en = new EnglishAppStrings();
        var pt = new PortugueseAppStrings();

        // 1. Toggle buttons parity
        Assert.Equal("Main Window", en.SettingsLayoutPreviewToggleLarge);
        Assert.Equal("Área Principal", pt.SettingsLayoutPreviewToggleLarge);

        Assert.Equal("Compact Menu", en.SettingsLayoutPreviewToggleCompact);
        Assert.Equal("Menu Compacto", pt.SettingsLayoutPreviewToggleCompact);

        // 2. Mock items parity
        Assert.Equal("Screenshot.png", en.SettingsLayoutMockImageTitle);
        Assert.Equal("CapturaDeTela.png", pt.SettingsLayoutMockImageTitle);

        Assert.Equal("Copyous for Windows", en.SettingsLayoutMockLinkDesc);
        Assert.Equal("Copyous para Windows", pt.SettingsLayoutMockLinkDesc);

        Assert.Equal("Project notes...", en.SettingsLayoutMockNotesTitle);
        Assert.Equal("Anotações do projeto...", pt.SettingsLayoutMockNotesTitle);

        Assert.Equal("Alignment and layout v2", en.SettingsLayoutMockNotesDesc);
        Assert.Equal("Alinhamento e layout v2", pt.SettingsLayoutMockNotesDesc);

        // 3. Flow direction badges parity
        Assert.Equal("★ Recent ➔ ➔ ➔ Old", en.SettingsLayoutFlowHorizontalRecentLeft);
        Assert.Equal("★ Recente ➔ ➔ ➔ Antigo", pt.SettingsLayoutFlowHorizontalRecentLeft);

        Assert.Equal("Old ➔ ➔ ➔ ★ Recent", en.SettingsLayoutFlowHorizontalRecentRight);
        Assert.Equal("Antigo ➔ ➔ ➔ ★ Recente", pt.SettingsLayoutFlowHorizontalRecentRight);

        Assert.Equal("★ Recent ⬇ Old", en.SettingsLayoutFlowVerticalRecentTop);
        Assert.Equal("★ Recente ⬇ Antigo", pt.SettingsLayoutFlowVerticalRecentTop);

        Assert.Equal("Old ⬇ ★ Recent", en.SettingsLayoutFlowVerticalRecentBottom);
        Assert.Equal("Antigo ⬇ ★ Recente", pt.SettingsLayoutFlowVerticalRecentBottom);

        Assert.Equal("★ Recent ➔ Old", en.SettingsLayoutFlowCompactRecentLeft);
        Assert.Equal("★ Recente ➔ Antigo", pt.SettingsLayoutFlowCompactRecentLeft);

        Assert.Equal("Old ➔ ★ Recent", en.SettingsLayoutFlowCompactRecentRight);
        Assert.Equal("Antigo ➔ ★ Recente", pt.SettingsLayoutFlowCompactRecentRight);
    }

    [Fact]
    public void CodeSyntaxTokenizer_DetectLanguage_ReturnsNullWhenUnknown()
    {
        // Must not return hardcoded Portuguese "Código" when unknown!
        Assert.Null(WindowsCM.Core.Previews.CodeSyntaxTokenizer.DetectLanguage(""));
        Assert.Null(WindowsCM.Core.Previews.CodeSyntaxTokenizer.DetectLanguage("   "));
        Assert.Null(WindowsCM.Core.Previews.CodeSyntaxTokenizer.DetectLanguage("just some words without code syntax"));
        Assert.Equal("SQL", WindowsCM.Core.Previews.CodeSyntaxTokenizer.DetectLanguage("SELECT * FROM users"));
        Assert.Equal("Python", WindowsCM.Core.Previews.CodeSyntaxTokenizer.DetectLanguage("def hello(): pass"));
        Assert.Equal("C#", WindowsCM.Core.Previews.CodeSyntaxTokenizer.DetectLanguage("public class Program {}"));
    }

    [Fact]
    public void AllStringProperties_In_IAppStrings_MustBeNonEmpty_InBothLanguages()
    {
        var en = new EnglishAppStrings();
        var pt = new PortugueseAppStrings();

        var stringProperties = typeof(IAppStrings)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string));

        foreach (var prop in stringProperties)
        {
            var enVal = (string?)prop.GetValue(en);
            var ptVal = (string?)prop.GetValue(pt);

            Assert.False(string.IsNullOrWhiteSpace(enVal), $"English string property '{prop.Name}' must not be null or empty.");
            Assert.False(string.IsNullOrWhiteSpace(ptVal), $"Portuguese string property '{prop.Name}' must not be null or empty.");
        }
    }

    [Fact]
    public void BadgeAndUnifiedTypeCustomization_CompleteParity()
    {
        var en = new EnglishAppStrings();
        var pt = new PortugueseAppStrings();

        Assert.Equal("Document", en.BadgeDocument);
        Assert.Equal("Documento", pt.BadgeDocument);

        Assert.Equal("Spreadsheet", en.BadgeSpreadsheet);
        Assert.Equal("Planilha", pt.BadgeSpreadsheet);

        Assert.Equal("Presentation", en.BadgePresentation);
        Assert.Equal("Apresentação", pt.BadgePresentation);

        Assert.Equal("Audio", en.BadgeAudio);
        Assert.Equal("Áudio", pt.BadgeAudio);

        Assert.Equal("Video", en.BadgeVideo);
        Assert.Equal("Vídeo", pt.BadgeVideo);

        Assert.Equal("Archive", en.BadgeArchive);
        Assert.Equal("Compactado", pt.BadgeArchive);

        Assert.Equal("Character", en.BadgeCharacter);
        Assert.Equal("Caractere", pt.BadgeCharacter);

        Assert.Equal("Color", en.BadgeColor);
        Assert.Equal("Cor", pt.BadgeColor);

        Assert.Equal("Other Files and Folders", en.UnifiedTypeOtherFilesName);
        Assert.Equal("Outros Arquivos e Pastas", pt.UnifiedTypeOtherFilesName);

        Assert.Equal("Plain Text", en.UnifiedTypePlainTextName);
        Assert.Equal("Textos Simples", pt.UnifiedTypePlainTextName);

        Assert.Equal("Characters / Emojis", en.UnifiedTypeCharacterName);
        Assert.Equal("Caracteres / Emojis", pt.UnifiedTypeCharacterName);

        Assert.Equal("Colors", en.UnifiedTypeColorName);
        Assert.Equal("Cores (Color)", pt.UnifiedTypeColorName);

        Assert.Equal("Shortcuts unavailable in this session.", en.SettingsShortcutStatusUnavailable);
        Assert.Equal("Atalhos indisponíveis nesta sessão.", pt.SettingsShortcutStatusUnavailable);

        Assert.Equal("Failed to delete some files.", en.SettingsClearCacheDefaultError);
        Assert.Equal("Falha na exclusão de alguns arquivos.", pt.SettingsClearCacheDefaultError);

        Assert.Equal("Shortcut registered successfully: Ctrl+V.", en.SettingsShortcutStatusSuccess("Ctrl+V"));
        Assert.Equal("Atalho registrado com sucesso: Ctrl+V.", pt.SettingsShortcutStatusSuccess("Ctrl+V"));

        Assert.Equal("Failed to remap shortcut.", en.SettingsShortcutStatusFailed);
        Assert.Equal("Falha ao remapear atalho.", pt.SettingsShortcutStatusFailed);
    }

    [Fact]
    public void RelativeTimeFormatting_Parity()
    {
        var en = new EnglishAppStrings();
        var pt = new PortugueseAppStrings();

        Assert.Equal("just now", en.TimeJustNow);
        Assert.Equal("agora", pt.TimeJustNow);

        Assert.Equal("5 min ago", en.TimeMinutesAgo(5));
        Assert.Equal("há 5 min", pt.TimeMinutesAgo(5));

        Assert.Equal("2h ago", en.TimeHoursAgo(2));
        Assert.Equal("há 2 h", pt.TimeHoursAgo(2));

        Assert.Equal("3d ago", en.TimeDaysAgo(3));
        Assert.Equal("há 3 d", pt.TimeDaysAgo(3));
    }

    [Fact]
    public void ScrollbarPlacementStrings_Parity()
    {
        var en = new EnglishAppStrings();
        var pt = new PortugueseAppStrings();

        Assert.False(string.IsNullOrWhiteSpace(en.SettingsLayoutScrollbarPosition));
        Assert.False(string.IsNullOrWhiteSpace(pt.SettingsLayoutScrollbarPosition));

        Assert.False(string.IsNullOrWhiteSpace(en.SettingsLayoutScrollbarHorizontal));
        Assert.False(string.IsNullOrWhiteSpace(pt.SettingsLayoutScrollbarHorizontal));

        Assert.False(string.IsNullOrWhiteSpace(en.SettingsLayoutScrollbarVertical));
        Assert.False(string.IsNullOrWhiteSpace(pt.SettingsLayoutScrollbarVertical));

        Assert.False(string.IsNullOrWhiteSpace(en.SettingsLayoutScrollbarPosRight));
        Assert.False(string.IsNullOrWhiteSpace(pt.SettingsLayoutScrollbarPosRight));

        Assert.False(string.IsNullOrWhiteSpace(en.SettingsLayoutScrollbarPosLeft));
        Assert.False(string.IsNullOrWhiteSpace(pt.SettingsLayoutScrollbarPosLeft));

        Assert.False(string.IsNullOrWhiteSpace(en.SettingsLayoutScrollbarPosBottom));
        Assert.False(string.IsNullOrWhiteSpace(pt.SettingsLayoutScrollbarPosBottom));

        Assert.False(string.IsNullOrWhiteSpace(en.SettingsLayoutScrollbarPosTop));
        Assert.False(string.IsNullOrWhiteSpace(pt.SettingsLayoutScrollbarPosTop));
    }
}

