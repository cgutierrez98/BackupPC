# Roadmap de Mejoras Locales — Local Backup Master

> **Restricción:** Ninguna fase usa red, cloud ni servicios externos.  
> **Priorización:** Coste bajo = rápido de hacer · Alto impacto = valor inmediato para el usuario.

---

## Estado Actual (Línea Base)

| Área | Estado |
|---|---|
| Motor MVVM + Estrategia Incremental (XXHash64) | ✅ Completo |
| ParallelBackupEngine (productor-consumidor + progreso split) | ✅ Completo |
| Resiliencia IO (Polly retry) | ✅ Completo |
| DB SQLite con EF Core + IDbContextFactory | ✅ Completo |
| Reportes JSON exportables | ✅ Completo |
| UI/UX: tokens de color, animaciones de entrada, hover, ProgressCard reactiva | ✅ Completo |

---

## Fase A — Fiabilidad del Motor (Coste Bajo · Impacto Alto)

### A1 · Modo Dry-Run (Simulacro)
**Qué:** Ejecutar el ciclo completo de escaneo sin copiar ningún archivo. Muestra exactamente qué archivos se copiarían, cuántos GB, estimación de tiempo.  
**Por qué:** El usuario puede revisar antes de confirmar una operación destructiva en destino.  
**Impacto en código:**
- `IBackupStrategy` ya existe. Añadir flag `DryRun` al `BackupDestination` o como parámetro de `ExecuteAsync`.
- El engine salta `TryCopyWithRetryAsync` pero cuenta bytes, igual que hoy.
- `BackupReport` añade `bool WasDryRun`.
- UI: checkbox "Simular antes de copiar" en la ConfigCard.

---

### A2 · Límite de Velocidad de Copia (Throttle)
**Qué:** Parámetro opcional `MaxBytesPerSecond` que regula el canal de copia para no saturar el disco durante uso normal.  
**Por qué:** Backups intensivos bloquean el disco para el resto de aplicaciones.  
**Impacto en código:**
- En el loop de copia del `ParallelBackupEngine`, tras cada `File.Copy`, calcular bytes/s y aplicar `await Task.Delay()` si supera el umbral.
- `BackupDestination` añade `int? ThrottleKBps`.
- UI: Slider secundario en la ConfigCard (0 = sin límite).

---

### A3 · Filtrado por Fecha de Modificación
**Qué:** Sólo copiar archivos modificados después de una fecha configurable ("backup desde el último lunes").  
**Por qué:** Reduce drásticamente el tiempo de escaneo en destinos grandes.  
**Impacto en código:**
- `IBackupStrategy.ShouldCopyAsync` ya recibe `FileInfo`. Añadir un `DateTimeOffset? SinceDate` al contexto de filtro (igual que `includeExtensions`).
- UI: DatePicker opcional en ConfigCard.

---

### A4 · Retry Feedback Visible
**Qué:** Cuando Polly reintenta un archivo, emitir un log de consola con cuenta de intento ("Reintento 2/3: svchost.db").  
**Por qué:** Actualmente los reintentos son silenciosos; el usuario no sabe por qué tarda.  
**Impacto en código:**
- `BackupScannerService.TryCopyWithRetryAsync` ya usa Polly. Añadir `onRetry` callback que llame a un `IProgress<string>` opcional.
- `ParallelBackupEngine` pasa el callback, que a su vez llama a `progress.Report`.

---

## Fase B — Seguridad Local (Coste Medio · Impacto Alto)

### B1 · Cifrado AES-256 Opt-In por Destino
**Qué:** Si el usuario activa el cifrado en un destino, los archivos copiados se cifran en el disco de destino con AES-256-CBC. La clave se guarda con `SecureStorage.Default`.  
**Por qué:** Protección contra robo físico del disco de backup.  
**Impacto en código:**
- Nueva `IEncryptionService` + `AesEncryptionService` (wrap de `System.Security.Cryptography.Aes`).
- `BackupDestination` añade `bool IsEncrypted` + `string? KeyHint`.
- `ParallelBackupEngine` en la fase de copia: si `dest.IsEncrypted`, cifrar stream antes de write.
- `FileRecord` añade `bool IsEncrypted` para saber al restaurar.
- Migración EF Core necesaria (nueva propiedad en tabla).
- UI: Toggle en la card de cada destino.

> ⚠️ **No** almacenar la clave en texto plano. Usar `SecureStorage` o `DPAPI` (`ProtectedData.Protect`).

---

### B2 · Verificación de Integridad en Destino (Bit-Rot Detection)
**Qué:** Comando manual "Verificar integridad" que relée todos los archivos del destino, calcula su XXHash64 y lo compara con el valor guardado en `FileRecord.FileHash`.  
**Por qué:** Los discos pueden corromper silenciosamente bits con el tiempo.  
**Impacto en código:**
- Nuevo `IIntegrityCheckService` con método `VerifyDestinationAsync(BackupDestination, IProgress<...>, CancellationToken)`.
- Devuelve lista de `CorruptedFile { RelativePath, StoredHash, ActualHash }`.
- UI: Botón secundario en la card de cada destino. Resultado en un dialog o nueva sub-página.
- `FileRecord` no cambia: el hash ya está guardado.

---

## Fase C — Gestión Avanzada (Coste Medio · Impacto Medio)

### C1 · Perfiles de Backup (Presets)
**Qué:** Guardar y recuperar una configuración completa (orígenes, destinos, extensiones, hilos, throttle) como un perfil con nombre.  
**Por qué:** El usuario hace distintos tipos de backup (fotos semanales, documentos diarios) con configuraciones distintas.  
**Impacto en código:**
- Nuevo model `BackupProfile { Id, Name, SourceIds[], DestinationIds[], IncludeExtensions, ParallelDegree, ThrottleKBps }`.
- `IDatabaseService` añade CRUD de perfiles.
- `MainViewModel` añade `ObservableCollection<BackupProfile>` + `LoadProfileCommand` + `SaveProfileCommand`.
- UI: ComboBox de "Cargar perfil" + botón "Guardar como perfil" en ConfigCard.

---

### C2 · Versionado de Archivos (Snapshots N)
**Qué:** Al copiar un archivo que ya existe en destino, no sobreescribirlo sino moverlo a una carpeta de versiones `.bk_versions/archivo_20260130_143022.ext` y guardar la versión nueva.  
**Por qué:** Permite recuperar el archivo de hace 3 días aunque se haya modificado.  
**Impacto en código:**
- `BackupDestination` añade `bool VersioningEnabled` + `int MaxVersions`.
- En `ParallelBackupEngine`, antes de copy: si versionado activo y destino existe, `File.Move` a `.bk_versions/`.
- Nuevo `IVersionCleanupService` que mantiene el límite `MaxVersions` borrando versiones antiguas.
- `FileRecord` añade `int VersionCount`.
- UI: Toggle + stepper de "Máximo de versiones" por destino.

---

### C3 · Deduplicación por Hash
**Qué:** Si dos archivos de origen tienen el mismo `FileHash` (mismo contenido aunque distinto nombre), sólo copiar uno y crear un hardlink o archivo de índice para el segundo.  
**Por qué:** Ahorra espacio en disco en destinos con carpetas duplicadas.  
**Impacto en código:**
- `IDatabaseService` añade índice por `FileHash` en `FileRecord`.
- En `ParallelBackupEngine`, antes de copy: consultar si `FileHash` ya existe en otro `FileRecord` del mismo destino.
- Si existe, crear un archivo `.dedup_ref` con la ruta del original en lugar de copiar de nuevo.
- `SmartRestore` (Fase E) debe entender los `.dedup_ref`.

---

### C4 · Confirmación al Eliminar Origen/Destino (Undo)
**Qué:** Al pulsar ✕ sobre un origen o destino, mostrar un diálogo de confirmación con opción de "Deshacer" durante 5 segundos (snackbar animado).  
**Por qué:** Los botones ✕ son pequeños y fáciles de pulsar por error.  
**Impacto en código:**
- `MainViewModel.RemoveSourceCommand` y `RemoveDestinationCommand`: antes de confirmar el remove, emitir evento `UndoRequested`.
- `MainPage.xaml.cs` muestra un `Snackbar` de CommunityToolkit.Maui con callback de "Deshacer".
- El item se guarda en una variable temporal durante 5 s y se re-agrega si el usuario confirma el undo.

---

## Fase D — Experiencia de Usuario Avanzada (Coste Bajo-Medio)

### D1 · Backup Programado (Scheduler Local)
**Qué:** El usuario configura "ejecutar cada X horas" o "ejecutar al conectar este disco". La app lanza el backup automáticamente si está en primer plano.  
**Por qué:** Elimina la necesidad de recordar hacer el backup manualmente.  
**Impacto en código:**
- Usar `System.Timers.Timer` o `PeriodicTimer` en `MainViewModel`.
- `BackupProfile` (C1) añade `TimeSpan? ScheduleInterval`.
- Al detectar el evento de `DeviceWatcherService.DeviceConnected`, verificar si el UUID del disco coincide con algún destino y lanzar backup automáticamente (ya existe el watcher).
- UI: Toggle "Auto-backup al conectar" por destino + selector de intervalo.

---

### D2 · Historial de Backups en la UI
**Qué:** Pestaña o panel que muestra los últimos N reportes con fecha, resultado y bytes copiados, sin salir de la app.  
**Por qué:** Hoy los reportes se pierden al cerrar la sesión o hay que buscar el JSON exportado.  
**Impacto en código:**
- `AppDbContext` añade tabla `BackupReportSummary { Id, Date, TotalCopied, TotalFailed, DurationSecs, WasDryRun }`.
- `ParallelBackupEngine` persiste el resumen al final de cada ejecución.
- Nueva `HistoryPage.xaml` / `HistoryViewModel` con `CollectionView` de reportes.
- Botón "Ver historial" en `MainPage` (AppShell).

---

### D3 · Smart Restore (Explorador de Backup)
**Qué:** Interfaz para explorar el contenido del destino de backup como si fuera un árbol de archivos, seleccionar archivos individualmente y restaurarlos a una carpeta de destino.  
**Por qué:** La restauración actual es manual (copiar desde el destino).  
**Impacto en código:**
- Nueva `RestorePage.xaml` con `TreeView` (o `CollectionView` anidado con `Expander`).
- `IRestoreService` con `GetBackupTreeAsync(BackupDestination)` y `RestoreFilesAsync(IEnumerable<string> relPaths, string outputDir)`.
- Usa `FileRecord` de DB para construir el árbol sin leer disco.
- Compatible con B1 (si cifrado activo, descifrar al restaurar) y C2 (selector de versión).

---

## Fase E — Integración con el SO Local (Coste Alto)

### E1 · VSS (Volume Shadow Copy)
**Qué:** Copiar archivos que están bloqueados por el sistema (NTFS locks) usando el servicio VSS de Windows para hacer una snapshot consistente.  
**Por qué:** Hoy los archivos como `Outlook.pst` o bases de datos SQLite en uso no se pueden copiar.  
**Impacto en código:**
- P/Invoke o nueva librería (ej. `AlphaVSS` — local, sin red).
- `BackupScannerService.TryCopyWithRetryAsync` como fallback si el copy normal devuelve `UnauthorizedAccess` / `SharingViolation`.
- Requiere elevación de privilegios (Admin).

---

### E2 · Context Menu del Explorador
**Qué:** Click derecho en cualquier carpeta → "Añadir a Local Backup Master".  
**Por qué:** Flujo de incorporación de orígenes mucho más rápido que el FolderPicker.  
**Impacto en código:**
- Registro en `HKCU\Software\Classes\Directory\shell\` vía instalador WiX (ya existe `Installer/`).
- La app detecta args de línea de comandos `--add-source "C:\ruta"` al arrancar.
- `MainViewModel.AddSourceFromArgAsync(string path)`.

---

### E3 · Windows Service Daemon (Auto-Start Silencioso)
**Qué:** Instalar un servicio de Windows ligero que ejecuta los backups programados (D1) sin necesidad de que la ventana esté abierta.  
**Por qué:** Un backup real no depende de que el usuario tenga la app abierta.  
**Impacto en código:**
- Nuevo proyecto `LocalBackupMaster.Service` (Worker Service, `dotnet new worker`).
- Comparte `Services/` y `Models/` como proyectos referenciados.
- El servicio usa `PeriodicTimer` + la misma `ParallelBackupEngine`.
- WiX instala el servicio con `<ServiceInstall>`.

---

## Resumen de Priorización

| ID | Mejora | Coste | Impacto | Dependencias |
|---|---|---|---|---|
| A1 | Dry-Run | Bajo | Alto | — |
| C4 | Confirmación/Undo al borrar | Bajo | Alto | — |
| A4 | Retry feedback visible | Bajo | Medio | — |
| A3 | Filtrado por fecha | Bajo | Medio | — |
| B2 | Bit-Rot Detection | Medio | Alto | — |
| C1 | Perfiles de backup | Medio | Alto | — |
| D2 | Historial de backups en UI | Medio | Alto | — |
| A2 | Throttle de copia | Bajo | Medio | — |
| D1 | Scheduler local | Medio | Alto | C1 |
| B1 | Cifrado AES-256 | Medio | Alto | — |
| C2 | Versionado (Snapshots) | Medio | Alto | — |
| D3 | Smart Restore | Alto | Alto | C2, B1 |
| C3 | Deduplicación | Medio | Medio | — |
| E2 | Context Menu | Medio | Medio | — |
| E1 | VSS | Alto | Medio | — |
| E3 | Windows Service Daemon | Alto | Alto | D1 |

---

*Generado: 2026-03-30 · Sin red, sin cloud, sin webhooks.*
