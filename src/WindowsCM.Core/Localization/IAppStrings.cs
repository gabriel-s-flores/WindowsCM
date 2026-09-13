// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Localization;

public interface IAppStrings
{
    // General / App info
    string AppName { get; }
    string SettingsTitle { get; }

    // Tray Menu & Notifications
    string TrayOpen { get; }
    string TrayCompactMenu { get; }
    string TrayIncognito { get; }
    string TrayIncognitoActive { get; }
    string TrayClearHistory { get; }
    string TraySettings { get; }
    string TrayExit { get; }
    string TrayShortcutConflictTitle { get; }
    string TrayMissingItemBalloon(long id);
    string TrayPasteFailedBalloon(string message);
    string TrayIconTooltip { get; }
    string TrayIconTooltipIncognito { get; }

    // Popup (Cards & Top Bar)
    string PopupSettingsTooltip { get; }
    string PopupIncognitoTooltip { get; }
    string PopupIncognitoTooltipActive { get; }
    string PopupIncognitoTooltipInactive { get; }
    string PopupReceiveMobileTooltip { get; }
    string PopupSearchPlaceholder { get; }
    string PopupSearchTooltip { get; }
    string PopupFilterTooltip { get; }
    string PopupFilterActiveTooltip { get; }
    string FilterAll { get; }
    string FilterLinks { get; }
    string FilterCode { get; }
    string FilterFiles { get; }
    string FilterImages { get; }
    string FilterEmojis { get; }
    string FilterColors { get; }
    string FilterText { get; }
    string PopupPinsTooltip { get; }
    string PopupClearText { get; }
    string PopupClearTooltip { get; }

    // Incognito Banner & Empty State
    string IncognitoBannerTitle { get; }
    string IncognitoBannerSubtitle { get; }
    string IncognitoBannerDescription { get; }
    string IncognitoSwitchToNormal { get; }
    string IncognitoSwitchToIncognito { get; }
    string IncognitoSwitchTooltip { get; }
    string IncognitoExitButton { get; }
    string IncognitoExitTooltip { get; }
    string IncognitoEmptyStateTitle { get; }
    string IncognitoEmptyStateDescription { get; }

    // Card Action Tooltips
    string CardPinnedBadge { get; }
    string CardQrTooltip { get; }
    string CardPinTooltip { get; }
    string CardMoreOptionsTooltip { get; }
    string CardDeleteTooltip { get; }

    // Card Context Menu
    string ContextMenuPaste { get; }
    string ContextMenuCopy { get; }
    string ContextMenuPin { get; }
    string ContextMenuUnpin { get; }
    string ContextMenuConvert { get; }
    string ContextMenuGenerateQr { get; }
    string ContextMenuEditTitle { get; }
    string ContextMenuEditContent { get; }
    string ContextMenuDelete { get; }

    // Compact Popup
    string CompactSearchPlaceholder { get; }
    string CompactClearSearchTooltip { get; }
    string CompactIncognitoSubtitle { get; }
    string CompactIncognitoExitTooltip { get; }
    string CompactSettingsTooltip { get; }
    string CompactIncognitoTooltip { get; }
    string CompactIncognitoTooltipActive { get; }
    string CompactIncognitoTooltipInactive { get; }
    string CompactMobileTransferTooltip { get; }
    string CompactClear { get; }
    string CompactClearTooltip { get; }
    string CompactClearConfirm { get; }
    string CompactClearConfirmMessage { get; }
    string CompactQrTooltip { get; }
    string CompactMoreOptionsTooltip { get; }
    string CompactDeleteTooltip { get; }
    string CompactPinTooltip { get; }

    // Popup UI & Incognito Helpers
    string PopupClearSearchTooltip { get; }
    string PopupClearButton { get; }
    string PopupPinnedBadgeTooltip { get; }
    string PopupQrTooltip { get; }
    string PopupPinTooltip { get; }
    string PopupMoreOptionsTooltip { get; }
    string PopupDeleteTooltip { get; }
    string PopupIncognitoBannerTitle { get; }
    string PopupIncognitoBannerSubtitle { get; }
    string PopupIncognitoBannerDesc { get; }
    string PopupSwitchHistoryNormal { get; }
    string PopupSwitchHistoryIncognito { get; }
    string PopupIncognitoBackgroundTitle { get; }
    string PopupIncognitoBackgroundSubtitle { get; }
    string PopupIncognitoBackgroundDesc { get; }
    string PopupIncognitoEmptyTitle { get; }
    string PopupIncognitoEmptyDesc { get; }
    string PopupExitIncognitoTooltip { get; }

    // Item Kinds & Types
    string KindImage { get; }
    string KindFile { get; }
    string KindFiles { get; }
    string KindFilesCount(int count, string first);
    string KindCode { get; }
    string KindText { get; }
    string KindColor { get; }
    string KindEmoji { get; }
    string KindCharacter { get; }
    string KindLink { get; }

    // Type Labels
    string LabelPngImage { get; }
    string LabelMultipleFiles { get; }
    string LabelFilesCount(int count);
    string LabelCodeWithLanguage(string lang);
    string LabelTextLength(int length);
    string LabelWebLink { get; }
    string LabelEmojiCount(int count);
    string LabelEmojiCode(string code);
    string LabelCharacterCode(string code);
    string LabelFilesSelected(int count);
    string LabelMoreFilesRemaining(int count);

    // Builtin Categories (Default file categories)
    string CategoryImages { get; }
    string CategoryCode { get; }
    string CategoryLinks { get; }
    string CategoryDocuments { get; }
    string CategorySpreadsheets { get; }
    string CategoryPresentations { get; }
    string CategoryAudio { get; }
    string CategoryVideo { get; }
    string CategoryArchives { get; }
    string CategoryTextOther { get; }

    // Relative Time
    string TimeJustNow { get; }
    string TimeMinutesAgo(int minutes);
    string TimeHoursAgo(int hours);
    string TimeDaysAgo(int days);

    // Toast
    string ToastAddedToClipboard { get; }
    string ToastLinkCopied { get; }

    // Settings Window
    string SettingsNavGeneral { get; }
    string SettingsNavLayout { get; }
    string SettingsNavColors { get; }
    string SettingsNavStorage { get; }
    string SettingsNavShortcuts { get; }
    string SettingsNavAbout { get; }
    string SettingsThemeStatusFluent { get; }
    string SettingsThemeStatusLight { get; }
    string SettingsThemeStatusDark { get; }
    string SettingsThemeStatusHighContrast { get; }

    // Settings Panel: Layout & Placement
    string SettingsSectionLayoutTitle { get; }
    string SettingsSectionLayoutSubtitle { get; }
    string SettingsLayoutLargeTitle { get; }
    string SettingsLayoutLargeDesc { get; }
    string SettingsLayoutOrientation { get; }
    string SettingsLayoutOrientationHorizontal { get; }
    string SettingsLayoutOrientationVertical { get; }
    string SettingsLayoutPosition { get; }
    string SettingsLayoutPosBottom { get; }
    string SettingsLayoutPosTop { get; }
    string SettingsLayoutPosLeft { get; }
    string SettingsLayoutPosRight { get; }
    string SettingsLayoutItemOrder { get; }
    string SettingsLayoutOrderRecentLeft { get; }
    string SettingsLayoutOrderRecentRight { get; }
    string SettingsLayoutOrderRecentTop { get; }
    string SettingsLayoutOrderRecentBottom { get; }

    string SettingsLayoutScrollbarPosition { get; }
    string SettingsLayoutScrollbarHorizontal { get; }
    string SettingsLayoutScrollbarVertical { get; }
    string SettingsLayoutScrollbarPosRight { get; }
    string SettingsLayoutScrollbarPosLeft { get; }
    string SettingsLayoutScrollbarPosBottom { get; }
    string SettingsLayoutScrollbarPosTop { get; }

    string SettingsLayoutCompactTitle { get; }
    string SettingsLayoutCompactDesc { get; }
    string SettingsLayoutCompactOrientation { get; }
    string SettingsLayoutCompactOrientationVertical { get; }
    string SettingsLayoutCompactOrientationHorizontal { get; }
    string SettingsLayoutCompactOrder { get; }
    string SettingsLayoutCompactOrderRecentLeft { get; }
    string SettingsLayoutCompactOrderRecentRight { get; }
    string SettingsLayoutCompactOrderRecentTop { get; }
    string SettingsLayoutCompactOrderRecentBottom { get; }

    string SettingsLayoutPreviewTitle { get; }
    string SettingsLayoutPreviewSubtitle { get; }
    string SettingsLayoutPreviewRecentBadge { get; }
    string SettingsLayoutPreviewOldestBadge { get; }
    string SettingsLayoutPreviewToggleLarge { get; }
    string SettingsLayoutPreviewToggleCompact { get; }
    string SettingsLayoutMockImageTitle { get; }
    string SettingsLayoutMockLinkTitle { get; }
    string SettingsLayoutMockLinkDesc { get; }
    string SettingsLayoutMockNotesTitle { get; }
    string SettingsLayoutMockNotesDesc { get; }
    string SettingsLayoutFlowHorizontalRecentLeft { get; }
    string SettingsLayoutFlowHorizontalRecentRight { get; }
    string SettingsLayoutFlowVerticalRecentTop { get; }
    string SettingsLayoutFlowVerticalRecentBottom { get; }
    string SettingsLayoutFlowCompactRecentLeft { get; }
    string SettingsLayoutFlowCompactRecentRight { get; }

    // Settings Panel 1: General
    string SettingsSectionGeneralTitle { get; }
    string SettingsSectionGeneralSubtitle { get; }
    string SettingsLanguageTitle { get; }
    string SettingsLanguageSubtitle { get; }
    string SettingsLanguageSystem { get; }
    string SettingsLanguageEnglish { get; }
    string SettingsLanguagePortuguese { get; }
    string SettingsThemeTitle { get; }
    string SettingsThemeSubtitle { get; }
    string SettingsThemeOptionSystem { get; }
    string SettingsThemeOptionDark { get; }
    string SettingsThemeOptionLight { get; }
    string SettingsThemeOptionHighContrast { get; }
    string SettingsHistoryLimitTitle { get; }
    string SettingsHistoryLimitSubtitle { get; }
    string SettingsHistoryLimitBadge(int count);
    string SettingsHistoryLimitRestore { get; }
    string SettingsAutostartTitle { get; }
    string SettingsAutostartSubtitle { get; }
    string SettingsEndOfSessionTitle { get; }
    string SettingsEndOfSessionSubtitle { get; }
    string SettingsEndOfSessionClearAll { get; }
    string SettingsEndOfSessionKeepPinsTags { get; }
    string SettingsEndOfSessionKeepAll { get; }

    // Settings Panel 2: Colors & Categories
    string SettingsSectionColorsTitle { get; }
    string SettingsSectionColorsSubtitle { get; }
    string SettingsAddCategoryButton { get; }
    string SettingsResetAllColorsButton { get; }
    string SettingsResetAllColorsTooltip { get; }
    string SettingsNewCategoryTitle { get; }
    string SettingsNewCategoryNameLabel { get; }
    string SettingsNewCategoryHexLabel { get; }
    string SettingsNewCategoryPickColor { get; }
    string SettingsNewCategoryExtLabel { get; }
    string SettingsNewCategoryCancel { get; }
    string SettingsNewCategorySave { get; }
    string SettingsColorLabel { get; }
    string SettingsChooseColorButton { get; }
    string SettingsResetButton { get; }
    string SettingsDeleteButton { get; }
    string SettingsExtensionsLabel { get; }
    string SettingsAddExtensionButton { get; }
    string SettingsAddExtensionPrompt { get; }
    string SettingsLinkHint { get; }
    string SettingsTextHint { get; }
    string BadgeDocument { get; }
    string BadgeSpreadsheet { get; }
    string BadgePresentation { get; }
    string BadgeAudio { get; }
    string BadgeVideo { get; }
    string BadgeArchive { get; }
    string BadgeCharacter { get; }
    string BadgeColor { get; }
    string UnifiedTypeOtherFilesName { get; }
    string UnifiedTypeOtherFilesHint { get; }
    string UnifiedTypePlainTextName { get; }
    string UnifiedTypeCharacterName { get; }
    string UnifiedTypeCharacterHint { get; }
    string UnifiedTypeColorName { get; }
    string UnifiedTypeColorHint { get; }

    // Settings Panel 3: Storage
    string SettingsSectionStorageTitle { get; }
    string SettingsSectionStorageSubtitle { get; }
    string SettingsClearCacheTitle { get; }
    string SettingsClearCacheSubtitle { get; }
    string SettingsClearCacheButton { get; }
    string SettingsClearCacheSuccess(int files, string sizeFormatted);
    string SettingsClearCacheAlreadyEmpty { get; }
    string SettingsClearCacheWarning(string message);
    string SettingsClearCacheDefaultError { get; }
    string SettingsStorageFoldersTitle { get; }
    string SettingsStorageFoldersSubtitle { get; }
    string SettingsOpenDataFolder { get; }
    string SettingsOpenConfigFolder { get; }
    string SettingsOpenCacheFolder { get; }
    string SettingsPathsLabelData { get; }
    string SettingsPathsLabelConfig { get; }
    string SettingsPathsLabelCache { get; }
    string SettingsPathsLabelDb { get; }
    string SettingsPathsLabelActions { get; }
    string SettingsPathsLabelSettings { get; }

    // Settings Panel 4: Shortcuts
    string SettingsSectionShortcutsTitle { get; }
    string SettingsSectionShortcutsSubtitle { get; }
    string SettingsShortcutCompactTitle { get; }
    string SettingsShortcutCompactSubtitle { get; }
    string SettingsShortcutIncognitoTitle { get; }
    string SettingsShortcutIncognitoSubtitle { get; }
    string SettingsShortcutApplyButton { get; }
    string SettingsShortcutRecordButton { get; }
    string SettingsShortcutRecordListening { get; }
    string SettingsShortcutRecordTooltip { get; }
    string SettingsShortcutStatusInfoTitle { get; }
    string SettingsShortcutStatusInfo { get; }
    string SettingsShortcutStatusUnavailable { get; }
    string SettingsShortcutStatusFailed { get; }
    string SettingsShortcutStatusListeningTitle(string shortcutName);
    string SettingsShortcutStatusListeningBody { get; }
    string SettingsShortcutStatusModifiersOnly(string modifiers);
    string SettingsShortcutStatusCanceledTitle { get; }
    string SettingsShortcutStatusSavedTitle { get; }
    string SettingsShortcutStatusSavedBody(string shortcutName, string gesture);
    string SettingsShortcutStatusNotSavedTitle { get; }
    string SettingsShortcutStatusKeepsPrevious(string gesture);
    string SettingsShortcutErrorUnrecognized(string gesture);
    string SettingsShortcutErrorWinKeyReserved { get; }
    string SettingsShortcutErrorF12Reserved { get; }
    string SettingsShortcutErrorNeedsModifier { get; }
    string SettingsShortcutErrorOccupied(string gesture);
    string SettingsShortcutErrorUnsupportedKey { get; }

    // Settings Panel 5: About
    string SettingsSectionAboutTitle { get; }
    string SettingsSectionAboutSubtitle { get; }
    string SettingsVersionTitle { get; }
    string SettingsTrayGuidanceTitle { get; }
    string SettingsTrayGuidanceBody { get; }
    string SettingsCreditsTitle { get; }

    // Mobile Transfer
    string MobileWindowTitle { get; }
    string MobileHeaderTitle { get; }
    string MobileHeaderSubtitle { get; }
    string MobileCopyLinkButton { get; }
    string MobileStatusActive { get; }
    string MobileStatusSubtext { get; }
    string MobileTextReceived(string excerpt);
    string MobileTextCopiedSuccess { get; }
    string MobileFilesReceived(int count, string first);
    string MobileSingleFileReceived(string first);
    string MobileFilesSavedSuccess { get; }
    string MobileOpenFolderButton { get; }
    string MobileCloseButton { get; }
    string MobileFolderOpenError(string error);

    // QR Window
    string QrWindowTitle { get; }
    string QrHeaderScanTitle { get; }
    string QrHeaderScanSubtitle { get; }
    string QrHeaderDownloadTitle { get; }
    string QrHeaderDownloadSubtitle { get; }
    string QrCopyAccessLink { get; }
    string QrPickAnotherFile { get; }
    string QrCloseButton { get; }
    string QrFileDialogTitle { get; }
    string QrFileDialogFilter { get; }
    string QrFallbackFileKind { get; }

    // Common Dialogs
    string DialogOk { get; }
    string DialogCancel { get; }
    string DialogEditTitle { get; }
    string DialogEditContent { get; }
    string DialogEditTitlePrompt { get; }
    string DialogEditContentPrompt { get; }

    // CLI & System errors
    string CliHelpText { get; }
    string ErrorSidDetermination { get; }
    string ErrorForwardFailed { get; }
}
