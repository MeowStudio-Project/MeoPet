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
    internal static class StartupSettings
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppValue = "MeoPet";

        public static bool IsEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
            {
                return key != null && key.GetValue(AppValue) != null;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("無法開啟 Windows 開機啟動設定。");
                }

                if (enabled)
                {
                    string executable = Application.ExecutablePath;
                    key.SetValue(AppValue, "\"" + executable + "\"");
                }
                else
                {
                    key.DeleteValue(AppValue, false);
                }
            }
        }
    }
}
