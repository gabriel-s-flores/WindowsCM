// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Capture;
using WindowsCM.Core.Feedback;
using WindowsCM.Core.Localization;
using WindowsCM.Core.Paste;
using WindowsCM.Core.Previews;

namespace WindowsCM.Core.Settings;

// Aggregate root for the settings window: every ported preference in one
// JSON object (GSettings→JSON map). First run is Default(): the Default
// profile, Dark popup default, system-follow theme, English labels in the
// UI layer. Incognito stays runtime-only (never persisted, spec).
public sealed class AppSettings
{
    public AppLanguage Language { get; set; } = AppLanguage.System;
    public HistorySettings History { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public ProcessExclusions Exclusions { get; set; } = new();
    public DialogSettings Dialog { get; set; } = new();
    public ItemSettings Item { get; set; } = new();
    public HeaderSettings Header { get; set; } = new();
    public PerTypeSettings PerType { get; set; } = new();
    public ThemeSettings Theme { get; set; } = new();
    public ShortcutSettings Shortcuts { get; set; } = new();
    public FeedbackSettings Feedback { get; set; } = new();
    public PasteSettings Paste { get; set; } = new();
    public ItemColorSettings ItemColors { get; set; } = new();
    public FileCategorySettings FileCategories { get; set; } = new();

    public static AppSettings Default() => new();

    public void ClampAll()
    {
        // Hand-edited JSON may null any section: coerce first, then clamp.
        History ??= new();
        Behavior ??= new();
        Exclusions ??= new();
        Dialog ??= new();
        Item ??= new();
        Header ??= new();
        PerType ??= new();
        PerType.Text ??= new();
        PerType.Code ??= new();
        PerType.Image ??= new();
        PerType.File ??= new();
        PerType.File.ExclusionGlobs ??= [];
        PerType.Link ??= new();
        PerType.Link.ExclusionPatterns ??= [];
        PerType.Character ??= new();
        Theme ??= new();
        Shortcuts ??= new();
        Feedback ??= new();
        Paste ??= new();
        ItemColors ??= new();
        FileCategories ??= new();
        if (!Enum.IsDefined(typeof(AppLanguage), Language))
        {
            Language = AppLanguage.System;
        }
        History.Clamp();
        Item.Clamp();
        Dialog.Clamp();
        PerType.Clamp();
        Feedback.Clamp();
        Paste.Clamp();
        ItemColors.Clamp();
        FileCategories.Clamp();
    }

    public ProfileName DetectProfile() => ProfilePresets.Detect(this);

    public void ApplyProfile(ProfileName profile) => ProfilePresets.Apply(this, profile);

    // Runtime bridges: the settings window edits this; the services keep
    // their existing option shapes built from here (no default drift).
    public CaptureOptions ToCaptureOptions() => new()
    {
        ExcludedProcesses = new HashSet<string>(Exclusions.Processes, StringComparer.OrdinalIgnoreCase),
        MaxCharacters = PerType.Character.MaxCharacters,
        UpdateDateOnCopy = Behavior.UpdateDateOnCopy,
    };

    public PasteOptions ToPasteOptions() => new()
    {
        PasteSequence = Paste.Sequence,
        PasteDelayMs = Paste.DelayMs,
        SwapCopyPaste = Shortcuts.SwapCopy,
    };

    public SoundOptions ToSoundOptions() => Feedback.ToSoundOptions();

    public CopyFeedbackOptions ToCopyFeedbackOptions() => Feedback.ToCopyFeedbackOptions();

    public LinkPreviewOptions ToLinkPreviewOptions() => PerType.Link.ToPreviewOptions();
}

public enum ProfileName
{
    Default,
    Compact,
    Custom,
}
