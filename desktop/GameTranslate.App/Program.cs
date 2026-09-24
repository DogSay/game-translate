using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using GameTranslate.Core;

namespace GameTranslate.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length > 0 && args[0] == "--ueextractor-host")
            return ToolHost.RunUeExtractor(args.Skip(1).ToArray());
        if (args.Length > 0 && (args[0] == "--diagnose" || args[0].StartsWith("--diagnose=", StringComparison.Ordinal)))
            return Diagnose(OptionPath(args, "--diagnose") ?? PortableRoot());
        if (args.Length > 0 && (args[0] == "--tools-test" || args[0].StartsWith("--tools-test=", StringComparison.Ordinal)))
            return TestTools(OptionPath(args, "--tools-test") ?? PortableRoot()).GetAwaiter().GetResult();
        if (args.Length > 0 && args[0] == "--api-test")
            return TestApi().GetAwaiter().GetResult();

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(PortableRoot()));
        return 0;
    }

    public static string PortableRoot() => Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    private static string? OptionPath(string[] args, string name)
    {
        if (args[0].StartsWith(name + "=", StringComparison.Ordinal)) return args[0][(name.Length + 1)..];
        return args.Length > 1 ? args[1] : null;
    }

    private static int Diagnose(string root)
    {
        try
        {
            var detection = EngineDetector.Detect(root);
            var patches = detection.PaksDirectory is null ? new PatchInspection([], []) : PatchManager.Inspect(detection.PaksDirectory);
            Console.WriteLine(JsonSerializer.Serialize(new { detection, patches }, new JsonSerializerOptions { WriteIndented = true }));
            return detection.Engine == EngineKind.Unknown ? 2 : 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static async Task<int> TestTools(string root)
    {
        try
        {
            var paths = ToolInstaller.Ensure(root);
            var runner = new ProcessRunner();
            var ue = await runner.RunAsync(paths.UeExtractorHost, [.. paths.UeExtractorPrefix, "--help"], Path.GetDirectoryName(paths.UeExtractorHost), CancellationToken.None);
            var repak = await runner.RunAsync(paths.Repak, ["--version"], Path.GetDirectoryName(paths.Repak), CancellationToken.None);
            var retoc = await runner.RunAsync(paths.Retoc, ["--version"], Path.GetDirectoryName(paths.Retoc), CancellationToken.None);
            Console.WriteLine(JsonSerializer.Serialize(new {
                ue = new { ue.ExitCode, ue.Output }, repak = new { repak.ExitCode, repak.Output }, retoc = new { retoc.ExitCode, retoc.Output },
            }, new JsonSerializerOptions { WriteIndented = true }));
            return ue.Success && repak.Success && retoc.Success ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static async Task<int> TestApi()
    {
        try
        {
            var value = await new ZhConvertClient().ConvertBatchAsync(["连线测试"], CancellationToken.None);
            var converted = value.Single();
            if (converted != "連線測試") throw new InvalidDataException("API did not return the expected Traditional Chinese result.");
            Console.WriteLine(JsonSerializer.Serialize(new { success = true, converted }));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}

internal static class ToolHost
{
    public static int RunUeExtractor(string[] args)
    {
        if (args.Length == 0) return 64;
        if (GetConsoleWindow() == IntPtr.Zero)
        {
            AllocConsole();
            var window = GetConsoleWindow();
            if (window != IntPtr.Zero) ShowWindow(window, 0);
        }
        var assemblyPath = Path.GetFullPath(args[0]);
        var toolArguments = args.Skip(1).ToArray();
        var toolDirectory = Path.GetDirectoryName(assemblyPath)!;
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var candidate = Path.Combine(toolDirectory, $"{name.Name}.dll");
            return File.Exists(candidate) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
        };
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        var entry = assembly.EntryPoint ?? throw new MissingMethodException("UEExtractor assembly has no entry point.");
        var result = entry.Invoke(null, [toolArguments]);
        if (result is Task<int> integerTask) return integerTask.GetAwaiter().GetResult();
        if (result is Task task) { task.GetAwaiter().GetResult(); return 0; }
        return result is int code ? code : 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
}
