using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Security;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Xml.Serialization;

namespace MeoPet
{
    internal static class Program
    {
        public const string Version = "1.1.4";

        private static readonly string NameFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "meopet",
            "pet-name.txt");
        private static readonly string SettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "meopet",
            "settings.xml");

        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null &&
                args.Length == 3 &&
                String.Equals(args[0], "--remove-app", StringComparison.Ordinal))
            {
                try
                {
                    AppRemovalService.RemoveAfterExit(args[1], args[2]);
                }
                catch (IOException ex)
                {
                    ShowRemovalError(ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    ShowRemovalError(ex.Message);
                }
                catch (SecurityException ex)
                {
                    ShowRemovalError(ex.Message);
                }
                catch (InvalidDataException ex)
                {
                    ShowRemovalError(ex.Message);
                }
                catch (COMException ex)
                {
                    ShowRemovalError(ex.Message);
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    ShowRemovalError(ex.Message);
                }
                catch (TimeoutException ex)
                {
                    ShowRemovalError(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    ShowRemovalError(ex.Message);
                }
                catch (ArgumentException ex)
                {
                    ShowRemovalError(ex.Message);
                }
                catch (Win32Exception ex)
                {
                    ShowRemovalError(ex.Message);
                }

                return;
            }

            if (args != null &&
                args.Length == 3 &&
                String.Equals(args[0], "--apply-update", StringComparison.Ordinal))
            {
                try
                {
                    UpdateService.ApplyStagedUpdate(args[1], args[2]);
                }
                catch (IOException ex)
                {
                    ShowUpdateError(ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    ShowUpdateError(ex.Message);
                }
                catch (InvalidDataException ex)
                {
                    ShowUpdateError(ex.Message);
                }
                catch (TimeoutException ex)
                {
                    ShowUpdateError(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    ShowUpdateError(ex.Message);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    ShowUpdateError(ex.Message);
                }

                return;
            }

            const string mutexName = @"Local\MeoPet.DesktopPet";
            bool waitForLauncher = args != null &&
                args.Length == 1 &&
                String.Equals(args[0], "--wait-for-launcher", StringComparison.Ordinal);
            bool firstInstance = true;
            Mutex appMutex;

            if (waitForLauncher)
            {
                appMutex = new Mutex(false, mutexName);
                bool ownsMutex;
                try
                {
                    ownsMutex = appMutex.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                }

                firstInstance = ownsMutex;
            }
            else
            {
                appMutex = new Mutex(true, mutexName, out firstInstance);
            }

            using (Mutex mutex = appMutex)
            {
                if (!firstInstance)
                {
                    return;
                }

                EnablePerMonitorDpi();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += ShowApplicationError;

                if (!AppInstaller.EnsureInstalled())
                {
                    return;
                }

                PetSettings settings;
                try
                {
                    settings = PetSettings.Load(SettingsFile);
                    settings.AutoStart = StartupSettings.IsEnabled();
                }
                catch (IOException ex)
                {
                    MessageBox.Show(
                        "無法讀取寵物設定：" + Environment.NewLine + ex.Message,
                        "MeoPet",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(
                        "無法讀取寵物設定：" + Environment.NewLine + ex.Message,
                        "MeoPet",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show(
                        "沒有權限讀取寵物設定：" + Environment.NewLine + ex.Message,
                        "MeoPet",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                catch (SecurityException ex)
                {
                    MessageBox.Show(
                        "沒有權限讀取 Windows 開機啟動設定：" + Environment.NewLine + ex.Message,
                        "MeoPet",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                SpeechContent speech;
                try
                {
                    speech = SpeechContent.Load();
                }
                catch (IOException ex)
                {
                    MessageBox.Show(
                        "無法讀取寵物對話 JSON：" + Environment.NewLine + ex.Message,
                        "MeoPet",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(
                        "寵物對話 JSON 格式錯誤：" + Environment.NewLine + ex.Message,
                        "MeoPet",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                string name;
                try
                {
                    name = File.Exists(NameFile)
                        ? File.ReadAllText(NameFile, Encoding.UTF8).Trim()
                        : String.Empty;
                }
                catch (IOException ex)
                {
                    MessageBox.Show(
                        "無法讀取寵物名字：" + ex.Message,
                        "MeoPet",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show(
                        "沒有權限讀取寵物名字：" + ex.Message,
                        "MeoPet",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                if (String.IsNullOrWhiteSpace(name))
                {
                    using (NameDialog dialog = new NameDialog(String.Empty))
                    {
                        if (dialog.ShowDialog() != DialogResult.OK)
                        {
                            return;
                        }

                        name = dialog.PetName;
                    }

                    try
                    {
                        PetStorage.SaveName(NameFile, name);
                    }
                    catch (IOException ex)
                    {
                        MessageBox.Show(
                            "無法儲存寵物名字：" + ex.Message,
                            "MeoPet",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        MessageBox.Show(
                            "沒有權限儲存寵物名字：" + ex.Message,
                            "MeoPet",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return;
                    }
                }

                Application.Run(new PetForm(name, NameFile, SettingsFile, settings, speech));
            }
        }

        private static void ShowApplicationError(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show(
                "寵物顯示發生錯誤：" + Environment.NewLine + e.Exception,
                "MeoPet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void ShowUpdateError(string details)
        {
            MessageBox.Show(
                "無法完成 MeoPet 更新：" + Environment.NewLine + details,
                "MeoPet 更新",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void ShowRemovalError(string details)
        {
            MessageBox.Show(
                "無法將 MeoPet 移至資源回收筒：" + Environment.NewLine + details,
                "刪除 MeoPet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void EnablePerMonitorDpi()
        {
            try
            {
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4)))
                {
                    SetProcessDPIAware();
                }
            }
            catch (EntryPointNotFoundException)
            {
                try
                {
                    SetProcessDpiAwareness(2);
                }
                catch (EntryPointNotFoundException)
                {
                    SetProcessDPIAware();
                }
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int awareness);
    }
}
