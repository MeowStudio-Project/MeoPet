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
    public sealed class PetSettings
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int BodyColorArgb { get; set; }
        public int ShadowColorArgb { get; set; }
        public int ShadowOpacity { get; set; }
        public bool AutoStart { get; set; }
        public bool SqueezeAtEdge { get; set; }

        public PetSettings()
        {
            Width = 150;
            Height = 150;
            BodyColorArgb = Color.FromArgb(235, 238, 243).ToArgb();
            ShadowColorArgb = Color.FromArgb(76, 72, 83, 98).ToArgb();
            ShadowOpacity = 0;
            SqueezeAtEdge = true;
        }

        public static PetSettings Load(string path)
        {
            if (!File.Exists(path))
            {
                return new PetSettings();
            }

            XmlSerializer serializer = new XmlSerializer(typeof(PetSettings));
            using (FileStream stream = File.OpenRead(path))
            {
                PetSettings settings = (PetSettings)serializer.Deserialize(stream);
                settings.Validate();
                return settings;
            }
        }

        public void Save(string path)
        {
            Validate();
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            XmlSerializer serializer = new XmlSerializer(typeof(PetSettings));
            using (FileStream stream = File.Create(path))
            {
                serializer.Serialize(stream, this);
            }
        }

        public void Validate()
        {
            if (Width < 70 || Width > 280 || Height < 70 || Height > 280)
            {
                throw new InvalidDataException("寵物寬高必須介於 70 到 280 像素。");
            }

            if (ShadowOpacity < 0 || ShadowOpacity > 100)
            {
                throw new InvalidDataException("陰影透明度必須介於 0 到 100。");
            }
        }
    }
}
