using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace WinScreenCap.Internal
{
    public class ImageQuantise
    {
        private readonly Bitmap _dst;
        private readonly Dictionary<int,byte> _cache = new();
        private readonly int[] _argbPalette;

        public ImageQuantise(Bitmap dst)
        {
            _dst = dst;
            _argbPalette = Normalise(dst.Palette.Entries);
        }

        public void RescaleImage(Bitmap src)
        {
            var width = src.Width;
            var height = src.Height;

            // Lock a rectangular portion of the bitmap for writing.
            var rect = new Rectangle(0, 0, width, height);

            var srcBitmapData = src.LockBits(
                rect,
                ImageLockMode.ReadWrite,
                src.PixelFormat);
            var srcPixels = srcBitmapData.Scan0;
            
            var dstBitmapData = _dst.LockBits(
                rect,
                ImageLockMode.ReadOnly,
                _dst.PixelFormat);
            var dstPixels = dstBitmapData.Scan0;
            
            unsafe
            {
                // Get the pointer to the image bits.
                PBits(srcBitmapData, srcPixels, height, out var srcBits, out var srcStride);
                PBits(dstBitmapData, dstPixels, height, out var dstBits, out var dstStride);

                for (int y = 0; y < height; y++)
                {
                    var srcBase = srcBits + (srcStride*y);
                    var dstBase = dstBits + (dstStride*y);
                    for (int x = 0; x < width; x++)
                    {
                        var b = *srcBase++;
                        var g = *srcBase++;
                        var r = *srcBase++;
                        srcBase++;
                    
                        var index = FindBestMatch(r,g,b);
                    
                        *dstBase++ = index;
                    }
                }

            } /* end unsafe */

            // To commit the changes, unlock the portion of the bitmap.  
            src.UnlockBits(srcBitmapData);
            _dst.UnlockBits(dstBitmapData);
        }

        private int[] Normalise(Color[] colors)
        {
            var outp = new int[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                outp[i] = colors[i].ToArgb() & 0x00ffffff;
            }
            return outp;
        }

        /// <summary>
        /// Really dumb search
        /// </summary>
        private byte FindBestMatch(int r, int g, int b)
        {
            var key = (r<<16)|(g<<8)|(b);
            if (_cache.ContainsKey(key)) return _cache[key];
            
            var bestError = int.MaxValue;
            var bestIndex = 0;
            for (int i = 0; i < _argbPalette.Length; i++)
            {
                var pr = (_argbPalette[i]>>16)&0xff;
                var pg = (_argbPalette[i]>>8)&0xff;
                var pb = (_argbPalette[i]>>0)&0xff;
                
                var dr = (pr-r)*(pr-r);
                var dg = (pg-g)*(pg-g);
                var db = (pb-b)*(pb-b);
                
                var err = dr + dg + db;

                if (err < bestError)
                {
                    bestIndex = i;
                    bestError = err;
                }
            }
            _cache.Add(key, (byte)bestIndex);
            return (byte)bestIndex;
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
    }
}
