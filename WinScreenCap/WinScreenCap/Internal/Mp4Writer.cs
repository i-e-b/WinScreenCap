using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace WinScreenCap.Internal
{
    internal class Mp4Writer : IFrameWriter
    {
        private readonly Size _size;
        private readonly bool _drawCursor;
        private const int FramesPerSecond = 20; // This is assumed in the capture timer and the GifWriter
        private const string ApiCode = "MSMF";
        
        private readonly VideoWriter _writer;
        private readonly Mat _writerFrame;
        private readonly Bitmap _frameImage;
        
        public Mp4Writer(string fileName, Size size, bool drawCursor)
        {
            _size = size;
            _drawCursor = drawCursor;
            var compressionType = VideoWriter.Fourcc('H', '2', '6', '4');

            var backends = CvInvoke.WriterBackends;
            var backendId = (from be in backends where be.Name?.Equals(ApiCode)??false select be.ID).FirstOrDefault();
            
            _writerFrame = new Mat(size, DepthType.Cv8U, 3);
            _frameImage = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb);
            _writer = new VideoWriter(fileName, backendId, compressionType, FramesPerSecond, size, isColor: true);
        }

        public void WriteScreenFrame(Point topLeft)
        {
            using var g = Graphics.FromImage(_frameImage);
            g.CopyFromScreen(topLeft, Point.Empty, _size);
            
            if (_drawCursor && Cursor.Current is not null) { // this doesn't draw the correct cursor, but at least it shows *something*
                var pt = new Point(Cursor.Position.X - topLeft.X - Cursor.Current.HotSpot.X, Cursor.Position.Y - topLeft.Y - Cursor.Current.HotSpot.Y);
                var rect = new Rectangle(pt, Cursor.Current.Size);
                Cursor.Current.Draw(g, rect);
            }
            
            CopyFrame(_frameImage, _writerFrame);
            _writer.Write(_writerFrame);
        }
        
        private static unsafe void CopyFrame(Bitmap? src, Mat? dst)
        {
            if (src == null || dst == null) return;
            var bits = src.LockBits(new Rectangle(0,0, src.Width, src.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                var length = bits.Stride * bits.Height;
                Buffer.MemoryCopy(bits.Scan0.ToPointer()!, dst.DataPointer.ToPointer()!, length, length);
            }
            finally
            {
                src.UnlockBits(bits);
            }
        }

        public void Dispose()
        {
            _writer.Dispose();
            _writerFrame.Dispose();
            _frameImage.Dispose();
        }
    }
}