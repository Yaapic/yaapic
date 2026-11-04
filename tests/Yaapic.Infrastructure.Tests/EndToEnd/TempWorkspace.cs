namespace Yaapic.Infrastructure.Tests.EndToEnd;

internal static class TempWorkspace
{
    /// <summary>Deletes a temp workspace; history is recorded fire-and-forget, so a write may still hold a file: retry.</summary>
    public static void Delete(string root)
    {
        for (var attempt = 0; Directory.Exists(root); attempt++)
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(50);
            }
        }
    }
}
