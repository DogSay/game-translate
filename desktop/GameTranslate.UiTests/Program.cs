using System.Reflection;
using System.Windows.Forms;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

var app = Assembly.Load("GameTranslate");
var formType = app.GetType("GameTranslate.App.MainForm", throwOnError: true)!;
var driveRoot = Path.GetPathRoot(Environment.CurrentDirectory)!;
using var form = (Form)Activator.CreateInstance(formType, [driveRoot])!;

foreach (var width in new[] { 940, 780 })
{
    form.Size = new(width, 720);
    form.CreateControl();
    var apiStatus = Find(form, "未測試");
    apiStatus.Text = "✓ 成功（連線測試）";
    var translateButton = Find(form, "開始翻譯並安裝 Patch");
    translateButton.Text = "重新翻譯並安裝 Patch";
    LayoutTree(form);

    var methodLabel = Find(form, "翻譯方法：");
    AssertCenters($"translation method at {width}px", methodLabel.Parent!, methodLabel,
        methodLabel.Parent!.Controls.OfType<ComboBox>().Single(),
        Find(form, "測試繁化姬 API"), apiStatus);

    var folderLabel = Find(form, "遊戲資料夾：");
    AssertCenters($"game folder at {width}px", folderLabel.Parent!, folderLabel,
        folderLabel.Parent!.Controls.OfType<TextBox>().Single(), Find(form, "選擇……"));

    var clear = Find(form, "清除快取…");
    AssertCenters($"actions at {width}px", clear.Parent!, clear.Parent!.Controls.OfType<Button>().ToArray());

    var logButton = Find(form, "開啟 Log 檔");
    AssertCenters($"footer at {width}px", logButton.Parent!, logButton,
        logButton.Parent!.Controls.OfType<LinkLabel>().Single());

    foreach (var row in new[] { methodLabel.Parent!, folderLabel.Parent!, clear.Parent!, logButton.Parent! })
    {
        if (row.Controls.Cast<Control>().Any(control => control.Right > row.ClientSize.Width) ||
            row.Right > row.Parent!.ClientSize.Width)
            throw new InvalidOperationException($"{row.GetType().Name} overflows at {width}px.");
    }

    apiStatus.Text = "未測試";
    translateButton.Text = "開始翻譯並安裝 Patch";
}

Console.WriteLine("UI control centers aligned at 940px and 780px.");

static void LayoutTree(Control control)
{
    control.PerformLayout();
    foreach (Control child in control.Controls) LayoutTree(child);
}

static Control Find(Control root, string text)
{
    if (root.Text == text) return root;
    foreach (Control child in root.Controls)
    {
        var match = FindOrNull(child, text);
        if (match is not null) return match;
    }
    throw new InvalidOperationException($"Control not found: {text}");
}

static Control? FindOrNull(Control root, string text)
{
    if (root.Text == text) return root;
    foreach (Control child in root.Controls)
    {
        var match = FindOrNull(child, text);
        if (match is not null) return match;
    }
    return null;
}

static void AssertCenters(string rowName, Control row, params Control[] controls)
{
    var centers = controls.Select(control => control.Top + control.Height / 2.0).ToArray();
    if (centers.Max() - centers.Min() > 2)
        throw new InvalidOperationException($"{rowName} is vertically misaligned: " +
            string.Join(", ", controls.Select((control, index) => $"{control.GetType().Name}={centers[index]:0.0}")));
    if (controls.Any(control => control.Parent != row))
        throw new InvalidOperationException($"{rowName} controls do not share one row.");
}
