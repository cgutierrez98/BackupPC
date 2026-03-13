using LocalBackupMaster.Models;

namespace LocalBackupMaster.Services.Validation;

public interface IBackupValidator
{
    (bool IsValid, string Message) Validate(IEnumerable<BackupSource> sources, IEnumerable<BackupDestination> destinations);
}

public class BackupValidator : IBackupValidator
{
    public (bool IsValid, string Message) Validate(IEnumerable<BackupSource> sources, IEnumerable<BackupDestination> destinations)
    {
        if (!sources.Any())
            return (false, "Añade al menos una carpeta de origen.");

        if (!destinations.Any())
            return (false, "Añade al menos una unidad de destino.");

        var dest = destinations.First();
        if (string.IsNullOrEmpty(dest.BackupPath))
            return (false, "La ruta de destino no es válida o está vacía.");

        if (!Directory.Exists(dest.BackupPath))
            return (false, "La unidad de destino no está conectada o la ruta no existe.");

        return (true, string.Empty);
    }
}
