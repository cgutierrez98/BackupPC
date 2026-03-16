param(
    [string]$ProjectPath = "..\LocalBackupMaster.csproj",
    [string]$Framework = "net9.0-windows10.0.19041.0",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win10-x64",
    [string]$PublishDir,
    [string]$OutputPath,
    [string]$ProductVersion
)

$ErrorActionPreference = "Stop"

function Get-NormalizedProductVersion {
    param(
        [string]$ProjectFile,
        [string]$RequestedVersion
    )

    if ($RequestedVersion) {
        $version = $RequestedVersion.TrimStart("v")
    }
    else {
        [xml]$projectXml = Get-Content -Path $ProjectFile
        $version = $projectXml.Project.PropertyGroup.ApplicationDisplayVersion | Select-Object -First 1
        if (-not $version) {
            throw "No se pudo determinar ApplicationDisplayVersion desde $ProjectFile."
        }
    }

    $parts = $version.Split(".", [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($parts.Count -lt 2 -or $parts.Count -gt 3) {
        throw "La versión '$version' no es válida para MSI. Usa formato major.minor o major.minor.patch."
    }

    if ($parts.Count -eq 2) {
        $parts += "0"
    }

    foreach ($part in $parts) {
        if ($part -notmatch '^\d+$') {
            throw "La versión '$version' contiene segmentos no numéricos."
        }
    }

    return ($parts -join ".")
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$resolvedProjectPath = Resolve-Path (Join-Path $scriptRoot $ProjectPath)
$projectName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedProjectPath)

if (-not $PublishDir) {
    $PublishDir = Join-Path $repoRoot "artifacts\publish"
}
if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot "artifacts\LocalBackupMaster.msi"
}

$PublishDir = [System.IO.Path]::GetFullPath($PublishDir)
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $OutputPath
$intermediateDirectory = Join-Path $repoRoot "artifacts\wix"
$resolvedProductVersion = Get-NormalizedProductVersion -ProjectFile $resolvedProjectPath -RequestedVersion $ProductVersion
$mainExecutable = Join-Path $PublishDir "$projectName.exe"

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $intermediateDirectory | Out-Null

if (-not (Test-Path $mainExecutable)) {
    Write-Host "Publicando aplicación Windows en $PublishDir" -ForegroundColor Yellow
    dotnet publish $resolvedProjectPath `
        -f $Framework `
        -c $Configuration `
        -p:RuntimeIdentifier=$RuntimeIdentifier `
        -p:WindowsPackageType=None `
        -p:SelfContained=true `
        -p:PublishSingleFile=false `
        --output $PublishDir

    if ($LASTEXITCODE -ne 0) {
        throw "Falló la publicación de la aplicación."
    }
}

if (-not (Test-Path $mainExecutable)) {
    throw "No se encontró $mainExecutable. La carpeta publish no es válida para construir el MSI."
}

Write-Host "Construyendo MSI $OutputPath con versión $resolvedProductVersion" -ForegroundColor Yellow
wix build (Join-Path $scriptRoot "Package.wxs") `
    -arch x64 `
    -bindpath "PublishDir=$PublishDir" `
    -define "ProductVersion=$resolvedProductVersion" `
    -intermediateFolder $intermediateDirectory `
    -o $OutputPath

if ($LASTEXITCODE -ne 0) {
    throw "Falló la compilación del MSI."
}

Write-Host "MSI generado en $OutputPath" -ForegroundColor Green
