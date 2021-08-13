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
            _writer = new VideoWriter(fileName, backendId, compressionType, FramesPerSecond, size,
                new Tuple<VideoWriter.WriterProperty, int>(VideoWriter.WriterProperty.Quality, 50), // this does nothing. https://github.com/opencv/opencv/issues/8961
                new Tuple<VideoWriter.WriterProperty, int>(VideoWriter.WriterProperty.IsColor,1)
            );
        }

        public void WriteScreenFrame(Point topLeft)
        {
            using var g = Graphics.FromImage(_frameImage);
            g.CopyFromScreen(topLeft, Point.Empty, _size);
            
            if (_drawCursor) { WindowsCursor.Draw(g,topLeft); }
            
            CopyFrame(_frameImage, _writerFrame);
            _writer.Write(_writerFrame);
        }
        
        private static unsafe void CopyFrame(Bitmap? src, Mat? dst)
        {
            if (src == null || dst == null) return;
            var srcBitmapData = src.LockBits(new Rectangle(0,0, src.Width, src.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
            try
            {
                // Get the pointer to the image bits.
                PBits(srcBitmapData, srcBitmapData.Scan0, srcBitmapData.Height, out var srcBits, out var srcStride);
                var width = Math.Min(srcBitmapData.Width, dst.Width);
                var height = Math.Min(srcBitmapData.Height, dst.Height);

                for (int y = 0; y < height; y++)
                {
                    var srcBase = srcBits + (srcStride * y);
                    var dstBase = (byte*)dst.DataPointer.ToPointer() + (dst.Width * y * 3  /*bytes per pixel*/);
                    for (int x = 0; x < width; x++)
                    {
                        *dstBase++ = *srcBase++;
                        *dstBase++ = *srcBase++;
                        *dstBase++ = *srcBase++;
                        srcBase++;
                    }
                }
            }
            finally
            {
                src.UnlockBits(srcBitmapData);
            }
        }
        
        private static unsafe void PBits(BitmapData srcBitmapData, IntPtr srcPixels, int height, out byte* pBits, out uint stride)
        {
            // This is the unsafe operation.
            if (srcBitmapData.Stride > 0)
                pBits = (byte*)srcPixels.ToPointer();
            else
                // If the Stride is negative, Scan0 points to the last 
                // scanline in the buffer. To normalize the loop, obtain
                // a pointer to the front of the buffer that is located 
                // (Height-1) scan-lines previous.
                pBits = (byte*)srcPixels.ToPointer() + srcBitmapData.Stride * (height - 1);
            stride = (uint)Math.Abs(srcBitmapData.Stride);
        }

        public void Dispose()
        {
            _writer.Dispose();
            _writerFrame.Dispose();
            _frameImage.Dispose();
        }
    }
}