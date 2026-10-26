# MeoPet

MeoPet 是以 Windows Forms 與 .NET Framework 4 製作的 Windows 小桌面寵物。

## 安裝

從 GitHub Release 下載並執行 `MeoPet.Setup.exe`。

- 請使用 `MeoPet.Setup.exe` 安裝；直接執行 ZIP 中的 `MeoPet.exe` 不會安裝或複製程式。
- 若尚未安裝，安裝程式會下載最新版並安裝到 `%LOCALAPPDATA%\meopet`。
- 安裝程式會將 MeoPet 登錄到目前使用者的 Windows「程式集」解除安裝清單，不需要系統管理員權限。
- 若偵測到 `%LOCALAPPDATA%\meopet` 中已有 `MeoPet.exe`，會詢問是否檢查更新；只有資料殘留時會視為尚未安裝。
- 已安裝版本若為最新版，安裝程式會告知並啟動現有程式。
- 安裝完成後可選擇建立 MeoPet 捷徑的位置，預設為桌面；桌面顯示名稱為「MeoPet」。
- 更新時請先退出正在執行的 MeoPet。
- `idle` 與 CPU 提醒會等目前的說話泡泡結束後再顯示，避免打斷正在閱讀的訊息。
- 右鍵選單提供使用說明與可保存的「禁止講話」切換；選單開啟時不會被背景台詞打斷。
- 設定中的「清空資料」會清除寵物名字與偏好設定，但保留程式。
- 要解除安裝 MeoPet，請在 Windows「控制台 > 程式集 > 解除安裝程序」中移除；設定頁的刪除按鈕僅供說明。

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
  core\Program.cs models\PetSettings.cs services\AppInstaller.cs services\AppRemovalService.cs `
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

## FAQ

1.
Q: **為甚麼一開始就從1.1.2開始更新起?**
A: **因為忘記用Git了**

2.
Q: **運作時會不會引響性能?**
A: **不會**