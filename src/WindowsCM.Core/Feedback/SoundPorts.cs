// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Feedback;

// Sound player boundary (spec Testing Decisions: sound player stub).
// Production plays the converted wavs via System.Windows.Media.MediaPlayer
// (SoundPlayer has no volume property, research 05 §8) with the mapped
// gain; tests fake it, asserting the name + exponential gain. MediaPlayer
// lives in WPF PresentationCore, so the adapter ships in the UI layer —
// Core owns only the policy and the map, never the audio device.
public interface ISoundPlayer
{
    void Play(SoundName name, double gain);
}

// Play policy: silent by default (None plays nothing); a named sound plays
// once with the dB-mapped gain.
public static class SoundFeedback
{
    public static bool ShouldPlay(SoundOptions options) =>
        options.Name != SoundName.None;

    public static void PlayIfEnabled(SoundOptions options, ISoundPlayer player)
    {
        if (!ShouldPlay(options))
        {
            return;
        }
        player.Play(options.Name, SoundGain.FromDecibels(options.VolumeDb));
    }
}
