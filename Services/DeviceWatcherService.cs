using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LocalBackupMaster.Services;

public class DeviceEventArgs : EventArgs
{
    public required string DriveName { get; set; }
    public required string DrivePath { get; set; }
    public string? VolumeLabel { get; set; }
}

public class DeviceWatcherService : IDisposable
{
    public event EventHandler<DeviceEventArgs>? DeviceConnected;
    public event EventHandler<DeviceEventArgs>? DeviceDisconnected;

    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(3);
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly object _drivesLock = new();
    private HashSet<string> _knownDrives = new();

    public void StartWatching()
    {
        if (_cancellationTokenSource != null)
            return;

        _cancellationTokenSource = new CancellationTokenSource();
        lock (_drivesLock)
        {
            _knownDrives = GetReadyDrives().Select(d => d.Name).ToHashSet();
        }

        Task.Run(() => WatchLoop(_cancellationTokenSource.Token));
    }

    public void StopWatching()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    public void Dispose() => StopWatching();

    private async Task WatchLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var currentDrives = GetReadyDrives();
                var currentDriveNames = currentDrives.Select(d => d.Name).ToHashSet();

                List<DriveInfo> newDrives;
                List<string> removedDriveNames;

                lock (_drivesLock)
                {
                    newDrives = currentDrives.Where(d => !_knownDrives.Contains(d.Name)).ToList();
                    removedDriveNames = _knownDrives.Except(currentDriveNames).ToList();

                    foreach (var drive in newDrives)
                        _knownDrives.Add(drive.Name);

                    foreach (var removedName in removedDriveNames)
                        _knownDrives.Remove(removedName);
                }

                // Detección de nuevos discos (fuera del lock para evitar deadlock en handlers)
                foreach (var drive in newDrives)
                {
                    string label = string.Empty;
                    try { label = drive.VolumeLabel; } catch { }

                    DeviceConnected?.Invoke(this, new DeviceEventArgs
                    {
                        DriveName = drive.Name,
                        DrivePath = drive.RootDirectory.FullName,
                        VolumeLabel = label
                    });
                }

                // Detección de discos desconectados
                foreach (var removedName in removedDriveNames)
                {
                    DeviceDisconnected?.Invoke(this, new DeviceEventArgs
                    {
                        DriveName = removedName,
                        DrivePath = removedName // as fallback
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en DeviceWatcherService: {ex.Message}");
            }

            await Task.Delay(_pollingInterval, token);
        }
    }

    private List<DriveInfo> GetReadyDrives()
    {
        try
        {
            // Filtramos unidades que estén listas y sean Removibles o Fijas (Los discos externos a veces se detectan como Fixed)
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && (d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed))
                .ToList();
        }
        catch
        {
            return new List<DriveInfo>();
        }
    }
}
