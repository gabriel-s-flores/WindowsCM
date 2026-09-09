// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Feedback;
using WindowsCM.Core.Paste;

namespace WindowsCM.Core.Settings;

// Feedback screen (Copyous Feedback parity minus indicator-display, which
// the tray covers): balloon + icon flash toggles plus the nine sounds with
// the dB slider. Conversions hand the runtime its existing option shapes.
public sealed class FeedbackSettings
{
    public bool SendNotification { get; set; } = false;
    public bool WiggleIndicator { get; set; } = true;
    public SoundName Sound { get; set; } = SoundName.None;
    public double VolumeDb { get; set; } = SettingLimits.VolumeDbDefault;

    public void Clamp()
    {
        VolumeDb = SettingLimits.ClampDouble(VolumeDb, SettingLimits.VolumeDbMin, SettingLimits.VolumeDbMax);
    }

    public double Gain => SoundGain.FromDecibels(VolumeDb);

    public bool ShouldPlay => Sound != SoundName.None;

    public string? AssetFile => SoundAssets.FileNameFor(Sound);

    public SoundOptions ToSoundOptions() => new() { Name = Sound, VolumeDb = VolumeDb };

    public CopyFeedbackOptions ToCopyFeedbackOptions() => new()
    {
        SendNotification = SendNotification,
        WiggleIndicator = WiggleIndicator,
    };
}

// Paste tunables (spec: Ctrl+V default, Shift+Insert opt-in, measured
// post-focus delay — never a ported constant). Swap lives on shortcuts;
// this owns the sequence + delay it combines with.
public sealed class PasteSettings
{
    public PasteSequence Sequence { get; set; } = PasteSequence.CtrlV;
    public int DelayMs { get; set; } = SettingLimits.PasteDelayMsDefault;

    public void Clamp()
    {
        DelayMs = SettingLimits.ClampInt(DelayMs, SettingLimits.PasteDelayMsMin, SettingLimits.PasteDelayMsMax);
    }
}
