<#
.SYNOPSIS
  Cài / gỡ ClaudeCAD cho AutoCAD 2022-2027 (bundle autoloader), có kiểm tra Node.js và đăng nhập Claude.

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

# ------------------------------------------------------------------ which AutoCAD is installed (warning only)
# Same releases as bundle\PackageContents.xml: R24.1-R24.3 (2022-2024), R25.0-R25.1 (2025-2026), R26.0 (2027).
# AutoCAD LT has its own key (AutoCAD LT) and cannot run .NET plugins.
if (-not $Uninstall) {
  $supported = 'R24.1', 'R24.2', 'R24.3', 'R25.0', 'R25.1', 'R26.0'
  $found = @(Get-ChildItem 'HKLM:\SOFTWARE\Autodesk\AutoCAD' -ErrorAction SilentlyContinue | Where-Object {
    $rel = $_
    @(Get-ChildItem $rel.PSPath -ErrorAction SilentlyContinue | Where-Object {
      $loc = (Get-ItemProperty -Path $_.PSPath -Name AcadLocation -ErrorAction SilentlyContinue).AcadLocation
      $loc -and (Test-Path $loc)
    }).Count -gt 0
  } | ForEach-Object { $_.PSChildName })
  $ok = @($found | Where-Object { $supported -contains $_ })
  $newer = @($found | Where-Object { $_ -match '^R(\d+)\.(\d+)$' -and ([int]$Matches[1] -gt 26 -or ([int]$Matches[1] -eq 26 -and [int]$Matches[2] -gt 0)) })
  if ($ok.Count -eq 0) {
    if ($newer.Count -gt 0) {
      Say "Máy này có AutoCAD bản mới hơn 2027 ($($newer -join ', ')): ClaudeCAD bản này chỉ chạy trên AutoCAD 2022-2027 nên sẽ không tự nạp vào bản đó." Yellow
    } else {
      Say 'Không tìm thấy AutoCAD 2022-2027 bản đầy đủ (AutoCAD LT không chạy được plugin). Vẫn cài tiếp; cài AutoCAD sau thì ClaudeCAD tự nạp khi mở AutoCAD.' Yellow
    }
  }
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

$bundledNode = Join-Path $BundlePath 'Contents\node\node.exe'
if (Test-Path $bundledNode) { $SkipNodeCheck = $true; Say 'Node.js: đi kèm plugin.' Green }
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
  Say "Node.js v${major}: OK" Green
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

# ------------------------------------------------------------------ AutoCAD trusted folder (no "load this plugin?" prompt)
$entry = Join-Path $target 'Contents\...'
$base = 'HKCU:\Software\Autodesk\AutoCAD'
if (Test-Path $base) {
  # R24 = 2022-2024, R25 = 2025-2026, R26 = 2027
  Get-ChildItem $base -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '^R2[456]' } | ForEach-Object {
    Get-ChildItem $_.PSPath -ErrorAction SilentlyContinue | ForEach-Object {
      $profiles = Join-Path $_.PSPath 'Profiles'
      if (Test-Path $profiles) {
        Get-ChildItem $profiles -ErrorAction SilentlyContinue | ForEach-Object {
          $vars = Join-Path $_.PSPath 'Variables'
          if (Test-Path $vars) {
            $cur = (Get-ItemProperty -Path $vars -Name TRUSTEDPATHS -ErrorAction SilentlyContinue).TRUSTEDPATHS
            if (-not $cur) { $cur = '' }
            if ($cur.ToLower().IndexOf($entry.ToLower()) -lt 0) {
              $new = if ($cur) { $cur.TrimEnd(';') + ';' + $entry } else { $entry }
              Set-ItemProperty -Path $vars -Name TRUSTEDPATHS -Value $new
            }
          }
        }
      }
    }
  }
  Say 'Đã thêm thư mục plugin vào TRUSTEDPATHS của AutoCAD.' Green
}

Say ''
Say 'Xong. Mở AutoCAD 2022-2027: khung chat tự mở lần đầu, bấm "Đăng nhập Claude" (đăng nhập một lần trên trình duyệt).' Green
Say "Gỡ cài đặt: powershell -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Uninstall$(if ($AllUsers) {' -AllUsers'})"
exit 0
