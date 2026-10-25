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
        private readonly Label _status;
        private readonly ProgressBar _progress;

        public SetupForm()
        {
            Text = "安裝 MeoPet";
            ClientSize = new Size(390, 125);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft JhengHei UI", 9F);

            _status = new Label();
            _status.Location = new Point(22, 20);
            _status.Size = new Size(346, 28);
            _status.Text = "正在連線並下載 MeoPet...";
            Controls.Add(_status);

            _progress = new ProgressBar();
            _progress.Location = new Point(22, 60);
            _progress.Size = new Size(346, 20);
            _progress.Style = ProgressBarStyle.Marquee;
            Controls.Add(_progress);

            Shown += BeginInstall;
        }

        private void BeginInstall(object sender, EventArgs e)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate
            {
                Process[] running = Process.GetProcessesByName("MeoPet");
                if (running.Length > 0)
                {
                    throw new InvalidOperationException(
                        "請先退出正在執行的 MeoPet，再重新執行安裝程式。");
                }

                string stagedDirectory = UpdateService.DownloadLatestPackage();
                try
                {
                    UpdateService.InstallStagedPackage(
                        stagedDirectory,
                        AppInstaller.GetInstallDirectory());
                }
                finally
                {
                    UpdateService.CleanupStaging(stagedDirectory);
                }
            };
            worker.RunWorkerCompleted += delegate(object workerSender, RunWorkerCompletedEventArgs workerEvent)
            {
                worker.Dispose();
                if (workerEvent.Error != null)
                {
                    MessageBox.Show(
                        this,
                        "安裝 MeoPet 失敗：" + Environment.NewLine + workerEvent.Error.Message,
                        "MeoPet 安裝程式",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Close();
                    return;
                }

                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = Path.Combine(
                        AppInstaller.GetInstallDirectory(),
                        "MeoPet.exe");
                    startInfo.WorkingDirectory = AppInstaller.GetInstallDirectory();
                    startInfo.UseShellExecute = true;
                    if (Process.Start(startInfo) == null)
                    {
                        throw new InvalidOperationException("MeoPet 沒有成功啟動。");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    ShowLaunchError(ex.Message);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    ShowLaunchError(ex.Message);
                }

                Close();
            };
            worker.RunWorkerAsync();
        }

        private void ShowLaunchError(string details)
        {
            MessageBox.Show(
                this,
                "已安裝，但無法自動啟動 MeoPet：" + Environment.NewLine + details,
                "MeoPet 安裝程式",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
