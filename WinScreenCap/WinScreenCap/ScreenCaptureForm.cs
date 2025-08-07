using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
using WinScreenCap.Internal;

namespace WinScreenCap
{
    public class ScreenCaptureForm : OverlayForm
    {
        private IFrameWriter? _outputFile;
        private bool _disposed;
        private readonly Timer _recordingTimer;
        private const int Fps20 = 50;//ms
        private ButtonHover _buttonHover;

        public ScreenCaptureForm()
        {
            _disposed = false;
            _recordingTimer = new Timer { Enabled = false, Interval = Fps20 };
            _recordingTimer.Tick += _recordingTimer_Tick;

            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            SetBounds(20, 200, 320, 240);
            UpdateOverlay();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                e.SuppressKeyPress = true;
                AddFrame();
            }
        }

        private void _recordingTimer_Tick(object? sender, EventArgs e)
        {
            _outputFile?.WriteScreenFrame(new Point(Left + 11, Top + 11));
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_disposed) return;
            _disposed = true;
            _outputFile?.Dispose();
            base.OnClosed(e);
            Application.Exit();
        }

        private void ChooseFile()
        {
            if (_recordingTimer.Enabled) return;

            if (_outputFile != null)
            {
                _outputFile?.Dispose();
                _outputFile = null;
                UpdateOverlay();
                return;
            }

            var dialog = new SaveFileDialog();
            dialog.OverwritePrompt = true;
            dialog.DefaultExt = "gif";
            dialog.AddExtension = true;
            dialog.Filter = "All files|*.*|GIF files|*.gif|MP4 Video files|*.mp4";
            var result = dialog.ShowDialog();
            
            switch (result) {
                case DialogResult.OK:
                case DialogResult.Yes:
                    {
                        _outputFile?.Dispose();
                        _outputFile = null;
                        
                        if (!string.IsNullOrWhiteSpace(dialog.FileName))
                        {
                            var fileExt = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                            
                            _outputFile = fileExt switch
                            {
                                ".gif" => new GifWriter(dialog.FileName, new Size(Width - 21, Height - 31), true),
                                ".mp4" => new Mp4Writer(dialog.FileName, new Size(Width - 21, Height - 31), true),
                                _ => null
                            };

                            UpdateOverlay();
                        }
                        break;
                    }
                default:
                    return;
            }
        }

        private void AddFrame()
        {
            if (_recordingTimer.Enabled) return;
            _outputFile?.WriteScreenFrame(new Point(Left + 11, Top + 11));
        }

        private void ToggleRecording()
        {
            _recordingTimer.Enabled = !_recordingTimer.Enabled;
            UpdateOverlay();
        }

        protected override void OnActivated(EventArgs e)
        {
            UpdateOverlay();
            base.OnActivated(e);
        }

        private void UpdateOverlay()
        {
            TopMost = true;
            TopLevel = true;
            BringToFront();
            using (var bitmap = DrawInner())
            {
                SetBitmap(bitmap);
            }
        }

        private Bitmap DrawInner()
        {
            var b = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.FromArgb(0, 0, 0, 0));
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;

                //Solid edge bars
                g.FillRectangle(Brushes.Beige, 0, 0, 10, Height);
                g.FillRectangle(Brushes.Beige, Width - 10, 0, Width, Height);
                g.FillRectangle(Brushes.Beige, 0, 0, Width, 10);
                g.FillRectangle(Brushes.Beige, 0, Height - 20, Width, Height);
                
                // outline of solid edge bars
                g.DrawRectangle(Pens.Black, 0,0,Width-1,Height-1);
                g.DrawRectangle(Pens.Black, 10,10,Width-20,Height-30);

                // resize chevrons
                if (_outputFile == null)
                {
                    g.DrawLine(Pens.Black, Width, Height - 18, Width - 18, Height);
                    g.DrawLine(Pens.Black, Width, Height - 14, Width - 14, Height);
                    g.DrawLine(Pens.Black, Width, Height - 10, Width - 10, Height);
                    g.DrawLine(Pens.Black, Width, Height - 6, Width - 6, Height);
                }

                // close cross
                g.DrawLine(Pens.Black, 2, 2, 9, 9);
                g.DrawLine(Pens.Black, 2, 9, 9, 2);

                // Set file icon
                DrawFileIcon(g, 10, Height - 17);

                // record icon
                DrawRecordIcon(g, 25, Height - 17);

                // snap frame icon
                DrawSnapFrameIcon(g, 40, Height - 17);
            }
            return b;
        }

        private void DrawSnapFrameIcon(Graphics g, int x, int y)
        {
            var disallowed = _outputFile is null || _recordingTimer.Enabled;
            
            if (_buttonHover == ButtonHover.AddFrame)
            {
                if (!disallowed) g.FillRectangle(Brushes.Tan, x,y, 12,12);
                var msg = "Add single frame (ctrl-C)";
                if (_recordingTimer.Enabled) msg += " (stop recording first)";
                if (_outputFile is null) msg += " (choose output first)";
                DrawHintText(g, msg, y);
            }

            var p = disallowed ? Pens.DarkGray : Pens.Black;
            var b = disallowed ? Brushes.DarkGray : Brushes.Black;
            g.DrawRectangle(p, x, y, 7, 7);
            
            g.DrawLine(p, x+11, y, x+11, y+11);
            g.DrawLine(p, x, y + 11, x + 11, y + 11);
            g.DrawLine(p, x + 9, y, x + 9, y + 9);
            g.DrawLine(p, x, y + 9, x + 9, y + 9);

            g.FillRectangle(b, x+2.5f, y, 2, 7);
            g.FillRectangle(b, x, y+2.5f, 7, 2);
        }

        private void DrawRecordIcon(Graphics g, int x, int y)
        {
            var disallowed = _outputFile is null;
            
            if (_buttonHover == ButtonHover.ToggleRecord)
            {
                if (!disallowed) g.FillRectangle(Brushes.Tan, x,y, 12,12);
                var msg = _recordingTimer.Enabled ? "Stop recording" : "Start recording";
                if (_outputFile is null) msg += " (choose output first)";
                DrawHintText(g, msg, y);
            }
            
            var p = disallowed ? Pens.DarkGray : Pens.Black;
            var b1 = disallowed ? Brushes.DarkGray : Brushes.Black;
            var b2 = disallowed ? Brushes.DarkGray : Brushes.Red;
            
            g.DrawRectangle(p, x, y, 11, 11);
            if (_recordingTimer.Enabled) g.FillRectangle(b1, x+2, y+2, 7, 7);
            else g.FillEllipse(b2, x+2, y+2, 7, 7);
        }

        private void DrawFileIcon(Graphics g, int x, int y)
        {
            if (_outputFile == null) DrawChooseFileIcon(g,x,y);
            else DrawCloseFileIcon(g,x,y);
        }

        private void DrawChooseFileIcon(Graphics g, int x, int y) {
            var disallowed = _recordingTimer.Enabled;
            
            if (_buttonHover == ButtonHover.File)
            {
                if (!disallowed) g.FillRectangle(Brushes.Tan, x,y, 12,12);
                var msg = "Choose output file";
                if (_recordingTimer.Enabled) msg += " (stop recording first)";
                DrawHintText(g, msg, y);
            }
            
            var p = disallowed ? Pens.DarkGray : Pens.Black;
            
            g.DrawLine(p, x    , y     , x + 6, y);
            g.DrawLine(p, x + 9, y + 3 , x + 9, y + 11);
            g.DrawLine(p, x + 9, y + 11, x    , y + 11);
            g.DrawLine(p, x    , y + 11, x    , y);

            g.DrawLine(p, x + 6, y  , x + 6, y + 3);
            g.DrawLine(p, x + 6, y  , x + 9, y + 3);
            g.DrawLine(p, x + 6, y+3, x + 9, y + 3);
        }
        
        private void DrawCloseFileIcon(Graphics g, int x, int y) {
            var disallowed = _recordingTimer.Enabled;
            
            if (_buttonHover == ButtonHover.File)
            {
                if (!disallowed) g.FillRectangle(Brushes.Tan, x,y, 12,12);
                var msg = "Close output file";
                if (disallowed) msg += " (stop recording first)";
                DrawHintText(g, msg, y);
            }
            
            var p = disallowed ? Pens.DarkGray : Pens.Black;
            
            g.DrawLine(p, x    , y     , x + 6, y);
            g.DrawLine(p, x + 9, y + 3 , x + 9, y + 11);
            g.DrawLine(p, x + 9, y + 11, x    , y + 11);
            g.DrawLine(p, x    , y + 11, x    , y);

            g.DrawLine(p, x + 6, y  , x + 6, y + 3);
            g.DrawLine(p, x + 6, y  , x + 9, y + 3);
            g.DrawLine(p, x + 6, y+3, x + 9, y + 3);
            
            g.DrawLine(p, x    , y  , x + 11, y + 11);
            g.DrawLine(p, x +11, y  , x     , y + 11);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            UpdateOverlay();
        }
        protected override void OnResize(EventArgs e)
        {
            UpdateOverlay();
        }

        private static bool InRect(Point p, int l, int t, int r, int b)
        {
            return p.X >= l && p.X <= r && p.Y >= t && p.Y <= b;
        }

        protected override void WndProc(ref Message m)
        {
            if (_disposed) return;
            switch (m.Msg)
            {
                case Win32.Win32.WM_NCHITTEST: // screen-space coords
                    {
                        var c = PointToClient(GetPoint(m.LParam));
                        var sizeEnabled = _outputFile == null; // disable resize once we start a file

                        if (sizeEnabled && c.X > Width - 10 && c.Y > Height - 10)
                        {
                            m.Result = Win32.Win32.HTBOTTOMRIGHT;
                            MouseOver(ButtonHover.None);
                        }
                        else if (
                            InRect(c, 0, 0, 10, 10) ||
                            InRect(c, 10, Height - 17, 22, Height) ||
                            InRect(c, 25, Height - 17, 37, Height) ||
                            InRect(c, 40, Height - 17, 52, Height)
                        )
                        {
                            m.Result = Win32.Win32.HTCLIENT; // If you don't return HTCLIENT, you won't get the button events
                            MouseOver(ButtonHover.None);
                        }
                        else
                        {
                            m.Result = Win32.Win32.HTCAPTION;
                            MouseOver(ButtonHover.None);
                        }

                        return;
                    }
                case Win32.Win32.WM_LBUTTONUP: // client-space coords
                    {
                        var c = GetPoint(m.LParam);
                        if (InRect(c,0,0,10,10)) { 
                            Close();
                            return;
                        }
                        
                        if (InRect(c, 10, Height - 17, 22, Height)) { ChooseFile(); }
                        else if (InRect(c, 25, Height - 17, 37, Height)) { ToggleRecording(); }
                        else if (InRect(c, 40, Height - 17, 52, Height)) { AddFrame(); }

                        base.WndProc(ref m);
                        return;
                    }
                case Win32.Win32.WM_MOUSEMOVE:
                {
                    var c = GetPoint(m.LParam);
                    
                    if (InRect(c, 10, Height - 17, 22, Height)) { MouseOver(ButtonHover.File); }
                    else if (InRect(c, 25, Height - 17, 37, Height)) { MouseOver(ButtonHover.ToggleRecord); }
                    else if (InRect(c, 40, Height - 17, 52, Height)) { MouseOver(ButtonHover.AddFrame); }
                    else { MouseOver(ButtonHover.None); }

                    base.WndProc(ref m);
                    return;
                }
                case Win32.Win32.WM_MOUSELEAVE:
                case Win32.Win32.WM_NCMOUSELEAVE:
                {
                    MouseOver(ButtonHover.None);
                    base.WndProc(ref m);
                    return;
                }
                default:
                    base.WndProc(ref m);
                    return;
            }
        }

        private void DrawHintText(Graphics g, string msg, int y)
        {
            var width = g.MeasureString(msg, Font).Width;
            g.DrawString(msg, Font, Brushes.Black, Width - width - 10, y);
        }
        private void MouseOver(ButtonHover state)
        {
            if (_buttonHover == state) return;
            _buttonHover = state;
            Invalidate();
        }

        private static Point GetPoint(IntPtr packed)
        {
            int x = (short)(packed.ToInt32() & 0x0000FFFF);
            int y = (short)((packed.ToInt32() & 0xFFFF0000) >> 16);
            return new Point(x, y);
        }
    }

    internal enum ButtonHover
    {
        None, File, ToggleRecord, AddFrame
    }
}
