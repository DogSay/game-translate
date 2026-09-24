using System.Diagnostics;
using GameTranslate.Core;

namespace GameTranslate.App;

internal sealed class MainForm : Form
{
    private readonly TextBox _root = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Label _status = new() { AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
    private readonly Label _details = new() { AutoSize = true, MaximumSize = new Size(850, 0) };
    private readonly Label _apiStatus = new() { AutoSize = true, Text = "未測試" };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 390 };
    private readonly Button _translate = new() { Text = "開始翻譯並安裝 Patch", AutoSize = true, Height = 34 };
    private readonly Button _restore = new() { Text = "還原／停用翻譯 Patch", AutoSize = true, Height = 34 };
    private readonly Button _delete = new() { Text = "刪除翻譯 Patch", AutoSize = true, Height = 34 };
    private readonly Button _cancel = new() { Text = "取消", AutoSize = true, Enabled = false };
    private readonly TextBox _log = new() { Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, Dock = DockStyle.Fill, Font = new Font("Consolas", 9), BackColor = Color.FromArgb(24, 28, 34), ForeColor = Color.Gainsboro };
    private string _gameRoot;
    private GameDetection? _detection;
    private PortableUiState? _uiState;
    private SessionLogger _logger;
    private CancellationTokenSource? _cancellation;

    public MainForm(string gameRoot)
    {
        _gameRoot = Path.GetFullPath(gameRoot);
        _logger = NewLogger(_gameRoot);
        Text = "Game Translate 可攜式遊戲繁化工具";
        Width = 940;
        Height = 720;
        MinimumSize = new Size(780, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);

        _mode.Items.Add("兼容模式：繁體取代簡中（已驗證）");
        _mode.Items.Add("原生模式：新增繁體語言 zh-Hant（實驗性）");
        _mode.SelectedIndex = 0;
        BuildLayout();
        Shown += (_, _) => DetectGame();
    }

    private void BuildLayout()
    {
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 7 };
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label { Text = "Game Translate", AutoSize = true, Font = new Font("Segoe UI Semibold", 22, FontStyle.Bold), ForeColor = Color.FromArgb(25, 58, 95), Margin = new Padding(0, 0, 0, 12) };
        main.Controls.Add(title);

        var pathPanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 12) };
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathPanel.Controls.Add(new Label { Text = "遊戲資料夾：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        pathPanel.Controls.Add(_root, 1, 0);
        var choose = new Button { Text = "選擇……", AutoSize = true };
        choose.Click += (_, _) => ChooseFolder();
        pathPanel.Controls.Add(choose, 2, 0);
        main.Controls.Add(pathPanel);

        var detectionPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12), BackColor = Color.White, Margin = new Padding(0, 0, 0, 12) };
        detectionPanel.Controls.Add(_status);
        detectionPanel.Controls.Add(_details);
        main.Controls.Add(detectionPanel);

        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 10) };
        options.Controls.Add(new Label { Text = "翻譯方法：", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        options.Controls.Add(_mode);
        var testApi = new Button { Text = "測試繁化姬 API", AutoSize = true };
        testApi.Click += async (_, _) => await TestApi();
        options.Controls.Add(testApi);
        options.Controls.Add(_apiStatus);
        main.Controls.Add(options);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
        _translate.Click += async (_, _) => await Translate();
        _restore.Click += (_, _) => { if (_uiState?.CanRestore == true) Restore(); else EnablePatch(); };
        _delete.Click += (_, _) => Delete();
        _cancel.Click += (_, _) => _cancellation?.Cancel();
        actions.Controls.Add(_translate);
        actions.Controls.Add(_restore);
        actions.Controls.Add(_delete);
        actions.Controls.Add(_cancel);
        main.Controls.Add(actions);

        var logGroup = new GroupBox { Text = "操作記錄 / Error Log", Dock = DockStyle.Fill, Padding = new Padding(8) };
        logGroup.Controls.Add(_log);
        main.Controls.Add(logGroup);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 8, 0, 0) };
        var openLog = new Button { Text = "開啟 Log 檔", AutoSize = true };
        openLog.Click += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_logger.LogPath}\"") { UseShellExecute = true });
        footer.Controls.Add(openLog);
        var service = new LinkLabel { Text = "翻譯服務：繁化姬（商用需付費）zhconvert.org　", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        service.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("https://zhconvert.org/") { UseShellExecute = true });
        footer.Controls.Add(service);
        main.Controls.Add(footer);
        Controls.Add(main);
    }

    private SessionLogger NewLogger(string root)
    {
        var logger = new SessionLogger(root);
        logger.Message += line =>
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(() => AppendLog(line)); else AppendLog(line);
        };
        return logger;
    }

    private void AppendLog(string line)
    {
        _log.AppendText(line + Environment.NewLine);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "選擇遊戲安裝資料夾", InitialDirectory = _gameRoot, UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _gameRoot = Path.GetFullPath(dialog.SelectedPath);
        _logger = NewLogger(_gameRoot);
        _log.Clear();
        DetectGame();
    }

    private void DetectGame()
    {
        _root.Text = _gameRoot;
        _detection = EngineDetector.Detect(_gameRoot);
        var patch = _detection.PaksDirectory is null ? new PatchInspection([], []) : PatchManager.Inspect(_detection.PaksDirectory);
        var engine = _detection.Engine switch { EngineKind.Unreal => "Unreal Engine", EngineKind.Unity => "Unity", _ => "未知" };
        var packaging = _detection.Packaging switch { PackagingKind.IoStore => "IoStore / Pak", PackagingKind.Pak => "Pak", PackagingKind.UnityAssets => "Unity Assets", _ => "未知" };
        _status.Text = _detection.Engine switch
        {
            EngineKind.Unreal => "✓ 偵測成功，可以分析 Unreal localization",
            EngineKind.Unity => "△ 偵測到 Unity；安全外加翻譯 adapter 尚未完成",
            _ => "✕ 偵測失敗：搵唔到支援嘅遊戲結構",
        };
        _status.ForeColor = _detection.Engine == EngineKind.Unreal ? Color.SeaGreen : _detection.Engine == EngineKind.Unity ? Color.DarkOrange : Color.Firebrick;
        var ui = PortableUi.From(patch);
        _details.Text = $"引擎：{engine}　封裝：{packaging}　Project：{_detection.ProjectName ?? "—"}\n" +
                        $"Archives：{_detection.Archives.Count}　工具 Patch：{patch.OwnedActive.Count}　其他外部 Patch：{patch.ForeignActive.Count}\n" +
                        (_detection.Engine == EngineKind.Unreal ? ui.PatchStatusLine + "\n" : string.Empty) +
                        (_detection.Engine == EngineKind.Unity ? "目前只做偵測，唔會冒險修改 Unity asset bundle。" :
                         patch.ForeignActive.Count > 0 ? "偵測到其他 mod patch；還原功能只會處理 Game Translate 自己嘅 patch。" : "原始 archive 只讀；輸出會以獨立 _P.pak 安裝。 ");
        _uiState = ui;
        _translate.Enabled = _detection.Engine == EngineKind.Unreal;
        _translate.Text = ui.TranslateButtonText;
        _mode.Enabled = _translate.Enabled;
        _restore.Text = ui.ToggleButtonText;
        _restore.Enabled = ui.CanToggle;
        _delete.Enabled = ui.CanDelete;
        _logger.Write($"偵測：Engine={_detection.Engine}, Packaging={_detection.Packaging}, Project={_detection.ProjectName ?? "unknown"}, Archives={_detection.Archives.Count}, OwnedPatch={patch.OwnedActive.Count}");
    }

    private async Task TestApi()
    {
        try
        {
            _apiStatus.Text = "連接中……";
            _apiStatus.ForeColor = Color.DarkOrange;
            var result = await new ZhConvertClient().ConvertBatchAsync(["连线测试"], CancellationToken.None);
            if (result.Count != 1 || result[0] != "連線測試") throw new InvalidDataException("API 未有回傳預期繁體結果。");
            _apiStatus.Text = $"✓ 成功（{result[0]}）";
            _apiStatus.ForeColor = Color.SeaGreen;
            _logger.Write($"繁化姬 API 連接成功：{result[0]}");
        }
        catch (Exception error)
        {
            _apiStatus.Text = "✕ 失敗";
            _apiStatus.ForeColor = Color.Firebrick;
            _logger.Write("繁化姬 API 連接失敗：" + error);
        }
    }

    private async Task Translate()
    {
        if (_detection?.Engine != EngineKind.Unreal) return;
        var mode = _mode.SelectedIndex == 1 ? TranslationMode.NativeTraditional : TranslationMode.Compatibility;
        var explanation = mode == TranslationMode.NativeTraditional
            ? "原生模式會新增 zh-Hant 語言並將玩家語言切換過去；如果遊戲語言選單係動態，會出現新嘅中文選項。\n如果選單冇出現新選項、或者設定被遊戲重設，請改用兼容模式。"
            : "兼容模式會以繁體內容覆蓋遊戲嘅簡中槽位；遊戲內語言選單仍然要選擇「簡體中文」。";
        if (MessageBox.Show(this, explanation + "\n\n流程會連接繁化姬 API、建立並驗證獨立 patch。繼續？", "確認翻譯方法", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
        SetBusy(true);
        _cancellation = new CancellationTokenSource();
        try
        {
            _logger.Write(mode == TranslationMode.NativeTraditional ? "開始翻譯：native zh-Hant" : "開始翻譯：compat（覆蓋簡中槽位）");
            var tools = ToolInstaller.Ensure(_gameRoot);
            _logger.Write("內置工具已解壓並驗證。");
            var memoryPath = Path.Combine(_gameRoot, ".game-translate", "translation-memory", "zhconvert-taiwan.json");
            var api = new TranslationMemoryApi(memoryPath, new ZhConvertClient());
            var workflow = new UnrealTranslationWorkflow(new ProcessRunner(), api, tools, _logger.Write);
            var result = await workflow.BuildAndInstallAsync(_detection, mode, _cancellation.Token);
            var followUp = mode == TranslationMode.NativeTraditional
                ? $"\n\n玩家語言設定已切換到 {result.Artifact.TargetCulture}；開遊戲檢查語言選單有冇新嘅中文選項。"
                : $"\n\n玩家語言設定已切換到 {result.Artifact.TargetCulture}；請喺遊戲內選擇「簡體中文」。";
            MessageBox.Show(this,
                $"翻譯 Patch 已建立、round-trip 驗證及安裝。\n\nTargets：{result.Targets}\nEntries：{result.Entries}\nSHA-256：{result.Artifact.Sha256}" + followUp,
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _logger.Write("操作已由使用者取消；未完成嘅 patch 不會安裝。");
        }
        catch (Exception error)
        {
            _logger.Write("ERROR：" + error);
            MessageBox.Show(this, error.Message + $"\n\n完整資料：{_logger.LogPath}", "翻譯失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            SetBusy(false);
            DetectGame();
        }
    }

    private void Restore()
    {
        if (_detection?.PaksDirectory is null) return;
        if (MessageBox.Show(this, "會將 Game Translate 自己嘅 active patch 改名為 .disabled，並將玩家語言設定還原到 zh-Hans；原始遊戲檔唔會被改動。繼續？", "還原翻譯", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var culture = SimplifiedCultures.InstalledTargetCulture(_detection.GameRoot);
            var result = PortableModeManager.DisableAndReset(_detection, localAppData, culture);
            foreach (var path in result.DisabledPatches) _logger.Write("已停用 patch：" + path);
            if (result.ConfigPath is not null) _logger.Write($"玩家語言設定已還原到 {culture}：" + result.ConfigPath);
            MessageBox.Show(this, $"已停用 {result.DisabledPatches.Count} 個翻譯 patch，玩家語言設定已還原到 {culture}。", "還原完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DetectGame();
        }
        catch (Exception error)
        {
            _logger.Write("還原 ERROR：" + error);
            MessageBox.Show(this, error.Message, "還原失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EnablePatch()
    {
        if (_detection?.PaksDirectory is null) return;
        try
        {
            var enabled = PatchManager.EnableDisabled(_detection.PaksDirectory);
            foreach (var path in enabled) _logger.Write("已啟用 patch 檔案：" + path);
            MessageBox.Show(this, $"已啟用 {enabled.Count} 個翻譯 patch 檔案；遊戲內選擇「中文（簡體）」即會顯示繁體。", "啟用完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DetectGame();
        }
        catch (Exception error)
        {
            _logger.Write("啟用 ERROR：" + error);
            MessageBox.Show(this, error.Message, "啟用失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Delete()
    {
        if (_detection?.PaksDirectory is null) return;
        if (MessageBox.Show(this,
            "會永久刪除 Game Translate 自己嘅 patch 檔案（包括 .disabled 舊版），並將玩家語言設定還原到 zh-Hans。\n\n" +
            "Translation memory 會保留，之後隨時可以重新翻譯；原始遊戲檔唔會被改動。繼續？",
            "刪除翻譯", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var culture = SimplifiedCultures.InstalledTargetCulture(_detection.GameRoot);
            var result = PortableModeManager.DeleteAndReset(_detection, localAppData, culture);
            foreach (var path in result.DeletedPaths) _logger.Write("已刪除 patch 檔案：" + path);
            if (result.ConfigPath is not null) _logger.Write($"玩家語言設定已還原到 {culture}：" + result.ConfigPath);
            MessageBox.Show(this, $"已刪除 {result.DeletedPaths.Count} 個 Game Translate patch 檔案，玩家語言設定已還原到 {culture}。", "刪除完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DetectGame();
        }
        catch (Exception error)
        {
            _logger.Write("刪除 ERROR：" + error);
            MessageBox.Show(this, error.Message, "刪除失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetBusy(bool busy)
    {
        _translate.Enabled = !busy && _detection?.Engine == EngineKind.Unreal;
        var ui = !busy && _detection?.PaksDirectory is not null ? PortableUi.From(PatchManager.Inspect(_detection.PaksDirectory)) : null;
        if (ui is not null)
        {
            _uiState = ui;
            _restore.Text = ui.ToggleButtonText;
        }
        _restore.Enabled = ui?.CanToggle == true;
        _delete.Enabled = ui?.CanDelete == true;
        _mode.Enabled = !busy;
        _cancel.Enabled = busy;
        UseWaitCursor = busy;
    }
}
