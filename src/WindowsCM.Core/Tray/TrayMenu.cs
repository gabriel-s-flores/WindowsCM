using WindowsCM.Core.Localization;

namespace WindowsCM.Core.Tray;

// The five tray menu items (research 03 §3.2, map Q4).
// Clear keeps pins+tags (never wipe-all from the tray).
public enum TrayMenuItem
{
    Open,
    Incognito,
    Clear,
    Settings,
    Exit,
}

public static class TrayMenu
{
    public static IReadOnlyList<TrayMenuItem> All { get; } =
    [
        TrayMenuItem.Open,
        TrayMenuItem.Incognito,
        TrayMenuItem.Clear,
        TrayMenuItem.Settings,
        TrayMenuItem.Exit,
    ];

    public static string LabelFor(TrayMenuItem item, bool? isPortuguese = null)
    {
        var pt = isPortuguese ?? LocalizationManager.IsPortuguese;
        if (pt)
        {
            return item switch
            {
                TrayMenuItem.Open => "Abrir",
                TrayMenuItem.Incognito => "Modo anônimo",
                TrayMenuItem.Clear => "Limpar histórico (manter fixados e tags)",
                TrayMenuItem.Settings => "Configurações",
                TrayMenuItem.Exit => "Sair",
                _ => throw new ArgumentOutOfRangeException(nameof(item)),
            };
        }
        else
        {
            return item switch
            {
                TrayMenuItem.Open => "Open",
                TrayMenuItem.Incognito => "Incognito mode",
                TrayMenuItem.Clear => "Clear history (keep pinned & tags)",
                TrayMenuItem.Settings => "Settings",
                TrayMenuItem.Exit => "Exit",
                _ => throw new ArgumentOutOfRangeException(nameof(item)),
            };
        }
    }
}
