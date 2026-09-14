; SPDX-License-Identifier: GPL-3.0-or-later
;
; WindowsCM per-user installer (ticket 18, grilling 08 Q16).
; Requires Inno Setup 6.1+.
;
; Decisions pinned here (mirrored in src/WindowsCM.Core/Release and
; asserted by InstallerScriptTests, so the script can never silently
; drift from the runtime):
; - Per-user, no admin: PrivilegesRequired=lowest, install root
;   {localappdata}\Programs\WindowsCM (InstallerContract.InstallSubPath).
; - Start Menu shortcut only ({group}); no Desktop shortcut by decision.
; - Autostart is opt-in (unchecked task): HKCU Run value
;   "WindowsCM" = "<exe>" --hidden, owned by AutostartManager.
;   The Run value is always removed on uninstall (uninsdeletevalue)
;   even when user data is kept.
; - LICENSE is shown at install time (LicenseFile) and a copy is
;   installed next to the exe (grilling 08: LICENSE raiz + copia).
; - Upgrades preserve data structurally: history, settings, actions,
;   images and caches live under %LocalAppData%\WindowsCM and
;   %AppData%\WindowsCM (UserDataPolicy), outside {app}, so replacing
;   {app} cannot reach them. No AppMutex: the single-instance mutex is
;   SID-qualified at runtime (InstanceNames) and cannot be named
;   statically; CloseApplications covers a running tray instance.
; - Uninstall keeps user data by default; a custom uninstall form with
;   an unchecked removal checkbox gates DelTree of the data roots.
;   (CreateCustomForm: custom wizard *pages* are not supported in the
;   uninstaller, modal forms are.)
;
; Release layout: publish the single-file exe first, then compile:
;   dotnet publish <app> -c Release -r win-x64 --self-contained true
;       /p:PublishSingleFile=true -o installer/publish
;   iscc installer/WindowsCM.iss
; See docs/release/smoke-matrix.md for the full release gate.

#define AppName "WindowsCM"
; CI passes the release version with /DAppVersion=x.y.z; local builds keep this.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppExe "WindowsCM.exe"
#define AppId "{5443123D-2460-456A-ABAA-B3ECD11A4134}"

[Setup]
AppId={{#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\WindowsCM
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
SetupIconFile=..\src\WindowsCM.App\Assets\app.ico
Compression=lzma2
SolidCompression=yes
LicenseFile=..\LICENSE
MinVersion=10.0.19042
ArchitecturesAllowed=x64compatible
WizardStyle=modern
CloseApplications=yes
UninstallDisplayName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: autostart; Description: "Start WindowsCM when I sign in"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: WindowsCM; ValueData: """{app}\{#AppExe}"" --hidden"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch WindowsCM now"; Flags: nowait postinstall skipifsilent unchecked

[Code]
var
  RemoveDataChosen: Boolean;

function InitializeUninstall(): Boolean;
var
  Form: TSetupForm;
  Check: TNewCheckBox;
  OkButton, CancelButton: TNewButton;
begin
  RemoveDataChosen := False;

  Form := CreateCustomForm(ScaleX(420), ScaleY(150), False, True);
  try
    Form.Caption := 'Remove user data?';
    Form.Position := poScreenCenter;

    Check := TNewCheckBox.Create(Form);
    Check.Parent := Form;
    Check.Left := ScaleX(12);
    Check.Top := ScaleY(12);
    Check.Width := Form.ClientWidth - ScaleX(24);
    Check.Height := ScaleY(64);
    Check.Caption := 'Also remove my clipboard history, settings, actions and caches. Leave unchecked to keep your data for a future reinstall.';
    Check.Checked := False;

    OkButton := TNewButton.Create(Form);
    OkButton.Parent := Form;
    OkButton.Left := Form.ClientWidth - ScaleX(75 + 6 + 75 + 12);
    OkButton.Top := Form.ClientHeight - ScaleY(23 + 12);
    OkButton.Width := ScaleX(75);
    OkButton.Height := ScaleY(23);
    OkButton.Caption := 'OK';
    OkButton.ModalResult := mrOk;
    OkButton.Default := True;

    CancelButton := TNewButton.Create(Form);
    CancelButton.Parent := Form;
    CancelButton.Left := OkButton.Left + OkButton.Width + ScaleX(6);
    CancelButton.Top := OkButton.Top;
    CancelButton.Width := ScaleX(75);
    CancelButton.Height := ScaleY(23);
    CancelButton.Caption := 'Cancel';
    CancelButton.ModalResult := mrCancel;
    CancelButton.Cancel := True;

    { Dismissing the prompt (X / Cancel / Esc) means "keep my data":
      uninstall proceeds, RemoveDataChosen stays False. Only an explicit
      OK with the box checked removes data. }
    RemoveDataChosen := (Form.ShowModal() = mrOk) and Check.Checked;
    Result := True;
  finally
    Form.Free();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and RemoveDataChosen then
  begin
    DelTree(ExpandConstant('{localappdata}\WindowsCM'), True, True, True);
    DelTree(ExpandConstant('{userappdata}\WindowsCM'), True, True, True);
  end;
end;
