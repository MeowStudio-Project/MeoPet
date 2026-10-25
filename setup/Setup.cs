using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
            bool alreadyInstalled = Directory.Exists(directory);
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

                string executable = Path.Combine(directory, "MeoPet.exe");
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
