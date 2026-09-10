// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.History;
using WindowsCM.Core.Tray;

namespace WindowsCM.Core.Lifecycle;

// Pipe/CLI command execution (research 05 §10, spec stories 36/41):
// toggle/show/hide drive the popup, clear variants hit the store. Clear
// keeps pins+tags, ClearAll wipes everything. Ping answers ok with no side
// effect. Unknown input answers "unknown" with no side effect, never throws.
public sealed class IpcDispatcher
{
    private readonly ITrayPopup _popup;
    private readonly IHistoryStore _store;

    public IpcDispatcher(ITrayPopup popup, IHistoryStore store)
    {
        _popup = popup;
        _store = store;
    }

    public string Handle(string? line)
    {
        if (!IpcProtocol.TryParse(line, out var command))
        {
            return IpcProtocol.Unknown;
        }
        return Handle(command);
    }

    public string Handle(IpcCommand command)
    {
        switch (command)
        {
            case IpcCommand.Toggle:
                _popup.Toggle();
                return IpcProtocol.Ok;
            case IpcCommand.Show:
                _popup.Show(incognito: false);
                return IpcProtocol.Ok;
            case IpcCommand.Hide:
                _popup.Hide();
                return IpcProtocol.Ok;
            case IpcCommand.Clear:
                _store.Clear(keepProtected: true);
                return IpcProtocol.Ok;
            case IpcCommand.ClearAll:
                _store.Clear(keepProtected: false);
                return IpcProtocol.Ok;
            case IpcCommand.Ping:
                return IpcProtocol.Ok;
            default:
                return IpcProtocol.Unknown;
        }
    }
}
