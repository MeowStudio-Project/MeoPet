using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace MeoPet
{
    internal static class AppRemovalService
    {
        private const string MutexName = @"Local\MeoPet.DesktopPet";
        private const uint DeleteOperation = 3;
        private const ushort AllowUndo = 0x0040;
        private const ushort NoConfirmation = 0x0010;
        private const ushort Silent = 0x0004;
        private const ushort NoErrorUi = 0x0400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FileOperation
        {
            public IntPtr Window;
            public uint Operation;
            public IntPtr From;
            public IntPtr To;
            public ushort Flags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool Aborted;
            public IntPtr NameMappings;
            public IntPtr ProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref FileOperation operation);

        public static void RemoveAfterExit(string installDirectory, string helperDirectory)
        {
            ValidateInstallDirectory(installDirectory);
            ValidateHelperDirectory(helperDirectory);

            using (Mutex appMutex = new Mutex(false, MutexName))
            {
                bool ownsMutex;
                try
                {
                    ownsMutex = appMutex.WaitOne(TimeSpan.FromMinutes(3));
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                }

                if (!ownsMutex)
                {
                    throw new TimeoutException("等待 MeoPet 關閉逾時，無法刪除應用。");
                }

                MoveToRecycleBin(installDirectory);
            }

            ScheduleHelperCleanup(helperDirectory);
        }

        private static void MoveToRecycleBin(string installDirectory)
        {
            IntPtr source = Marshal.StringToHGlobalUni(installDirectory + "\0");
            try
            {
                FileOperation operation = new FileOperation();
                operation.Operation = DeleteOperation;
                operation.From = source;
                operation.Flags = AllowUndo | NoConfirmation | Silent | NoErrorUi;

                int result = SHFileOperation(ref operation);
                if (result != 0)
                {
                    throw new Win32Exception(result, "無法將 MeoPet 移至資源回收筒。");
                }

                if (operation.Aborted)
                {
                    throw new IOException("移至資源回收筒的作業已取消。");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(source);
            }
        }

        private static void ValidateInstallDirectory(string installDirectory)
        {
            string expected = Path.GetFullPath(AppInstaller.GetInstallDirectory())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string actual = Path.GetFullPath(installDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(actual))
            {
                throw new InvalidDataException("MeoPet 安裝路徑驗證失敗，未移動任何檔案。");
            }
        }

        private static void ValidateHelperDirectory(string helperDirectory)
        {
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string actual = Path.GetFullPath(helperDirectory);
            string runningDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!actual.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(
                    actual.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    runningDirectory,
                    StringComparison.OrdinalIgnoreCase) ||
                !new DirectoryInfo(actual).Name.StartsWith(
                    "MeoPet-remove-",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("MeoPet 刪除輔助程式路徑驗證失敗。");
            }
        }

        private static void ScheduleHelperCleanup(string helperDirectory)
        {
            ProcessStartInfo cleanup = new ProcessStartInfo();
            cleanup.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            cleanup.Arguments =
                "/d /c ping 127.0.0.1 -n 4 > nul & rd /s /q \"" +
                helperDirectory +
                "\"";
            cleanup.CreateNoWindow = true;
            cleanup.UseShellExecute = false;
            cleanup.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(cleanup);
        }
    }
}
