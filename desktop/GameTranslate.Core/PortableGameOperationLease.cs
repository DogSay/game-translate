namespace GameTranslate.Core;

public sealed class PortableGameOperationLease : IDisposable
{
    private Semaphore? _semaphore;

    private PortableGameOperationLease(Semaphore semaphore) => _semaphore = semaphore;

    public static PortableGameOperationLease Acquire(string gameRoot, string? localAppDataRoot = null)
    {
        var identity = Path.GetFileName(PortableStorage.GameDirectory(gameRoot, localAppDataRoot));
        var semaphore = new Semaphore(1, 1, @"Local\GameTranslate.Game." + identity);
        try
        {
            if (!semaphore.WaitOne(0))
                throw new IOException("Another Game Translate operation is using this game's data. Wait for it to finish before clearing cache.");
            return new PortableGameOperationLease(semaphore);
        }
        catch
        {
            semaphore.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        var semaphore = Interlocked.Exchange(ref _semaphore, null);
        if (semaphore is null) return;
        semaphore.Release();
        semaphore.Dispose();
    }
}
