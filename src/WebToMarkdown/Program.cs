namespace WebToMarkdown;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        NativeMethods.HideConsoleWindow();

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var context = new ExtractionApplicationContext(args, completion);
        Application.Run(context);
        return completion.Task.GetAwaiter().GetResult();
    }
}

internal sealed class ExtractionApplicationContext : ApplicationContext
{
    readonly TaskCompletionSource<int> _completion;
    readonly HiddenHostForm _host;
    bool _completed;

    public ExtractionApplicationContext(string[] args, TaskCompletionSource<int> completion)
    {
        _completion = completion;
        _host = new HiddenHostForm();
        MainForm = _host;

        _host.Shown += async (_, _) => await StartAsync(args).ConfigureAwait(true);
        _host.FormClosed += (_, _) => Complete(_completed ? _completion.Task.Status == TaskStatus.RanToCompletion ? _completion.Task.Result : 1 : 1);
    }

    async Task StartAsync(string[] args)
    {
        try
        {
            var result = await ExtractionApplication.RunAsync(_host, args).ConfigureAwait(true);
            Complete(result.ExitCode);
        }
        catch (Exception ex)
        {
            try
            {
                var paths = AppPaths.Initialize();
                Logger.Error(paths.LogFilePath, "Fatal application error.", ex);
            }
            catch
            {
                // No UI/console output by design.
            }

            Complete(1);
        }
        finally
        {
            _host.Close();
        }
    }

    void Complete(int exitCode)
    {
        if (_completed)
            return;

        _completed = true;
        _completion.TrySetResult(exitCode);
        ExitThread();
    }
}

internal sealed class HiddenHostForm : Form
{
    readonly WebView2Host _webViewHost;

    public HiddenHostForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
        Size = new Size(1, 1);
        Opacity = 0;
        Text = "WebToMarkdown";
        _webViewHost = new WebView2Host
        {
            Dock = DockStyle.Fill,
            TabStop = false
        };
        Controls.Add(_webViewHost);
    }

    public WebView2Host Browser => _webViewHost;
}
