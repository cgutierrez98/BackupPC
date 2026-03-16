using System;
using System.Threading.Tasks;

namespace LocalBackupMaster.Helpers;

public static class TaskExtensions
{
    public static void SafeFireAndForget(this Task task, Action<Exception>? onException = null)
    {
        _ = SafeFireAndForgetAsync(task, onException);
    }

    private static async Task SafeFireAndForgetAsync(Task task, Action<Exception>? onException)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try
            {
                onException?.Invoke(ex);
            }
            catch
            {
                // swallow
            }
        }
    }
}
