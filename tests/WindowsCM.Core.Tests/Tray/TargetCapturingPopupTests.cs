// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Tray;

namespace WindowsCM.Core.Tests.Tray;

internal sealed class FakePopup : ITrayPopup
{
    public bool Visible;
    public List<string> Events = [];
    public bool IsVisible => Visible;
    public void Show(bool incognito)
    {
        Events.Add($"show:{incognito}");
        Visible = true;
    }
    public void Hide()
    {
        Events.Add("hide");
        Visible = false;
    }
    public void Toggle()
    {
        Events.Add("toggle");
        Visible = !Visible;
    }
}

// Ticket 22: every popup show path captures the paste target first so the
// orchestrator never pastes with a stale hotkey-time handle.
public sealed class TargetCapturingPopupTests
{
    [Fact]
    public void Show_CapturesBeforeShowing()
    {
        var inner = new FakePopup();
        var order = new List<string>();
        var subject = new TargetCapturingPopup(
            inner,
            captureTarget: () => { order.Add("capture"); return new IntPtr(77); },
            onCaptured: hw => order.Add($"captured:{hw}"));

        subject.Show(incognito: true);

        Assert.Equal(["capture", "captured:77"], order);
        Assert.Equal(["show:True"], inner.Events);
    }

    [Fact]
    public void ToggleWhenHidden_CapturesBeforeShowing()
    {
        var inner = new FakePopup { Visible = false };
        var captured = new List<IntPtr>();
        var subject = new TargetCapturingPopup(
            inner, () => new IntPtr(11), captured.Add);

        subject.Toggle();

        Assert.Equal([new IntPtr(11)], captured);
        Assert.Equal(["toggle"], inner.Events);
        Assert.True(inner.Visible);
    }

    [Fact]
    public void ToggleWhenVisible_HidesWithoutCapturing()
    {
        var inner = new FakePopup { Visible = true };
        var captured = new List<IntPtr>();
        var subject = new TargetCapturingPopup(
            inner, () => new IntPtr(11), captured.Add);

        subject.Toggle();

        Assert.Empty(captured);
        Assert.Equal(["hide"], inner.Events);
        Assert.False(inner.Visible);
    }

    [Fact]
    public void Hide_NeverCaptures()
    {
        var inner = new FakePopup { Visible = true };
        var captured = new List<IntPtr>();
        var subject = new TargetCapturingPopup(
            inner, () => new IntPtr(11), captured.Add);

        subject.Hide();

        Assert.Empty(captured);
        Assert.Equal(["hide"], inner.Events);
    }
}
