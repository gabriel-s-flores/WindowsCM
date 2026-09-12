// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Tray;

// Guidance for Windows 11 system tray overflow drawer (research 03, ticket 24).
// In Windows 11, new tray icons start in the hidden overflow flyout (^) by default.
// Microsoft provides no supported API to programmatically promote icons onto the
// main taskbar, so WindowsCM guides the user to drag the icon out of the drawer
// rather than attempting fragile or prohibited hacks.
public static class TrayOnboarding
{
    public const string Guidance =
        "No Windows 11, novos ícones da bandeja aparecem na gaveta de opções ocultas (^) por padrão.\n" +
        "Para manter o WindowsCM sempre visível ao lado do relógio, clique no ícone ^ na barra de tarefas " +
        "e arraste o ícone do WindowsCM para a barra de tarefas (ou ative-o em Configurações do Windows > Personalização > Barra de tarefas > Outros ícones da bandeja do sistema).\n" +
        "A promoção programática não é realizada por padrão, respeitando as regras da plataforma Windows.";

    public const string GuidanceSummary =
        "Arraste o ícone do WindowsCM da gaveta (^) para a barra de tarefas. Promoção programática não é realizada por padrão.";
}
