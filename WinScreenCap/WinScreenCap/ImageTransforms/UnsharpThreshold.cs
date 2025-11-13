using System;

namespace WinScreenCap.ImageTransforms;

/// <summary>
/// Threshold images into 2 color bitmaps.
/// This is used for bar-code/matrix-codes.
/// </summary>
public static class UnsharpThreshold
{
    private const int UpperLimit = 240;
    private const int LowerLimit = 16;

    private static int[]  columns = [];
    private static byte[] results = [];

    /// <summary>
    /// Threshold an entire bitmap, returning a new bitmap
    /// </summary>
    /// <param name="src">Input luminance map</param>
    /// <param name="height">height of the source</param>
    /// <param name="invert">If <c>true</c> the output bitmap will be inverted</param>
    /// <param name="scale">Target scale of features. Adjust to pick out different detail levels. Range 1..8 inclusive. 5 or 6 are good defaults</param>
    /// <param name="exposure">Negative for lighter image, positive for darker. Zero is no bias. Between -16 and 16 seem to work in most cases</param>
    /// <param name="width">width of source</param>
    public static byte[] Matrix(byte[] src, int width, int height, bool invert, int scale, int exposure)
    {
        if (columns.Length < src.Length) columns = new int[src.Length + 32];
        if (results.Length < src.Length) results = new byte[src.Length + 32];

        byte white = 255;
        byte black = 0;

        if (invert)
        {
            white = 0;
            black = 255;
        }

        var radius = 1 << scale;
        var diam   = scale + 1;
        var right  = width - 1;
        var span   = width * radius;
        var leadIn = (-radius) * width;

        for (var x = 0; x < width; x++) { // for each column
            var sum = 0;

            // feed in
            var row = leadIn;
            for (var i = -radius; i < radius; i++) {
                var y = Math.Max(row, 0);
                sum += src[y + x] & 0xFF;
                row += width;
            }

            var end = src.Length - x - 1;
            row = 0;
            for (var y = 0; y < height; y++) {
                columns[row + x] = sum >> diam;

                // update running average
                var yr       = Math.Min(row + span, end);
                var yl       = Math.Max(row - span, 0);
                var incoming = src[yr + x];
                var outgoing = src[yl + x];

                sum += (int)incoming - (int)outgoing;
                row += width;
            }
        }

        for (var y = 0; y < height; y++) { // for each scanline
            var yOff = y * width;
            var sum = 0;

            // feed in
            for (var i = -radius; i < radius; i++) {
                var x = Math.Max(i, 0);
                sum += columns[yOff + x];
            }

            // running average threshold
            for (var x = 0; x < width; x++) {
                // calculate threshold values
                var actual = (src[yOff + x] & 0xFF) - exposure;
                var target = sum >>> diam;

                // don't let the target be too extreme
                if (target > UpperLimit) target = UpperLimit;
                if (target < LowerLimit) target = LowerLimit;

                // Decide what side of the threshold we are on
                results[yOff + x] = actual < target ? black : white;

                // update running average
                var xr = Math.Min(x + radius, right);
                var xl = Math.Max(x - radius, 0);
                var incoming = columns[yOff + xr];
                var outgoing = columns[yOff + xl];

                sum += incoming - outgoing;
            }
        }

        return results;
    }
}