namespace WinScreenCap.Win32
{
    internal enum WindowStack : uint
    {
        BelowTarget = 2, //GW_HWNDNEXT
        AboveTarget = 3  //GW_HWNDPREV
    }
}