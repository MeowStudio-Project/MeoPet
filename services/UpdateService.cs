using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace MeoPet
{
    internal sealed class UpdateManifest
    {
        public string Version { get; set; }
        public string PackageUrl { get; set; }
    }

    internal static class UpdateService
    {
        private const string ManifestUrl =
            "https://raw.githubusercontent.com/MeowStudio-Project/MeoPet/main/ver.json";
        private const string MutexName = @"Local\MeoPet.DesktopPet";
        private const int MaximumDownloadBytes = 100 * 1024 * 1024;

        public static string DownloadUpdateIfAvailable(string installedDirectory)
        {
            UpdateManifest local = ReadManifest(
                Path.Combine(installedDirectory, "ver.json"),
                "本機版本資料");
            UpdateManifest latest = DownloadManifest();
            int comparison = CompareVersions(latest.Version, local.Version);
            if (comparison <= 0)
            {
                return null;
            }

            return DownloadAndExtract(latest);
        }

        public static string DownloadLatestPackage()
        {
            return DownloadAndExtract(DownloadManifest());
        }

        public static void CleanupStaging(string stagedDirectory)
        {
            if (String.IsNullOrWhiteSpace(stagedDirectory))
            {
                return;
            }

            string fullPath = Path.GetFullPath(stagedDirectory);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath());
            if (!temporaryRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                temporaryRoot += Path.DirectorySeparatorChar;
            }

            DirectoryInfo payload = new DirectoryInfo(fullPath);
            if (!String.Equals(payload.Name, "payload", StringComparison.OrdinalIgnoreCase) ||
                payload.Parent == null ||
                !payload.Parent.Name.StartsWith("MeoPet-update-", StringComparison.OrdinalIgnoreCase) ||
                !payload.Parent.FullName.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("更新暫存路徑無效。");
            }

            if (Directory.Exists(payload.Parent.FullName))
            {
                Directory.Delete(payload.Parent.FullName, true);
            }
        }

        public static void InstallStagedPackage(string stagedDirectory, string installDirectory)
        {
            ValidatePackage(stagedDirectory, null);
            Directory.CreateDirectory(installDirectory);
            CopyPackageFiles(stagedDirectory, installDirectory);
        }

        public static void ApplyStagedUpdate(string stagedDirectory, string installDirectory)
        {
            ValidatePackage(stagedDirectory, null);
            using (Mutex updateMutex = new Mutex(false, MutexName))
            {
                bool ownsMutex;
                try
                {
                    ownsMutex = updateMutex.WaitOne(TimeSpan.FromMinutes(3));
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                }

                if (!ownsMutex)
                {
                    throw new TimeoutException("等待 MeoPet 關閉逾時，無法套用更新。");
                }

                Directory.CreateDirectory(installDirectory);
                CopyPackageFiles(stagedDirectory, installDirectory);
                CleanupStaging(stagedDirectory);

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Path.Combine(installDirectory, "MeoPet.exe");
                startInfo.Arguments = "--wait-for-launcher";
                startInfo.WorkingDirectory = installDirectory;
                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
            }
        }

        private static UpdateManifest DownloadManifest()
        {
            string json = DownloadText(ManifestUrl, 64 * 1024);
            UpdateManifest manifest;
            try
            {
                manifest = new JavaScriptSerializer().Deserialize<UpdateManifest>(json);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidDataException("GitHub 版本資料格式錯誤。", ex);
            }

            ValidateManifest(manifest);
            return manifest;
        }

        private static UpdateManifest ReadManifest(string path, string description)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(description + "不存在。", path);
            }

            UpdateManifest manifest;
            try
            {
                manifest = new JavaScriptSerializer().Deserialize<UpdateManifest>(
                    File.ReadAllText(path, Encoding.UTF8));
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidDataException(description + "格式錯誤。", ex);
            }

            ValidateManifest(manifest);
            return manifest;
        }

        private static void ValidateManifest(UpdateManifest manifest)
        {
            Version parsedVersion;
            Uri packageUri;
            if (manifest == null ||
                !Version.TryParse(manifest.Version, out parsedVersion) ||
                !Uri.TryCreate(manifest.PackageUrl, UriKind.Absolute, out packageUri) ||
                packageUri.Scheme != Uri.UriSchemeHttps ||
                !String.Equals(packageUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                !packageUri.AbsolutePath.Contains("/releases/download/"))
            {
                throw new InvalidDataException("版本資料缺少有效版本號或 GitHub Release ZIP 網址。");
            }
        }

        private static int CompareVersions(string left, string right)
        {
            Version leftVersion;
            Version rightVersion;
            if (!Version.TryParse(left, out leftVersion) ||
                !Version.TryParse(right, out rightVersion))
            {
                throw new InvalidDataException("版本號格式無效。");
            }

            return leftVersion.CompareTo(rightVersion);
        }

        private static string DownloadAndExtract(UpdateManifest manifest)
        {
            string temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "MeoPet-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            string archivePath = Path.Combine(temporaryDirectory, "package.zip");
            string payloadDirectory = Path.Combine(temporaryDirectory, "payload");

            bool completed = false;
            try
            {
                DownloadFile(manifest.PackageUrl, archivePath);
                ExtractArchive(archivePath, payloadDirectory);
                ValidatePackage(payloadDirectory, manifest.Version);
                File.Delete(archivePath);
                completed = true;
                return payloadDirectory;
            }
            finally
            {
                if (!completed && Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, true);
                }
            }
        }

        private static string DownloadText(string address, int maximumBytes)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                Download(address, stream, maximumBytes);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static void DownloadFile(string address, string destination)
        {
            using (FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
            {
                Download(address, output, MaximumDownloadBytes);
            }
        }

        private static void Download(string address, Stream destination, int maximumBytes)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(address);
            request.UserAgent = "MeoPet-Updater";
            request.Timeout = 15000;
            request.ReadWriteTimeout = 30000;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.ContentLength > maximumBytes)
                {
                    throw new InvalidDataException("下載檔案超過允許大小。");
                }

                using (Stream input = response.GetResponseStream())
                {
                    byte[] buffer = new byte[81920];
                    int read;
                    int total = 0;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += read;
                        if (total > maximumBytes)
                        {
                            throw new InvalidDataException("下載檔案超過允許大小。");
                        }

                        destination.Write(buffer, 0, read);
                    }
                }
            }
        }

        private static void ExtractArchive(string archivePath, string destination)
        {
            Directory.CreateDirectory(destination);
            string root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            long totalBytes = 0;
            int fileCount = 0;

            using (FileStream stream = File.OpenRead(archivePath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (String.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    string relativePath = entry.FullName.Replace(
                        '/',
                        Path.DirectorySeparatorChar);
                    string outputPath = Path.GetFullPath(Path.Combine(root, relativePath));
                    if (!outputPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("ZIP 封裝含有無效的檔案路徑。");
                    }

                    totalBytes += entry.Length;
                    fileCount++;
                    if (totalBytes > MaximumDownloadBytes || fileCount > 1000)
                    {
                        throw new InvalidDataException("ZIP 封裝內容超過允許大小。");
                    }

                    string parentDirectory = Path.GetDirectoryName(outputPath);
                    Directory.CreateDirectory(parentDirectory);
                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write))
                    {
                        CopyWithLimit(input, output, entry.Length);
                    }
                }
            }
        }

        private static void CopyWithLimit(Stream input, Stream output, long expectedLength)
        {
            byte[] buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > expectedLength || total > MaximumDownloadBytes)
                {
                    throw new InvalidDataException("ZIP 檔案內容大小不符。");
                }

                output.Write(buffer, 0, read);
            }

            if (total != expectedLength)
            {
                throw new InvalidDataException("ZIP 檔案內容不完整。");
            }
        }

        private static void ValidatePackage(string directory, string expectedVersion)
        {
            string executablePath = Path.Combine(directory, "MeoPet.exe");
            string manifestPath = Path.Combine(directory, "ver.json");
            if (!File.Exists(executablePath) || !File.Exists(manifestPath))
            {
                throw new InvalidDataException("Release ZIP 必須在根目錄包含 MeoPet.exe 與 ver.json。");
            }

            using (FileStream executable = File.OpenRead(executablePath))
            {
                if (executable.Length < 2 || executable.ReadByte() != 0x4D || executable.ReadByte() != 0x5A)
                {
                    throw new InvalidDataException("Release ZIP 中的 MeoPet.exe 格式無效。");
                }
            }

            UpdateManifest manifest = ReadManifest(manifestPath, "Release ZIP 版本資料");
            if (expectedVersion != null &&
                CompareVersions(manifest.Version, expectedVersion) != 0)
            {
                throw new InvalidDataException("Release ZIP 版本與 GitHub 版本資料不一致。");
            }
        }

        private static void CopyPackageFiles(string sourceDirectory, string destinationDirectory)
        {
            string[] fileNames = { "MeoPet.exe", "ver.json" };
            for (int i = 0; i < fileNames.Length; i++)
            {
                string source = Path.Combine(sourceDirectory, fileNames[i]);
                string destination = Path.Combine(destinationDirectory, fileNames[i]);
                string temporary = destination + ".new";

                File.Copy(source, temporary, true);
                if (File.Exists(destination))
                {
                    File.Replace(temporary, destination, null);
                }
                else
                {
                    File.Move(temporary, destination);
                }
            }
        }
    }
}
