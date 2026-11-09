; Inno Setup script: builds ClaudeCAD-Setup-<version>.exe from bundle\out\ClaudeCAD.bundle.
; Build on Windows:  "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=0.9.3 installer\ClaudeCAD.iss
; (CI does this in .github/workflows/build.yml.)

#ifndef AppVersion
  #define AppVersion "0.9.3"
#endif
#define BundleDir "..\bundle\out\ClaudeCAD.bundle"

[Setup]
AppId={{8D3C1A52-6E4B-4F0A-9C27-3B5E7A1D9F40}
AppName=ClaudeCAD
AppVersion={#AppVersion}
AppPublisher=ClaudeCAD
DefaultDirName={userappdata}\Autodesk\ApplicationPlugins\ClaudeCAD.bundle
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\bundle\out
; fixed name: https://github.com/<owner>/<repo>/releases/latest/download/ClaudeCAD-Setup.exe always points to the newest release
OutputBaseFilename=ClaudeCAD-Setup
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
; always write %TEMP%\Setup Log yyyy-mm-dd #nnn.txt — "Gửi báo cáo lỗi" in the palette attaches it
SetupLogging=yes
UninstallDisplayName=ClaudeCAD for AutoCAD

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; program files are replaced on upgrade
Source: "{#BundleDir}\*"; DestDir: "{app}"; Excludes: "Contents\skills\*"; Flags: recursesubdirs createallsubdirs ignoreversion
; skills: never overwrite files the user may have edited
Source: "{#BundleDir}\Contents\skills\*"; DestDir: "{app}\Contents\skills"; Flags: recursesubdirs createallsubdirs onlyifdoesntexist uninsneveruninstall

#define ClaudeExe "{app}\Contents\agent\node_modules\@anthropic-ai\claude-agent-sdk-win32-x64\claude.exe"

[Run]
; Sign in now (no AutoCAD needed): the Claude Code bundled with the plugin opens the browser; the user only
; confirms. runasoriginaluser: the credentials must land in the profile of the person using the PC, not of an
; admin account typed into UAC. skipifsilent: /VERYSILENT installs never open a browser. Hidden when signed in.
Filename: "{#ClaudeExe}"; Parameters: "auth login --claudeai"; Description: "Đăng nhập Claude ngay (trình duyệt mở ra, bấm đồng ý một lần — không cần mở AutoCAD)"; Flags: postinstall runasoriginaluser skipifsilent nowait; Check: NeedsLogin

[Messages]
FinishedLabel=ClaudeCAD đã được cài.%n%nĐể dùng ngay: giữ ô "Đăng nhập Claude ngay" rồi bấm Finish — trình duyệt mở trang cấp quyền, cậu bấm đồng ý bằng tài khoản Claude Pro/Max (một cửa sổ đen hiện lên trong lúc đăng nhập và tự đóng khi xong). Sau đó mở AutoCAD 2022–2025 là dùng được.%n%nNếu bỏ qua bước này, lần đầu mở AutoCAD plugin sẽ tự mở trang đăng nhập.

[Code]
#if FileExists(AddBackslash(SourcePath) + BundleDir + "\Contents\node\node.exe")
  #define BundledNode
#endif

{ AutoCAD stops asking "load this plugin?" when its folder is in TRUSTEDPATHS (per release / product / profile). }
procedure AddTrustedPath(const Key, Entry: String);
var
  Cur: String;
begin
  if not RegKeyExists(HKCU, Key) then exit;
  if not RegQueryStringValue(HKCU, Key, 'TRUSTEDPATHS', Cur) then Cur := '';
  if Pos(Lowercase(Entry), Lowercase(Cur)) > 0 then exit;
  if Cur = '' then Cur := Entry
  else
  begin
    if Cur[Length(Cur)] <> ';' then Cur := Cur + ';';
    Cur := Cur + Entry;
  end;
  RegWriteStringValue(HKCU, Key, 'TRUSTEDPATHS', Cur);
end;

procedure TrustBundle();
var
  Releases, Products, Profiles: TArrayOfString;
  i, j, k: Integer;
  Base, Rel, Entry: String;
begin
  Entry := ExpandConstant('{app}\Contents\...');
  Base := 'Software\Autodesk\AutoCAD';
  if not RegGetSubkeyNames(HKCU, Base, Releases) then exit;
  for i := 0 to GetArrayLength(Releases) - 1 do
  begin
    Rel := Releases[i];
    if (Copy(Rel, 1, 3) = 'R24') or (Copy(Rel, 1, 3) = 'R25') then
      if RegGetSubkeyNames(HKCU, Base + '\' + Rel, Products) then
        for j := 0 to GetArrayLength(Products) - 1 do
          if RegGetSubkeyNames(HKCU, Base + '\' + Rel + '\' + Products[j] + '\Profiles', Profiles) then
            for k := 0 to GetArrayLength(Profiles) - 1 do
              AddTrustedPath(Base + '\' + Rel + '\' + Products[j] + '\Profiles\' + Profiles[k] + '\Variables', Entry);
  end;
end;

{ True when this Windows user is not signed in to Claude yet (no API key, "claude auth status" says loggedIn false). }
var
  LoginChecked, LoginNeeded: Boolean;

function NeedsLogin(): Boolean;
var
  ResultCode: Integer;
  TmpFile, Exe: String;
  Text: AnsiString;
begin
  { evaluated several times while the finish page is shown: run "claude auth status" only once }
  if LoginChecked then begin Result := LoginNeeded; exit; end;
  LoginChecked := True;
  LoginNeeded := False;
  Result := False;
  if GetEnv('ANTHROPIC_API_KEY') <> '' then exit;
  Exe := ExpandConstant('{#ClaudeExe}');
  if not FileExists(Exe) then exit;
  Result := True;
  TmpFile := ExpandConstant('{tmp}\authstatus.json');
  if ExecAsOriginalUser(ExpandConstant('{cmd}'), '/C ""' + Exe + '" auth status --json > "' + TmpFile + '" 2>&1"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    if LoadStringFromFile(TmpFile, Text) then
      if Pos('"loggedIn": true', String(Text)) > 0 then Result := False;
  LoginNeeded := Result;
end;

function NodeMajor(): Integer;
var
  ResultCode: Integer;
  TmpFile, Line: String;
  Lines: TArrayOfString;
begin
  Result := -1;
  TmpFile := ExpandConstant('{tmp}\nodever.txt');
  if Exec(ExpandConstant('{cmd}'), '/C node --version > "' + TmpFile + '" 2>&1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
    if LoadStringsFromFile(TmpFile, Lines) and (GetArrayLength(Lines) > 0) then
    begin
      Line := Trim(Lines[0]);
      if (Length(Line) > 1) and (Line[1] = 'v') then
        Result := StrToIntDef(Copy(Line, 2, Pos('.', Line) - 2), -1);
    end;
end;

function InitializeSetup(): Boolean;
var
  Major, ErrCode: Integer;
begin
  Result := True;
#ifdef BundledNode
  exit; { Node.js ships with the plugin }
#endif
  Major := NodeMajor();
  if Major < 18 then
  begin
    if MsgBox('ClaudeCAD needs Node.js 18 or newer, which was not found on this computer.' + #13#10 +
              'Open the Node.js download page now? (Setup continues; install Node.js before using ClaudeCAD.)',
              mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://nodejs.org/en/download', '', '', SW_SHOWNORMAL, ewNoWait, ErrCode);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { sign-in now happens inside AutoCAD ("Đăng nhập Claude" button) — nothing to type in a terminal }
  if CurStep = ssPostInstall then TrustBundle();
end;
