using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VoxSub.Services;

/// <summary>
/// macOS Dock 图标设置辅助类。
/// 通过原生 libMacDockIcon.dylib 调用 AppKit API，避免 ARM64 上 objc_msgSend 的 P/Invoke ABI 问题。
/// </summary>
internal static class MacDockIcon
{
#if MACOS
    [DllImport("libMacDockIcon.dylib", EntryPoint = "SetDockIcon")]
    private static extern void NativeSetDockIcon([MarshalAs(UnmanagedType.LPUTF8Str)] string iconPath);
#endif

    /// <summary>
    /// 设置 macOS Dock 图标。
    /// </summary>
    /// <param name="iconPath">.icns 文件的绝对路径</param>
    public static void SetDockIcon(string iconPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        if (!File.Exists(iconPath))
            return;

        try
        {
#if MACOS
            NativeSetDockIcon(iconPath);
#endif
        }
        catch
        {
            // 静默失败
        }
    }
}

