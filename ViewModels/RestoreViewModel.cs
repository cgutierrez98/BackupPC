using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalBackupMaster.Models;
using LocalBackupMaster.Services;
using CommunityToolkit.Maui.Storage;

namespace LocalBackupMaster.ViewModels;

/// <summary>D3 — ViewModel para la página de restauración inteligente.</summary>
public partial class RestoreViewModel : ObservableObject
{
    private readonly IDatabaseService  _db;
    private readonly IRestoreService   _restoreService;

    public ObservableCollection<BackupDestination> Destinations  { get; } = [];
    public ObservableCollection<FileRecord>        FileTree      { get; } = [];
    public ObservableCollection<FileRecord>        SelectedFiles { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRestore))]
    private BackupDestination? _selectedDestination;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private bool   _isRestoring;
    [ObservableProperty] private string _statusMessage  = "";
    [ObservableProperty] private string _emptyMessage   = "Selecciona un destino para ver los archivos.";

    public bool CanRestore => SelectedDestination != null && SelectedFiles.Count > 0 && !IsRestoring;

    public RestoreViewModel(IDatabaseService db, IRestoreService restoreService)
    {
        _db             = db;
        _restoreService = restoreService;
    }

    public async Task LoadDestinationsAsync()
    {
        var dests = await _db.GetDestinationsAsync();
        Destinations.Clear();
        foreach (var d in dests) Destinations.Add(d);
    }

    partial void OnSelectedDestinationChanged(BackupDestination? value)
    {
        if (value != null)
            LoadFileTreeCommand.Execute(value);
    }

    [RelayCommand]
    private async Task LoadFileTree(BackupDestination destination)
    {
        IsLoading = true;
        FileTree.Clear();
        SelectedFiles.Clear();
        try
        {
            var records = await _restoreService.GetFileTreeAsync(destination);
            foreach (var r in records.OrderBy(r => r.RelativePath))
                FileTree.Add(r);

            EmptyMessage = FileTree.Count == 0
                ? "No hay archivos catalogados para este destino."
                : "";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleFileSelection(FileRecord file)
    {
        if (SelectedFiles.Contains(file))
            SelectedFiles.Remove(file);
        else
            SelectedFiles.Add(file);

        OnPropertyChanged(nameof(CanRestore));
    }

    [RelayCommand]
    private void SelectAll()
    {
        SelectedFiles.Clear();
        foreach (var f in FileTree) SelectedFiles.Add(f);
        OnPropertyChanged(nameof(CanRestore));
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedFiles.Clear();
        OnPropertyChanged(nameof(CanRestore));
    }

    [RelayCommand]
    private async Task Restore()
    {
        if (SelectedDestination == null || SelectedFiles.Count == 0) return;

        var result = await FolderPicker.Default.PickAsync();
        if (!result.IsSuccessful) return;

        IsRestoring   = true;
        StatusMessage = "Restaurando archivos...";
        OnPropertyChanged(nameof(CanRestore));

        try
        {
            int count = await _restoreService.RestoreFilesAsync(
                SelectedDestination,
                SelectedFiles.ToList(),
                result.Folder.Path);

            StatusMessage = $"✅ {count} archivo(s) restaurados en {result.Folder.Path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsRestoring = false;
            OnPropertyChanged(nameof(CanRestore));
        }
    }
}
