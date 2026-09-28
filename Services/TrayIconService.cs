using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;
using Color = System.Drawing.Color;

namespace Verbal.Services;

internal sealed class TrayIconService : IDisposable
{
    private readonly Bitmap _baseIcon;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _recordingMenuItem;
    private readonly Func<bool> _isRecording;
    private readonly Func<bool> _isBusy;
    private readonly Action _toggleRecording;
    private readonly Action _showWindow;
    private readonly Action _exit;
    private Icon? _currentIcon;
    private int _meterStep = -2;
    private bool _recording;

    public TrayIconService(
        string iconPath,
        Action showWindow,
        Action toggleRecording,
        Func<bool> isRecording,
        Func<bool> isBusy,
        Action exit)
    {
        using (var appIcon = new Icon(iconPath))
        {
            _baseIcon = appIcon.ToBitmap();
        }

        _showWindow = showWindow;
        _toggleRecording = toggleRecording;
        _isRecording = isRecording;
        _isBusy = isBusy;
        _exit = exit;
        _currentIcon = CreateIcon(0, false);
        _meterStep = -1;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem("Open Verbal", null, (_, _) => _showWindow()));
        _recordingMenuItem = new Forms.ToolStripMenuItem(
            "Start recording",
            null,
            (_, _) => _toggleRecording());
        menu.Items.Add(_recordingMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Exit", null, (_, _) => _exit()));
        menu.Opening += (_, _) => RefreshMenu();

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _currentIcon,
            ContextMenuStrip = menu,
            Text = "Verbal - Ready; double-click to open",
            Visible = true
        };
        _notifyIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                _showWindow();
            }
        };
    }

    public void UpdateInputLevel(float level, bool isRecording)
    {
        var meterStep = isRecording
            ? (int)Math.Round(Math.Clamp(level, 0, 100) / 10)
            : -1;
        if (_recording == isRecording && _meterStep == meterStep)
        {
            return;
        }

        _recording = isRecording;
        _meterStep = meterStep;
        SetIcon(CreateIcon(isRecording ? meterStep / 10f : 0, isRecording));
        _notifyIcon.Text = isRecording
            ? "Verbal - Recording; microphone level shown in icon"
            : "Verbal - Ready; double-click to open";
    }

    public void RefreshMenu()
    {
        var recording = _isRecording();
        _recordingMenuItem.Text = recording ? "Stop recording" : "Start recording";
        _recordingMenuItem.Enabled = !_isBusy();
    }

    private Icon CreateIcon(float level, bool isRecording)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(_baseIcon, 0, 0, 32, 32);

            using var background = new SolidBrush(Color.FromArgb(225, 22, 27, 39));
            using var overlay = RoundedRectangle(new Rectangle(3, 20, 26, 10), 4);
            graphics.FillPath(background, overlay);

            var factors = new[] { 0.56f, 0.82f, 1f, 0.76f, 0.48f };
            using var barBrush = new SolidBrush(
                isRecording ? Color.FromArgb(116, 226, 173) : Color.FromArgb(195, 202, 218));
            for (var index = 0; index < factors.Length; index++)
            {
                var height = 1 + (int)Math.Round(level * factors[index] * 7);
                graphics.FillRectangle(
                    barBrush,
                    new Rectangle(7 + index * 4, 29 - height, 2, height));
            }
        }

        var iconHandle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(iconHandle).Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void SetIcon(Icon icon)
    {
        var previous = _currentIcon;
        _currentIcon = icon;
        _notifyIcon.Icon = icon;
        previous?.Dispose();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        _baseIcon.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);
}
