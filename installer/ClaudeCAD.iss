; Inno Setup script: builds ClaudeCAD-Setup-<version>.exe from bundle\out\ClaudeCAD.bundle.
; Build on Windows:  "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=0.9.2 installer\ClaudeCAD.iss
; (CI does this in .github/workflows/build.yml.)

#ifndef AppVersion
  #define AppVersion "0.9.2"
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

[Messages]
FinishedLabel=ClaudeCAD đã được cài. Mở AutoCAD 2022–2025: khung chat sẽ tự mở với nút "Đăng nhập Claude" (đăng nhập một lần trên trình duyệt). Sau đó cứ gõ yêu cầu bằng tiếng Việt.

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
