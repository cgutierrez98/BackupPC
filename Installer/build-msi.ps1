
param(
  [Parameter(Mandatory=$false)][string]$PublishDir = 'artifacts/publish',
  [Parameter(Mandatory=$false)][string]$OutputPath = 'artifacts',
  [Parameter(Mandatory=$false)][string]$ProductVersion = '0.0.0'
)

Set-StrictMode -Version Latest

function Fail($m){ Write-Error $m; exit 1 }

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Test-Path $PublishDir)) { Fail "PublishDir '$PublishDir' does not exist." }

# normalize
$pv = $ProductVersion -replace '^v',''
if (-not $pv) { Fail 'ProductVersion empty' }

$productName = 'LocalBackupMaster'
$upgradeCode = '4f5e6d7c-8b9a-0a1b-2c3d-4e5f6a7b8c9d'

# Deterministic GUID from relative path (stable across builds for MSI upgrades)
function DeterministicGuid([string]$seed) {
  $bytes = [System.Text.Encoding]::UTF8.GetBytes($upgradeCode + '/' + $seed)
  $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
  return [guid]::new(
    [BitConverter]::ToInt32($hash, 0),
    [BitConverter]::ToInt16($hash, 4),
    [BitConverter]::ToInt16($hash, 6),
    $hash[8], $hash[9], $hash[10], $hash[11],
    $hash[12], $hash[13], $hash[14], $hash[15]
  ).ToString()
}

Write-Host "Building MSI for $productName v$pv"

New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# ensure dotnet tools path
$toolsPath = Join-Path $env:USERPROFILE '.dotnet\tools'
if (Test-Path $toolsPath) { $env:PATH = $toolsPath + ';' + $env:PATH }

try {
  $wixCmd = Get-Command wix -ErrorAction SilentlyContinue
  if ($wixCmd) {
    $wixVersion = (wix --version) -join ''
    $wixExe = 'wix'
  } else {
    $wixVersion = (& dotnet wix --version) -join ''
    $wixExe = 'dotnet'
  }
} catch {
  Fail "WiX CLI not found. Install via 'dotnet tool install --global wix' or add wix to PATH"
}
Write-Host "WiX CLI: $wixVersion (invoker: $wixExe)"

$generatedComponents = Join-Path $scriptDir 'GeneratedComponents.wxs'
$templateProduct = Join-Path $scriptDir 'Product.wxs'
$productWxs = Join-Path $scriptDir 'Product.Generated.wxs'

if (-not (Test-Path $templateProduct)) { Fail "Template missing: $templateProduct" }

# collect files
$files = Get-ChildItem -Path $PublishDir -Recurse -File | Sort-Object FullName
if ($files.Count -eq 0) { Fail "No files in publish dir" }

# compute publish full path for substring operations (PowerShell 5.1 compatible)
$publishFull = (Get-Item $PublishDir).FullName

# build directory map
$dirNodes = @{}
foreach ($f in $files) {
  $rel = $f.FullName.Substring($publishFull.Length) -replace '^[\\/]+','' -replace '/','\\'
  $dir = Split-Path $rel -Parent
  if ($dir -and $dir -ne '.') {
    $parts = $dir -split '\\'
    $accum = ''
    $parent = 'INSTALLFOLDER'
    foreach ($p in $parts) {
      $accum = if ($accum) { "$accum\\$p" } else { $p }
      if (-not $dirNodes.ContainsKey($accum)) {
        $id = 'dir_' + ($accum -replace '[^A-Za-z0-9_]','_')
        $dirNodes[$accum] = @{ Id=$id; Name=$p; Parent=$parent }
      }
      $parent = $dirNodes[$accum].Id
    }
  }
}

# helper: emit directory tree (structure only, no components)
function EmitDirTree($parent) {
  $out = @()
  $children = $dirNodes.GetEnumerator() | Where-Object { $_.Value.Parent -eq $parent } | Sort-Object { $_.Key }
  foreach ($c in $children) {
    $id = $c.Value.Id; $name = $c.Value.Name
    $out += '      <Directory Id="' + $id + '" Name="' + $name + '">'
    $out += EmitDirTree $id
    $out += '      </Directory>'
  }
  return ,$out
}

# collect components BEFORE building XML
$dirContent = @{}
$compRefs = @()
$compIndex = 0
$mainExe = $null
foreach ($f in $files) {
  $rel = $f.FullName.Substring($publishFull.Length) -replace '^[\\/]+','' -replace '/','\\'
  $relDir = Split-Path $rel -Parent
  $compIndex++
  $compId = "cmp_$compIndex"
  $guid = DeterministicGuid $rel
  $fileId = "fil_$compIndex"
  $src = $f.FullName
  $compDef = @()
  $compDef += '        <Component Id="' + $compId + '" Guid="' + $guid + '">'
  $compDef += '          <File Id="' + $fileId + '" Source="' + $src + '" KeyPath="yes" />'
  $compDef += '        </Component>'
  $targetDirId = 'INSTALLFOLDER'
  if ($relDir -and $relDir -ne '.') {
    $norm = $relDir -replace '/','\\'
    if ($dirNodes.ContainsKey($norm)) { $targetDirId = $dirNodes[$norm].Id }
    else { Write-Warning "Directory mapping not found for '$relDir' -- attaching to INSTALLFOLDER" }
  }
  if (-not $dirContent.ContainsKey($targetDirId)) { $dirContent[$targetDirId] = @() }
  $dirContent[$targetDirId] += $compDef
  $compRefs += '      <ComponentRef Id="' + $compId + '" />'
  if (-not $mainExe -and $f.Extension -eq '.exe') { $mainExe = @{ Rel=$rel; FileId=$fileId } }
}

if ($mainExe) {
  $exeName = Split-Path $mainExe.Rel -Leaf
  $scId = 'cmp_Shortcuts'
  $scGuid = DeterministicGuid '__shortcuts__'
  $scDef = @()
  $scDef += '        <Component Id="' + $scId + '" Guid="' + $scGuid + '">'
  $scDef += '          <Shortcut Id="DesktopShortcut" Directory="DesktopFolder" Name="' + $productName + '" WorkingDirectory="INSTALLFOLDER" Target="[INSTALLFOLDER]' + $exeName + '" />'
  $scDef += '          <Shortcut Id="StartMenuShortcut" Directory="ProgramMenuFolder" Name="' + $productName + '" WorkingDirectory="INSTALLFOLDER" Target="[INSTALLFOLDER]' + $exeName + '" />'
  $scDef += '          <RemoveFolder Id="RemoveProgramMenu" Directory="ProgramMenuFolder" On="uninstall" />'
  $scDef += '          <RegistryValue Root="HKCU" Key="Software\' + $productName + '" Name="installed" Type="integer" Value="1" KeyPath="yes" />'
  $scDef += '        </Component>'
  if (-not $dirContent.ContainsKey('INSTALLFOLDER')) { $dirContent['INSTALLFOLDER'] = @() }
  $dirContent['INSTALLFOLDER'] += $scDef
  $compRefs += '      <ComponentRef Id="' + $scId + '" />'
}

# build XML fragment
$xmlLines = @()
$xmlLines += '<Fragment>'

# 1) directory tree
$xmlLines += '    <DirectoryRef Id="INSTALLFOLDER">'
$xmlLines += (EmitDirTree 'INSTALLFOLDER')
$xmlLines += '    </DirectoryRef>'

# 2) components per directory via DirectoryRef
foreach ($entry in $dirContent.GetEnumerator()) {
  $xmlLines += '    <DirectoryRef Id="' + $entry.Key + '">'
  $xmlLines += $entry.Value
  $xmlLines += '    </DirectoryRef>'
}

# 3) component group
$xmlLines += '    <ComponentGroup Id="ProductComponents">'
$xmlLines += $compRefs
$xmlLines += '    </ComponentGroup>'
$xmlLines += '</Fragment>'

# write GeneratedComponents.wxs
$xmlLines | Out-File -FilePath $generatedComponents -Encoding utf8
Write-Host "Wrote: $generatedComponents"

# Prepare product file: inline generated fragment directly into product (avoid include complexity)
$templ = Get-Content -Raw -Path $templateProduct
$templ = $templ -replace '__PRODUCT_VERSION__', $pv
$templ = $templ -replace '__UPGRADE_CODE__', $upgradeCode
# remove any include directive
$templ = $templ -replace '<\?include GeneratedComponents.wxs\?>',''
# insert our fragment immediately before the <Package ...> element (WiX v4/v5)
$frag = ($xmlLines -join "`n").Trim()
$templ = [regex]::Replace($templ, '(<Package\b)', { param($m) return $frag + "`n" + $m.Groups[1].Value })
$templ | Out-File -FilePath $productWxs -Encoding utf8
Write-Host "Wrote: $productWxs"

# build MSI
$msiName = "$productName-$pv-win-x64.msi"
$msiPath = Join-Path $OutputPath $msiName

if ($wixExe -eq 'wix') {
  Write-Host "Invoking: wix build $productWxs -o $msiPath"
  $proc = Start-Process -FilePath 'wix' -ArgumentList @('build', $productWxs, '-o', $msiPath) -NoNewWindow -Wait -PassThru
} else {
  Write-Host "Invoking: dotnet wix build $productWxs -o $msiPath -d:SourceDir=$PublishDir"
  $proc = Start-Process -FilePath 'dotnet' -ArgumentList @('wix','build', $productWxs, '-o', $msiPath, ("-d:SourceDir=$PublishDir")) -NoNewWindow -Wait -PassThru
}
if ($proc.ExitCode -ne 0) { Fail "wix build failed: $($proc.ExitCode)" }

Write-Host "MSI created: $msiPath"

