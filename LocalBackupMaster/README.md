# Local Backup Master

Aplicación de escritorio multiplataforma (Windows/macOS) desarrollada en .NET MAUI para realizar copias de seguridad de carpetas locales hacia unidades externas USB/HDD/SSD.

## 🚀 Requisitos Previos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) instalado.
- Visual Studio 2022 (con soporte para desarrollo MAUI) o Visual Studio Code con las extensiones de MAUI y C# Dev Kit instaladas.
- Windows 10 (1809 o superior) o Windows 11.

## 🛠️ Instrucciones de Compilación y Ejecución (Windows)

Para compilar y ejecutar el proyecto desde la línea de comandos en Windows, puedes seguir estos pasos:

### 1. Restaurar las dependencias del proyecto
Asegúrate de estar en la carpeta donde reside el archivo `.csproj` (`/LocalBackupMaster`).
```bash
dotnet restore
```

### 2. Compilar y Ejecutar en Windows

En .NET MAUI para Windows es necesario especificar el framework objetivo (Target Framework) al ejecutar el comando. El framework para Windows es `net9.0-windows10.0.19041.0`.

Ejecuta el siguiente comando para compilar e iniciar la aplicación inmediatamente:
```bash
dotnet build -t:Run -f net9.0-windows10.0.19041.0
```

También puedes usar simplemente `dotnet run` especificando el framework de Windows de esta forma:
```bash
dotnet run -f net9.0-windows10.0.19041.0
```

> **Nota:** El comando clásico `dotnet start` no existe, se utiliza `dotnet run` junto con el parámetro `-f` para decirle a MAUI bajo qué sistema operativo quieres arrancar la aplicación.

### 🐛 Entornos de Desarrollo
- Si usas **Visual Studio 2022**, puedes seleccionar "Windows Machine" en el menú desplegable del botón de "Depurar (Debug)" y simplemente pulsar el botón de Play (F5).
- Si usas **VS Code**, en el depurador elige "C#: LocalBackupMaster" y selecciona el entorno "Windows".

## 📚 Estructura Principal del Proyecto

- `MainPage.xaml`: Vista principal con los controles de selección de origen, destino y visor de progreso.
- `Services/AppDbContext.cs`: Configuración de la base de datos local SQLite (`Microsoft.EntityFrameworkCore.Sqlite`).
- `Services/DatabaseService.cs`: Lógica para realizar consultas e inserciones locales de forma asincrónica.
- `Models/`: Contiene `BackupSource`, `BackupDestination` y `FileRecord` listos para ser usados.

---
*Este proyecto sigue la filosofía "Local First". Privacidad total, sin dependencia de la nube, velocidad extrema.*
