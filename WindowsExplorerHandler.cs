using BepInEx.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TarkinItemExporter
{
    internal class WindowsExplorerHandler
    {
        private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(WindowsExplorerHandler));

        public static void OpenOrFlash(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            DirectoryInfo dirInfo = new DirectoryInfo(folderPath);
            string folderName = dirInfo.Name;

            try
            {
                IntPtr hWnd = FindWindow("CabinetWClass", folderName);
                if (hWnd == IntPtr.Zero) 
                    hWnd = FindWindow("CabinetWClass", folderPath); // user's windows explorer might be set to display full path in title

                if (hWnd != IntPtr.Zero)
                {
                    FlashWindow(hWnd);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private static void FlashWindow(IntPtr hWnd)
        {
            FLASHWINFO fInfo = new FLASHWINFO();
            fInfo.cbSize = (uint)Marshal.SizeOf(fInfo);
            fInfo.hwnd = hWnd;
            fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
            fInfo.uCount = uint.MaxValue;
            fInfo.dwTimeout = 0;

            FlashWindowEx(ref fInfo);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_ALL = 3;        // Caption + Tray
        private const uint FLASHW_TIMERNOFG = 12; // Flash until window comes to foreground
    }
}
