using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: AssemblyTitle("Huawei MateBook Fan Control UI")]
[assembly: AssemblyDescription("Graphical interface for the watchdog-backed Huawei MateBook fan controller")]
[assembly: AssemblyCompany("HuaweiFanControl community project")]
[assembly: AssemblyProduct("Huawei MateBook Fan Control")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

namespace HuaweiFanControl
{
    internal static class UiProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args));
        }
    }

    internal static class AppleGeometry
    {
        internal static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        internal int CornerRadius { get; set; }
        internal Color FillColor { get; set; }
        internal Color OutlineColor { get; set; }

        internal RoundedPanel()
        {
            CornerRadius = 18;
            FillColor = Color.White;
            OutlineColor = Color.FromArgb(228, 228, 232);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = AppleGeometry.RoundedRectangle(bounds, CornerRadius))
            using (SolidBrush fill = new SolidBrush(FillColor))
            using (Pen outline = new Pen(OutlineColor))
            {
                args.Graphics.FillPath(fill, path);
                args.Graphics.DrawPath(outline, path);
            }
        }
    }

    internal sealed class RoundedButton : Button
    {
        private Color normalColor;
        private Color hoverColor;
        private bool hovered;

        internal int CornerRadius { get; set; }

        internal RoundedButton()
        {
            CornerRadius = 14;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        internal void SetPalette(Color background, Color hover, Color foreground)
        {
            normalColor = background;
            hoverColor = hover;
            ForeColor = foreground;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs args)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(args);
        }

        protected override void OnMouseLeave(EventArgs args)
        {
            hovered = false;
            Invalidate();
            base.OnMouseLeave(args);
        }

        protected override void OnResize(EventArgs args)
        {
            base.OnResize(args);
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height));
            using (GraphicsPath path = AppleGeometry.RoundedRectangle(bounds, CornerRadius))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            Color fillColor = Enabled ? (hovered ? hoverColor : normalColor) : Color.FromArgb(228, 228, 232);
            Color textColor = Enabled ? ForeColor : Color.FromArgb(142, 142, 147);
            using (GraphicsPath path = AppleGeometry.RoundedRectangle(bounds, CornerRadius))
            using (SolidBrush fill = new SolidBrush(fillColor))
            {
                args.Graphics.FillPath(fill, path);
                TextRenderer.DrawText(args.Graphics, Text, Font, bounds, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }

    internal sealed class StatusPill : Control
    {
        internal Color FillColor { get; set; }
        internal Color TextColor { get; set; }

        internal StatusPill()
        {
            FillColor = Color.FromArgb(231, 247, 239);
            TextColor = Color.FromArgb(19, 122, 78);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = AppleGeometry.RoundedRectangle(bounds, Height / 2))
            using (SolidBrush fill = new SolidBrush(FillColor))
            {
                args.Graphics.FillPath(fill, path);
            }
            TextRenderer.DrawText(args.Graphics, Text, Font, bounds, TextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    internal sealed class FanAppIcon : Control
    {
        internal FanAppIcon()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            using (GraphicsPath path = AppleGeometry.RoundedRectangle(bounds, 13))
            using (LinearGradientBrush gradient = new LinearGradientBrush(bounds,
                Color.FromArgb(87, 170, 255), Color.FromArgb(0, 102, 230), 90f))
            {
                args.Graphics.FillPath(gradient, path);
            }

            args.Graphics.TranslateTransform(Width / 2f, Height / 2f);
            using (SolidBrush blade = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
            {
                for (int index = 0; index < 4; index++)
                {
                    args.Graphics.RotateTransform(90f);
                    args.Graphics.FillEllipse(blade, -4, -17, 8, 15);
                }
                args.Graphics.FillEllipse(blade, -5, -5, 10, 10);
            }
            args.Graphics.ResetTransform();
        }
    }

    internal sealed class MainForm : Form
    {
        private const string PayloadVersion = "1.3.0";
        private static readonly Color Navy = Color.FromArgb(29, 29, 31);
        private static readonly Color Blue = Color.FromArgb(0, 113, 227);
        private static readonly Color Green = Color.FromArgb(19, 122, 78);
        private static readonly Color Orange = Color.FromArgb(180, 85, 0);
        private static readonly Color Red = Color.FromArgb(215, 0, 21);
        private static readonly Color Surface = Color.White;
        private static readonly Color Canvas = Color.FromArgb(245, 245, 247);
        private static readonly Color Muted = Color.FromArgb(110, 110, 115);

        private readonly Regex samplePattern = new Regex(
            @"(?<time>\d{2}:\d{2}:\d{2})\s*\|\s*(?<temp>-?\d+)\s*C\s*\|\s*Request\s+(?<request>\d+)\s*\|\s*Fan0\s+(?<fan0>\d+)\s*\|\s*Fan1\s+(?<fan1>\d+)",
            RegexOptions.Compiled);
        private readonly Regex ansiPattern = new Regex(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);

        private StatusPill statusLabel;
        private Label temperatureValue;
        private Label fan0Value;
        private Label fan1Value;
        private Label requestValue;
        private RichTextBox logBox;
        private Button monitorButton;
        private Button quietButton;
        private Button fullButton;
        private Button stopButton;
        private NumericUpDown fullSpeedMinutes;
        private NumericUpDown emergencyTemperature;
        private Process controllerProcess;
        private string stopSignalPath;
        private string currentMode;
        private string payloadDirectory;
        private bool closeRequested;
        private bool allowClose;
        private DateTime closeDeadline;
        private Timer closeTimer;

        internal MainForm(string[] args)
        {
            Text = "Huawei MateBook Fan Control";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1040, 760);
            MinimumSize = new Size(940, 680);
            BackColor = Canvas;
            Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            FormClosing += HandleFormClosing;
            InitializeLayout();

            try
            {
                ExtractPayload();
                SetStatus("原厂自动控制", Green);
                AppendLog("UI 已就绪。请选择监测或控制模式。", false);
                ConfigureAutomatedValidation(args);
            }
            catch (Exception exception)
            {
                SetStatus("初始化失败", Red);
                SetButtonsEnabled(false);
                AppendLog("初始化失败：" + exception.Message, true);
                MessageBox.Show(this, exception.Message, "初始化失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ConfigureAutomatedValidation(string[] args)
        {
            if (args == null || args.Length != 2) return;
            string requestedMode = null;
            if (String.Equals(args[0], "--ui-test-monitor", StringComparison.OrdinalIgnoreCase)) requestedMode = "Monitor";
            if (String.Equals(args[0], "--ui-test-quiet", StringComparison.OrdinalIgnoreCase)) requestedMode = "Quiet";
            int seconds;
            if (requestedMode == null || !Int32.TryParse(args[1], out seconds) || seconds < 5 || seconds > 300) return;

            Shown += delegate
            {
                AppendLog("启动 UI 定时验收：" + requestedMode + " / " + seconds + " 秒。", false);
                StartController(requestedMode);
                Timer validationTimer = new Timer();
                validationTimer.Interval = seconds * 1000;
                validationTimer.Tick += delegate
                {
                    validationTimer.Stop();
                    validationTimer.Dispose();
                    closeRequested = true;
                    closeDeadline = DateTime.Now.AddSeconds(10);
                    RequestStopOrRestore();
                    closeTimer = new Timer();
                    closeTimer.Interval = 250;
                    closeTimer.Tick += CheckCloseProgress;
                    closeTimer.Start();
                };
                validationTimer.Start();
            };
        }

        private void InitializeLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 164));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 158));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            root.Controls.Add(CreateHeader(), 0, 0);
            root.Controls.Add(CreateMetrics(), 0, 1);
            root.Controls.Add(CreateControls(), 0, 2);
            root.Controls.Add(CreateLogPanel(), 0, 3);
            root.Controls.Add(CreateSafetyFooter(), 0, 4);
        }

        private Control CreateHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Canvas;
            header.Padding = new Padding(28, 18, 28, 12);

            FanAppIcon icon = new FanAppIcon();
            icon.Size = new Size(54, 54);
            icon.Location = new Point(30, 24);

            Label title = new Label();
            title.Text = "Fan Control";
            title.ForeColor = Navy;
            title.Font = new Font("Segoe UI Variable Display", 22f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(98, 18);

            Label subtitle = new Label();
            subtitle.Text = "Huawei MateBook 14 2024  ·  BIOS/EC  ·  v1.3.0";
            subtitle.ForeColor = Muted;
            subtitle.Font = new Font("Microsoft YaHei UI", 9.2f);
            subtitle.AutoSize = true;
            subtitle.Location = new Point(101, 61);

            statusLabel = new StatusPill();
            statusLabel.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            statusLabel.Size = new Size(170, 38);
            statusLabel.Location = new Point(4, 17);

            Panel statusHost = new Panel();
            statusHost.Dock = DockStyle.Right;
            statusHost.Width = 184;
            statusHost.BackColor = Canvas;
            statusHost.Controls.Add(statusLabel);

            header.Controls.Add(icon);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(statusHost);
            return header;
        }

        private Control CreateMetrics()
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.BackColor = Canvas;
            table.Padding = new Padding(22, 4, 22, 10);
            table.ColumnCount = 4;
            table.RowCount = 1;
            for (int index = 0; index < 4; index++)
            {
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            }

            table.Controls.Add(CreateMetricCard("CPU 温度", "-- °C", "实时温度", Blue, out temperatureValue), 0, 0);
            table.Controls.Add(CreateMetricCard("风扇 0", "-- RPM", "物理转速", Color.FromArgb(94, 92, 230), out fan0Value), 1, 0);
            table.Controls.Add(CreateMetricCard("风扇 1", "-- RPM", "物理转速", Color.FromArgb(94, 92, 230), out fan1Value), 2, 0);
            table.Controls.Add(CreateMetricCard("BIOS/EC 请求", "-- RPM", "控制目标", Orange, out requestValue), 3, 0);
            return table;
        }

        private RoundedPanel CreateMetricCard(string caption, string initialValue, string footnote, Color accent, out Label valueLabel)
        {
            RoundedPanel card = new RoundedPanel();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(7, 5, 7, 7);
            card.Padding = new Padding(19, 17, 19, 12);
            card.FillColor = Surface;
            card.OutlineColor = Color.FromArgb(232, 232, 236);
            card.CornerRadius = 20;

            Label captionLabel = new Label();
            captionLabel.Text = caption;
            captionLabel.ForeColor = Muted;
            captionLabel.Dock = DockStyle.Top;
            captionLabel.Height = 25;
            captionLabel.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            captionLabel.BackColor = Surface;

            Label footnoteLabel = new Label();
            footnoteLabel.Text = "●  " + footnote;
            footnoteLabel.ForeColor = accent;
            footnoteLabel.Dock = DockStyle.Bottom;
            footnoteLabel.Height = 25;
            footnoteLabel.Font = new Font("Microsoft YaHei UI", 8.2f);
            footnoteLabel.BackColor = Surface;

            valueLabel = new Label();
            valueLabel.Text = initialValue;
            valueLabel.ForeColor = Navy;
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;
            valueLabel.Font = new Font("Segoe UI Variable Display", 22f, FontStyle.Bold);
            valueLabel.BackColor = Surface;

            card.Controls.Add(valueLabel);
            card.Controls.Add(footnoteLabel);
            card.Controls.Add(captionLabel);
            return card;
        }

        private Control CreateControls()
        {
            RoundedPanel shell = new RoundedPanel();
            shell.Dock = DockStyle.Fill;
            shell.Margin = new Padding(29, 2, 29, 10);
            shell.Padding = new Padding(20, 14, 20, 12);
            shell.FillColor = Surface;
            shell.OutlineColor = Color.FromArgb(232, 232, 236);
            shell.CornerRadius = 22;

            TableLayoutPanel container = new TableLayoutPanel();
            container.Dock = DockStyle.Fill;
            container.BackColor = Surface;
            container.ColumnCount = 2;
            container.RowCount = 2;
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Label sectionTitle = new Label();
            sectionTitle.Text = "控制模式     选择预设后实时接管，停止时自动恢复原厂控制";
            sectionTitle.Font = new Font("Microsoft YaHei UI", 10.2f, FontStyle.Bold);
            sectionTitle.ForeColor = Navy;
            sectionTitle.Dock = DockStyle.Fill;
            sectionTitle.TextAlign = ContentAlignment.MiddleLeft;
            sectionTitle.BackColor = Surface;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.WrapContents = false;
            buttons.BackColor = Surface;

            monitorButton = CreateButton("仅监测", Color.FromArgb(232, 232, 237), Color.FromArgb(218, 218, 224), Navy, 100);
            quietButton = CreateButton("安静自动", Blue, Color.FromArgb(0, 96, 205), Color.White, 114);
            fullButton = CreateButton("全速", Navy, Color.FromArgb(60, 60, 64), Color.White, 86);
            stopButton = CreateButton("停止并恢复原厂", Color.FromArgb(255, 232, 231), Color.FromArgb(255, 218, 216), Red, 150);
            monitorButton.Click += delegate { StartController("Monitor"); };
            quietButton.Click += delegate { StartController("Quiet"); };
            fullButton.Click += delegate { StartController("Full"); };
            stopButton.Click += delegate { RequestStopOrRestore(); };
            buttons.Controls.Add(monitorButton);
            buttons.Controls.Add(quietButton);
            buttons.Controls.Add(fullButton);
            buttons.Controls.Add(stopButton);

            FlowLayoutPanel settings = new FlowLayoutPanel();
            settings.Dock = DockStyle.Fill;
            settings.WrapContents = false;
            settings.FlowDirection = FlowDirection.LeftToRight;
            settings.Padding = new Padding(2, 0, 0, 0);
            settings.BackColor = Surface;

            Label durationLabel = new Label();
            durationLabel.Text = "全速分钟\n0 = 无限";
            durationLabel.ForeColor = Muted;
            durationLabel.Size = new Size(65, 48);
            durationLabel.TextAlign = ContentAlignment.MiddleLeft;
            durationLabel.BackColor = Surface;

            fullSpeedMinutes = new NumericUpDown();
            fullSpeedMinutes.Minimum = 0;
            fullSpeedMinutes.Maximum = 1440;
            fullSpeedMinutes.Value = 5;
            fullSpeedMinutes.Size = new Size(60, 30);
            fullSpeedMinutes.Margin = new Padding(0, 10, 12, 0);
            fullSpeedMinutes.BackColor = Color.FromArgb(247, 247, 249);
            fullSpeedMinutes.BorderStyle = BorderStyle.FixedSingle;

            Label emergencyLabel = new Label();
            emergencyLabel.Text = "紧急温度\n°C";
            emergencyLabel.ForeColor = Muted;
            emergencyLabel.Size = new Size(57, 48);
            emergencyLabel.TextAlign = ContentAlignment.MiddleLeft;
            emergencyLabel.BackColor = Surface;

            emergencyTemperature = new NumericUpDown();
            emergencyTemperature.Minimum = 75;
            emergencyTemperature.Maximum = 95;
            emergencyTemperature.Value = 85;
            emergencyTemperature.Size = new Size(58, 30);
            emergencyTemperature.Margin = new Padding(0, 10, 0, 0);
            emergencyTemperature.BackColor = Color.FromArgb(247, 247, 249);
            emergencyTemperature.BorderStyle = BorderStyle.FixedSingle;

            settings.Controls.Add(durationLabel);
            settings.Controls.Add(fullSpeedMinutes);
            settings.Controls.Add(emergencyLabel);
            settings.Controls.Add(emergencyTemperature);
            container.Controls.Add(sectionTitle, 0, 0);
            container.SetColumnSpan(sectionTitle, 2);
            container.Controls.Add(buttons, 0, 1);
            container.Controls.Add(settings, 1, 1);
            shell.Controls.Add(container);
            return shell;
        }

        private Button CreateButton(string text, Color background, Color hover, Color foreground, int width)
        {
            RoundedButton button = new RoundedButton();
            button.Text = text;
            button.Size = new Size(width, 44);
            button.Margin = new Padding(4, 4, 4, 3);
            button.Font = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.SetPalette(background, hover, foreground);
            return button;
        }

        private Control CreateLogPanel()
        {
            RoundedPanel panel = new RoundedPanel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(29, 2, 29, 8);
            panel.Padding = new Padding(19, 12, 19, 15);
            panel.FillColor = Surface;
            panel.OutlineColor = Color.FromArgb(232, 232, 236);
            panel.CornerRadius = 22;

            Label title = new Label();
            title.Text = "活动记录";
            title.Dock = DockStyle.Top;
            title.Height = 32;
            title.Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
            title.ForeColor = Navy;
            title.BackColor = Surface;

            logBox = new RichTextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.ReadOnly = true;
            logBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            logBox.BackColor = Color.FromArgb(250, 250, 252);
            logBox.ForeColor = Color.FromArgb(66, 66, 69);
            logBox.BorderStyle = BorderStyle.None;
            logBox.Font = new Font("Consolas", 9.2f);
            logBox.Margin = new Padding(0);

            panel.Controls.Add(logBox);
            panel.Controls.Add(title);
            return panel;
        }

        private Control CreateSafetyFooter()
        {
            Label footer = new Label();
            footer.Dock = DockStyle.Fill;
            footer.TextAlign = ContentAlignment.MiddleCenter;
            footer.BackColor = Canvas;
            footer.ForeColor = Muted;
            footer.Font = new Font("Microsoft YaHei UI", 8.6f);
            footer.Text = "85°C 默认紧急恢复    ·    双风扇转速计保护    ·    独立 watchdog    ·    关闭窗口时恢复原厂控制";
            return footer;
        }

        private void ExtractPayload()
        {
            payloadDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HuaweiFanControl",
                "payload-" + PayloadVersion);
            Directory.CreateDirectory(payloadDirectory);
            Directory.CreateDirectory(Path.Combine(payloadDirectory, "runtime"));
            ExtractResource("HuaweiFanControl.Resources.Controller.ps1", "HuaweiFan-AutoController.ps1");
            ExtractResource("HuaweiFanControl.Resources.Watchdog.ps1", "HuaweiFan-Watchdog.ps1");
            ExtractResource("HuaweiFanControl.Resources.Restore.ps1", "Restore-HuaweiFanVendorControl.ps1");
            ExtractResource("HuaweiFanControl.Resources.QuietCurve.json", "quiet-balanced-curve.json");
            ExtractResource("HuaweiFanControl.Resources.FullSpeedCurve.json", "full-speed-curve.json");
        }

        private void ExtractResource(string resourceName, string fileName)
        {
            string destination = Path.Combine(payloadDirectory, fileName);
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (input == null)
                {
                    throw new InvalidOperationException("缺少内嵌资源：" + resourceName);
                }
                string temporary = destination + ".tmp";
                using (FileStream output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                }
                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }
                File.Move(temporary, destination);
            }
        }

        private void StartController(string mode)
        {
            if (IsControllerRunning())
            {
                MessageBox.Show(this, "请先点击“停止并恢复原厂”，等待当前模式退出。", "控制器正在运行", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            currentMode = mode;
            stopSignalPath = Path.Combine(payloadDirectory, "runtime", "ui-stop-" + Guid.NewGuid().ToString("N") + ".signal");
            string controllerPath = Path.Combine(payloadDirectory, "HuaweiFan-AutoController.ps1");
            string curvePath = Path.Combine(payloadDirectory, mode == "Full" ? "full-speed-curve.json" : "quiet-balanced-curve.json");
            List<string> arguments = new List<string>();
            arguments.Add("-NoProfile");
            arguments.Add("-ExecutionPolicy");
            arguments.Add("Bypass");
            arguments.Add("-File");
            arguments.Add(Quote(controllerPath));
            arguments.Add("-CurvePath");
            arguments.Add(Quote(curvePath));
            arguments.Add("-SampleSeconds");
            arguments.Add("2");
            arguments.Add("-EmergencyTemperatureC");
            arguments.Add(((int)emergencyTemperature.Value).ToString());
            arguments.Add("-StopSignalPath");
            arguments.Add(Quote(stopSignalPath));
            arguments.Add("-MaxMinutes");
            arguments.Add(mode == "Full" ? ((int)fullSpeedMinutes.Value).ToString() : "0");
            if (mode != "Monitor")
            {
                arguments.Add("-Apply");
            }

            string displayMode = mode == "Monitor" ? "仅监测" : (mode == "Full" ? "全速请求" : "安静自动");
            Color displayColor = mode == "Monitor" ? Muted : (mode == "Full" ? Orange : Blue);
            StartPowerShell(arguments, displayMode, displayColor);
        }

        private void RunVendorRestore()
        {
            currentMode = "Restore";
            string restorePath = Path.Combine(payloadDirectory, "Restore-HuaweiFanVendorControl.ps1");
            List<string> arguments = new List<string>();
            arguments.Add("-NoProfile");
            arguments.Add("-ExecutionPolicy");
            arguments.Add("Bypass");
            arguments.Add("-File");
            arguments.Add(Quote(restorePath));
            StartPowerShell(arguments, "正在恢复原厂", Orange);
        }

        private void StartPowerShell(List<string> arguments, string displayMode, Color displayColor)
        {
            string powershellPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = powershellPath;
            startInfo.Arguments = String.Join(" ", arguments.ToArray());
            startInfo.WorkingDirectory = payloadDirectory;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            Process process = new Process();
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += HandleProcessOutput;
            process.ErrorDataReceived += HandleProcessError;
            process.Exited += HandleProcessExited;
            controllerProcess = process;

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                SetStatus(displayMode, displayColor);
                SetRunningControls(true);
                AppendLog("启动模式：" + displayMode, false);
            }
            catch (Exception exception)
            {
                controllerProcess = null;
                process.Dispose();
                SetStatus("启动失败", Red);
                SetRunningControls(false);
                AppendLog("启动失败：" + exception.Message, true);
            }
        }

        private void HandleProcessOutput(object sender, DataReceivedEventArgs args)
        {
            if (String.IsNullOrWhiteSpace(args.Data)) return;
            BeginInvoke(new Action<string, bool>(AppendLog), args.Data, false);
        }

        private void HandleProcessError(object sender, DataReceivedEventArgs args)
        {
            if (String.IsNullOrWhiteSpace(args.Data)) return;
            BeginInvoke(new Action<string, bool>(AppendLog), args.Data, true);
        }

        private void HandleProcessExited(object sender, EventArgs args)
        {
            Process finished = (Process)sender;
            int exitCode = -1;
            try { exitCode = finished.ExitCode; } catch { }
            BeginInvoke(new Action<Process, int>(FinishProcess), finished, exitCode);
        }

        private void FinishProcess(Process finished, int exitCode)
        {
            if (controllerProcess != finished)
            {
                finished.Dispose();
                return;
            }
            controllerProcess = null;
            finished.Dispose();
            bool wasRestore = currentMode == "Restore";
            currentMode = null;
            SetRunningControls(false);
            if (exitCode == 0)
            {
                SetStatus("原厂自动控制", Green);
                AppendLog(wasRestore ? "原厂自动控制恢复成功。" : "控制器已退出，原厂自动控制已恢复。", false);
            }
            else
            {
                SetStatus("已停止 / 请检查日志", Red);
                AppendLog("进程退出码：" + exitCode + "。watchdog 将尝试恢复原厂控制。", true);
            }

            if (closeRequested)
            {
                allowClose = true;
                Close();
            }
        }

        private void RequestStopOrRestore()
        {
            if (IsControllerRunning())
            {
                try
                {
                    File.WriteAllText(stopSignalPath, "stop");
                    SetStatus("正在停止并恢复", Orange);
                    stopButton.Enabled = false;
                    AppendLog("已发送安全停止信号，等待 BIOS 原厂控制接管。", false);
                }
                catch (Exception exception)
                {
                    AppendLog("发送停止信号失败：" + exception.Message, true);
                }
            }
            else
            {
                RunVendorRestore();
            }
        }

        private bool IsControllerRunning()
        {
            return controllerProcess != null && !controllerProcess.HasExited;
        }

        private void AppendLog(string rawLine, bool isError)
        {
            string line = ansiPattern.Replace(rawLine, String.Empty).Trim();
            if (line.Length == 0) return;
            Match match = samplePattern.Match(line);
            if (match.Success)
            {
                int temperature;
                Int32.TryParse(match.Groups["temp"].Value, out temperature);
                temperatureValue.Text = match.Groups["temp"].Value + " °C";
                temperatureValue.ForeColor = temperature >= (int)emergencyTemperature.Value - 5 ? Red : Navy;
                fan0Value.Text = match.Groups["fan0"].Value + " RPM";
                fan1Value.Text = match.Groups["fan1"].Value + " RPM";
                requestValue.Text = match.Groups["request"].Value + " RPM";
            }

            string formatted = match.Success
                ? line + Environment.NewLine
                : DateTime.Now.ToString("HH:mm:ss") + "  " + line + Environment.NewLine;
            if (logBox.TextLength > 50000)
            {
                logBox.Text = logBox.Text.Substring(logBox.TextLength - 30000);
            }
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionColor = isError ? Red : Color.FromArgb(66, 66, 69);
            logBox.AppendText(formatted);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        private void SetStatus(string text, Color color)
        {
            statusLabel.Text = text;
            statusLabel.FillColor = Color.FromArgb(
                (color.R + 255 * 7) / 8,
                (color.G + 255 * 7) / 8,
                (color.B + 255 * 7) / 8);
            statusLabel.TextColor = color;
            statusLabel.Invalidate();
        }

        private void SetRunningControls(bool running)
        {
            monitorButton.Enabled = !running;
            quietButton.Enabled = !running;
            fullButton.Enabled = !running;
            fullSpeedMinutes.Enabled = !running;
            emergencyTemperature.Enabled = !running;
            stopButton.Enabled = true;
            stopButton.Text = running ? "停止并恢复原厂" : "强制恢复原厂";
        }

        private void SetButtonsEnabled(bool enabled)
        {
            monitorButton.Enabled = enabled;
            quietButton.Enabled = enabled;
            fullButton.Enabled = enabled;
            stopButton.Enabled = enabled;
        }

        private void HandleFormClosing(object sender, FormClosingEventArgs args)
        {
            if (allowClose || !IsControllerRunning()) return;
            args.Cancel = true;
            if (!closeRequested)
            {
                closeRequested = true;
                closeDeadline = DateTime.Now.AddSeconds(10);
                RequestStopOrRestore();
                closeTimer = new Timer();
                closeTimer.Interval = 250;
                closeTimer.Tick += CheckCloseProgress;
                closeTimer.Start();
            }
        }

        private void CheckCloseProgress(object sender, EventArgs args)
        {
            if (!IsControllerRunning())
            {
                closeTimer.Stop();
                allowClose = true;
                Close();
                return;
            }
            if (DateTime.Now >= closeDeadline)
            {
                closeTimer.Stop();
                try { controllerProcess.Kill(); } catch { }
                allowClose = true;
                Close();
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }

}
