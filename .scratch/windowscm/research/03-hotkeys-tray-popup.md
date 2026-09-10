# 03 — Hotkeys globais + tray + popup (fontes primárias)

Ticket: `.scratch/windowscm/issues/03-research-hotkeys-tray-popup.md` (não editado).
Regra: apenas fontes primárias — Microsoft Learn (Win32 / WPF / .NET),
`dotnet/wpf`, `microsoft/WPF-Samples`, repos oficiais `hardcodet/wpf-notifyicon`
e `HavenDV/H.NotifyIcon`, docs oficiais VS Code (só §2, conflito de defaults).
Cada claim termina com o link primário entre parênteses.

## 1. `RegisterHotKey` / `UnregisterHotKey` → `WM_HOTKEY`

### 1.1 Assinatura

```c
BOOL RegisterHotKey(HWND hWnd, int id, UINT fsModifiers, UINT vk);
BOOL UnregisterHotKey(HWND hWnd, int id);
```

- `hWnd`: janela que recebe `WM_HOTKEY`. Se `NULL`, a mensagem vai para a fila
  da thread chamadora e precisa ser tratada no loop de mensagens.
  ([RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))
- `UnregisterHotKey(hWnd, id)` libera hotkey registrada pela thread chamadora;
  `hWnd` deve ser `NULL` se a hotkey não está associada a janela.
  ([UnregisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unregisterhotkey))
- Falha ao associar hotkey a janela criada por **outra thread**; nesse caso
  (e se a combinação já estiver registrada) o retorno é zero e o detalhe sai
  via `GetLastError`.
  ([RegisterHotKey — Return value](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))

### 1.2 IDs por janela (faixa segura)

- Aplicação usa `id` em `0x0000–0xBFFF`; DLL compartilhada usa
  `0xC000–0xFFFF` (faixa do `GlobalAddAtom`); DLL deve obter o id via
  `GlobalAddAtom` para não colidir com outras DLLs.
  ([RegisterHotKey — Windows CE 3.0, arquivado, mesma semântica](https://learn.microsoft.com/en-us/previous-versions/ms961355(v=msdn.10)))
- WindowsCM é app (não DLL): usar constantes pequenas por `HWND`
  (ex. `1` = abrir, `2` = incognito). O `id` só precisa ser único **dentro do
  par (`hWnd`, thread)** — é ele que chega em `wParam` de `WM_HOTKEY`.
  ([WM_HOTKEY — wParam](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey))
- Atenção ao re-registrar: se já existe hotkey com mesmo `hWnd`+`id`, a antiga
  é **mantida junto** da nova — o app precisa chamar `UnregisterHotKey`
  explicitamente na antiga.
  ([RegisterHotKey — Remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))

### 1.3 Modificadores

| Flag | Valor | Significado |
|---|---|---|
| `MOD_ALT` | `0x0001` | Alt pressionado |
| `MOD_CONTROL` | `0x0002` | Ctrl pressionado |
| `MOD_SHIFT` | `0x0004` | Shift pressionado |
| `MOD_WIN` | `0x0008` | Tecla Windows — **reservada para o SO** (ver §1.5) |
| `MOD_NOREPEAT` | `0x4000` | Auto-repeat do teclado não gera múltiplas notificações |

Tabela e reserva do `MOD_WIN` em
([RegisterHotKey — fsModifiers](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)).
`vk` é um virtual-key code qualquer
([Virtual Key Codes](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes)).

### 1.4 `MOD_NOREPEAT` — desde qual Windows

- O flag existe com a ressalva **"Windows Vista: This flag is not supported"**,
  ou seja, na prática usar a partir do **Windows 7+**.
  ([RegisterHotKey — fsModifiers](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))
- Exemplo oficial `ALT+b` com `MOD_ALT | MOD_NOREPEAT` registrado para a thread
  (`hWnd = NULL`) e lido via `GetMessage`/`WM_HOTKEY` — mesma página e no
  sample
  ([Windows-classic-samples RegisterHotKey.cpp](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/Win7Samples/winui/RegisterHotKey/RegisterHotKey.cpp)).
- Recomendação: sempre combinar `MOD_NOREPEAT` nas hotkeys do WindowsCM
  (abrir popup segurando teclas não deve disparar N vezes).

### 1.5 Erro `ERROR_HOTKEY_ALREADY_REGISTERED` — detectar e fallback

- A doc só promete: retorna zero; "tipicamente, `RegisterHotKey` também falha
  se as teclas já foram registradas por outra hotkey"; detalhe via
  `GetLastError`.
  ([RegisterHotKey — Return value](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))
- Padrão de detecção em C# (nome simbólico `ERROR_HOTKEY_ALREADY_REGISTERED`;
  **confirmar o valor numérico em `winerror.h` / "System Error Codes" no
  momento da implementação** — lacuna §6.1):

```csharp
[DllImport("user32.dll", SetLastError = true)]
static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

if (!RegisterHotKey(hwnd, id, mods, vk))
{
    int err = Marshal.GetLastWin32Error(); // exige SetLastError = true
    if (err == 1409 /* ERROR_HOTKEY_ALREADY_REGISTERED — confirmar em winerror.h */)
    {
        // Fallback: avisar no tray balloon + abrir tela de hotkeys pedindo outra combinação.
        // Nunca tentar UnregisterHotKey da hotkey alheia: só a thread dona consegue liberar.
    }
}
```

- Só a thread que registrou consegue liberar via `UnregisterHotKey`
  (discussão oficial confirma a semântica por-thread).
  ([MS Q&A — RegisterHotKey/UnregisterHotKey por thread](https://learn.microsoft.com/en-us/answers/questions/1343773/does-the-shortcut-key-registered-by-the-registerho))

### 1.6 Por que `Win+V` / `Win+Shift+V` estão fora

1. **Reserva genérica**: "Keyboard shortcuts that involve the WINDOWS key are
   reserved for use by the operating system" e "Hotkeys that involve the
   Windows key are reserved for use by the operating system".
   ([RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey),
   [WM_HOTKEY](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey))
2. **Conflito específico**: `Win+V` abre o histórico da área de transferência
   do Windows ("Press Windows logo key + V").
   ([Clipboard History — Microsoft Windows](https://www.microsoft.com/en-gb/windows/tips/clipboard-history),
   [Using the clipboard — Microsoft Support](https://support.microsoft.com/en-us/windows/apps/using-the-clipboard))
3. `Win+Shift+V` cai na mesma reserva (qualquer combo com `MOD_WIN`), então
   `RegisterHotKey(MOD_WIN|MOD_SHIFT, 'V')` tende a falhar ou a brigar com o
   SO — mesma base dos itens 1–2. Resposta oficial a caso análogo (`Win+D`)
   recomenda não usar a tecla Windows como hotkey.
   ([MS Q&A — hotkey com Windows key](https://learn.microsoft.com/en-us/answers/questions/1020405/error-while-registering-the-hotkey-in-c))
4. Extra: `F12` é reservado ao debugger e não deve ser registrado como hotkey.
   ([RegisterHotKey — Remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey))

### 1.7 Recebimento via `HwndSource.AddHook` em WPF (amostra mínima)

- `HwndSource.AddHook(HwndSourceHook)` recebe **todas** as mensagens da janela;
  é o caminho para mensagens sem equivalente WPF (como `WM_HOTKEY = 0x0312`).
  ([HwndSource.AddHook](https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource.addhook))
- `HwndSource` embrulha conteúdo WPF numa `HWND`; `Handle` expõe o `HWND`
  para P/Invoke; hook pode ser adicionado na construção ou depois via
  `AddHook`.
  ([HwndSource](https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource?view=windowsdesktop-10.0),
  [WPF and Win32 interop](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-and-win32-interoperation),
  [fonte dotnet/wpf](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndSource.cs))

```csharp
// cs: HotkeyService.cs (mínimo, janela principal já criada)
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

public sealed class HotkeyService : IDisposable
{
    const int WM_HOTKEY = 0x0312; // https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey
    const uint MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_ALT = 0x0001, MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    const int IdOpen = 1, IdIncognito = 2;
    HwndSource? _src;

    public void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var helper = new WindowInteropHelper(window);
            _src = HwndSource.FromHwnd(helper.Handle);
            _src?.AddHook(WndProc);
            RegisterHotKey(helper.Handle, IdOpen, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.V));
            RegisterHotKey(helper.Handle, IdIncognito, MOD_CONTROL | MOD_SHIFT | MOD_ALT | MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.V));
        };
        window.Closed += (_, _) => Dispose();
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            handled = true;
            if (wParam.ToInt32() == IdOpen) /* abrir popup */;
            if (wParam.ToInt32() == IdIncognito) /* abrir incognito */;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_src is null) return;
        var hwnd = _src.Handle;
        UnregisterHotKey(hwnd, IdOpen);
        UnregisterHotKey(hwnd, IdIncognito);
        _src.RemoveHook(WndProc);
        _src = null;
    }
}
```

- `wParam` = id da hotkey; `lParam`: word baixa = modificadores, word alta =
  virtual-key code.
  ([WM_HOTKEY — Parameters](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey))

## 2. Defaults `Ctrl+Shift+V` (abrir) e `Ctrl+Shift+Alt+V` (incognito)

### 2.1 Risco de conflito com apps comuns

- **Conflito real, documentado**: `Ctrl+Shift+V` = "paste as plain text"
  (colar sem formatação) na lista oficial de atalhos do Windows.
  ([Windows shortcuts — Microsoft](https://www.microsoft.com/en-us/windows/tips/windows-shortcuts))
- **VS Code**: `Ctrl+Shift+V` é disputado — cola no terminal integrado **e**
  abre Markdown preview (`markdown.showPreview` quando
  `editorLangId == 'markdown'`); issue oficial mostra o choque e workaround
  via `keybindings.json`.
  ([VS Code — Default keybindings](https://code.visualstudio.com/docs/reference/default-keybindings),
  [microsoft/vscode#315171](https://github.com/microsoft/vscode/issues/315171))
-Natureza do choque: hotkey **global** (`RegisterHotKey`) dispara mesmo com
  outro app em foco, então com WindowsCM rodando o `Ctrl+Shift+V` abre o popup
  em vez de "colar sem formatação" no app focado. Para um clipboard manager é
  aceitável como default (o usuário quer o popup sob o cursor), mas **precisa**
  ser configurável + exibir o aviso de conflito — daí §2.2.
- `Ctrl+Shift+Alt+V` (incognito): combinação quádrupla, sem default conhecido
  nos apps pesquisados; risco baixo, mas mesma regra de configurabilidade
  (qualquer app pode ter registrado antes → §1.5).

### 2.2 Configurabilidade (persistir + re-registrar em runtime)

- Persistir como **user-scoped settings**: `Properties.Settings.Default.<nome>`,
  leitura via `Properties.Settings.Default`, escrita + `Save()` para durar
  entre sessões (`user.config` criado sob demanda; defaults vivem no
  `app.exe.config`).
  ([Using Application Settings and User Settings](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/using-application-settings-and-user-settings),
  [How To: Write User Settings at Run Time with C#](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/how-to-write-user-settings-at-run-time-with-csharp),
  [How To: Read Settings at Run Time With C#](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/how-to-read-settings-at-run-time-with-csharp),
  [Application Settings Architecture](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/application-settings-architecture))
- Re-registro em runtime = `UnregisterHotKey(hWnd, idAntigo)` **antes** de
  `RegisterHotKey(hWnd, idNovo, ...)` (a doc exige liberar a antiga
  explicitamente — §1.2). Fluxo: falhou → checar `ERROR_HOTKEY_ALREADY_REGISTERED`
  (§1.5) → toast/balloon + reabrir editor de hotkeys.

```csharp
// cs: troca de hotkey em runtime (mesmo HWND/id — Unregister explícito, cf. §1.2)
UnregisterHotKey(hwnd, IdOpen);
if (!RegisterHotKey(hwnd, IdOpen, newMods | MOD_NOREPEAT, newVk))
{
    int err = Marshal.GetLastWin32Error();
    // err == ERROR_HOTKEY_ALREADY_REGISTERED → reverter p/ default e notificar
}
Properties.Settings.Default.HotkeyOpen = gestureString; // ex. "Ctrl+Shift+V"
Properties.Settings.Default.Save();
```

## 3. Tray em WPF

WPF não tem `NotifyIcon` próprio — as duas opções primárias:

### 3.1 Opção A — `System.Windows.Forms.NotifyIcon`

- Componente WinForms para ícone de processo em background na área de
  notificação; props-chave `Icon` + `Visible` (ícone só aparece com
  `Visible = true`).
  ([NotifyIcon Component Overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-overview-windows-forms),
  [NotifyIcon Class](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon?view=windowsdesktop-10.0))
- `Icon` é `System.Drawing.Icon` carregado de `.ico`; `Text` = tooltip ao
  pairar; exemplo oficial usa `DoubleClick` para ativar o form e
  `ContextMenu` com item Exit.
  ([Add Icons to the TaskBar](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/app-icons-to-the-taskbar-with-wf-notifyicon),
  [NotifyIcon.Text](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.text?view=windowsdesktop-10.0))
- Balloon: `ShowBalloonTip(timeout, title, text, icon)` + props
  `BalloonTipText/Title/Icon`; se já houver balloon visível, o timeout é
  ignorado (comportamento varia por SO/app).
  ([NotifyIcon.ShowBalloonTip](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip?view=windowsdesktop-10.0))
- Prós: zero dependência externa, API estável do .NET, suficiente p/ ícone +
  tooltip + menu + balloon. Contras (avaliação de engenharia, fatos de API nas
  páginas acima): é WinForms (`ContextMenuStrip`, sem data binding WPF nativo,
  sem `ICommand` do WPF, tooltip de texto simples sem rich tooltip XAML);
  exige `Dispose()`/`Visible=false` ao sair senão o ícone fantasma fica até o
  hover; precisa referenciar WinForms no projeto WPF.

```csharp
// cs: wiring mínimo WinForms dentro do App WPF
_notifyIcon = new System.Windows.Forms.NotifyIcon
{
    Icon = new System.Drawing.Icon("Assets/app.ico"),
    Text = "WindowsCM",
    Visible = true,
    ContextMenuStrip = menu, // Abrir / Incognito / Limpar (manter pins+tags) / Configs / Sair
};
_notifyIcon.MouseClick += (_, e) =>
{
    if (e.Button == System.Windows.Forms.MouseButtons.Left) TogglePopup();
    // botão direito: ContextMenuStrip abre sozinho
};
_notifyIcon.DoubleClick += (_, _) => TogglePopup(); // amostra oficial usa DoubleClick p/ ativar
```

### 3.2 Opção B — `H.NotifyIcon.WPF` (`TaskbarIcon`) — recomendada

- Controle WPF puro (não embrulha o WinForms): rich tooltips, popups,
  context menus, balloons e **suporte a comandos em single/double-click**;
  `MenuActivation`/`PopupActivation` configuram qual clique abre menu vs popup.
  ([hardcodet/wpf-notifyicon — README](https://github.com/hardcodet/wpf-notifyicon),
  [H.NotifyIcon — readme](https://github.com/HavenDV/H.NotifyIcon/blob/master/readme.md))
- `H.NotifyIcon` é a continuação ativa do projeto base inativo, p/
  .NET 6+ WPF/WinUI/Uno/Console; pacotes `H.NotifyIcon.Wpf` etc.
  ([H.NotifyIcon — readme](https://github.com/HavenDV/H.NotifyIcon/blob/master/readme.md))
- **Licença MIT** (confirmada no arquivo).
  ([H.NotifyIcon — LICENSE.md](https://github.com/HavenDV/H.NotifyIcon/blob/master/LICENSE.md))
- Amostra XAML canônica (mesma nos dois repos):

```xml
<!-- XAML: TaskbarIcon com menu + comandos (adaptar p/ WindowsCM) -->
<Window xmlns:tb="clr-namespace:H.NotifyIcon;assembly=H.NotifyIcon.Wpf" ...>
  <tb:TaskbarIcon x:Name="Tray"
                  ToolTipText="WindowsCM"
                  IconSource="/Assets/app.ico"
                  ContextMenu="{StaticResource TrayMenu}"
                  MenuActivation="RightClick"
                  LeftClickCommand="{Binding TogglePopupCommand}"
                  DoubleClickCommand="{Binding TogglePopupCommand}" />
</Window>
```

```xml
<!-- XAML: menu Abrir/Incognito/Limpar(manter pins+tags)/Configs/Sair -->
<ContextMenu x:Key="TrayMenu">
  <MenuItem Header="Abrir" Command="{Binding OpenCommand}" />
  <MenuItem Header="Abrir incógnito" Command="{Binding OpenIncognitoCommand}" />
  <MenuItem Header="Limpar (mantém pins e tags)" Command="{Binding ClearUnpinnedCommand}" />
  <Separator />
  <MenuItem Header="Configurações" Command="{Binding OpenSettingsCommand}" />
  <MenuItem Header="Sair" Command="{Binding ExitCommand}" />
</ContextMenu>
```

- Extras úteis já no repo: `TrayPopup`/`TrayToolTip` (popup/tooltip ricos em
  XAML com data binding), `GeneratedIconSource` (ícone dinâmico, ex. contador
  — útil p/ badge), `ForceCreate()` (cria ícone mesmo windowless) e
  Efficiency Mode, recriação automática se o Explorer reiniciar
  (`TaskbarCreated`).
  ([H.NotifyIcon — readme](https://github.com/HavenDV/H.NotifyIcon/blob/master/readme.md),
  [TaskbarIcon.cs](https://github.com/HavenDV/H.NotifyIcon/blob/master/src/libs/H.NotifyIcon.Shared/TaskbarIcon.cs))
- Decisão: **Opção B**. Motivo: popup rico + `ICommand` + binding com o
  ViewModel do WindowsCM sem camada WinForms; MIT permite uso comercial.

### 3.3 Comportamento: single vs double-click, tooltip, balloon, ícone

- Especificar `LeftClick` = abre/toggle popup, `RightClick` = menu de contexto
  (convenção Windows atual; `DoubleClick` herdado da amostra WinForms antiga —
  manter como alias de abrir, nunca como único caminho: touch e descoberta
  penalizam double-click). `H.NotifyIcon` permite declarar isso
  (`MenuActivation`/`PopupActivation`/`LeftClickCommand`).
  ([hardcodet/wpf-notifyicon — README](https://github.com/hardcodet/wpf-notifyicon),
  [Add Icons to the TaskBar — amostra DoubleClick](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/app-icons-to-the-taskbar-with-wf-notifyicon))
- Tooltip: texto curto de fallback (`ToolTipText`); balloon padrão do Windows
  para avisos (ex. hotkey ocupada §1.5) via `ShowBalloonTip`.
  ([NotifyIcon Component Overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/notifyicon-component-overview-windows-forms),
  [NotifyIcon.ShowBalloonTip](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip?view=windowsdesktop-10.0))
- Ícone: `.ico` multi-tamanho (16/24/32/48/256); `IconSource` aceita `.ico`
  ([H.NotifyIcon — readme](https://github.com/HavenDV/H.NotifyIcon/blob/master/readme.md)).
  Dark/light: prover variantes do `.ico` e trocar `IconSource` na mudança de
  tema do app (o `ContextMenuThemeMode` Light/Dark do H.NotifyIcon cobre o
  menu nativo no modo `PopupMenu` — ver seção WinUI context menu do readme).

## 4. Popup

### 4.1 Janela: `ShowActivated=false` + `Topmost`

```xml
<!-- XAML -->
<Window x:Class="WindowsCM.PopupWindow"
        ShowActivated="False"
        Topmost="True"
        ShowInTaskbar="False"
        WindowStyle="None"
        ResizeMode="NoResize"
        SizeToContent="WidthAndHeight"
        Deactivated="Popup_Deactivated"
        PreviewKeyDown="Popup_PreviewKeyDown" />
```

- `ShowActivated` = se a janela é ativada ao ser mostrada pela primeira vez;
  `false` + amostra dedicada "abrir sem ativar".
  ([Window Class — ShowActivated](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window?view=windowsdesktop-10.0),
  [WPF-Samples ShowWindowWithoutActivation](https://github.com/microsoft/WPF-Samples/blob/main/Windows/ShowWindowWithoutActivation/README.md))
- `Topmost=true` = acima de todas as janelas com `Topmost=false` (dentro do
  grupo topmost, a ativa fica no topo).
  ([Window.Topmost](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.topmost?view=windowsdesktop-10.0))
- Perda de foco → auto-hide: tratar `Deactivated` (dispara ao desativar;
  `IsActive` diz o estado).
  ([Window.Deactivated](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.deactivated?view=windowsdesktop-10.0),
  [Application Management Overview — Activated/Deactivated](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/application-management-overview))
  Decisão de produto (paridade Copyous): `Deactivated → Hide()` com exceção
  configurável futura; `Esc` fecha (`Key.Escape → Hide()` no `PreviewKeyDown`).
- Animação ~150 ms: `Popup`/`Window` com `Storyboard` (`DoubleAnimation` em
  `Opacity` + `TranslateTransform`), equivalente ao close-animation do Copyous;
  base em
  ([Popup — WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup),
  [Animate a Popup](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup)).
  Duração exata é decisão de paridade, não de API.

### 4.2 Posição: `GetCursorPos` (mouse) vs caret (texto)

- `GetCursorPos` retorna a posição do mouse **em screen coordinates**
  (não afetada por mapping mode).
  ([GetCursorPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos))
  → default v1 do WindowsCM: abrir sob o cursor. Simples, global, sem
  permissão especial além de `WINSTA_READATTRIBUTES`/input desktop
  (mesma página, Remarks).
- `GetCaretPos` retorna o caret **em client coordinates da janela que contém
  o caret** e **não participa de DPI virtualization** (valores lógicos da
  janela dona; thread chamadora ignorada).
  ([GetCaretPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcaretpos))
  Na prática só serve para o caret da própria thread → para caret **global**
  usar `GetGUIThreadInfo`.
- `GetGUIThreadInfo(idThread, &gui)`: com `idThread = NULL` retorna info da
  **foreground thread**; funciona mesmo se a janela ativa é de outro processo;
  entrega `hwndCaret` + `rcCaret` (bounding rect do caret, em client coords de
  `hwndCaret`) — converter com `ClientToScreen`.
  ([GetGUIThreadInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo),
  [GUITHREADINFO](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-guithreadinfo))
  Ressalvas da própria doc: pode não retornar handles válidos enquanto a janela
  perde ativação; para edit control, `rcCaret` inclui direção de texto/padding
  (posição exata pode exigir ajuste).
  ([GetGUIThreadInfo — Remarks](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo))
- Alternativa moderna: UI Automation — `AutomationElement.FocusedElement`
  ([FocusedElement](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationelement.focusedelement?view=windowsdesktop-10.0)) /
  `IUIAutomation::GetFocusedElement`
  ([Win32](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomation-getfocusedelement))
  + `TextPattern` do elemento
  ([TextPattern](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.textpattern?view=windowsdesktop-10.0)).
  Faixa de caret via `IUIAutomationTextPattern2::GetCaretRange` **não verificada
  nesta pesquisa** (lacuna §6.3).
- Decisão: v1 = cursor (`GetCursorPos`); v2 = tentar caret via
  `GetGUIThreadInfo(0)` + `ClientToScreen`, com fallback para cursor quando
  inválido. UIA fica como evolução.

### 4.3 Multi-monitor + per-monitor DPI (amostra com DPI)

- Monitores: `MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST)` devolve o
  `HMONITOR` do ponto em virtual-screen coordinates.
  ([MonitorFromPoint](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfrompoint))
  Métricas do desktop virtual: `SM_X/Y/CX/CYVIRTUALSCREEN`, `SM_CMONITORS`.
  ([Multiple Monitor System Metrics](https://learn.microsoft.com/en-us/windows/win32/gdi/multiple-monitor-system-metrics))
  Lado gerenciado: `Screen.FromPoint` devolve a tela do ponto (ou a mais
  próxima) com `Bounds`/`WorkingArea` para clampar o popup.
  ([Screen.FromPoint](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.screen.frompoint?view=windowsdesktop-10.0))
- DPI: declarar per-monitor no manifesto (`dpiAwareness` PerMonitor) —
  WPF é system-DPI-aware por default e precisa opt-in.
  ([WPF-Samples PerMonitorDPI](https://github.com/microsoft/WPF-Samples/blob/main/PerMonitorDPI/readme.md))
  Para processo já criado, `SetProcessDpiAwarenessContext` (recomendado via
  manifesto; chamar antes de qualquer UI; PerMonitorV2 = Win10 1703+).
  ([SetProcessDpiAwarenessContext](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setprocessdpiawarenesscontext),
  [DPI_AWARENESS_CONTEXT](https://learn.microsoft.com/en-us/windows/win32/hidpi/dpi-awareness-context))
  Conversão px→DIPs no ponto de posicionamento:
  `VisualTreeHelper.GetDpi(visual)` → `DpiScale`.
  ([VisualTreeHelper.GetDpi](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.visualtreehelper.getdpi?view=windowsdesktop-10.0))

```csharp
// cs: posicionar popup no cursor, com DPI e clamp no monitor (mínimo)
[DllImport("user32.dll", SetLastError = true)]
static extern bool GetCursorPos(out POINT lpPoint);
[StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

void PlaceAtCursor(Window popup)
{
    GetCursorPos(out var pt); // screen px — https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos
    var src = PresentationSource.FromVisual(popup);
    double dx = 1, dy = 1;
    if (src?.CompositionTarget != null) // px físicos → DIPs
    {
        var m = src.CompositionTarget.TransformFromDevice;
        dx = m.M11; dy = m.M22;
    }
    // Alternativa por-visual: VisualTreeHelper.GetDpi(popup).DpiScaleX/Y
    // https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.visualtreehelper.getdpi?view=windowsdesktop-10.0
    var area = System.Windows.Forms.Screen.FromPoint(
        new System.Drawing.Point(pt.X, pt.Y)).WorkingArea; // monitor do ponto
    // https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.screen.frompoint?view=windowsdesktop-10.0
    popup.Left = Math.Min(pt.X * dx, (area.Right - popup.Width) * dx);
    popup.Top = Math.Min(pt.Y * dy, (area.Bottom - popup.Height) * dy);
    popup.Left = Math.Max(popup.Left, area.Left * dx);
    popup.Top = Math.Max(popup.Top, area.Top * dy);
}
```

## 5. Atalhos internos do Copyous → WPF (`KeyBinding`/`InputBinding`)

Base WPF: `KeyBinding` liga `KeyGesture` a `ICommand`
([KeyBinding](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.keybinding?view=windowsdesktop-10.0),
sintaxe `Gesture="CTRL+R"` ou `Key`+`Modifiers`)
em `Window.InputBindings`/`UIElement.InputBindings`
([InputBinding](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.inputbinding?view=windowsdesktop-10.0)).
Eventos preview tunelam (raiz→alvo) antes dos bubbling — `PreviewKeyDown` no
container externo roda antes do controle focado; controles compostos podem
marcar bubbling como handled (ex. `ButtonBase` marca `MouseLeftButtonDown` e
sobe `Click`).
([Input Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/input-overview),
[Preview events](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/events/preview-events)).
Comandos roteados têm bindings default (ex. `CTRL+C` vem junto com Copy) e
`TextBox` já traz `CommandBinding` interno para edição (Paste/Copy/Cut/Undo…).
([Commanding Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview),
[Hook Up a Command](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/how-to-hook-up-a-command-to-a-control-with-command-support)).

| Copyous | Proposta WPF | Colisão / resolução |
|---|---|---|
| `Enter`/`Space` copy-vs-paste, `Shift` inverte, `swap-copy-shortcut` | `PreviewKeyDown` no `Window`/lista; setting bool inverte | `TextBox` não suporta comandos de formatação mas suporta básicos como `MoveToLineEnd` ([TextBox](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/textbox)); `TextEditor` interno trata `Space`/`Shift+Space` antes do `KeyDown` ([dotnet/wpf#8249](https://github.com/dotnet/wpf/issues/8249)). Só tratar Enter/Space quando o foco **não** está na caixa de busca (`Keyboard.FocusedElement is not TextBox`) |
| `Ctrl+Enter` ação default | `KeyBinding Ctrl+Enter` no `Window` | Em `TextBox` multilinha (`AcceptsReturn`) Enter insere quebra; popup usa busca single-line → seguro; reforçar com `PreviewKeyDown` se precisar |
| `Ctrl+S` pin | `KeyBinding Ctrl+S` | Sem binding de edição padrão no `TextBox`; seguro em escopo de janela |
| `Delete` (＋`Shift` força) | `PreviewKeyDown` (Delete/Shift+Delete) | `Shift+Delete` = Cut em controles de texto (gesto default Cut); `Delete` apaga char na busca. Escopar à lista focada, nunca global |
| `Ctrl+E` editar | `KeyBinding Ctrl+E` | `TextBox` não implementa comandos de formatação (`ToggleBold` etc.) ([TextBox](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/textbox)); livre |
| `Ctrl+T` título | `KeyBinding Ctrl+T` | Sem default conhecido; livre |
| `Ctrl+A` menu | `PreviewKeyDown` escopado à lista | `Ctrl+A` = SelectAll dentro de `TextBox` (binding default da família ApplicationCommands — **confirmar gesto exato na página do comando**, lacuna §6.4). Não sequestrar quando a busca tem foco |
| `Ctrl+0..9` jump | `KeyBinding` ×10 no `Window` | Sem defaults; atenção: `Key.D0..D9` + também numpad (`NumPad0..9`) se quiser cobrir |
| `Alt` (sozinho) toggle pinned | **Recomendado trocar por `Alt+P`** | Tecla Alt sozinha gera `Key.System` e aciona modo menu/foco de menu ([Input Overview — Key.System / TextInput](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/input-overview)); Alt puro rouba foco e quebra acessibilidade |
| `Ctrl+F` busca | `KeyBinding Ctrl+F` → foco na busca | `Find` existe na família ApplicationCommands mas sem UI embutida no `TextBox` ([Commanding Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview)); seguro |
| `Ctrl+Tab` tipo | `KeyBinding` ou `PreviewKeyDown` | Tab navega foco; `Ctrl+Tab` em `TextBox` com `AcceptsTab` insere tab. Busca com `AcceptsTab=false` → seguro |
| `Ctrl+\`` tag | `KeyBinding Key=Oem3` (ou `OemBackquote` conforme layout) | Sem default; notar dependência de layout (`` ` `` varia por teclado ABNT/US) — expor no editor de hotkeys |
| `Ctrl+Shift+0..9` tag | `KeyBinding` ×10 | Sem defaults; livre |
| Scroll | manter `ScrollViewer` default | Nenhum binding custom; paridade = não interceptar wheel |

Regra geral: o que é **global-no-popup** (`Ctrl+S/E/T/F`, `Ctrl+0..9`,
`Ctrl+Shift+0..9`, `Ctrl+Enter`) vai em `Window.InputBindings`; o que depende
de **onde está o foco** (`Enter`, `Space`, `Delete`, `Ctrl+A`) vai em
`PreviewKeyDown` tunelado no `Window` checando `Keyboard.FocusedElement`,
porque preview roda antes do controle e `e.Handled = true` impede o
comportamento default.

```xml
<!-- XAML: bindings globais-no-popup -->
<Window.InputBindings>
  <KeyBinding Gesture="CTRL+S" Command="{Binding TogglePinCommand}" />
  <KeyBinding Gesture="CTRL+E" Command="{Binding EditCommand}" />
  <KeyBinding Gesture="CTRL+T" Command="{Binding EditTitleCommand}" />
  <KeyBinding Gesture="CTRL+F" Command="{Binding FocusSearchCommand}" />
  <KeyBinding Gesture="CTRL+ENTER" Command="{Binding DefaultActionCommand}" />
  <KeyBinding Gesture="CTRL+1" Command="{Binding JumpCommand}" CommandParameter="1" />
</Window.InputBindings>
```

```csharp
// cs: sensível-ao-foco (Enter/Space/Delete/Ctrl+A) — preview tunela antes do TextBox
// https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/input-overview
void Popup_PreviewKeyDown(object sender, KeyEventArgs e)
{
    bool inSearch = Keyboard.FocusedElement is System.Windows.Controls.TextBox;
    if (inSearch) return; // busca mantém comportamento de edição default
    bool swap = Properties.Settings.Default.SwapCopyShortcut;
    if (e.Key == Key.Enter) { /* Enter=copy / Shift+Enter=paste (ou invertido) */ e.Handled = true; }
    else if (e.Key == Key.Space) { /* idem */ e.Handled = true; }
    else if (e.Key == Key.Delete) { /* Delete / Shift+Delete=força */ e.Handled = true; }
    else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control) { /* menu */ e.Handled = true; }
    else if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
}
```

## 6. Lacunas (a confirmar na implementação)

1. Valor numérico de `ERROR_HOTKEY_ALREADY_REGISTERED` (citar `winerror.h` /
   "System Error Codes (1300-1699)"; usamos 1409 como hipótese a verificar).
2. Versão corrente do pacote `H.NotifyIcon.Wpf` no NuGet e TFMs suportados
   (.NET 8/9) — checar no nuget.org na hora de referenciar.
3. `IUIAutomationTextPattern2::GetCaretRange` para caret global via UIA —
   alternativa não verificada; v1 fica em `GetGUIThreadInfo`.
4. Gestos default exatos de `ApplicationCommands`/`EditingCommands`
   (`SelectAll`, `Delete`, `Find`) nas páginas de cada comando — checar antes
   de finalizar a tabela de colisões §5.
5. Limite de chars do tooltip do tray (`NOTIFYICONDATA.szTip`) e comportamento
   do balloon no Win11 atual — confirmar em `NOTIFYICONDATA` + teste manual.
6. `Ctrl+Shift+V` em browsers (colar sem formatação) não verificado em doc
   primária de cada browser — impacto prático igual ao VS Code (§2.1), mas sem
   link primário coletado.
