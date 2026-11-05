<#
.SYNOPSIS
  Cài / gỡ ClaudeCAD cho AutoCAD 2022-2025 (bundle autoloader), có kiểm tra Node.js và đăng nhập Claude.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File installer\install.ps1
  powershell -ExecutionPolicy Bypass -File installer\install.ps1 -AllUsers
  powershell -ExecutionPolicy Bypass -File installer\install.ps1 -Uninstall

.NOTES
  - Bundle nguồn: thư mục ClaudeCAD.bundle cạnh script (khi giải nén từ file zip phát hành)
    hoặc bundle\out\ClaudeCAD.bundle (khi chạy trong repo sau `node bundle/make-bundle.mjs`).
  - Khi cài đè bản cũ, skill trong Contents\skills mà cậu đã sửa được GIỮ NGUYÊN; chỉ skill/tệp mới được thêm.
#>
[CmdletBinding()]
param(
  [string]$BundlePath,
  [switch]$AllUsers,
  [switch]$Uninstall,
  [switch]$SkipNodeCheck,
  [switch]$InstallNode,
  [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$MinNodeMajor = 18

function Say([string]$msg, [string]$color = 'Gray') { if (-not $Quiet) { Write-Host $msg -ForegroundColor $color } }
function Ask([string]$question) {
  if ($Quiet) { return $false }
  $a = Read-Host "$question [y/N]"
  return $a -match '^(y|yes|c|co|có)$'
}

$root = if ($AllUsers) { Join-Path $env:ProgramData 'Autodesk\ApplicationPlugins' } else { Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins' }
$target = Join-Path $root 'ClaudeCAD.bundle'

# ------------------------------------------------------------------ AutoCAD must be closed
$acad = Get-Process -Name acad -ErrorAction SilentlyContinue
if ($acad) {
  Say 'AutoCAD đang chạy. Hãy đóng AutoCAD rồi chạy lại script (file plugin đang bị khóa).' Yellow
  exit 2
}

# ------------------------------------------------------------------ uninstall
if ($Uninstall) {
  if (Test-Path $target) {
    Remove-Item -Recurse -Force $target
    Say "Đã gỡ ClaudeCAD khỏi $target" Green
  } else {
    Say "Không thấy ClaudeCAD tại $target" Yellow
  }
  exit 0
}

# ------------------------------------------------------------------ locate the bundle
if (-not $BundlePath) {
  $candidates = @(
    (Join-Path $PSScriptRoot 'ClaudeCAD.bundle'),
    (Join-Path $PSScriptRoot '..\ClaudeCAD.bundle'),
    (Join-Path $PSScriptRoot '..\bundle\out\ClaudeCAD.bundle')
  )
  $BundlePath = $candidates | Where-Object { Test-Path (Join-Path $_ 'PackageContents.xml') } | Select-Object -First 1
}
if (-not $BundlePath -or -not (Test-Path (Join-Path $BundlePath 'PackageContents.xml'))) {
  Say 'Không tìm thấy ClaudeCAD.bundle. Giải nén file zip phát hành cạnh script này, hoặc chạy `node bundle/make-bundle.mjs` trước, hoặc truyền -BundlePath.' Red
  exit 1
}
$BundlePath = (Resolve-Path $BundlePath).Path

# ------------------------------------------------------------------ Node.js check
function Get-NodeMajor {
  $node = Get-Command node -ErrorAction SilentlyContinue
  if (-not $node) {
    foreach ($p in @("$env:ProgramFiles\nodejs\node.exe", "$env:LOCALAPPDATA\Programs\nodejs\node.exe")) { if (Test-Path $p) { $node = $p; break } }
  } else { $node = $node.Source }
  if (-not $node) { return $null }
  $v = & $node --version 2>$null
  if ($v -match '^v(\d+)\.') { return [int]$Matches[1] }
  return $null
}

if (-not $SkipNodeCheck) {
  $major = Get-NodeMajor
  if ($null -eq $major -or $major -lt $MinNodeMajor) {
    if ($null -eq $major) { Say "Chưa có Node.js (cần >= $MinNodeMajor)." Yellow } else { Say "Node.js v$major quá cũ (cần >= $MinNodeMajor)." Yellow }
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget -and ($InstallNode -or (Ask 'Cài Node.js LTS bằng winget ngay bây giờ?'))) {
      & winget install --exact --id OpenJS.NodeJS.LTS --accept-source-agreements --accept-package-agreements
      $env:PATH = [Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('PATH', 'User')
      $major = Get-NodeMajor
    }
    if ($null -eq $major -or $major -lt $MinNodeMajor) {
      Say 'Hãy cài Node.js LTS từ https://nodejs.org rồi chạy lại script (hoặc dùng -SkipNodeCheck và đặt CLAUDECAD_NODE).' Red
      Start-Process 'https://nodejs.org/en/download' -ErrorAction SilentlyContinue
      exit 3
    }
  }
  Say "Node.js v$major: OK" Green
}

# ------------------------------------------------------------------ copy the bundle
New-Item -ItemType Directory -Force -Path $root | Out-Null
$srcSkills = Join-Path $BundlePath 'Contents\skills'
$dstSkills = Join-Path $target 'Contents\skills'
Say "Cài ClaudeCAD vào $target ..."
# program files: mirror everything except skills
& robocopy $BundlePath $target /E /PURGE /XD $srcSkills $dstSkills /R:3 /W:2 /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { Say "Copy thất bại (robocopy $LASTEXITCODE)." Red; exit 4 }
# skills: add only files that do not exist yet (keep the user's edits)
& robocopy $srcSkills $dstSkills /E /XC /XN /XO /R:2 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { Say "Copy skill thất bại (robocopy $LASTEXITCODE)." Red; exit 4 }

# ------------------------------------------------------------------ Claude credentials
$cred = Join-Path $env:USERPROFILE '.claude\.credentials.json'
if ($env:ANTHROPIC_API_KEY) {
  Say 'Đăng nhập Claude: dùng ANTHROPIC_API_KEY.' Green
} elseif (Test-Path $cred) {
  Say 'Đăng nhập Claude: đã có (claude login).' Green
} else {
  Say 'Chưa thấy đăng nhập Claude. Mở terminal và chạy:' Yellow
  Say '    npm install -g @anthropic-ai/claude-code' Yellow
  Say '    claude login' Yellow
  Say '  (gói Pro/Max) — hoặc đặt biến môi trường ANTHROPIC_API_KEY.' Yellow
}

Say ''
Say 'Xong. Mở AutoCAD 2022-2025, gõ CLAUDECHAT để mở khung chat.' Green
Say "Gỡ cài đặt: powershell -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Uninstall$(if ($AllUsers) {' -AllUsers'})"
exit 0
