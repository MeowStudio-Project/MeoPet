using System;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MeoPet
{
    internal static class AppInstaller
    {
        private const string InstallFolderName = "meopet";
        private const string ExecutableName = "MeoPet.exe";
        private const string UninstallRegistryPath =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MeoPet";

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

            MessageBox.Show(
                "請使用 MeoPet.Setup.exe 安裝 MeoPet。" + Environment.NewLine +
                "安裝完成後，請從桌面捷徑啟動寵物。",
                "請先安裝 MeoPet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return false;
        }

        public static string GetInstallDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                InstallFolderName);
        }

        public static void RegisterUninstallEntry()
        {
            string installDirectory = GetInstallDirectory();
            string executable = Path.Combine(installDirectory, ExecutableName);
            if (!File.Exists(executable))
            {
                throw new FileNotFoundException("找不到已安裝的 MeoPet 程式。", executable);
            }

            UpdateManifest manifest;
            try
            {
                manifest = new JavaScriptSerializer().Deserialize<UpdateManifest>(
                    File.ReadAllText(Path.Combine(installDirectory, "ver.json")));
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidDataException("無法讀取已安裝的 MeoPet 版本資料。", ex);
            }

            if (manifest == null || String.IsNullOrWhiteSpace(manifest.Version))
            {
                throw new InvalidDataException("已安裝的 MeoPet 版本資料無效。");
            }

            using (RegistryKey entry = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath))
            {
                if (entry == null)
                {
                    throw new InvalidOperationException("無法建立控制台解除安裝項目。");
                }

                entry.SetValue("DisplayName", "MeoPet");
                entry.SetValue("DisplayVersion", manifest.Version);
                entry.SetValue("Publisher", "MeowStudio-Project");
                entry.SetValue("InstallLocation", installDirectory);
                entry.SetValue("DisplayIcon", executable);
                entry.SetValue("UninstallString", "\"" + executable + "\" --uninstall");
                entry.SetValue("NoModify", 1, RegistryValueKind.DWord);
                entry.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        public static void RemoveUninstallEntry()
        {
            using (RegistryKey entry = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath, true))
            {
                if (entry == null)
                {
                    return;
                }

                string registeredLocation = entry.GetValue("InstallLocation") as string;
                if (!String.Equals(
                    Path.GetFullPath(registeredLocation ?? String.Empty)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(GetInstallDirectory())
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("控制台解除安裝項目路徑不符，未移除登錄資料。");
                }
            }

            Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, false);
        }

        private static void EnsureVersionFile(string installDirectory, bool overwrite)
        {
            string versionFile = Path.Combine(installDirectory, "ver.json");
            if (File.Exists(versionFile) && !overwrite)
            {
                return;
            }

            string temporaryFile = versionFile + "." + Guid.NewGuid().ToString("N") + ".new";
            try
            {
                using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("MeoPet.ver.json"))
                {
                    if (resource == null)
                    {
                        throw new InvalidDataException("找不到內嵌版本檔 MeoPet.ver.json。");
                    }

                    using (FileStream output = new FileStream(temporaryFile, FileMode.CreateNew, FileAccess.Write))
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
            finally
            {
                if (File.Exists(temporaryFile))
                {
                    File.Delete(temporaryFile);
                }
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
