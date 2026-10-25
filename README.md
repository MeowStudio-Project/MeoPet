# MeoPet

MeoPet 是以 Windows Forms 與 .NET Framework 4 製作的 Windows 小桌面寵物。

## 安裝

從 GitHub Release 下載並執行 `MeoPet.Setup.exe`。

- 若尚未安裝，安裝程式會下載最新版並安裝到 `%LOCALAPPDATA%\meopet`。
- 若偵測到已有 `meopet` 資料夾，會詢問是否檢查更新。
- 已安裝版本若為最新版，安裝程式會告知並啟動現有程式。
- 更新時請先退出正在執行的 MeoPet。

發布 ZIP 的根目錄必須包含：

- `MeoPet.exe`
- `ver.json`

## 建置

在專案根目錄使用 .NET Framework C# 編譯器建置寵物程式：

```powershell
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /target:winexe /out:MeoPet.exe /win32icon:assets\app.ico `
  /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
  /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
  /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll /reference:System.Xml.dll `
  /resource:data\speech.json,MeoPet.data.speech.json `
  /resource:data\expressions.json,MeoPet.data.expressions.json `
  /resource:assets\app.png,MeoPet.assets.app.png `
  /resource:ver.json,MeoPet.ver.json `
  core\Program.cs models\PetSettings.cs services\AppInstaller.cs `
  services\CpuUsageMonitor.cs services\PetStorage.cs services\SpeechContent.cs `
  services\StartupSettings.cs services\UpdateService.cs ui\NameDialog.cs `
  ui\PetForm.cs ui\SpeechBubbleForm.cs ui\SettingsForm.cs
```

建置獨立的網路安裝程式：

```powershell
& $csc /nologo /target:winexe /out:MeoPet.Setup.exe /win32icon:assets\app.ico `
  /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
  /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
  /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
  setup\Setup.cs services\AppInstaller.cs services\UpdateService.cs
```

## 發布新版順序

1. 提高 `core\Program.cs` 的 `Program.Version`，並在本機以相同版本更新 `ver.json`。
2. 建置 `MeoPet.exe` 與 `MeoPet.Setup.exe`。
3. 建立 `MeoPet-<版本>.zip`，ZIP 根目錄只放 `MeoPet.exe` 和新版 `ver.json`。
4. 將程式碼推送到 GitHub 預設分支，但先不要推送本機更新後的 `ver.json`；讓 GitHub 保留目前的舊版版本檔。
5. 建立 `v<版本>` GitHub Release，上傳 `MeoPet-<版本>.zip` 與 `MeoPet.Setup.exe`，再發布 Release。
6. 確認 ZIP 可下載後，再單獨提交並推送新版 `ver.json`，確認 `packageUrl` 與 Release 資產網址完全一致。

`ver.json` 格式範例：

```json
{
  "version": "1.1.4",
  "packageUrl": "https://github.com/MeowStudio-Project/MeoPet/releases/download/v1.1.4/MeoPet-1.1.4.zip"
}
```
