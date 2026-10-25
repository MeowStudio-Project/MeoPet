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
    internal static class PetStorage
    {
        public static void SaveName(string nameFile, string name)
        {
            string directory = Path.GetDirectoryName(nameFile);
            Directory.CreateDirectory(directory);
            File.WriteAllText(nameFile, name, new UTF8Encoding(false));
        }
    }
}
