// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Settings;

namespace WindowsCM.Core.Tests.Settings;

// Behavior flags + per-process exclusions + GNOME-cut scope.
public sealed class BehaviorExclusionsTests
{
    [Fact]
    public void BehaviorDefaults_MatchCopyous()
    {
        var behavior = new BehaviorSettings();

        Assert.False(behavior.RememberSearch);
        Assert.False(behavior.ExcludePinned);
        Assert.False(behavior.ExcludeTagged);
        Assert.True(behavior.ProtectPinned);
        Assert.True(behavior.ProtectTagged);
        Assert.True(behavior.UpdateDateOnCopy);
    }

    [Fact]
    public void Behavior_HasNoSyncPrimary_Win32HasOneClipboard()
    {
        var names = typeof(BehaviorSettings).GetProperties().Select(p => p.Name);

        Assert.DoesNotContain("SyncPrimary", names);
        Assert.DoesNotContain("PasteOnCopy", names);
    }

    [Fact]
    public void GnomeCuts_PinTheDecidedScope()
    {
        Assert.Contains(GnomeCuts.Dropped, d => d.Contains("Yaru"));
        Assert.Contains(GnomeCuts.Dropped, d => d.Contains("indicator-display"));
        Assert.Contains(GnomeCuts.Dropped, d => d.Contains("WM_CLASS"));
        Assert.Contains(GnomeCuts.Dropped, d => d.Contains("Sync primary"));
        Assert.Contains(GnomeCuts.Dropped, d => d.Contains("Memory/JSON"));
    }

    [Fact]
    public void Exclusions_AddMatchesWithoutExeCaseInsensitively()
    {
        var exclusions = new ProcessExclusions();

        Assert.True(exclusions.Add("notepad.exe"));
        Assert.True(exclusions.IsExcluded("NOTEPAD"));
        Assert.True(exclusions.IsExcluded("notepad.exe"));
        Assert.False(exclusions.IsExcluded("wordpad"));
    }

    [Fact]
    public void Exclusions_RemoveFindsExeAlias()
    {
        var exclusions = new ProcessExclusions();
        exclusions.Add("notepad.exe");

        Assert.True(exclusions.Remove("notepad"));
        Assert.False(exclusions.IsExcluded("notepad.exe"));
    }

    [Fact]
    public void Exclusions_RejectsPathsAndBlanks()
    {
        var exclusions = new ProcessExclusions();

        Assert.False(exclusions.Add(""));
        Assert.False(exclusions.Add("   "));
        Assert.False(exclusions.Add("C:\\Windows\\notepad.exe"));
        Assert.NotNull(ProcessExclusions.ValidateProcessName(""));
        Assert.NotNull(ProcessExclusions.ValidateProcessName("a/b"));
        Assert.Null(ProcessExclusions.ValidateProcessName("notepad.exe"));
    }

    [Fact]
    public void Exclusions_BlankProcessNeverMatches()
    {
        var exclusions = new ProcessExclusions();
        exclusions.Add("notepad.exe");

        Assert.False(exclusions.IsExcluded(null));
        Assert.False(exclusions.IsExcluded("  "));
    }
}
