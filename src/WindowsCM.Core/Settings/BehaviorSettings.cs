// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Settings;

// Behavior screen (Copyous Behavior parity, 01 §5): the six surviving
// flags. `sync-primary` is permanently N/A on Win32 (single clipboard,
// research 04 §6) and `paste-on-copy` was deprecated upstream in favor of
// `swap-copy-shortcut` — neither appears here (see GnomeCuts).
public sealed class BehaviorSettings
{
    public bool RememberSearch { get; set; } = false;
    public bool ExcludePinned { get; set; } = false;
    public bool ExcludeTagged { get; set; } = false;
    public bool ProtectPinned { get; set; } = true;
    public bool ProtectTagged { get; set; } = true;
    public bool UpdateDateOnCopy { get; set; } = true;

    // WindowsCM addition (no Copyous flag): picking an item pastes it into
    // the field that was focused before the popup opened. On by default;
    // off means a pick only copies. Toggled from the tray menu and Settings;
    // settings files written before this flag existed load it as true.
    public bool AutoPaste { get; set; } = true;
}
