using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace EnigmaDisk.Core;

/// <summary>One interactive Disk window per Windows user; scheduled snapshots are separate.</summary>
public sealed class AppInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();
    private readonly string _name;
    public bool OwnsInstance { get; }

    public AppInstance()
    {
        _name = "EnigmaDisk-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).ToUpperInvariant())))[..24];
        _mutex = new Mutex(true, _name, out var created);
        OwnsInstance = created;
        if (created) _ = ListenAsync();
        else
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", _name, PipeDirection.Out);
                pipe.Connect(2000);
                pipe.WriteByte(1);
            }
            catch (Exception e) when (e is IOException or TimeoutException) { }
        }
    }

    private async Task ListenAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(_name, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token);
                _ = Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var window = Application.Current.MainWindow;
                    if (window == null) return;
                    window.Show();
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Activate();
                }));
            }
            catch (OperationCanceledException) { break; }
            catch (IOException) { await Task.Delay(100); }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (OwnsInstance) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
