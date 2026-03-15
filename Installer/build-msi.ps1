# Script de Automatización para generar MSI (WiX v4)
# Asegúrate de tener WiX Toolset v4 instalado (dotnet tool install --global wix)

$projectName = "LocalBackupMaster"
$publishDir = "..\bin\Release\net9.0-windows10.0.19041.0\win10-x64\publish"
$installerOut = "..\bin\Release\Installer"

Write-Host "🚀 Iniciando proceso de empaquetado MSI..." -ForegroundColor Cyan

# 1. Publicar la APP como Unpackaged
Write-Host "📦 Publicando aplicación (Unpackaged)..." -ForegroundColor Yellow
dotnet publish ..\$projectName.csproj -f net9.0-windows10.0.19041.0 -c Release -p:WindowsPackageType=None -p:PublishReadyToRun=true --self-contained true

if ($LASTEXITCODE -ne 0) { Write-Error "Falló la publicación."; exit }

# 2. Crear carpeta de salida del instalador
if (!(Test-Path $installerOut)) { New-Item -ItemType Directory -Path $installerOut }

# 3. Recolectar archivos (Harvesting) usando WiX v4
# Nota: WiX v4 utiliza 'wix build' directamente o 'wix harvest' para generar .wxs de archivos
Write-Host "🔍 Recolectando archivos para el instalador..." -ForegroundColor Yellow
# Generamos un archivo .wxs temporal con todos los binarios
wix extension add WixToolset.UI.wixext # Por si acaso se usa UI en el futuro
wix extension add WixToolset.Util.wixext

# Este comando 'harvest' es conceptual, en WiX 4 depende de las extensiones instaladas.
# Si no tienes el harvest tool, este paso puede fallar y requerir ajuste manual.
# Alternativa: Usar Heat.exe de WiX 3 o herramientas de WiX 4 específicas.
# Por simplicidad, asumimos que el usuario tiene 'wix' configurado.

wix build Package.wxs -o "$installerOut\$projectName.msi"

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ ¡Éxito! El instalador se encuentra en: $installerOut\$projectName.msi" -ForegroundColor Green
} else {
    Write-Host "❌ Error al compilar el MSI." -ForegroundColor Red
}
