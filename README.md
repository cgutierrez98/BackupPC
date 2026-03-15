# 🛡️ Local Backup Master

**Local Backup Master** es una aplicación de escritorio multiplataforma (Windows/macOS) diseñada para realizar copias de seguridad locales incrementales hacia unidades externas (USB/HDD/SSD).  

Sigue la filosofía **"Local First"**: Privacidad total, sin dependencia de la nube, velocidad extrema mediante procesamiento paralelo y deduplicación inteligente por Hash.

---

## ✨ Características Principales

- **🔄 Backup Incremental Inteligente**: Compara archivos por metadatos (fecha/tamaño) y realiza validación profunda mediante **XXHash64** (el algoritmo de hashing no criptográfico más rápido del mundo).
- **⚡ Procesamiento Paralelo**: Utiliza el patrón **Producer-Consumer** con canales asíncronos para maximizar la velocidad de transferencia, permitiendo configurar el número de hilos.
- **🏗️ Arquitectura Profesional**: Implementada bajo el patrón **MVVM** puro, con desacoplamiento total mediante interfaces y servicios especializados.
- **📼 Historial y Catálogo**: Registro detallado en base de datos local **SQLite** para evitar copias redundantes.
- **🔌 Detección Automática**: Monitoriza la conexión de dispositivos extraíbles para sugerir destinos de backup al instante.
- **🎨 UI Moderna**: Interfaz adaptativa (Dark/Light mode) con animaciones fluidas y feedback visual de progreso en tiempo real.

---

## 🛠️ Stack Tecnológico

- **Framework**: .NET MAUI (.NET 9)
- **Lenguaje**: C# 12
- **Persistencia**: Entity Framework Core con SQLite
- **Patrones**: MVVM, Strategy, Orchestrator, Repository, Dependency Injection
- **Toolkit**: CommunityToolkit.Mvvm & CommunityToolkit.Maui

---

## 🚀 Requisitos e Instalación

### Requisitos Previos
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows 10 (1809+) o Windows 11 / macOS Monterey+

### Compilación y Ejecución (Windows)
Desde la terminal en el directorio raíz del proyecto:

```bash
# Restaurar dependencias
dotnet restore

# Compilar y ejecutar
dotnet run -f net9.0-windows10.0.19041.0
dotnet build LocalBackupMaster.csproj -f net9.0-windows10.0.19041.0
```

---

## 📚 Estructura de la Solución

El proyecto ha sido refactorizado recientemente para seguir las mejores prácticas de ingeniería de software:

- **`/ViewModels`**: Contiene la lógica de estado y comandos de la interfaz (`MainViewModel`).
- **`/Services/BackupEngine`**: Orquestador de copias paralelas (`IBackupEngine`).
- **`/Services/Strategies`**: Algoritmos de decisión de backup (`IBackupStrategy`).
- **`/Services/Navigation & Validation`**: Servicios desacoplados de infraestructura.
- **`/Models`**: Entidades de datos y reportes de progreso.

---

## 📝 Notas de Versión
**v1.01**: Refactorización completa a MVVM y patrones de diseño industriales.

---
*Privacidad total. Velocidad extrema. Control absoluto sobre tus datos.*
