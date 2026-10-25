using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace MeoPet
{
    internal static class AppInstaller
    {
        private const string InstallFolderName = "meopet";
        private const string ExecutableName = "MeoPet.exe";

        public static bool EnsureInstalled()
        {
            string installDirectory = GetInstallDirectory();
            string installedExecutable = Path.Combine(installDirectory, ExecutableName);
            string currentExecutable = Path.GetFullPath(Application.ExecutablePath);

            if (String.Equals(currentExecutable, installedExecutable, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    EnsureVersionFile(installDirectory, false);
                    return true;
                }
                catch (IOException ex)
                {
                    ShowInstallError(ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    ShowInstallError(ex.Message);
                }

                return false;
            }

            try
            {
                Directory.CreateDirectory(installDirectory);
                File.Copy(currentExecutable, installedExecutable, true);
                EnsureVersionFile(installDirectory, true);

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = installedExecutable;
                startInfo.Arguments = "--wait-for-launcher";
                startInfo.WorkingDirectory = installDirectory;
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
                return false;
            }
            catch (IOException ex)
            {
                ShowInstallError(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowInstallError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                ShowInstallError(ex.Message);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                ShowInstallError(ex.Message);
            }

            return false;
        }

        public static string GetInstallDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                InstallFolderName);
        }

        private static void EnsureVersionFile(string installDirectory, bool overwrite)
        {
            string versionFile = Path.Combine(installDirectory, "ver.json");
            if (File.Exists(versionFile) && !overwrite)
            {
                return;
            }

            string temporaryFile = versionFile + ".new";
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("MeoPet.ver.json"))
            {
                if (resource == null)
                {
                    throw new InvalidDataException("找不到內嵌版本檔 MeoPet.ver.json。");
                }

                using (FileStream output = new FileStream(temporaryFile, FileMode.Create, FileAccess.Write))
                {
                    resource.CopyTo(output);
                }
            }

            if (File.Exists(versionFile))
            {
                File.Replace(temporaryFile, versionFile, null);
            }
            else
            {
                File.Move(temporaryFile, versionFile);
            }
        }

        private static void ShowInstallError(string details)
        {
            MessageBox.Show(
                "無法安裝或啟動 MeoPet：" + Environment.NewLine + details,
                "MeoPet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
