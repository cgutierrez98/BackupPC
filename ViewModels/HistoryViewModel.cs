using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalBackupMaster.Models;
using LocalBackupMaster.Services;

namespace LocalBackupMaster.ViewModels;

/// <summary>D2 — ViewModel para la página de historial de backups.</summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IDatabaseService _db;

    public ObservableCollection<BackupReportSummary> HistoryItems { get; } = [];

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _emptyMessage = "No hay ejecuciones registradas aún.";

    public HistoryViewModel(IDatabaseService db)
    {
        _db = db;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            HistoryItems.Clear();
            var items = await _db.GetReportHistoryAsync(100);
            foreach (var item in items)
                HistoryItems.Add(item);

            if (HistoryItems.Count == 0)
                EmptyMessage = "No hay ejecuciones registradas aún.";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();
}
