namespace ConsuntivoOre.UI;

/// <summary>Base comune: scaling DPI, icona, font.</summary>
internal class BaseForm : Form
{
    public BaseForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.75F);
        Icon = AppIcon.Window;
        StartPosition = FormStartPosition.CenterScreen;
    }
}

internal static class Ui
{
    public static readonly Color Good = Color.FromArgb(21, 128, 61);
    public static readonly Color Warn = Color.FromArgb(180, 83, 9);

    public static Label Label(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 7, 10, 3),
    };

    public static Button Button(string text, Action onClick, int minWidth = 90)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(minWidth, 30),
            Padding = new Padding(6, 0, 6, 0),
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    public static TableLayoutPanel Grid(params SizeType[] columns)
    {
        var grid = new TableLayoutPanel
        {
            ColumnCount = columns.Length,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        foreach (var c in columns)
            grid.ColumnStyles.Add(c == SizeType.Percent ? new ColumnStyle(SizeType.Percent, 100) : new ColumnStyle(c));
        return grid;
    }

    public static FlowLayoutPanel Flow(params Control[] controls)
    {
        var flow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0),
        };
        flow.Controls.AddRange(controls);
        return flow;
    }

    public static GroupBox Group(string title, Control content)
    {
        var box = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 6, 10, 8),
            Margin = new Padding(0, 6, 0, 6),
        };
        content.Dock = DockStyle.Fill;
        box.Controls.Add(content);
        return box;
    }

    public static void BringToFront(Form form)
    {
        if (!form.Visible) form.Show();
        if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
        form.TopMost = true;
        form.Activate();
        form.TopMost = false;
    }

    public static void ShowWarning(IWin32Window owner, string message) =>
        MessageBox.Show(owner, message, "Consuntivo Ore", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static string OneLine(string text) =>
        text.ReplaceLineEndings(" ").Trim();
}
