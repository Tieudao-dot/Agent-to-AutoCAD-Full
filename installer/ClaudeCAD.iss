; Inno Setup script: builds ClaudeCAD-Setup-<version>.exe from bundle\out\ClaudeCAD.bundle.
; Build on Windows:  "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=0.9.4 installer\ClaudeCAD.iss
; (CI does this in .github/workflows/build.yml.)

#ifndef AppVersion
  #define AppVersion "0.9.4"
#endif
#define BundleDir "..\bundle\out\ClaudeCAD.bundle"
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

{ ======================================================================= v0.9.4 activation
  Every machine needs a key from the owner. The key page shows the machine code and the owner's contact, and
  emails the owner (through the owner's Google Apps Script web app) the device name, the Windows user, the
  machine code and the key for it. Only the owner sees the key; the user types what the owner sends back. A
  right key returns a signed licence, saved in %LocalAppData%\ClaudeCAD\license.txt — the plugin checks its
  signature at every AutoCAD start (copying the folder / installing from the .zip does not skip it).
  Silent installs: /KEY=XXXX-XXXX-XXXX-XXXX. }

var
  KeyPage: TWizardPage;
  KeyEdit, CodeEdit: TNewEdit;
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
  if LoadStringFromFile(LicenseFile(), Text) then
    Result := Pos('CCL1|' + MachineCode() + '|', String(Text)) = 1;
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

{ GET through WinHTTP (follows Google's redirect to script.googleusercontent.com) }
function HttpGet(const Url: String; var Body: String): Boolean;
var
  Req: Variant;
begin
  Result := False;
  Body := '';
  try
    Req := CreateOleObject('WinHttp.WinHttpRequest.5.1');
    Req.SetTimeouts(10000, 10000, 15000, 25000);
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
  if KeyStatus <> nil then KeyStatus.Caption := Text;
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
  SetStatus('Đang gửi yêu cầu kích hoạt cho chủ plugin...');
  if HttpGet('{#ActivationUrl}?action=request&machine=' + MachineCode() + '&device=' + UrlEncode(GetComputerNameString()) +
             '&user=' + UrlEncode(GetUserNameString()) + '&from=' + UrlEncode('setup {#AppVersion}'), Body) and JsonOk(Body) then
  begin
    Requested := True;
    SetStatus('Đã gửi yêu cầu kích hoạt (mã máy + tên thiết bị) cho chủ plugin. Nhận key qua Zalo rồi dán vào ô trên.');
  end
  else
    SetStatus('Không gửi được yêu cầu tự động (máy chưa có mạng?). Nhắn mã máy ở trên cho chủ plugin qua Zalo.');
end;

procedure AskClick(Sender: TObject);
begin
  SendRequest();
end;

procedure CopyClick(Sender: TObject);
var
  ResultCode: Integer;
begin
  if Exec(ExpandConstant('{cmd}'), '/C echo Ma may ClaudeCAD: ' + MachineCode() + ' (' + GetComputerNameString() + ')| clip', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    SetStatus('Đã sao chép mã máy — dán vào Zalo gửi cho chủ plugin.');
end;

function SaveLicense(const License: String): Boolean;
begin
  ForceDirectories(ExpandConstant('{localappdata}\ClaudeCAD'));
  Result := SaveStringToFile(LicenseFile(), License, False);
end;

{ key → signed licence (online), or an offline activation code pasted as is. Err = why not. }
function Activate(const Input: String; var Err: String): Boolean;
var
  K, Body, Lic: String;
begin
  Result := False;
  Err := '';
  K := Trim(Input);
  if Pos('CCL1|', K) = 1 then
  begin
    if Pos('CCL1|' + MachineCode() + '|', K) <> 1 then begin Err := 'Mã kích hoạt offline này của máy khác.'; exit; end;
    Result := SaveLicense(K);
    if not Result then Err := 'Không lưu được giấy phép.';
    exit;
  end;
  StringChangeEx(K, ' ', '', True);
  StringChangeEx(K, '-', '', True);
  K := Uppercase(K);
  if Length(K) <> 16 then begin Err := 'Key gồm 16 ký tự dạng XXXX-XXXX-XXXX-XXXX.'; exit; end;
  if '{#ActivationUrl}' = '' then begin Err := 'Bộ cài này chưa có máy chủ kích hoạt — xin chủ plugin mã kích hoạt offline (dòng dài bắt đầu bằng CCL1|).'; exit; end;
  if not HttpGet('{#ActivationUrl}?action=activate&machine=' + MachineCode() + '&key=' + K, Body) then
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
end;

procedure InitializeWizard();
var
  Intro, CodeLabel, KeyLabel: TNewStaticText;
  CopyButton: TNewButton;
begin
  KeyPage := CreateCustomPage(wpWelcome, 'Kích hoạt ClaudeCAD', 'Cần key kích hoạt cho máy này để cài đặt.');

  Intro := TNewStaticText.Create(KeyPage);
  Intro.Parent := KeyPage.Surface;
  Intro.WordWrap := True;
  Intro.Width := KeyPage.SurfaceWidth;
  Intro.Caption := 'ClaudeCAD chỉ cài được khi có key do chủ plugin cấp, riêng cho từng máy.' + #13#10 +
                   '{#ActivationContact} để nhận key.';

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

  KeyLabel := TNewStaticText.Create(KeyPage);
  KeyLabel.Parent := KeyPage.Surface;
  KeyLabel.Top := CodeEdit.Top + CodeEdit.Height + ScaleY(16);
  KeyLabel.Caption := 'Key kích hoạt (XXXX-XXXX-XXXX-XXXX):';

  KeyEdit := TNewEdit.Create(KeyPage);
  KeyEdit.Parent := KeyPage.Surface;
  KeyEdit.Top := KeyLabel.Top + KeyLabel.Height + ScaleY(4);
  KeyEdit.Width := KeyPage.SurfaceWidth;

  KeyStatus := TNewStaticText.Create(KeyPage);
  KeyStatus.Parent := KeyPage.Surface;
  KeyStatus.Top := KeyEdit.Top + KeyEdit.Height + ScaleY(12);
  KeyStatus.Width := KeyPage.SurfaceWidth;
  KeyStatus.WordWrap := True;
  KeyStatus.Caption := '';
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (KeyPage <> nil) and (PageID = KeyPage.ID) and HasLicense();
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  { the owner gets the email as soon as the page opens (once per run; "Gửi lại yêu cầu" sends again) }
  if (KeyPage <> nil) and (CurPageID = KeyPage.ID) then
  begin
    if not Requested then SendRequest();
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Err: String;
begin
  Result := True;
  if (KeyPage = nil) or (CurPageID <> KeyPage.ID) then exit;
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
