# MeoPet

MeoPet is a small Windows desktop pet built with Windows Forms and .NET Framework 4.

## Install

Download `MeoPet.Setup.exe` from the GitHub release and run it. The single-file installer downloads the matching release package and installs MeoPet under `%LOCALAPPDATA%\meopet`.

The release ZIP contains only:

- `MeoPet.exe`
- `ver.json`

## Build

Build the app with the .NET Framework C# compiler from the project root:

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

Build the standalone network installer from the project root:

```powershell
& $csc /nologo /target:winexe /out:MeoPet.Setup.exe /win32icon:assets\app.ico `
  /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
  /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
  /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
  setup\Setup.cs services\AppInstaller.cs services\UpdateService.cs
```

## Publishing a release

1. Build `MeoPet.exe` and `MeoPet.Setup.exe`.
2. Create a ZIP with `MeoPet.exe` and `ver.json` at its root. Name it `MeoPet-<version>.zip`.
3. Create a GitHub release using tag `v<version>` and upload the ZIP and `MeoPet.Setup.exe`.
4. Publish `ver.json` to the default branch after the release assets are available. It provides the current version and the exact ZIP download URL used by the in-app updater and installer.

`ver.json` format:

```json
{
  "version": "1.1.2",
  "packageUrl": "https://github.com/MeowStudio-Project/MeoPet/releases/download/v1.1.2/MeoPet-1.1.2.zip"
}
```
