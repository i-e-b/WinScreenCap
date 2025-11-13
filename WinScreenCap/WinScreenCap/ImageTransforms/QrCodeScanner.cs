using System.Drawing;
using System.Drawing.Imaging;
using ZXing;

namespace WinScreenCap.ImageTransforms;

/// <summary>
/// Tries to read QR codes from the screen
/// </summary>
public static class QrCodeScanner
{
    private static Bitmap? _screenBuffer;

    /// <summary>
    /// Try to scan the screen capture area for a barcode.
    /// Returns the text of the code, or <c>null</c> if no code is found.
    /// </summary>
    public static string? TryScan(Point topLeft, Size size)
    {
        if (_screenBuffer is null || _screenBuffer.Width < size.Width || _screenBuffer.Height < size.Height)
        {
            _screenBuffer?.Dispose();
            _screenBuffer = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        }

        using (var g = Graphics.FromImage(_screenBuffer))
        {
            g.CopyFromScreen(topLeft, Point.Empty, size);
        }

        GetLuminanceFromBitmap(_screenBuffer, out var luminance);

        var reader = new BarcodeReaderGeneric();
        reader.Options.TryHarder = true;
        reader.Options.PossibleFormats = [BarcodeFormat.CODE_128, BarcodeFormat.CODE_39, BarcodeFormat.QR_CODE, BarcodeFormat.DATA_MATRIX];

        // First try without anything special:
        {
            var lumSrc = new PlanarYUVLuminanceSource(luminance, size.Width, size.Height, 0, 0, size.Width, size.Height, false);
            var result = reader.Decode(lumSrc);
            if (result is not null) return result.Text;
        }

        // do our own thresholding
        for (var scale = 4; scale > 1; scale--)
        {
            for (var exposure = -4; exposure < 12; exposure += 2)
            {
                var matrix = UnsharpThreshold.Matrix(luminance, size.Width, size.Height, false, scale, exposure);

                var lumSrc = new PlanarYUVLuminanceSource(matrix, size.Width, size.Height, 0, 0, size.Width, size.Height, false);

                var result = reader.Decode(lumSrc);

                if (result is not null) return result.Text;
            }
        }

        return null;
    }

    private static unsafe void GetLuminanceFromBitmap(Bitmap src, out byte[] luminance)
    {
        var ri      = new Rectangle(Point.Empty, src.Size);
        var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var len     = srcData.Height * srcData.Width;
        luminance = new byte[len];
        try
        {
            var s = (uint*)srcData.Scan0;
            for (var i = 0; i < len; i++)
            {
                var c = s[i];
                _ = (int) ((c >> 24) & 0xff);
                var r   = (int) ((c >> 16) & 0xff);
                var g   = (int) ((c >>  8) & 0xff);
                var b   = (int) ((c      ) & 0xff);
                var y   = r - b;
                var tmp = b + (y / 2);
                var z   = g - tmp;

                luminance[i] = Clamp(tmp + (z / 2));
            }
        }
        finally
        {
            src.UnlockBits(srcData);
        }
    }

    private static byte Clamp(int v)
    {
        if (v < 0) return 0;
        if (v > 255) return 255;
        return (byte)v;
    }
}