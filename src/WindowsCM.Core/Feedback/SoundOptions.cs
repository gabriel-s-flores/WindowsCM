// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Feedback;

// Sound choices (Copyous sound.ts:10 parity): the 8 GNOME alert oggs plus
// silence. Nine options total; default None keeps copies silent.
public enum SoundName
{
    None,
    Click,
    Hum,
    String,
    Swing,
    Message,
    MessageNewInstant,
    Bell,
    DialogWarning,
}

// Feedback sound tunables (Copyous feedbackSettings parity, 01 §5, with one
// deviation): Copyous allows volume -20..+20dB, but the v1 MediaPlayer
// backend caps gain at 1.0, so +dB would be silently indistinguishable
// from 0dB. The range is therefore -20..0dB until a DSP mixer restores
// true boost (deferred per spec).
public sealed class SoundOptions
{
    public const double MinVolumeDb = -20.0;
    public const double MaxVolumeDb = 0.0;
    public const double DefaultVolumeDb = 0.0;

    public SoundName Name { get; set; } = SoundName.None;
    public double VolumeDb { get; set; } = DefaultVolumeDb;
}

// Exponential dB-to-gain map (research 05 §8): gain = 10^(dB/20), floored
// at 0. 0dB = full volume; -20dB ~= 0.1; -6dB halves amplitude. The range
// cap above (not this function) is what keeps +dB unreachable.
public static class SoundGain
{
    public static double FromDecibels(double db)
    {
        if (double.IsNaN(db))
        {
            // No slider position: silence rather than propagating NaN gain.
            return 0.0;
        }
        var gain = Math.Pow(10.0, db / 20.0);
        return Math.Clamp(gain, 0.0, 1.0);
    }
}

// Converted wav assets (research 05 §8): the 8 GNOME oggs become 44.1kHz
// 16-bit PCM wavs packed with the app, mapped 1:1 by kebab name. None has
// no file. The UI layer resolves these against the install/pack root.
public static class SoundAssets
{
    public static string? FileNameFor(SoundName name) => name switch
    {
        SoundName.None => null,
        SoundName.Click => "click.wav",
        SoundName.Hum => "hum.wav",
        SoundName.String => "string.wav",
        SoundName.Swing => "swing.wav",
        SoundName.Message => "message.wav",
        SoundName.MessageNewInstant => "message-new-instant.wav",
        SoundName.Bell => "bell.wav",
        SoundName.DialogWarning => "dialog-warning.wav",
        _ => null,
    };
}
