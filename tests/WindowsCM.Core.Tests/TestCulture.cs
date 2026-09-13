// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Runtime.CompilerServices;

namespace WindowsCM.Core.Tests;

// The suite was written on a pt-BR machine: with the language on "System",
// LocalizationManager follows CurrentUICulture, so many assertions expect
// the Portuguese catalog and pt-BR number formatting ("1,0 KB"). Pin that
// culture for every test thread so results never depend on the machine's
// locale (GitHub's Windows runners are en-US).
internal static class TestCulture
{
#pragma warning disable CA2255 // Test assembly, not a library: this is the intended use.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void PinBrazilianPortuguese()
    {
        var ptBr = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = ptBr;
        CultureInfo.DefaultThreadCurrentUICulture = ptBr;
        CultureInfo.CurrentCulture = ptBr;
        CultureInfo.CurrentUICulture = ptBr;
    }
}
