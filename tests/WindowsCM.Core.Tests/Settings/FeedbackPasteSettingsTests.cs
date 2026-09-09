// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Feedback;
using WindowsCM.Core.Paste;
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

// Feedback + paste tunables behind the settings window.
public sealed class FeedbackPasteSettingsTests
{
    [Fact]
    public void FeedbackDefaults_SilentWithFlashOn()
    {
        var feedback = new FeedbackSettings();

        Assert.False(feedback.SendNotification);
        Assert.True(feedback.WiggleIndicator);
        Assert.Equal(SoundName.None, feedback.Sound);
        Assert.Equal(0.0, feedback.VolumeDb);
        Assert.False(feedback.ShouldPlay);
        Assert.Null(feedback.AssetFile);
    }

    [Fact]
    public void FeedbackClamp_PinsVolumeToDbRange()
    {
        var feedback = new FeedbackSettings { VolumeDb = 99.0 };
        feedback.Clamp();

        Assert.Equal(20.0, feedback.VolumeDb);
        Assert.Equal(1.0, feedback.Gain, precision: 9);
    }

    [Fact]
    public void FeedbackConversions_HandRuntimeItsShapes()
    {
        var feedback = new FeedbackSettings
        {
            Sound = SoundName.Click,
            VolumeDb = -20.0,
            SendNotification = true,
        };

        Assert.True(feedback.ShouldPlay);
        Assert.Equal("click.wav", feedback.AssetFile);
        Assert.Equal(0.1, feedback.Gain, precision: 9);
        Assert.Equal(SoundName.Click, feedback.ToSoundOptions().Name);
        Assert.True(feedback.ToCopyFeedbackOptions().SendNotification);
    }

    [Fact]
    public void PasteDefaults_CtrlVWithMeasuredDelay()
    {
        var paste = new PasteSettings();

        Assert.Equal(PasteSequence.CtrlV, paste.Sequence);
        Assert.Equal(200, paste.DelayMs);
    }

    [Fact]
    public void PasteClamp_PinsDelayToMeasuredRange()
    {
        var paste = new PasteSettings { DelayMs = 5 };
        paste.Clamp();

        Assert.Equal(100, paste.DelayMs);
    }
}
