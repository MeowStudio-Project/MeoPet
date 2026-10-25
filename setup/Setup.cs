using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;

namespace MeoPet
{
    internal static class SetupProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly Label _status = new Label();
        private readonly ProgressBar _progress = new ProgressBar();

        public SetupForm()
        {
            Text = "安裝 MeoPet";
            ClientSize = new Size(390, 125);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft JhengHei UI", 9F);

            _status.SetBounds(22, 20, 346, 28);
            _progress.SetBounds(22, 60, 346, 20);
            _progress.Style = ProgressBarStyle.Marquee;
            Controls.AddRange(new Control[] { _status, _progress });
            Shown += Install;
        }

        private void Install(object sender, EventArgs e)
        {
            string directory = AppInstaller.GetInstallDirectory();
            string executable = Path.Combine(directory, "MeoPet.exe");
            bool alreadyInstalled = File.Exists(executable);
            if (alreadyInstalled &&
                MessageBox.Show(
                    this,
                    "檢測到您已安裝過 MeoPet，是否檢查更新？",
                    "MeoPet 安裝程式",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                Close();
                return;
            }

            _status.Text = alreadyInstalled ? "正在檢查更新..." : "正在下載並安裝 MeoPet...";
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object source, DoWorkEventArgs args)
            {
                if (Process.GetProcessesByName("MeoPet").Length > 0)
                {
                    throw new InvalidOperationException(
                        "請先退出正在執行的 MeoPet，再重新執行安裝程式。");
                }

                string manifest = Path.Combine(directory, "ver.json");
                string package = alreadyInstalled && File.Exists(executable) && File.Exists(manifest)
                    ? UpdateService.DownloadUpdateIfAvailable(directory)
                    : UpdateService.DownloadLatestPackage();

                if (package == null)
                {
                    args.Result = false;
                    return;
                }

                try
                {
                    UpdateService.InstallStagedPackage(package, directory);
                    args.Result = true;
                }
                finally
                {
                    UpdateService.CleanupStaging(package);
                }
            };
            worker.RunWorkerCompleted += delegate(object source, RunWorkerCompletedEventArgs args)
            {
                worker.Dispose();
                if (args.Error != null)
                {
                    ShowError("安裝 MeoPet 失敗", args.Error.Message);
                    Close();
                    return;
                }

                if (alreadyInstalled && args.Result is bool && !(bool)args.Result)
                {
                    MessageBox.Show(
                        this,
                        "目前已是最新版。",
                        "MeoPet 安裝程式",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                try
                {
                    CreateApplicationShortcut();
                }
                catch (InvalidOperationException ex)
                {
                    ShowError("無法建立 MeoPet 捷徑", ex.Message);
                }
                catch (IOException ex)
                {
                    ShowError("無法建立 MeoPet 捷徑", ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    ShowError("無法建立 MeoPet 捷徑", ex.Message);
                }
                catch (SecurityException ex)
                {
                    ShowError("無法建立 MeoPet 捷徑", ex.Message);
                }
                catch (COMException ex)
                {
                    ShowError("無法建立 MeoPet 捷徑", ex.Message);
                }
                catch (TargetInvocationException ex)
                {
                    ShowError("無法建立 MeoPet 捷徑", ex.Message);
                }

                try
                {
                    Process started = Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(directory, "MeoPet.exe"),
                        WorkingDirectory = directory,
                        UseShellExecute = true
                    });
                    if (started == null)
                    {
                        throw new InvalidOperationException("MeoPet 沒有成功啟動。");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    ShowError("已安裝，但無法啟動 MeoPet", ex.Message);
                }
                catch (Win32Exception ex)
                {
                    ShowError("已安裝，但無法啟動 MeoPet", ex.Message);
                }

                Close();
            };
            worker.RunWorkerAsync();
        }

        private void CreateApplicationShortcut()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "選擇要放置 MeoPet 捷徑的位置（預設為桌面）";
                dialog.SelectedPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory);
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                string shortcutPath = Path.Combine(dialog.SelectedPath, "MeoPet.lnk");
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    throw new InvalidOperationException("找不到 Windows 捷徑建立元件。");
                }

                object shell = null;
                object shortcut = null;
                try
                {
                    shell = Activator.CreateInstance(shellType);
                    shortcut = shellType.InvokeMember(
                        "CreateShortcut",
                        BindingFlags.InvokeMethod,
                        null,
                        shell,
                        new object[] { shortcutPath });
                    Type shortcutType = shortcut.GetType();
                    shortcutType.InvokeMember(
                        "TargetPath",
                        BindingFlags.SetProperty,
                        null,
                        shortcut,
                        new object[] { Path.Combine(AppInstaller.GetInstallDirectory(), "MeoPet.exe") });
                    shortcutType.InvokeMember(
                        "WorkingDirectory",
                        BindingFlags.SetProperty,
                        null,
                        shortcut,
                        new object[] { AppInstaller.GetInstallDirectory() });
                    shortcutType.InvokeMember(
                        "IconLocation",
                        BindingFlags.SetProperty,
                        null,
                        shortcut,
                        new object[] { Path.Combine(AppInstaller.GetInstallDirectory(), "MeoPet.exe") + ",0" });
                    shortcutType.InvokeMember(
                        "Save",
                        BindingFlags.InvokeMethod,
                        null,
                        shortcut,
                        null);
                    File.WriteAllText(
                        Path.Combine(AppInstaller.GetInstallDirectory(), "shortcut-location.txt"),
                        shortcutPath);
                }
                finally
                {
                    if (shortcut != null && Marshal.IsComObject(shortcut))
                    {
                        Marshal.FinalReleaseComObject(shortcut);
                    }

                    if (shell != null && Marshal.IsComObject(shell))
                    {
                        Marshal.FinalReleaseComObject(shell);
                    }
                }
            }
        }

        private void ShowError(string title, string details)
        {
            MessageBox.Show(
                this,
                title + ":" + Environment.NewLine + details,
                "MeoPet 安裝程式",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
