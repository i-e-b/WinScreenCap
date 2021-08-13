using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinScreenCap.Internal
{
    internal static class WindowsCursor
    {
        private static readonly Brush _backFill = new SolidBrush(Color.FromArgb(80,0,0,0));
        
        public static void Draw(Graphics g, Point topLeft)
        {
            // Try to get the correct cursor (the hard way)
            // This is not perfect, but works better than the built-in dotnet call.
            if (TryGetHCursor(out var cursor) && cursor is not null)
            {
                var hh = cursor.Size.Height/2;
                var hw = cursor.Size.Width/2;
                var cursX = Cursor.Position.X - topLeft.X;
                var cursY = Cursor.Position.Y - topLeft.Y;
                var pt = new Point(cursX - cursor.HotSpot.X, cursY - cursor.HotSpot.Y);

                // dotnet Cursor.Draw doesn't handle XOR cursors properly -- they come out white.
                // so we hack it by drawing an oval behind the cursor
                g.FillEllipse(_backFill, cursX - hw, cursY - hh, cursor.Size.Width, cursor.Size.Height);
                
                var rect = new Rectangle(pt, cursor.Size);
                cursor.Draw(g, rect);
                cursor.Dispose();
                return;
            }

            // this doesn't draw the correct cursor, but at least it shows *something*
            if (Cursor.Current is not null) {
                var pt = new Point(Cursor.Position.X - topLeft.X - Cursor.Current.HotSpot.X, Cursor.Position.Y - topLeft.Y - Cursor.Current.HotSpot.Y);
                var rect = new Rectangle(pt, Cursor.Current.Size);
                Cursor.Current.Draw(g, rect);
            }
        }

        private static bool TryGetHCursor(out Cursor? cursor)
        {
            cursor = null;
            if (!Win32.Win32.GetCursorPos(out var point)) { return false; }
            
            var hWnd = Win32.Win32.WindowFromPoint(point);
            if (hWnd == IntPtr.Zero) return false;
            
            var targetThreadId = Win32.Win32.GetWindowThreadProcessId(hWnd, out _);
            var currentThreadId = Win32.Win32.GetCurrentThreadId();

            if (targetThreadId != currentThreadId)
            {
                var ok = Win32.Win32.AttachThreadInput(currentThreadId, targetThreadId, true);
                if (!ok) return false;
                
                cursor = Cursor.Current;
                
                Win32.Win32.AttachThreadInput(currentThreadId, targetThreadId, false);
            }
            else
            {
                cursor = Cursor.Current;
            }
            
            return true;
        }
    }
}