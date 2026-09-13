// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Localization;

public sealed class EnglishAppStrings : IAppStrings
{
    public static EnglishAppStrings Instance { get; } = new();

    public string AppName => "WindowsCM";
    public string SettingsTitle => "Settings — WindowsCM";

    // Tray Menu & Notifications
    public string TrayOpen => "Open";
    public string TrayCompactMenu => "Compact menu";
    public string TrayIncognito => "Incognito mode";
    public string TrayIncognitoActive => "Incognito mode (active)";
    public string TrayClearHistory => "Clear history (keep pins and tags)";
    public string TraySettings => "Settings";
    public string TrayExit => "Exit";
    public string TrayShortcutConflictTitle => "Shortcut conflict";
    public string TrayMissingItemBalloon(long id) => $"Item {id} is no longer in history, so nothing was copied.";
    public string TrayPasteFailedBalloon(string message) => $"Failed to paste after copying: {message}";
    public string TrayIconTooltip => "WindowsCM";
    public string TrayIconTooltipIncognito => "WindowsCM — Incognito mode";

    // Popup (Cards & Top Bar)
    public string PopupSettingsTooltip => "Settings";
    public string PopupIncognitoTooltip => "Turn on incognito mode (Ctrl+Shift+Alt+V)";
    public string PopupIncognitoTooltipActive => "Incognito mode active (Ctrl+Shift+Alt+V)";
    public string PopupIncognitoTooltipInactive => "Turn on incognito mode (Ctrl+Shift+Alt+V)";
    public string PopupReceiveMobileTooltip => "Send from mobile to PC";
    public string PopupSearchPlaceholder => "Type to search...";
    public string PopupSearchTooltip => "Search history in real time";
    public string PopupFilterTooltip => "Filter by content type";
    public string PopupFilterActiveTooltip => "Active filter: {0} (Click to change)";
    public string FilterAll => "All";
    public string FilterLinks => "Links";
    public string FilterCode => "Code";
    public string FilterFiles => "Files";
    public string FilterImages => "Images";
    public string FilterEmojis => "Emojis & Symbols";
    public string FilterColors => "Colors";
    public string FilterText => "Plain Text";
    public string PopupPinsTooltip => "Show only pinned (Alt+P)";
    public string PopupClearText => "Clear";
    public string PopupClearTooltip => "Clear history (keeps pins and tags)";

    // Incognito Banner & Empty State
    public string IncognitoBannerTitle => "INCOGNITO MODE ACTIVE";
    public string IncognitoBannerSubtitle => "· Temporary private session";
    public string IncognitoBannerDescription => "Items copied during this session are only kept while you are in incognito mode and will be permanently discarded when you exit.";
    public string IncognitoSwitchToNormal => "View regular history";
    public string IncognitoSwitchToIncognito => "View incognito session";
    public string IncognitoSwitchTooltip => "Switch between regular history and incognito session";
    public string IncognitoExitButton => "Exit incognito";
    public string IncognitoExitTooltip => "End incognito mode and clear temporary history";
    public string IncognitoEmptyStateTitle => "Incognito clipboard is empty";
    public string IncognitoEmptyStateDescription => "Anything you copy in this private session will only be saved temporarily here. Once you exit incognito mode, all contents will be destroyed leaving no trace.";

    // Card Action Tooltips
    public string CardPinnedBadge => "Pinned item";
    public string CardQrTooltip => "Generate QR Code (Ctrl+Q)";
    public string CardPinTooltip => "Pin / Unpin (Alt+P)";
    public string CardMoreOptionsTooltip => "More options";
    public string CardDeleteTooltip => "Delete (Delete)";

    // Card Context Menu
    public string ContextMenuPaste => "Paste";
    public string ContextMenuCopy => "Copy";
    public string ContextMenuPin => "Pin";
    public string ContextMenuUnpin => "Unpin";
    public string ContextMenuConvert => "Convert";
    public string ContextMenuGenerateQr => "Generate QR code";
    public string ContextMenuEditTitle => "Edit title...";
    public string ContextMenuEditContent => "Edit content...";
    public string ContextMenuDelete => "Delete";

    // Compact Popup
    public string CompactSearchPlaceholder => "Search...";
    public string CompactClearSearchTooltip => "Clear search";
    public string CompactIncognitoSubtitle => "Temporary session under cursor";
    public string CompactIncognitoExitTooltip => "Exit incognito mode";
    public string CompactSettingsTooltip => "Settings (Alt+S)";
    public string CompactIncognitoTooltip => "Incognito Mode (Ctrl+Shift+Alt+V)";
    public string CompactIncognitoTooltipActive => "Incognito mode active (Ctrl+Shift+Alt+V)";
    public string CompactIncognitoTooltipInactive => "Turn on incognito mode (Ctrl+Shift+Alt+V)";
    public string CompactMobileTransferTooltip => PopupReceiveMobileTooltip;
    public string CompactClear => PopupClearText;
    public string CompactClearTooltip => "Clear history (Alt+C)";
    public string CompactClearConfirm => CompactClearConfirmMessage;
    public string CompactClearConfirmMessage => "Do you want to clear all clipboard history?\n(Pinned items will be preserved)";
    public string CompactQrTooltip => CardQrTooltip;
    public string CompactMoreOptionsTooltip => CardMoreOptionsTooltip;
    public string CompactDeleteTooltip => CardDeleteTooltip;
    public string CompactPinTooltip => CardPinTooltip;

    // Popup UI & Incognito Helpers
    public string PopupClearSearchTooltip => CompactClearSearchTooltip;
    public string PopupClearButton => PopupClearText;
    public string PopupPinnedBadgeTooltip => CardPinnedBadge;
    public string PopupQrTooltip => CardQrTooltip;
    public string PopupPinTooltip => CardPinTooltip;
    public string PopupMoreOptionsTooltip => CardMoreOptionsTooltip;
    public string PopupDeleteTooltip => CardDeleteTooltip;
    public string PopupIncognitoBannerTitle => IncognitoBannerTitle;
    public string PopupIncognitoBannerSubtitle => IncognitoBannerSubtitle;
    public string PopupIncognitoBannerDesc => IncognitoBannerDescription;
    public string PopupSwitchHistoryNormal => IncognitoSwitchToNormal;
    public string PopupSwitchHistoryIncognito => IncognitoSwitchToIncognito;
    public string PopupIncognitoBackgroundTitle => IncognitoEmptyStateTitle;
    public string PopupIncognitoBackgroundSubtitle => IncognitoBannerSubtitle;
    public string PopupIncognitoBackgroundDesc => IncognitoEmptyStateDescription;
    public string PopupIncognitoEmptyTitle => IncognitoEmptyStateTitle;
    public string PopupIncognitoEmptyDesc => IncognitoEmptyStateDescription;
    public string PopupExitIncognitoTooltip => IncognitoExitTooltip;

    // Item Kinds & Types
    public string KindImage => "Image";
    public string KindFile => "File";
    public string KindFiles => "Files";
    public string KindFilesCount(int count, string first) => $"{count} files ({first})";
    public string KindCode => "Code";
    public string KindText => "Text";
    public string KindColor => "Color";
    public string KindEmoji => "Emoji";
    public string KindCharacter => "Character";
    public string KindLink => "Link";

    // Type Labels
    public string LabelPngImage => "PNG Image";
    public string LabelMultipleFiles => "Multiple files";
    public string LabelFilesCount(int count) => $"{count} files";
    public string LabelCodeWithLanguage(string lang) => $"Code ({lang})";
    public string LabelTextLength(int length) => $"Text • {length} characters";
    public string LabelWebLink => "Web Link";
    public string LabelEmojiCount(int count) => $"Emoji • {count} emojis";
    public string LabelEmojiCode(string code) => $"Emoji • {code}";
    public string LabelCharacterCode(string code) => $"Character • {code}";
    public string LabelFilesSelected(int count) => $"{count} files selected";
    public string LabelMoreFilesRemaining(int count) => $"• + {count} more files...";

    // Builtin Categories
    public string CategoryImages => "Images";
    public string CategoryCode => "Code & Scripts";
    public string CategoryLinks => "Links & Pages";
    public string CategoryDocuments => "Documents";
    public string CategorySpreadsheets => "Spreadsheets";
    public string CategoryPresentations => "Presentations";
    public string CategoryAudio => "Audio";
    public string CategoryVideo => "Videos";
    public string CategoryArchives => "Archives";
    public string CategoryTextOther => "Text & Other";

    // Relative Time
    public string TimeJustNow => "just now";
    public string TimeMinutesAgo(int minutes) => $"{minutes} min ago";
    public string TimeHoursAgo(int hours) => $"{hours}h ago";
    public string TimeDaysAgo(int days) => $"{days}d ago";

    // Toast
    public string ToastAddedToClipboard => "Added to clipboard";
    public string ToastLinkCopied => "Link copied to clipboard!";

    // Settings Window
    public string SettingsNavGeneral => "History & General";
    public string SettingsNavLayout => "Layout & Placement";
    public string SettingsNavColors => "Item Colors";
    public string SettingsNavStorage => "Cache & Storage";
    public string SettingsNavShortcuts => "Global Shortcuts";
    public string SettingsNavAbout => "About & System";
    public string SettingsThemeStatusFluent => "Fluent Mode Active";
    public string SettingsThemeStatusLight => "Light Mode Active";
    public string SettingsThemeStatusDark => "Dark Mode Active";
    public string SettingsThemeStatusHighContrast => "High Contrast Mode Active";

    // Settings Panel: Layout & Placement
    public string SettingsSectionLayoutTitle => "Layout & Screen Placement";
    public string SettingsSectionLayoutSubtitle => "Customize window orientation, screen dock position, and chronological item flow direction.";
    public string SettingsLayoutLargeTitle => "Large Clipboard Window";
    public string SettingsLayoutLargeDesc => "Full clipboard window with rich previews for code, images, links, and files.";
    public string SettingsLayoutOrientation => "Window Orientation";
    public string SettingsLayoutOrientationHorizontal => "Horizontal (Full-width card strip)";
    public string SettingsLayoutOrientationVertical => "Vertical (Tall side panel)";
    public string SettingsLayoutPosition => "Screen Position";
    public string SettingsLayoutPosBottom => "Bottom (Dock to bottom of screen)";
    public string SettingsLayoutPosTop => "Top (Dock to top of screen)";
    public string SettingsLayoutPosLeft => "Left (Dock to left of screen)";
    public string SettingsLayoutPosRight => "Right (Dock to right of screen)";
    public string SettingsLayoutItemOrder => "Item Ordering (Flow Direction)";
    public string SettingsLayoutOrderRecentLeft => "Most recent on the left (Default)";
    public string SettingsLayoutOrderRecentRight => "Most recent on the right";
    public string SettingsLayoutOrderRecentTop => "Most recent top to bottom (Default)";
    public string SettingsLayoutOrderRecentBottom => "Most recent bottom to top";

    public string SettingsLayoutScrollbarPosition => "Scrollbar Placement";
    public string SettingsLayoutScrollbarHorizontal => "Scrollbar Position (Horizontal)";
    public string SettingsLayoutScrollbarVertical => "Scrollbar Position (Vertical)";
    public string SettingsLayoutScrollbarPosRight => "Right (Default)";
    public string SettingsLayoutScrollbarPosLeft => "Left";
    public string SettingsLayoutScrollbarPosBottom => "Bottom (Default)";
    public string SettingsLayoutScrollbarPosTop => "Top";

    public string SettingsLayoutCompactTitle => "Compact Menu (Quick Hotkey)";
    public string SettingsLayoutCompactDesc => "Agile, lightweight popup directly under mouse cursor for rapid paste workflows.";
    public string SettingsLayoutCompactOrientation => "Compact Menu Format";
    public string SettingsLayoutCompactOrientationVertical => "Vertical (List under cursor - Default)";
    public string SettingsLayoutCompactOrientationHorizontal => "Horizontal (Compact cards under cursor)";
    public string SettingsLayoutCompactOrder => "Compact Menu Item Ordering";
    public string SettingsLayoutCompactOrderRecentLeft => "Most recent left to right (Default)";
    public string SettingsLayoutCompactOrderRecentRight => "Most recent right to left";
    public string SettingsLayoutCompactOrderRecentTop => "Most recent top to bottom (Default)";
    public string SettingsLayoutCompactOrderRecentBottom => "Most recent bottom to top";

    public string SettingsLayoutPreviewTitle => "Interactive Layout & Flow Preview";
    public string SettingsLayoutPreviewSubtitle => "Dynamic simulation with mock data illustrating screen placement and item flow.";
    public string SettingsLayoutPreviewRecentBadge => "⭐ Most Recent";
    public string SettingsLayoutPreviewOldestBadge => "Older Items";
    public string SettingsLayoutPreviewToggleLarge => "Main Window";
    public string SettingsLayoutPreviewToggleCompact => "Compact Menu";
    public string SettingsLayoutMockImageTitle => "Screenshot.png";
    public string SettingsLayoutMockLinkTitle => "github.com/copyous";
    public string SettingsLayoutMockLinkDesc => "Copyous for Windows";
    public string SettingsLayoutMockNotesTitle => "Project notes...";
    public string SettingsLayoutMockNotesDesc => "Alignment and layout v2";
    public string SettingsLayoutFlowHorizontalRecentLeft => "★ Recent ➔ ➔ ➔ Old";
    public string SettingsLayoutFlowHorizontalRecentRight => "Old ➔ ➔ ➔ ★ Recent";
    public string SettingsLayoutFlowVerticalRecentTop => "★ Recent ⬇ Old";
    public string SettingsLayoutFlowVerticalRecentBottom => "Old ⬇ ★ Recent";
    public string SettingsLayoutFlowCompactRecentLeft => "★ Recent ➔ Old";
    public string SettingsLayoutFlowCompactRecentRight => "Old ➔ ★ Recent";

    // Settings Panel 1: General
    public string SettingsSectionGeneralTitle => "Clipboard & General";
    public string SettingsSectionGeneralSubtitle => "Configure history retention limits, startup, and data preservation.";
    public string SettingsLanguageTitle => "Application Language";
    public string SettingsLanguageSubtitle => "Choose between following Windows system language or manually setting English or Portuguese.";
    public string SettingsLanguageSystem => "💻 Follow Windows (Default)";
    public string SettingsLanguageEnglish => "🇺🇸 English";
    public string SettingsLanguagePortuguese => "🇧🇷 Português";
    public string SettingsThemeTitle => "Theme and Appearance";
    public string SettingsThemeSubtitle => "Choose between dark mode, light mode, high contrast for accessibility, or sync with Windows.";
    public string SettingsThemeOptionSystem => "💻 Follow Windows (Default)";
    public string SettingsThemeOptionDark => "🌙 Dark Mode";
    public string SettingsThemeOptionLight => "☀️ Light Mode";
    public string SettingsThemeOptionHighContrast => "🔲 High Contrast (Accessibility)";
    public string SettingsHistoryLimitTitle => "Maximum history items limit";
    public string SettingsHistoryLimitSubtitle => "Sets the number of items kept in clipboard. The recommended and maximum value is 100 items.";
    public string SettingsHistoryLimitBadge(int count) => $"{count} items";
    public string SettingsHistoryLimitRestore => "Restore recommended (100)";
    public string SettingsAutostartTitle => "Start with Windows";
    public string SettingsAutostartSubtitle => "Run WindowsCM silently in the system tray when starting the computer.";
    public string SettingsEndOfSessionTitle => "Cleanup on session end";
    public string SettingsEndOfSessionSubtitle => "History behavior when restarting, signing out, or shutting down Windows.";
    public string SettingsEndOfSessionClearAll => "Clear all";
    public string SettingsEndOfSessionKeepPinsTags => "Keep favorites and tags (Recommended)";
    public string SettingsEndOfSessionKeepAll => "Keep all";

    // Settings Panel 2: Colors & Categories
    public string SettingsSectionColorsTitle => "Colors and Item Types";
    public string SettingsSectionColorsSubtitle => "Customize semantic colors, badges, and file extensions associated with each clipboard content type.";
    public string SettingsAddCategoryButton => "+ New Category";
    public string SettingsResetAllColorsButton => "Restore defaults";
    public string SettingsResetAllColorsTooltip => "Restore all colors and categories to recommended defaults";
    public string SettingsNewCategoryTitle => "Create New File Category";
    public string SettingsNewCategoryNameLabel => "Category Name (e.g. 3D Models):";
    public string SettingsNewCategoryHexLabel => "Color (Hex):";
    public string SettingsNewCategoryPickColor => "Choose color";
    public string SettingsNewCategoryExtLabel => "Associated extensions separated by comma (e.g. .obj, .blend, .fbx, .stl):";
    public string SettingsNewCategoryCancel => "Cancel";
    public string SettingsNewCategorySave => "Save Category";
    public string SettingsColorLabel => "Color:";
    public string SettingsChooseColorButton => "Choose color";
    public string SettingsResetButton => "Reset";
    public string SettingsDeleteButton => "Delete";
    public string SettingsExtensionsLabel => "Extensions:";
    public string SettingsAddExtensionButton => "+ Add";
    public string SettingsAddExtensionPrompt => "Add extension (e.g. .dat):";
    public string SettingsLinkHint => "Automatically detected for web URLs and links (http, https, ftp...)";
    public string SettingsTextHint => "Automatically detected for plain text clipboard contents";
    public string BadgeDocument => "Document";
    public string BadgeSpreadsheet => "Spreadsheet";
    public string BadgePresentation => "Presentation";
    public string BadgeAudio => "Audio";
    public string BadgeVideo => "Video";
    public string BadgeArchive => "Archive";
    public string BadgeCharacter => "Character";
    public string BadgeColor => "Color";
    public string UnifiedTypeOtherFilesName => "Other Files and Folders";
    public string UnifiedTypeOtherFilesHint => "Used for files without cataloged extension or multiple selected files";
    public string UnifiedTypePlainTextName => "Plain Text";
    public string UnifiedTypeCharacterName => "Characters / Emojis";
    public string UnifiedTypeCharacterHint => "Automatically detected for individual characters and emojis";
    public string UnifiedTypeColorName => "Colors";
    public string UnifiedTypeColorHint => "Automatically detected for color codes (HEX, RGB, HSL)";

    // Settings Panel 3: Storage
    public string SettingsSectionStorageTitle => "Cache & Storage";
    public string SettingsSectionStorageSubtitle => "Manage disk space occupied by temporary data and access system folders.";
    public string SettingsClearCacheTitle => "Clear cache directly";
    public string SettingsClearCacheSubtitle => "Deletes downloaded website favicons, cached link preview images, and temporary files to free up space. History items and pasted images are not affected.";
    public string SettingsClearCacheButton => "Clear Cache Now";
    public string SettingsClearCacheSuccess(int files, string sizeFormatted) => $"Cache cleared successfully: {files} file(s) removed ({sizeFormatted} freed).";
    public string SettingsClearCacheAlreadyEmpty => "Cache was already empty. No temporary files pending.";
    public string SettingsClearCacheWarning(string message) => $"Warning clearing cache: {message}";
    public string SettingsClearCacheDefaultError => "Failed to delete some files.";
    public string SettingsStorageFoldersTitle => "Storage Folders";
    public string SettingsStorageFoldersSubtitle => "Quickly open local directories in File Explorer:";
    public string SettingsOpenDataFolder => "Open data folder";
    public string SettingsOpenConfigFolder => "Open settings folder";
    public string SettingsOpenCacheFolder => "Open cache folder";
    public string SettingsPathsLabelData => "Data";
    public string SettingsPathsLabelConfig => "Settings";
    public string SettingsPathsLabelCache => "Cache";
    public string SettingsPathsLabelDb => "Database";
    public string SettingsPathsLabelActions => "Actions";
    public string SettingsPathsLabelSettings => "Configuration";

    // Settings Panel 4: Shortcuts
    public string SettingsSectionShortcutsTitle => "Global Keyboard Shortcuts";
    public string SettingsSectionShortcutsSubtitle => "Customize key combinations to invoke the clipboard and private mode from any application.";
    public string SettingsShortcutCompactTitle => "Compact Menu (History and Paste)";
    public string SettingsShortcutCompactSubtitle => "Global shortcut to open the compact menu under mouse cursor with quick search (Default: Ctrl+Shift+V).";
    public string SettingsShortcutIncognitoTitle => "Incognito History (Private)";
    public string SettingsShortcutIncognitoSubtitle => "Opens the compact menu under cursor in incognito mode, suspending persistent saves (Default: Ctrl+Shift+Alt+V).";
    public string SettingsShortcutApplyButton => "Apply";
    public string SettingsShortcutRecordButton => "Record";
    public string SettingsShortcutRecordListening => "Press keys…";
    public string SettingsShortcutRecordTooltip => "Click, press the new combination, then release all keys to save it.";
    public string SettingsShortcutStatusInfoTitle => "How to change a shortcut";
    public string SettingsShortcutStatusInfo => "Click Record and press the new combination (e.g. Ctrl+Shift+V or Ctrl+Alt+Ç), then release the keys to save it. You can also type it in the box and click Apply. Any key on your keyboard works; Windows key combinations are reserved by the system.";
    public string SettingsShortcutStatusUnavailable => "Shortcuts unavailable in this session.";
    public string SettingsShortcutStatusFailed => "Failed to remap shortcut.";
    public string SettingsShortcutStatusListeningTitle(string shortcutName) => $"Listening for a new shortcut: {shortcutName}";
    public string SettingsShortcutStatusListeningBody => "Hold Ctrl, Shift or Alt and press a key, then release everything to save. Press Esc to cancel.";
    public string SettingsShortcutStatusModifiersOnly(string modifiers) => $"{modifiers} alone isn't a shortcut. Hold it and also press a letter, number or symbol.";
    public string SettingsShortcutStatusCanceledTitle => "Recording canceled";
    public string SettingsShortcutStatusSavedTitle => "Shortcut saved";
    public string SettingsShortcutStatusSavedBody(string shortcutName, string gesture) => $"{shortcutName} now opens with {gesture}.";
    public string SettingsShortcutStatusNotSavedTitle => "Shortcut not saved";
    public string SettingsShortcutStatusKeepsPrevious(string gesture) => $"{gesture} is still the active shortcut.";
    public string SettingsShortcutErrorUnrecognized(string gesture) => $"\"{gesture}\" isn't a valid combination. Type it like Ctrl+Shift+V (any key on your keyboard works, e.g. Ctrl+Alt+Ç) or use Record.";
    public string SettingsShortcutErrorWinKeyReserved => "Combinations with the Windows key are reserved by the system.";
    public string SettingsShortcutErrorF12Reserved => "F12 is reserved by Windows for debuggers.";
    public string SettingsShortcutErrorNeedsModifier => "Add at least one modifier (Ctrl, Shift or Alt). A single key would block normal typing.";
    public string SettingsShortcutErrorOccupied(string gesture) => $"{gesture} is already used by another app. Close that app or pick another combination.";
    public string SettingsShortcutErrorUnsupportedKey => "That key can't be used in a global shortcut. Try a letter, number, symbol or F1–F24.";

    // Settings Panel 5: About
    public string SettingsSectionAboutTitle => "About WindowsCM";
    public string SettingsSectionAboutSubtitle => "Version information, license, and project credits.";
    public string SettingsVersionTitle => "Application & Runtime Version";
    public string SettingsTrayGuidanceTitle => "System Tray";
    public string SettingsTrayGuidanceBody =>
        "In Windows 11, new system tray icons appear in the hidden overflow flyout (^) by default.\n" +
        "To keep WindowsCM always visible next to the clock, click the ^ icon in the taskbar " +
        "and drag the WindowsCM icon to the taskbar (or enable it in Windows Settings > Personalization > Taskbar > Other system tray icons).\n" +
        "Programmatic promotion is not performed by default, adhering to Windows platform policies.";
    public string SettingsCreditsTitle => "Credits & Acknowledgments";

    // Mobile Transfer
    public string MobileWindowTitle => "Send from Mobile to Computer";
    public string MobileHeaderTitle => "Send from Mobile to PC";
    public string MobileHeaderSubtitle => "Point your smartphone camera at the QR code below:";
    public string MobileCopyLinkButton => "Copy Link";
    public string MobileStatusActive => "Server active. Waiting for mobile upload...";
    public string MobileStatusSubtext => "Anything you send will go straight to the Windows clipboard.";
    public string MobileTextReceived(string excerpt) => $"✅ Text received: \"{excerpt}\"";
    public string MobileTextCopiedSuccess => "Successfully copied to Windows clipboard.";
    public string MobileFilesReceived(int count, string first) => $"✅ {count} files received (e.g. {first})";
    public string MobileSingleFileReceived(string first) => $"✅ File received: {first}";
    public string MobileFilesSavedSuccess => "Saved to Downloads\\WindowsCM Transfers and added to clipboard.";
    public string MobileOpenFolderButton => "Open Transfers Folder";
    public string MobileCloseButton => "Close";
    public string MobileFolderOpenError(string error) => $"Could not open folder: {error}";

    // QR Window
    public string QrWindowTitle => "Share via QR Code";
    public string QrHeaderScanTitle => "Scan with Phone";
    public string QrHeaderScanSubtitle => "Point your camera to open and download on your device";
    public string QrHeaderDownloadTitle => "Download on Smartphone";
    public string QrHeaderDownloadSubtitle => "Scan the QR code with your camera to access the item";
    public string QrCopyAccessLink => "Copy Access Link";
    public string QrPickAnotherFile => "Choose another file...";
    public string QrCloseButton => "Close";
    public string QrFileDialogTitle => "Select file to send to phone";
    public string QrFileDialogFilter => "All Files (*.*)|*.*|Audio (*.mp3;*.wav;*.m4a;*.ogg)|*.mp3;*.wav;*.m4a;*.ogg|Images (*.png;*.jpg;*.jpeg;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.webp|Documents (*.pdf;*.docx;*.xlsx;*.txt)|*.pdf;*.docx;*.xlsx;*.txt";
    public string QrFallbackFileKind => "Computer File";

    // Common Dialogs
    public string DialogOk => "OK";
    public string DialogCancel => "Cancel";
    public string DialogEditTitle => "Edit item title";
    public string DialogEditContent => "Edit item content";
    public string DialogEditTitlePrompt => "Edit title";
    public string DialogEditContentPrompt => "Edit content";

    // CLI & System errors
    public string CliHelpText =>
        "WindowsCM — Clipboard Manager\n\n" +
        "--toggle | --show | --hide | --clear | --clear-all\n" +
        "--hidden   starts minimized to tray (system startup)\n" +
        "--help     displays this help";
    public string ErrorSidDetermination => "Could not determine current user SID; exiting.";
    public string ErrorForwardFailed => "WindowsCM is not running and the command could not be delivered.";

    // First-run welcome guide
    public string WelcomeWindowTitle => "Welcome to WindowsCM";
    public string WelcomeHeaderTitle => "Welcome to WindowsCM";
    public string WelcomeHeaderSubtitle => "Everything you copy, saved and one shortcut away.";
    public string WelcomeTrayTitle => "WindowsCM lives in the system tray";
    public string WelcomeTrayBody => "There is no main window: WindowsCM runs quietly next to the clock and saves everything you copy. Windows 11 hides new icons, so click the ^ arrow at the right end of the taskbar to find it, then drag it onto the taskbar to keep it always visible.";
    public string WelcomeTrayClicks => "Left-click the icon to open your history. Right-click it for the compact menu, incognito mode, settings and exit.";
    public string WelcomeShortcutsTitle => "Open it from anywhere";
    public string WelcomeShortcutOpenDescription => "Opens the compact menu right under your mouse, with search.";
    public string WelcomeShortcutIncognitoDescription => "Opens incognito mode: new copies stay in memory only.";
    public string WelcomeShortcutsHint => "You can change these keys anytime in Settings > Global Shortcuts.";
    public string WelcomeFeaturesTitle => "What you can do";
    public string WelcomeFeatureCardsTitle => "Rich previews";
    public string WelcomeFeatureCardsBody => "Text, code, links, images, files, colors and emoji become cards with previews, so you find things at a glance.";
    public string WelcomeFeaturePasteTitle => "Search and paste";
    public string WelcomeFeaturePasteBody => "Type to filter. Enter pastes into the app you were using; Shift+Enter only copies.";
    public string WelcomeFeaturePinTitle => "Pins and categories";
    public string WelcomeFeaturePinBody => "Pin what matters so it is never cleared, and color files by category.";
    public string WelcomeFeatureIncognitoTitle => "Incognito mode";
    public string WelcomeFeatureIncognitoBody => "Copies made in incognito never touch the disk and vanish as soon as you leave it.";
    public string WelcomeFeatureMobileTitle => "Phone ↔ PC";
    public string WelcomeFeatureMobileBody => "Send items to your phone, or from your phone to the PC, with a QR code over your Wi-Fi. No account needed.";
    public string WelcomeFeatureCustomizeTitle => "Make it yours";
    public string WelcomeFeatureCustomizeBody => "Themes, layout, item colors, shortcuts and language are all in Settings.";
    public string WelcomeAutostartCheck => "Start WindowsCM when I sign in to Windows";
    public string WelcomeOpenSettingsButton => "Open Settings";
    public string WelcomeCloseButton => "Got it";
    public string SettingsShowWelcomeButton => "Show welcome guide";
}
