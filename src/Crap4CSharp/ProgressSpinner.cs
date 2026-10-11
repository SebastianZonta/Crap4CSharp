namespace Crap4CSharp;

internal sealed class ProgressSpinner : IAsyncDisposable
{
    private static readonly string[] _frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage", "CA2213:Disposable fields should be disposed",
        Justification = "Writer is owned by the caller; the spinner never disposes it.")]
    private readonly TextWriter _err;
    private readonly string _message;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private bool _drewFrame;

    private ProgressSpinner(TextWriter err, string message, TimeSpan showAfter, TimeSpan interval)
    {
        _err = err;
        _message = message;
        _loop = RunAsync(showAfter, interval, _cts.Token);
    }

    internal static ProgressSpinner? StartIfEnabled(TextWriter err, string message, bool enabled) =>
        StartIfEnabled(err, message, enabled, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(80));

    internal static ProgressSpinner? StartIfEnabled(
        TextWriter err, string message, bool enabled, TimeSpan showAfter, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(err);
        ArgumentNullException.ThrowIfNull(message);
        if (!enabled)
        {
            return null;
        }
        return new ProgressSpinner(err, message, showAfter, interval);
    }

    internal static bool IsEnabled(TextWriter err, bool noProgress)
    {
        if (noProgress)
        {
            return false;
        }
        if (!ReferenceEquals(err, Console.Error))
        {
            return false;
        }
        try
        {
            return !Console.IsErrorRedirected;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private async Task RunAsync(TimeSpan showAfter, TimeSpan interval, CancellationToken token)
    {
        try
        {
            await Task.Delay(showAfter, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        var i = 0;
        while (!token.IsCancellationRequested)
        {
            var frame = _frames[i++ % _frames.Length];
            try
            {
                await _err.WriteAsync($"\r{frame} {_message}").ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
            {
                return;
            }
            _drewFrame = true;
            try
            {
                await Task.Delay(interval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        if (_drewFrame)
        {
            try
            {
                await _err.WriteAsync("\r" + new string(' ', _message.Length + 2) + "\r").ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
            {
            }
        }
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}