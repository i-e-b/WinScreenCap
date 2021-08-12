using System;
using System.Drawing;

namespace WinScreenCap.Internal
{
    internal interface IFrameWriter: IDisposable
    {
        void WriteScreenFrame(Point point);
    }
}