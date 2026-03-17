param(
    [string]$PublishDir = "artifacts/publish",
    [string]$OutputPath = "artifacts",
    [string]$ProductVersion = "0.0.0"
)

Set-StrictMode -Version Latest

function Write-ErrAndExit($msg){ Write-Error $msg; exit 1 }

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Test-Path $PublishDir)) { Write-ErrAndExit "PublishDir '$PublishDir' does not exist." }

# Normalize version (strip leading 'v' if present)
$pv = $ProductVersion -replace '^v',''
if (-not $pv) { Write-ErrAndExit "ProductVersion is empty after normalization." }

$productName = 'LocalBackupMaster'
$upgradeCode = 'E1B3C8D0-5F1A-4D2E-9A3B-7C9D8E6F5A1B'  # Fixed UpgradeCode (change once per product)

Write-Host "Product: $productName Version: $pv"

# Ensure output path exists
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Ensure wix CLI is available
try {
    $wixVersion = (wix --version) -join ""
} catch {
    Write-ErrAndExit "WiX CLI 'wix' not found in PATH. Install via 'dotnet tool install --global wix' or add to PATH."
}

Write-Host "Using WiX CLI: $wixVersion"

# Paths for generated WiX fragments
$generatedComponents = Join-Path $scriptDir 'GeneratedComponents.wxs'
$templateProduct = Join-Path $scriptDir 'Product.wxs'
$productWxs = Join-Path $scriptDir 'Product.Generated.wxs'

if (-not (Test-Path $templateProduct)) { Write-ErrAndExit "Template Product.wxs not found at $templateProduct" }

# Build list of files
$files = Get-ChildItem -Path $PublishDir -Recurse -File | Sort-Object FullName
if ($files.Count -eq 0) { Write-ErrAndExit "No files found in PublishDir: $PublishDir" }

# Create directory structure map (relative to publish dir)
$dirMap = @{}
function Get-DirId($relPath){
    $key = ($relPath -replace '[^A-Za-z0-9_]','_')
    return 'dir_' + ($key)
}

# Build XML for directories and components
[System.Text.StringBuilder]$dirXml = New-Object System.Text.StringBuilder
[System.Text.StringBuilder]$compXml = New-Object System.Text.StringBuilder

# Root DirectoryRef will be INSTALLFOLDER, we'll emit nested Directory elements for each unique dir
$allDirs = $files | ForEach-Object { Split-Path -Path ($_.FullName.Substring($PublishDir.Length).TrimStart('\','/')) -Parent } | Sort-Object -Unique

$dirNodes = @{}
foreach ($d in $allDirs) {
    if ($d -eq '.') { continue }
    $parts = $d -split '[\\/]'
    $parent = 'INSTALLFOLDER'
    $accum = ''
    foreach ($p in $parts) {
        $accum = if ($accum) { "$accum\\$p" } else { $p }
        if (-not $dirNodes.ContainsKey($accum)) {
            $id = Get-DirId $accum
            $dirNodes[$accum] = @{ Id = $id; Name = $p; Parent = $parent }
            $parent = $id
        } else {
            $parent = $dirNodes[$accum].Id
        }
    }
}

# Emit Directory tree under INSTALLFOLDER — simple flat emission (WiX requires nesting, we'll assemble by parent links)
function Emit-DirectoryRecursive($parentId) {
    $children = $dirNodes.GetEnumerator() | Where-Object { $_.Value.Parent -eq $parentId }
    foreach ($c in $children) {
        $id = $c.Value.Id; $name = $c.Value.Name
        $null = $dirXml.AppendLine("      <Directory Id=\"$id\" Name=\"$name\">")
        Emit-DirectoryRecursive $id
        $null = $dirXml.AppendLine("      </Directory>")
    }
}

# Start DirectoryRef
$null = $dirXml.AppendLine("    <DirectoryRef Id=\"INSTALLFOLDER\">")
# Emit nested directories
Emit-DirectoryRecursive 'INSTALLFOLDER'

# For each file, emit a component under the proper Directory (if file in root, attach to INSTALLFOLDER)
$compIndex = 0
$mainExeInfo = $null
foreach ($f in $files) {
  $rel = $f.FullName.Substring($PublishDir.Length).TrimStart('\\','/')
  $relDir = Split-Path $rel -Parent
  $targetDirId = 'INSTALLFOLDER'
  if ($relDir -and $relDir -ne '.') {
    $targetDirId = $dirNodes[$relDir].Id
  }
  $compIndex++
  $compId = "cmp_$compIndex"
  $guid = [guid]::NewGuid().ToString()
  $fileId = "fil_$compIndex"
  $source = '$(var.SourceDir)\' + ($rel -replace '/','\\')
  $null = $compXml.AppendLine("      <Component Id=\"$compId\" Guid=\"$guid\">")
  $null = $compXml.AppendLine("        <File Id=\"$fileId\" Source=\"$source\" KeyPath=\"yes\" />")
  $null = $compXml.AppendLine("      </Component>")
  # Track main EXE for shortcuts (first EXE found)
  if (-not $mainExeInfo -and $f.Extension -eq '.exe') {
    $mainExeInfo = @{ Rel = $rel; FileId = $fileId }
  }
  # Attach component to directory
  $null = $dirXml.AppendLine("      <!-- Component for $rel -->")
  $null = $dirXml.AppendLine("      <ComponentRef Id=\"$compId\" />")
}

# If we found an EXE, create a shortcut component (desktop + start menu)
if ($mainExeInfo) {
  $exeName = Split-Path $mainExeInfo.Rel -Leaf
  $scCompId = "cmp_Shortcuts"
  $scGuid = [guid]::NewGuid().ToString()
  $scRegGuid = [guid]::NewGuid().ToString()
  $null = $compXml.AppendLine("      <Component Id=\"$scCompId\" Guid=\"$scGuid\">")
  $null = $compXml.AppendLine("        <Shortcut Id=\"DesktopShortcut\" Directory=\"DesktopFolder\" Name=\"$productName\" WorkingDirectory=\"INSTALLFOLDER\" Target=\"[INSTALLFOLDER]$exeName\" />")
  $null = $compXml.AppendLine("        <Shortcut Id=\"StartMenuShortcut\" Directory=\"ProgramMenuFolder\" Name=\"$productName\" WorkingDirectory=\"INSTALLFOLDER\" Target=\"[INSTALLFOLDER]$exeName\" />")
  $null = $compXml.AppendLine("        <RemoveFolder Id=\"RemoveProgramMenu\" Directory=\"ProgramMenuFolder\" On=\"uninstall\" />")
  $null = $compXml.AppendLine("        <RegistryValue Root=\"HKCU\" Key=\"Software\$productName\" Name=\"installed\" Type=\"integer\" Value=\"1\" KeyPath=\"yes\" />")
  $null = $compXml.AppendLine("      </Component>")
  $null = $dirXml.AppendLine("      <!-- Shortcut component -->")
  $null = $dirXml.AppendLine("      <ComponentRef Id=\"$scCompId\" />")
}

$null = $dirXml.AppendLine("    </DirectoryRef>")

# Assemble GeneratedComponents.wxs
$gen = @"
<?xml version="1.0" encoding="utf-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
$($dirXml.ToString())
    <ComponentGroup Id="ProductComponents">
$($compXml.ToString())
    </ComponentGroup>
  </Fragment>
</Wix>
"@

Set-Content -Path $generatedComponents -Value $gen -Encoding UTF8
Write-Host "Generated components at: $generatedComponents"

# Prepare Product.Generated.wxs by replacing placeholders
$templ = Get-Content -Raw -Path $templateProduct
$templ = $templ -replace '__PRODUCT_VERSION__', $pv
$templ = $templ -replace '__PRODUCT_NAME__', $productName
$templ = $templ -replace '__UPGRADE_CODE__', $upgradeCode

Set-Content -Path $productWxs -Value $templ -Encoding UTF8
Write-Host "Prepared Product WXS: $productWxs"

# Build MSI using wix CLI
$msiName = "$productName-$pv-win-x64.msi"
$msiPath = Join-Path $OutputPath $msiName

Write-Host "Running: wix build $productWxs -o $msiPath -dSourceDir=$PublishDir"
$proc = Start-Process -FilePath wix -ArgumentList @('build', $productWxs, '-o', $msiPath, "-dSourceDir=$PublishDir") -NoNewWindow -Wait -PassThru
if ($proc.ExitCode -ne 0) { Write-ErrAndExit "wix build failed with exit code $($proc.ExitCode)" }

Write-Host "MSI generated at: $msiPath"

Write-Host "Done. Uploaded artifacts expected in $OutputPath"
param (
    [string]$PublishDir,
    [string]$OutputPath,
    [string]$ProductVersion
)

Write-Host "Building MSI version $ProductVersion"

$wxs = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package
    Name="LocalBackupMaster"
    Manufacturer="TuEmpresa"
    Version="$ProductVersion"
    UpgradeCode="PUT-GUID-HERE">

    <MediaTemplate />

    <Feature Id="MainFeature" Title="Main Feature" Level="1">
      <ComponentGroupRef Id="AppFiles" />
    </Feature>

  </Package>

  <Fragment>
    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFilesFolder">
        <Directory Id="INSTALLFOLDER" Name="LocalBackupMaster" />
      </Directory>
    </Directory>
  </Fragment>

  <Fragment>
    <ComponentGroup Id="AppFiles" Directory="INSTALLFOLDER">
"@

Get-ChildItem -Path $PublishDir -Recurse -File | ForEach-Object {
    $fileId = $_.Name.Replace(".", "_")
    $wxs += @"
      <Component Id="$fileId" Guid="*">
        <File Source="$($_.FullName)" />
      </Component>
"@
}

$wxs += @"
    </ComponentGroup>
  </Fragment>
</Wix>
"@

$wxsPath = "$PSScriptRoot\installer.wxs"
$wxs | Out-File $wxsPath -Encoding utf8

wix build $wxsPath -o $OutputPath

Write-Host "MSI creado en $OutputPath"