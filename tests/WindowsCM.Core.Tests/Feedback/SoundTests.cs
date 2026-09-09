// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Feedback;

namespace WindowsCM.Core.Tests.Feedback;

// Sound seams (Copyous Feedback prefs parity: 9 options — none + 8 GNOME
// alerts — default none/silent; volume -20..+20dB default 0). Core owns the
// option model, the exponential dB-to-gain map, and the play policy over a
// stubbed player (spec Testing Decisions). The MediaPlayer adapter lives in
// the UI layer (WPF PresentationCore, not net8.0 Core).
public sealed class SoundTests
{
    [Fact]
    public void Options_Default_IsSilent()
    {
        var options = new SoundOptions();

        Assert.Equal(SoundName.None, options.Name);
        Assert.Equal(0.0, options.VolumeDb);
        Assert.False(SoundFeedback.ShouldPlay(options));
    }

    [Fact]
    public void Options_NineChoices_MatchCopyousAlerts()
    {
        var names = Enum.GetValues<SoundName>();

        Assert.Equal(9, names.Length);
        Assert.Contains(SoundName.None, names);
        foreach (var sound in new[]
            {
                SoundName.Click, SoundName.Hum, SoundName.String, SoundName.Swing,
                SoundName.Message, SoundName.MessageNewInstant,
                SoundName.Bell, SoundName.DialogWarning,
            })
        {
            Assert.Contains(sound, names);
            Assert.True(SoundFeedback.ShouldPlay(new SoundOptions { Name = sound }));
        }
    }

    [Fact]
    public void Gain_ZeroDb_IsFullVolume()
    {
        Assert.Equal(1.0, SoundGain.FromDecibels(0.0), precision: 9);
    }

    [Fact]
    public void Gain_Minus20Db_IsOneTenth()
    {
        Assert.Equal(0.1, SoundGain.FromDecibels(-20.0), precision: 9);
    }

    [Fact]
    public void Gain_Plus20Db_ClampsToOne()
    {
        Assert.Equal(1.0, SoundGain.FromDecibels(20.0), precision: 9);
    }

    [Fact]
    public void Gain_IsExponential()
    {
        // -6dB halves amplitude: 10^(-6/20) ≈ 0.5012.
        Assert.Equal(0.5011872336, SoundGain.FromDecibels(-6.0), precision: 9);
    }

    [Fact]
    public void Assets_EightWavs_MapOneToOne()
    {
        Assert.Null(SoundAssets.FileNameFor(SoundName.None));
        Assert.Equal("click.wav", SoundAssets.FileNameFor(SoundName.Click));
        Assert.Equal("hum.wav", SoundAssets.FileNameFor(SoundName.Hum));
        Assert.Equal("string.wav", SoundAssets.FileNameFor(SoundName.String));
        Assert.Equal("swing.wav", SoundAssets.FileNameFor(SoundName.Swing));
        Assert.Equal("message.wav", SoundAssets.FileNameFor(SoundName.Message));
        Assert.Equal(
            "message-new-instant.wav",
            SoundAssets.FileNameFor(SoundName.MessageNewInstant));
        Assert.Equal("bell.wav", SoundAssets.FileNameFor(SoundName.Bell));
        Assert.Equal("dialog-warning.wav", SoundAssets.FileNameFor(SoundName.DialogWarning));
    }

    [Fact]
    public void PlayIfEnabled_NamedSound_PlaysWithMappedGain()
    {
        var player = new FakeSoundPlayer();
        var options = new SoundOptions { Name = SoundName.Click, VolumeDb = -20.0 };

        SoundFeedback.PlayIfEnabled(options, player);

        var call = Assert.Single(player.Calls);
        Assert.Equal(SoundName.Click, call.Name);
        Assert.Equal(0.1, call.Gain, precision: 9);
    }

    [Fact]
    public void PlayIfEnabled_SilentDefault_PlaysNothing()
    {
        var player = new FakeSoundPlayer();

        SoundFeedback.PlayIfEnabled(new SoundOptions(), player);

        Assert.Empty(player.Calls);
    }

    private sealed class FakeSoundPlayer : ISoundPlayer
    {
        public List<(SoundName Name, double Gain)> Calls = [];
        public void Play(SoundName name, double gain) => Calls.Add((name, gain));
    }
}
