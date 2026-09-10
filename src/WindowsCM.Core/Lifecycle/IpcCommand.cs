// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Lifecycle;

// DBus parity (research 05 §10): Toggle/Show/Hide/ClearHistory. Clear keeps
// pins+tags (DBus false), ClearAll wipes everything (DBus true). Ping is a
// health-check extra with no side effect; v2 may add payloads (copy <id>).
public enum IpcCommand
{
    Toggle,
    Show,
    Hide,
    Clear,
    ClearAll,
    Ping,
}
