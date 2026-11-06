using System.IO.Pipes;

namespace ConsuntivoOre.Platform;

/// <summary>
/// Una sola istanza per utente. Il mutex è usato anche dall'installer (AppMutex) per rilevare l'app in esecuzione;
/// un secondo avvio chiede all'istanza attiva, tramite una named pipe, di mostrare la finestra.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = "ConsuntivoOreMutex";
    private static readonly string PipeName = $"ConsuntivoOreShow-{Environment.UserName}";

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    /// <summary>Null se l'app è già in esecuzione.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (createdNew) return new SingleInstance(mutex);
        mutex.Dispose();
        return null;
    }

    /// <summary>Chiede all'istanza attiva di mostrare la finestra.</summary>
    public static void SignalShow()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            client.WriteByte(1);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            Log.Error(ex);
        }
    }

    /// <summary>Ascolta i secondi avvii; <paramref name="onShow"/> è chiamato su un thread in background.</summary>
    public void Listen(Action onShow) => _ = Task.Run(async () =>
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_stop.Token);
                server.ReadByte();
                onShow();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                await Task.Delay(1000);
            }
        }
    });

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // rilasciato da un altro thread o già rilasciato: non importa all'uscita
        }
        _mutex.Dispose();
    }
}
