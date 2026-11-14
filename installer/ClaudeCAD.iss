; Inno Setup script: builds ClaudeCAD-Setup-<version>.exe from bundle\out\ClaudeCAD.bundle.
; Build on Windows:  "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=0.9.4 installer\ClaudeCAD.iss
; (CI does this in .github/workflows/build.yml.)

#ifndef AppVersion
  #define AppVersion "0.9.4"
#endif
#define BundleDir "..\bundle\out\ClaudeCAD.bundle"
; the name people see (decided 12/11/2026). The technical identity stays ClaudeCAD's (AppId, ClaudeCAD.bundle, the
; commands, %LocalAppData%\ClaudeCAD) so this edition installs over the original ClaudeCAD like an upgrade.
#define AppDisplayName "Agent to AutoCAD Full"
; one installer for AutoCAD 2022-2027: refuse to build it without all three plugin builds
#if !FileExists(AddBackslash(SourcePath) + BundleDir + "\Contents\ClaudeCAD.Plugin.dll")
  #error ClaudeCAD.bundle has no AutoCAD 2022-2024 build (Contents\ClaudeCAD.Plugin.dll)
#endif
#if !FileExists(AddBackslash(SourcePath) + BundleDir + "\Contents\2025\ClaudeCAD.Plugin.dll")
  #error ClaudeCAD.bundle has no AutoCAD 2025-2026 build (Contents\2025\ClaudeCAD.Plugin.dll)
#endif
#if !FileExists(AddBackslash(SourcePath) + BundleDir + "\Contents\2027\ClaudeCAD.Plugin.dll")
  #error ClaudeCAD.bundle has no AutoCAD 2027 build (Contents\2027\ClaudeCAD.Plugin.dll)
#endif
; activation service url + owner contact (make-bundle writes it from activation/activation.json)
#if FileExists(AddBackslash(SourcePath) + "..\bundle\out\activation.iss")
  #include "..\bundle\out\activation.iss"
#endif
#ifndef ActivationUrl
  #define ActivationUrl ""
#endif
#ifndef ActivationContact
  #define ActivationContact "Liên hệ Zalo owner: 0344644415"
#endif
; the contact text goes into Pascal string literals: double any '
#define ContactPas StringChange(ActivationContact, "'", "''")

[Setup]
; AppId: the same as the original ClaudeCAD, so Windows treats this install as its upgrade (one entry in the app list)
AppId={{8D3C1A52-6E4B-4F0A-9C27-3B5E7A1D9F40}
AppName={#AppDisplayName}
AppVerName={#AppDisplayName} {#AppVersion}
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
; TT-01 (14/11/2026): no Restart Manager (its English "unable to automatically close all applications" box with three
; buttons left people stuck). PrepareToInstall does it instead: asks in Vietnamese to close AutoCAD (never closes it:
; unsaved drawings), stops the plugin's own node.exe / claude.exe in {app}, and stops with a Vietnamese message if a
; plugin file is still in use.
CloseApplications=no
; always write %TEMP%\Setup Log yyyy-mm-dd #nnn.txt — "Gửi báo cáo lỗi" in the palette attaches it
SetupLogging=yes
UninstallDisplayName={#AppDisplayName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[InstallDelete]
; KT-38 (12/11/2026): replacing the original ClaudeCAD, its update link (to the original repository) must not stay: a
; Full build made with --update-url brings its own, one made without would otherwise update back to the original
Type: files; Name: "{app}\Contents\update-config.json"; Check: ReplacesOriginal

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
; TT-01: should a file still be in use while copying, the retry box is Vietnamese too
AbortRetryIgnoreSelectAction=Chọn cách xử lý
AbortRetryIgnoreRetry=&Thử lại (sau khi đóng AutoCAD)
AbortRetryIgnoreIgnore=&Bỏ qua lỗi và cài tiếp (không nên)
AbortRetryIgnoreCancel=Hủy cài đặt (khởi động lại máy rồi chạy lại bộ cài)
FinishedLabel={#AppDisplayName} đã được cài (lệnh mở khung chat: CLAUDECHAT).%n%nĐể dùng ngay: giữ ô "Đăng nhập Claude ngay" rồi bấm Finish — trình duyệt mở trang cấp quyền, cậu bấm đồng ý bằng tài khoản Claude Pro/Max (một cửa sổ đen hiện lên trong lúc đăng nhập và tự đóng khi xong). Sau đó mở AutoCAD 2022–2027 là dùng được.%n%nNếu bỏ qua bước này, lần đầu mở AutoCAD plugin sẽ tự mở trang đăng nhập.

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
    { R24 = 2022-2024, R25 = 2025-2026, R26 = 2027 (the releases PackageContents.xml loads in) }
    if (Copy(Rel, 1, 3) = 'R24') or (Copy(Rel, 1, 3) = 'R25') or (Copy(Rel, 1, 3) = 'R26') then
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
  ResultCode, i: Integer;
  TmpFile, DoneFile, Exe: String;
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
  DoneFile := ExpandConstant('{tmp}\authstatus.done');
  { at most ~15 s: a "claude auth status" that hangs must not freeze the finish page (then the box is shown) }
  if ExecAsOriginalUser(ExpandConstant('{cmd}'), '/C ""' + Exe + '" auth status --json > "' + TmpFile + '" 2>&1 & echo done> "' + DoneFile + '""', '', SW_HIDE, ewNoWait, ResultCode) then
  begin
    for i := 1 to 30 do
    begin
      if FileExists(DoneFile) then break;
      Sleep(500);
    end;
    if FileExists(DoneFile) and LoadStringFromFile(TmpFile, Text) then
      if Pos('"loggedIn": true', String(Text)) > 0 then Result := False;
    if not FileExists(DoneFile) then Log('sign-in check: no answer within 15 s, the box is shown');
  end;
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

{ AutoCAD releases installed on this machine, from HKLM\SOFTWARE\Autodesk\AutoCAD\R<major>.<minor>\ACAD-xxxx:yyy
  (AutoCAD and its verticals; AutoCAD LT has its own key and cannot run .NET plugins). Supported = the releases
  bundle\PackageContents.xml loads in: R24.1-R24.3 (2022-2024), R25.0-R25.1 (2025-2026), R26.0 (2027). }
function IsSupportedRelease(const Rel: String): Boolean;
begin
  Result := (Rel = 'R24.1') or (Rel = 'R24.2') or (Rel = 'R24.3') or (Rel = 'R25.0') or (Rel = 'R25.1') or (Rel = 'R26.0');
end;

{ R26.1 / R27.0 ... : newer than the last release this build declares }
function IsNewerRelease(const Rel: String): Boolean;
var
  Dot, Major, Minor: Integer;
begin
  Result := False;
  if (Length(Rel) < 4) or (Rel[1] <> 'R') then exit;
  Dot := Pos('.', Rel);
  if Dot = 0 then exit;
  Major := StrToIntDef(Copy(Rel, 2, Dot - 2), -1);
  Minor := StrToIntDef(Copy(Rel, Dot + 1, 3), -1);
  Result := (Major > 26) or ((Major = 26) and (Minor > 0));
end;

function AcadReleaseInstalled(const Rel: String): Boolean;
var
  Products: TArrayOfString;
  i: Integer;
  Loc: String;
begin
  Result := False;
  if not RegGetSubkeyNames(HKLM64, 'SOFTWARE\Autodesk\AutoCAD\' + Rel, Products) then exit;
  for i := 0 to GetArrayLength(Products) - 1 do
    if RegQueryStringValue(HKLM64, 'SOFTWARE\Autodesk\AutoCAD\' + Rel + '\' + Products[i], 'AcadLocation', Loc) and DirExists(Loc) then
    begin
      Result := True;
      exit;
    end;
end;

{ Setup continues either way (AutoCAD may be installed later); the user only learns why ClaudeCAD would not load. }
function CheckAutoCAD(): Boolean;
var
  Releases: TArrayOfString;
  i: Integer;
  Supported, Newer: String;
begin
  Result := True;
  Supported := '';
  Newer := '';
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\Autodesk\AutoCAD', Releases) then
    for i := 0 to GetArrayLength(Releases) - 1 do
      if AcadReleaseInstalled(Releases[i]) then
      begin
        if IsSupportedRelease(Releases[i]) then Supported := Supported + ' ' + Releases[i]
        else if IsNewerRelease(Releases[i]) then Newer := Newer + ' ' + Releases[i];
      end;
  Log('AutoCAD releases found: supported [' + Trim(Supported) + '] newer [' + Trim(Newer) + ']');
  if Supported <> '' then exit;
  if Newer <> '' then
    Result := SuppressibleMsgBox('Máy này có AutoCAD bản mới hơn 2027 (' + Trim(Newer) + '). {#AppDisplayName} bản này chỉ chạy trên AutoCAD 2022–2027 nên sẽ không tự nạp vào bản đó.' + #13#10#13#10 +
              'Vẫn cài tiếp?', mbConfirmation, MB_YESNO, IDYES) = IDYES
  else
    Result := SuppressibleMsgBox('Không tìm thấy AutoCAD 2022–2027 bản đầy đủ trên máy này. {#AppDisplayName} chỉ chạy trên AutoCAD bản đầy đủ (và các bản chuyên ngành như Civil 3D), không chạy trên AutoCAD LT.' + #13#10#13#10 +
              'Vẫn cài tiếp? (Cài AutoCAD sau cũng được, {#AppDisplayName} tự nạp khi mở AutoCAD.)', mbConfirmation, MB_YESNO, IDYES) = IDYES;
end;

{ ======================================================================= the Full edition over the original ClaudeCAD
  Decided 12/11/2026: this edition keeps ClaudeCAD's technical identity, so on a machine with the original ClaudeCAD
  (2D only) it installs over it like an upgrade: chats, settings and the Claude sign-in are kept; the machine asks the
  Full edition's activation service again (the old key cannot work). Same rules as EditionLogic in
  plugin\ClaudeCAD.Plugin\Core\Edition.cs (EditionTests keeps them in step):
    Contents\edition.json with "edition":"full"           -> full: a normal upgrade, no question
    no edition.json, update-config.json -> agent-to-autocad/ (the original repository) -> original
    update-config.json -> agent-to-autocad-full/           -> full (a Full build from before edition.json)
    a bundle folder without either                         -> unknown, treated like the original
  Texts are compared in lower case without spaces / line breaks. }

var
  ExistingEdition, ExistingVersion: String;

function BundlePath(): String;
begin
  Result := ExpandConstant('{userappdata}\Autodesk\ApplicationPlugins\ClaudeCAD.bundle');
end;

{ the file in lower case without spaces, tabs or line breaks ('' when missing / unreadable) }
function ReadSqueezed(const FileName: String; var Text: String): Boolean;
var
  Raw: AnsiString;
  T: String;
begin
  Text := '';
  Result := False;
  if not FileExists(FileName) then exit;
  if not LoadStringFromFile(FileName, Raw) then exit;
  T := Lowercase(String(Raw));
  StringChangeEx(T, ' ', '', True);
  StringChangeEx(T, #9, '', True);
  StringChangeEx(T, #13, '', True);
  StringChangeEx(T, #10, '', True);
  Text := T;
  Result := True;
end;

{ none / full / original / unknown }
function DetectEdition(): String;
var
  Dir, Text: String;
begin
  Dir := BundlePath();
  if not DirExists(Dir) then
  begin
    Result := 'none';
    exit;
  end;
  if ReadSqueezed(Dir + '\Contents\edition.json', Text) and (Pos('"edition":"', Text) > 0) then
  begin
    if Pos('"edition":"full"', Text) > 0 then Result := 'full' else Result := 'unknown';
    exit;
  end;
  if ReadSqueezed(Dir + '\Contents\update-config.json', Text) then
  begin
    if Pos('agent-to-autocad/', Text) > 0 then
    begin
      Result := 'original';
      exit;
    end;
    if Pos('agent-to-autocad-full/', Text) > 0 then
    begin
      Result := 'full';
      exit;
    end;
  end;
  Result := 'unknown';
end;

{ AppVersion="..." of PackageContents.xml in the bundle folder ('' when it cannot be read) }
function ExistingAppVersion(): String;
var
  Raw: AnsiString;
  Text: String;
  p: Integer;
begin
  Result := '';
  if not FileExists(BundlePath() + '\PackageContents.xml') then exit;
  if not LoadStringFromFile(BundlePath() + '\PackageContents.xml', Raw) then exit;
  Text := String(Raw);
  p := Pos('AppVersion="', Text);
  if p = 0 then exit;
  Text := Copy(Text, p + 12, 30);
  p := Pos('"', Text);
  if (p > 1) and (p <= 21) then Result := Copy(Text, 1, p - 1);
end;

function ReplacesOriginal(): Boolean;
begin
  Result := (ExistingEdition = 'original') or (ExistingEdition = 'unknown');
end;

{ The original ClaudeCAD (or a bundle that cannot be identified) is about to be replaced: say what is kept and what is
  not, and let the user stop. Silent installs (/SILENT, /VERYSILENT) do not ask: the log says what was replaced. }
function ConfirmReplaceOriginal(): Boolean;
var
  What: String;
begin
  Result := True;
  ExistingEdition := DetectEdition();
  ExistingVersion := ExistingAppVersion();
  Log('existing install: ' + ExistingEdition + ' ' + ExistingVersion + ' (' + BundlePath() + ')');
  if not ReplacesOriginal() then exit;
  if ExistingEdition = 'original' then
    What := 'ClaudeCAD (bản gốc, chỉ 2D)'
  else
    What := 'một bản ClaudeCAD chưa rõ loại (có thể là bản gốc, chỉ 2D)';
  if ExistingVersion <> '' then What := What + ', phiên bản ' + ExistingVersion;
  if WizardSilent() then
  begin
    Log('silent install: replacing ' + What + ' with {#AppDisplayName} {#AppVersion} without asking');
    exit;
  end;
  { a task dialog, so the two buttons read "Tiếp tục" / "Hủy" (decided 12/11/2026); this setup has only the English
    language, whose MsgBox buttons are OK / Cancel. Hủy (or closing the dialog) leaves Setup before anything changed. }
  Result := TaskDialogMsgBox('Máy này đang có ' + What + '.',
                             'Cài {#AppDisplayName} sẽ thay bản này. Lịch sử chat, cài đặt và đăng nhập Claude được giữ.' + #13#10 +
                             'Key cũ không dùng được: máy sẽ xin kích hoạt lại, chủ plugin duyệt và chọn gói cho máy.' + #13#10#13#10 +
                             'Bấm Tiếp tục để cài, Hủy để thoát (máy giữ nguyên bản đang có).',
                             mbConfirmation, MB_OKCANCEL, ['Tiếp tục', 'Hủy'], 0) = IDOK;
  if Result then Log('the user agreed to replace ' + What)
  else Log('the user cancelled: ' + What + ' is kept');
end;

{ An update the original ClaudeCAD downloaded (CLAUDECHATUPDATE) waits in %LocalAppData%\ClaudeCAD\updates\stage, and
  its apply-update.cmd waits for AutoCAD to close to copy it over the bundle: it would put the original back over this
  edition. KT-07 (12/11/2026): removed in PrepareToInstall, before Setup closes AutoCAD (CloseApplications), so the
  waiting script finds nothing to copy (and a deleted batch file stops). Only for a replaced original / unknown bundle. }
procedure DropOriginalStagedUpdate();
var
  Stage, Script: String;
begin
  Script := ExpandConstant('{localappdata}\ClaudeCAD\updates\apply-update.cmd');
  if FileExists(Script) then
  begin
    if DeleteFile(Script) then Log('removed the updater script of the replaced edition: ' + Script)
    else Log('could not remove the updater script ' + Script);
  end;
  Stage := ExpandConstant('{localappdata}\ClaudeCAD\updates\stage');
  if not DirExists(Stage) then exit;
  if DelTree(Stage, True, True, True) then
    Log('removed the update staged by the replaced edition: ' + Stage)
  else
    Log('could not remove the staged update ' + Stage);
end;

{ ======================================================================= TT-01 (14/11/2026): files in use
  AutoCAD keeps the plugin DLLs loaded, and the plugin's helpers (node.exe in Contents\node, claude.exe of the agent)
  keep theirs. Setup asks to close AutoCAD (never closes it), stops only the helpers inside the plugin folder, then checks the
  program files can be written; otherwise it stops with a Vietnamese message before anything changed. }

{ Running processes named Name (e.g. 'acad.exe') of this user (another user's process has no readable path) whose
  path is inside Dir ('' = anywhere). Kill: stop them (taskkill /F). Returns how many were found. }
function PluginProcesses(const Name, Dir: String; Kill: Boolean): Integer;
var
  Locator, Service, Procs, P: Variant;
  I, Code, Pid: Integer;
  Path, Prefix: String;
begin
  Result := 0;
  Prefix := Lowercase(AddBackslash(Dir));
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Procs := Service.ExecQuery('SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE Name = ''' + Name + '''');
    for I := 0 to Procs.Count - 1 do
    begin
      P := Procs.ItemIndex(I);
      if VarIsNull(P.ExecutablePath) then continue;
      Path := P.ExecutablePath;
      if (Dir <> '') and (Pos(Prefix, Lowercase(Path)) <> 1) then continue;
      Result := Result + 1;
      Pid := P.ProcessId;
      if Kill then
      begin
        Exec(ExpandConstant('{sys}\taskkill.exe'), '/PID ' + IntToStr(Pid) + ' /F', '', SW_HIDE, ewWaitUntilTerminated, Code);
        Log('stopped the plugin helper ' + Path + ' (pid ' + IntToStr(Pid) + ', taskkill ' + IntToStr(Code) + ')');
      end;
    end;
  except
    Log('could not list the running ' + Name + ': ' + GetExceptionMessage);
  end;
end;

{ Ask to close AutoCAD until it is closed (Thử lại) or the user stops (Hủy). '' = go on, else the reason to stop. }
function WaitForAutoCADClosed(): String;
begin
  Result := '';
  while PluginProcesses('acad.exe', '', False) > 0 do
  begin
    Log('AutoCAD is running: the plugin files are in use');
    if WizardSilent() then
    begin
      Result := 'AutoCAD đang mở nên không cài được. Hãy lưu bản vẽ, đóng hết AutoCAD rồi chạy lại bộ cài.';
      exit;
    end;
    if TaskDialogMsgBox('AutoCAD đang mở.',
                        'Hãy lưu bản vẽ và đóng hết AutoCAD, rồi bấm Thử lại.' + #13#10 +
                        'Bộ cài không tự tắt AutoCAD để cậu không mất bản vẽ chưa lưu.',
                        mbInformation, MB_OKCANCEL, ['Thử lại', 'Hủy'], 0) <> IDOK then
    begin
      Log('the user cancelled while AutoCAD was running');
      Result := 'Đã hủy cài: AutoCAD vẫn đang mở. Máy chưa bị thay đổi gì. Đóng AutoCAD rồi chạy lại bộ cài.';
      exit;
    end;
  end;
end;

{ The first program file of the installed bundle that cannot be opened for writing (still in use), or ''. }
function FileInUse(): String;
var
  Dirs: array of String;
  I: Integer;
  F: TFindRec;
  Ext: String;
  S: TFileStream;
begin
  Result := '';
  SetArrayLength(Dirs, 5);
  Dirs[0] := ExpandConstant('{app}\Contents');
  Dirs[1] := ExpandConstant('{app}\Contents\2025');
  Dirs[2] := ExpandConstant('{app}\Contents\2027');
  Dirs[3] := ExpandConstant('{app}\Contents\node');
  Dirs[4] := ExpandConstant('{app}\Contents\agent\node_modules\@anthropic-ai\claude-agent-sdk-win32-x64');
  for I := 0 to GetArrayLength(Dirs) - 1 do
    if FindFirst(Dirs[I] + '\*', F) then
    try
      repeat
        Ext := Lowercase(ExtractFileExt(F.Name));
        if (F.Attributes and FILE_ATTRIBUTE_DIRECTORY = 0) and ((Ext = '.dll') or (Ext = '.exe')) then
        begin
          try
            S := TFileStream.Create(Dirs[I] + '\' + F.Name, fmOpenReadWrite or fmShareExclusive);
            S.Free;
          except
            Result := Dirs[I] + '\' + F.Name;
            exit;
          end;
        end;
      until not FindNext(F);
    finally
      FindClose(F);
    end;
end;

{ KT-07: the staged update of a replaced original goes first (before anything else is checked or stopped).
  TT-01: then AutoCAD must be closed, the plugin's helpers are stopped and the files must be free. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Busy: String;
  Tries: Integer;
begin
  Result := '';
  if ReplacesOriginal() then DropOriginalStagedUpdate();
  Result := WaitForAutoCADClosed();
  if Result <> '' then exit;
  PluginProcesses('node.exe', ExpandConstant('{app}'), True);
  PluginProcesses('claude.exe', ExpandConstant('{app}'), True);
  { a stopped process can take a moment to let go of its files }
  Busy := FileInUse();
  Tries := 0;
  while (Busy <> '') and (Tries < 10) do
  begin
    Sleep(500);
    Tries := Tries + 1;
    Busy := FileInUse();
  end;
  if Busy <> '' then
  begin
    Log('a plugin file is still in use: ' + Busy);
    Result := 'Một file của plugin vẫn đang được dùng (' + ExtractFileName(Busy) + '), nên chưa cài được. ' +
              'Máy chưa bị thay đổi gì.' + #13#10#13#10 +
              'Hãy khởi động lại máy rồi chạy lại bộ cài, trước khi mở AutoCAD.';
  end;
end;

function InitializeSetup(): Boolean;
var
  Major, ErrCode: Integer;
begin
  Result := CheckAutoCAD();
  if not Result then exit;
  Result := ConfirmReplaceOriginal();
  if not Result then exit;
#ifdef BundledNode
  exit; { Node.js ships with the plugin }
#endif
  Major := NodeMajor();
  if Major < 18 then
  begin
    if MsgBox('{#AppDisplayName} needs Node.js 18 or newer, which was not found on this computer.' + #13#10 +
              'Open the Node.js download page now? (Setup continues; install Node.js before using it.)',
              mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://nodejs.org/en/download', '', '', SW_SHOWNORMAL, ewNoWait, ErrCode);
  end;
end;

{ ======================================================================= v0.9.4 activation + device list
  Every machine needs the owner's permission. The key page shows the machine code and the owner's contact, and
  emails the owner (through the owner's Google Apps Script web app) the device name, the Windows user, the
  machine code and the key for it; the machine also lands in the owner's Google Sheet as "Chờ duyệt". Two ways
  forward: the owner sets the row Active (the user installs without a key; the plugin unlocks by itself at the
  next AutoCAD start, or at once with "Kiểm tra lại"), or the owner sends the key by Zalo and the user types it here. A right key returns a signed licence
  (license.txt) plus a signed status (status.txt) in %LocalAppData%\ClaudeCAD — the plugin checks the licence at
  every AutoCAD start and re-asks the owner's Active/Disabled decision there and every ~6 hours.
  Silent installs: /KEY=XXXX-XXXX-XXXX-XXXX. }

var
  KeyPage: TWizardPage;
  CodeEdit: TNewEdit;
  KeyEdit: TNewMemo;
  KeyStatus: TNewStaticText;
  AskButton: TNewButton;
  Machine: String;
  Requested: Boolean;

function LicenseFile(): String;
begin
  Result := ExpandConstant('{localappdata}\ClaudeCAD\license.txt');
end;

{ CC-XXXX-XXXX-XXXX = first 48 bits of SHA-256("ClaudeCAD|" + MachineGuid in lower case) — same as LicenseLogic.MachineCode }
function MachineCode(): String;
var
  Guid, H: String;
begin
  if Machine <> '' then begin Result := Machine; exit; end;
  if not RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Cryptography', 'MachineGuid', Guid) then Guid := '';
  Guid := Trim(Guid);
  if Guid = '' then Guid := GetComputerNameString();
  H := Uppercase(GetSHA256OfString('ClaudeCAD|' + Lowercase(Guid)));
  Machine := 'CC-' + Copy(H, 1, 4) + '-' + Copy(H, 5, 4) + '-' + Copy(H, 9, 4);
  Result := Machine;
end;

{ licence of this machine already saved (reinstall / update): no key page. The signature is checked by the plugin. }
function HasLicense(): Boolean;
var
  Text: AnsiString;
begin
  Result := False;
  { a licence the original ClaudeCAD saved (or one of a bundle that cannot be identified) is not this edition's: the
    Full edition has its own activation service and key pair. Show the key page, which asks the owner again. }
  if ReplacesOriginal() then exit;
  { CCL1|machine|date|signature (≈ 380 characters): a truncated / empty file does not hide the key page }
  if LoadStringFromFile(LicenseFile(), Text) then
    Result := (Pos('CCL1|' + MachineCode() + '|', String(Text)) = 1) and (Length(Text) > 100);
end;

{ percent-encoding of the UTF-8 bytes (device / user names may be Vietnamese); Pascal Script has no nested routines }
function Pct(b: Integer): String;
begin
  Result := '%' + Copy('0123456789ABCDEF', (b shr 4) + 1, 1) + Copy('0123456789ABCDEF', (b and 15) + 1, 1);
end;

function UrlEncode(const S: String): String;
var
  i, c: Integer;
begin
  Result := '';
  for i := 1 to Length(S) do
  begin
    c := Ord(S[i]);
    if ((c >= Ord('A')) and (c <= Ord('Z'))) or ((c >= Ord('a')) and (c <= Ord('z'))) or ((c >= Ord('0')) and (c <= Ord('9'))) or (c = Ord('-')) or (c = Ord('_')) or (c = Ord('.')) then
      Result := Result + S[i]
    else if c < $80 then Result := Result + Pct(c)
    else if c < $800 then Result := Result + Pct($C0 or (c shr 6)) + Pct($80 or (c and $3F))
    else Result := Result + Pct($E0 or (c shr 12)) + Pct($80 or ((c shr 6) and $3F)) + Pct($80 or (c and $3F));
  end;
end;

{ GET through WinHTTP (follows Google's redirect to script.googleusercontent.com). Short timeouts: the wizard
  does not repaint while it waits. WinHTTP ignores the Windows (Internet Options) proxy unless told: use it. }
function HttpGet(const Url: String; var Body: String): Boolean;
var
  Req: Variant;
  ProxyOn: Cardinal;
  Proxy: String;
begin
  Result := False;
  Body := '';
  try
    Req := CreateOleObject('WinHttp.WinHttpRequest.5.1');
    { receive 25 s: a cold Apps Script start + its 5 s lock + sending the email can take well over 12 s }
    Req.SetTimeouts(5000, 5000, 8000, 25000);
    if RegQueryDWordValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Internet Settings', 'ProxyEnable', ProxyOn) and (ProxyOn <> 0) then
      if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Internet Settings', 'ProxyServer', Proxy) and (Trim(Proxy) <> '') then
        Req.SetProxy(2, Trim(Proxy), '<local>');
    Req.Open('GET', Url, False);
    Req.SetRequestHeader('User-Agent', 'ClaudeCAD-Setup/{#AppVersion}');
    Req.Send('');
    Body := Req.ResponseText;
    Result := Req.Status = 200;
    if not Result then Log('activation: HTTP ' + IntToStr(Req.Status));
  except
    Log('activation: request failed: ' + GetExceptionMessage);
  end;
end;

{ "name":"value" from the service's small JSON answers (values never contain quotes) }
function JsonValue(const Json, Name: String): String;
var
  p: Integer;
  Rest: String;
begin
  Result := '';
  p := Pos('"' + Name + '":"', Json);
  if p = 0 then exit;
  Rest := Copy(Json, p + Length(Name) + 4, Length(Json));
  p := Pos('"', Rest);
  if p > 0 then Result := Copy(Rest, 1, p - 1);
end;

function JsonOk(const Json: String): Boolean;
begin
  Result := Pos('"ok":true', Json) > 0;
end;

procedure SetStatus(const Text: String);
begin
  if KeyStatus <> nil then
  begin
    KeyStatus.Caption := Text;
    KeyStatus.Refresh; { the label has its own window: repaint it, not only the wizard }
  end;
  WizardForm.Refresh; { paint now: the request that follows blocks the window for a few seconds }
end;

procedure SendRequest();
var
  Body: String;
begin
  if '{#ActivationUrl}' = '' then
  begin
    SetStatus('Nhắn mã máy ở trên cho chủ plugin để nhận key.');
    exit;
  end;
  SetStatus('Đang gửi yêu cầu kích hoạt cho chủ plugin (có thể mất tới 20 giây, cửa sổ sẽ đứng yên)...');
  if not HttpGet('{#ActivationUrl}?action=request&machine=' + MachineCode() + '&device=' + UrlEncode(GetComputerNameString()) +
                 '&user=' + UrlEncode(GetUserNameString()) + '&from=' + UrlEncode('setup {#AppVersion}'), Body) then
    SetStatus('Không gửi được yêu cầu tự động (máy chưa có mạng?). Nhắn mã máy ở trên cho chủ plugin qua Zalo.')
  else if not JsonOk(Body) then
  begin
    if JsonValue(Body, 'error') <> '' then SetStatus(JsonValue(Body, 'error') + ' Nhắn mã máy ở trên cho chủ plugin qua Zalo.')
    else SetStatus('Máy chủ kích hoạt chưa sẵn sàng. Nhắn mã máy ở trên cho chủ plugin qua Zalo.');
  end
  else
  begin
    Requested := True;
    if Pos('"sent":false', Body) > 0 then
      SetStatus('Yêu cầu của máy này đã được gửi cho chủ plugin trước đó. Dán key vào ô trên, hoặc chờ duyệt rồi bấm Next cài tiếp.')
    else
      SetStatus('Đã gửi yêu cầu kích hoạt (mã máy + tên thiết bị) cho chủ plugin. Dán key vào ô trên, hoặc chờ duyệt rồi bấm Next cài tiếp.');
  end;
end;

procedure AskClick(Sender: TObject);
begin
  SendRequest();
end;

procedure CopyClick(Sender: TObject);
var
  ResultCode: Integer;
begin
  if Exec(ExpandConstant('{cmd}'), '/C echo Ma may {#AppDisplayName}: ' + MachineCode() + ' (' + GetComputerNameString() + ')| clip', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    SetStatus('Đã sao chép mã máy — dán vào Zalo gửi cho chủ plugin.');
end;

function SaveLicense(const License: String): Boolean;
begin
  ForceDirectories(ExpandConstant('{localappdata}\ClaudeCAD'));
  Result := SaveStringToFile(LicenseFile(), License, False);
end;

{ Saves a signed line as Name (status.txt / plan.txt). When the machine already has one, the line goes to
  <name>-new.txt instead: the installer cannot check signatures or dates, so the plugin applies it with the same
  "not older" rules as a pasted line (an old no-network pass must not replace a newer "disabled", KT-43) and
  deletes it. }
function SaveSignedLine(const Name, Line: String): Boolean;
var
  Dir, Target: String;
begin
  Dir := ExpandConstant('{localappdata}\ClaudeCAD');
  ForceDirectories(Dir);
  Target := Dir + '\' + Name + '.txt';
  if FileExists(Target) then Target := Dir + '\' + Name + '-new.txt';
  Result := SaveStringToFile(Target, Line, False);
end;

{ the signed device status (CCS1|...) that comes with the licence: saved so the plugin starts unlocked }
function SaveStatusLine(const Line: String): Boolean;
begin
  Result := SaveSignedLine('status', Line);
end;

{ the signed service plan (CCF1|...): saved so the plugin starts with the machine's plan }
function SavePlanLine(const Line: String): Boolean;
begin
  Result := SaveSignedLine('plan', Line);
end;

{ the first word of S (S keeps the rest); words are separated by spaces }
function TakeToken(var S: String): String;
var
  p: Integer;
begin
  S := Trim(S);
  p := Pos(' ', S);
  if p = 0 then
  begin
    Result := S;
    S := '';
  end
  else
  begin
    Result := Copy(S, 1, p - 1);
    S := Copy(S, p + 1, Length(S));
  end;
end;

{ key → signed licence (online), or an offline activation code pasted as is. Err = why not.
  KT-01 (12/11/2026): the no-network email has up to 3 lines, CCL1 (licence), CCS1 (no-network pass), CCF1 (plan), in any
  order: split on spaces / tabs / line breaks like Activation.ActivateAsync in the plugin and save each line in its own file
  (license.txt, status.txt, plan.txt). Before, everything from "CCS1|" on went to status.txt and the plugin refused it. }
function Activate(const Input: String; var Err: String): Boolean;
var
  K, Body, Lic, St, Pl, Rest, Tok: String;
begin
  Result := False;
  Err := '';
  K := Input;
  StringChangeEx(K, #13, ' ', True);
  StringChangeEx(K, #10, ' ', True);
  StringChangeEx(K, #9, ' ', True);
  K := Trim(K);
  if (Pos('CCL1|', K) > 0) or (Pos('CCS1|', K) > 0) or (Pos('CCF1|', K) > 0) then
  begin
    Lic := '';
    St := '';
    Pl := '';
    Rest := K;
    while Rest <> '' do
    begin
      Tok := TakeToken(Rest);
      if Pos('CCL1|', Tok) = 1 then Lic := Tok
      else if Pos('CCS1|', Tok) = 1 then St := Tok
      else if Pos('CCF1|', Tok) = 1 then Pl := Tok;
    end;
    if Lic = '' then begin Err := 'Thiếu dòng giấy phép CCL1| — dán đủ các dòng trong email của chủ plugin.'; exit; end;
    if Pos('CCL1|' + MachineCode() + '|', Lic) <> 1 then begin Err := 'Mã kích hoạt offline này của máy khác.'; exit; end;
    if Pos('CCS1|' + MachineCode() + '|', St) = 1 then SaveStatusLine(St)
    else if St <> '' then Log('activation: the CCS1 line is for another machine, not saved');
    if Pos('CCF1|' + MachineCode() + '|', Pl) = 1 then SavePlanLine(Pl)
    else if Pl <> '' then Log('activation: the CCF1 line is for another machine, not saved');
    Result := SaveLicense(Lic);
    if not Result then Err := 'Không lưu được giấy phép.';
    exit;
  end;
  StringChangeEx(K, ' ', '', True);
  StringChangeEx(K, '-', '', True);
  K := Uppercase(K);
  if Length(K) <> 16 then begin Err := 'Key gồm 16 ký tự dạng XXXX-XXXX-XXXX-XXXX.'; exit; end;
  if '{#ActivationUrl}' = '' then begin Err := 'Bộ cài này chưa có máy chủ kích hoạt — xin chủ plugin mã kích hoạt offline (dòng dài bắt đầu bằng CCL1|).'; exit; end;
  if not HttpGet('{#ActivationUrl}?action=activate&machine=' + MachineCode() + '&key=' + UrlEncode(K), Body) then
  begin
    Err := 'Không kết nối được máy chủ kích hoạt. Kiểm tra mạng rồi bấm Next lại.';
    exit;
  end;
  Lic := JsonValue(Body, 'license');
  if (not JsonOk(Body)) or (Pos('CCL1|' + MachineCode() + '|', Lic) <> 1) then
  begin
    Err := JsonValue(Body, 'error');
    if Err = '' then Err := 'Key không đúng với máy này.';
    exit;
  end;
  Result := SaveLicense(Lic);
  if not Result then Err := 'Không lưu được giấy phép.';
  { the service marks the machine Active with the key and signs that too }
  St := JsonValue(Body, 'status');
  if Pos('CCS1|' + MachineCode() + '|', St) = 1 then SaveStatusLine(St);
  { KT-08: and the machine's plan, so the plugin starts with it (not with the default 2D until its first status check) }
  Pl := JsonValue(Body, 'plan');
  if Pos('CCF1|' + MachineCode() + '|', Pl) = 1 then SavePlanLine(Pl);
end;

procedure InitializeWizard();
var
  Intro, CodeLabel, KeyLabel: TNewStaticText;
  CopyButton: TNewButton;
begin
  KeyPage := CreateCustomPage(wpWelcome, 'Kích hoạt {#AppDisplayName}', 'Chủ plugin cấp quyền dùng cho từng máy.');

  Intro := TNewStaticText.Create(KeyPage);
  Intro.Parent := KeyPage.Surface;
  Intro.WordWrap := True;
  Intro.Width := KeyPage.SurfaceWidth;
  if '{#ActivationUrl}' <> '' then
    Intro.Caption := '{#AppDisplayName} chỉ dùng được khi máy này được chủ plugin cho phép. Yêu cầu được gửi tự động;' + #13#10 +
                     '{#ContactPas} để được duyệt nhanh.' + #13#10 +
                     'Có key thì dán vào ô dưới. Chưa có key vẫn bấm Next cài tiếp được — sau khi được duyệt,' + #13#10 +
                     'mở AutoCAD là tự dùng được (hoặc bấm "Kiểm tra lại" trong khung chat Claude).'
  else
    Intro.Caption := '{#AppDisplayName} chỉ cài được khi có mã kích hoạt do chủ plugin cấp, riêng cho từng máy.' + #13#10 +
                     '{#ContactPas}, gửi mã máy bên dưới để nhận mã kích hoạt (dòng dài bắt đầu bằng CCL1|).';

  CodeLabel := TNewStaticText.Create(KeyPage);
  CodeLabel.Parent := KeyPage.Surface;
  CodeLabel.Top := Intro.Top + Intro.Height + ScaleY(14);
  CodeLabel.Caption := 'Mã máy (gửi mã này cho chủ plugin):';

  CodeEdit := TNewEdit.Create(KeyPage);
  CodeEdit.Parent := KeyPage.Surface;
  CodeEdit.Top := CodeLabel.Top + CodeLabel.Height + ScaleY(4);
  CodeEdit.Width := ScaleX(170);
  CodeEdit.ReadOnly := True;
  CodeEdit.Text := MachineCode();
  CodeEdit.Font.Style := [fsBold];

  CopyButton := TNewButton.Create(KeyPage);
  CopyButton.Parent := KeyPage.Surface;
  CopyButton.Left := CodeEdit.Left + CodeEdit.Width + ScaleX(8);
  CopyButton.Top := CodeEdit.Top - ScaleY(1);
  CopyButton.Width := ScaleX(90);
  CopyButton.Height := ScaleY(23);
  CopyButton.Caption := 'Sao chép';
  CopyButton.OnClick := @CopyClick;

  AskButton := TNewButton.Create(KeyPage);
  AskButton.Parent := KeyPage.Surface;
  AskButton.Left := CopyButton.Left + CopyButton.Width + ScaleX(6);
  AskButton.Top := CopyButton.Top;
  AskButton.Width := ScaleX(140);
  AskButton.Height := ScaleY(23);
  AskButton.Caption := 'Gửi lại yêu cầu';
  AskButton.OnClick := @AskClick;
  AskButton.Visible := '{#ActivationUrl}' <> ''; { nothing to send to in a build without the activation service }

  KeyLabel := TNewStaticText.Create(KeyPage);
  KeyLabel.Parent := KeyPage.Surface;
  KeyLabel.Top := CodeEdit.Top + CodeEdit.Height + ScaleY(16);
  KeyLabel.Width := KeyPage.SurfaceWidth;
  KeyLabel.WordWrap := True;
  KeyLabel.Caption := 'Key kích hoạt (XXXX-XXXX-XXXX-XXXX), hoặc mã không cần mạng (dán đủ các dòng trong email):';

  { several lines: the no-network code is 2-3 lines (CCL1 / CCS1 / CCF1); a one-line box keeps only the first (KT-01) }
  KeyEdit := TNewMemo.Create(KeyPage);
  KeyEdit.Parent := KeyPage.Surface;
  KeyEdit.Top := KeyLabel.Top + KeyLabel.Height + ScaleY(4);
  KeyEdit.Width := KeyPage.SurfaceWidth;
  KeyEdit.Height := ScaleY(46);
  KeyEdit.ScrollBars := ssVertical;
  KeyEdit.WordWrap := True;
  KeyEdit.Text := ExpandConstant('{param:KEY|}'); { /KEY= also fills the box in a normal (not silent) run }

  KeyStatus := TNewStaticText.Create(KeyPage);
  KeyStatus.Parent := KeyPage.Surface;
  KeyStatus.Top := KeyEdit.Top + KeyEdit.Height + ScaleY(12);
  KeyStatus.Width := KeyPage.SurfaceWidth;
  KeyStatus.WordWrap := True;
  KeyStatus.Caption := '';
end;

{ Silent installs never show the page (Inno would click Next with an empty box and abort): /KEY= is used in
  CurStepChanged, and without it the plugin asks for the key inside AutoCAD. }
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (KeyPage <> nil) and (PageID = KeyPage.ID) and (HasLicense() or WizardSilent());
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  { the owner gets the email as soon as the page opens (once per run; "Gửi lại yêu cầu" sends again) }
  if (KeyPage <> nil) and (CurPageID = KeyPage.ID) then
  begin
    { a key already given with /KEY=: nothing to ask the owner for }
    if (not Requested) and (Trim(KeyEdit.Text) = '') then SendRequest();
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Err: String;
begin
  Result := True;
  if (KeyPage = nil) or (CurPageID <> KeyPage.ID) then exit;
  if (Trim(KeyEdit.Text) = '') and ('{#ActivationUrl}' <> '') then
  begin
    { the sheet way: install without a key; the plugin unlocks once the owner sets the machine Active }
    Result := MsgBox('Chưa nhập key. Cài tiếp và chờ chủ plugin duyệt máy này?' + #13#10 +
                     'Sau khi được duyệt: mở AutoCAD là tự dùng được (hoặc bấm "Kiểm tra lại" trong khung chat Claude).',
                     mbConfirmation, MB_YESNO) = IDYES;
    if Result then Log('activation: continuing without a key (the owner approves via the device sheet)');
    exit;
  end;
  SetStatus('Đang kiểm tra key...');
  Result := Activate(KeyEdit.Text, Err);
  if Result then SetStatus('Đã kích hoạt máy này.')
  else
  begin
    SetStatus(Err);
    MsgBox(Err, mbError, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Err: String;
begin
  { silent installs: /KEY=... (without it the plugin asks for the key inside AutoCAD) }
  if (CurStep = ssInstall) and WizardSilent() and not HasLicense() then
    if ExpandConstant('{param:KEY|}') <> '' then
    begin
      if Activate(ExpandConstant('{param:KEY|}'), Err) then Log('activation: activated') else Log('activation: ' + Err);
    end
    else Log('activation: silent install without /KEY= — the plugin will ask for the key');
  { sign-in now happens inside AutoCAD ("Đăng nhập Claude" button) — nothing to type in a terminal }
  if CurStep = ssPostInstall then TrustBundle();
end;
