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
    internal sealed class SettingsForm : Form
    {
        private readonly PetSettings _settings;
        private readonly Func<PetSettings, bool> _applySettings;
        private readonly Func<bool> _clearData;
        private readonly Func<bool> _deleteApplication;
        private readonly Func<string> _downloadUpdate;
        private readonly Func<string, bool> _startUpdate;
        private readonly Panel _page;
        private readonly Button[] _navigation;
        private bool _loading;

        public SettingsForm(
            PetSettings settings,
            Func<PetSettings, bool> applySettings,
            Func<bool> clearData,
            Func<bool> deleteApplication,
            Func<string> downloadUpdate,
            Func<string, bool> startUpdate)
        {
            _settings = Copy(settings);
            _applySettings = applySettings;
            _clearData = clearData;
            _deleteApplication = deleteApplication;
            _downloadUpdate = downloadUpdate;
            _startUpdate = startUpdate;

            Text = "MeoPet 設定";
            ClientSize = new Size(760, 510);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            Font = new Font("Microsoft JhengHei UI", 9F);
            BackColor = Color.FromArgb(248, 249, 251);

            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = 176;
            sidebar.BackColor = Color.FromArgb(242, 244, 247);
            Controls.Add(sidebar);

            PictureBox appIcon = new PictureBox();
            appIcon.Location = new Point(20, 19);
            appIcon.Size = new Size(30, 30);
            appIcon.SizeMode = PictureBoxSizeMode.Zoom;
            appIcon.Image = LoadEmbeddedImage("MeoPet.assets.app.png");
            sidebar.Controls.Add(appIcon);

            Label appTitle = new Label();
            appTitle.Location = new Point(59, 16);
            appTitle.Size = new Size(105, 36);
            appTitle.Text = "MeoPet";
            appTitle.Font = new Font("Microsoft JhengHei UI", 13F, FontStyle.Bold);
            appTitle.ForeColor = Color.FromArgb(47, 56, 70);
            appTitle.TextAlign = ContentAlignment.MiddleLeft;
            sidebar.Controls.Add(appTitle);

            Panel divider = new Panel();
            divider.Location = new Point(16, 65);
            divider.Size = new Size(144, 1);
            divider.BackColor = Color.FromArgb(224, 228, 234);
            sidebar.Controls.Add(divider);

            _page = new Panel();
            _page.Location = new Point(sidebar.Width, 0);
            _page.Size = new Size(ClientSize.Width - sidebar.Width, ClientSize.Height);
            _page.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _page.AutoScroll = true;
            _page.Padding = new Padding(12, 0, 12, 12);
            Controls.Add(_page);

            _navigation = new Button[3];
            string[] pageNames = { "樣貌", "其他設定", "關於" };
            for (int i = 0; i < pageNames.Length; i++)
            {
                int pageIndex = i;
                Button button = new Button();
                button.Location = new Point(12, 82 + i * 46);
                button.Size = new Size(152, 39);
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.Text = pageNames[i];
                button.TextAlign = ContentAlignment.MiddleLeft;
                button.Padding = new Padding(14, 0, 0, 0);
                button.Font = new Font("Microsoft JhengHei UI", 9F);
                button.Cursor = Cursors.Hand;
                button.Click += delegate { SelectPage(pageIndex); };
                _navigation[i] = button;
                sidebar.Controls.Add(button);
            }

            SelectPage(0);
        }

        private static PetSettings Copy(PetSettings source)
        {
            return new PetSettings
            {
                Width = source.Width,
                Height = source.Height,
                BodyColorArgb = source.BodyColorArgb,
                ShadowColorArgb = source.ShadowColorArgb,
                ShadowOpacity = source.ShadowOpacity,
                AutoStart = source.AutoStart,
                SqueezeAtEdge = source.SqueezeAtEdge
            };
        }

        private static Image LoadEmbeddedImage(string resourceName)
        {
            using (Stream stream = typeof(SettingsForm).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("找不到應用程式圖示：" + resourceName);
                }

                using (Image image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }
        }

        private void SelectPage(int pageIndex)
        {
            for (int i = 0; i < _navigation.Length; i++)
            {
                bool selected = i == pageIndex;
                _navigation[i].BackColor = selected
                    ? Color.FromArgb(226, 232, 241)
                    : Color.Transparent;
                _navigation[i].ForeColor = selected
                    ? Color.FromArgb(48, 60, 78)
                    : Color.FromArgb(103, 113, 127);
            }

            _page.Controls.Clear();
            if (pageIndex == 0)
            {
                BuildAppearancePage();
            }
            else if (pageIndex == 1)
            {
                BuildOtherSettingsPage();
            }
            else
            {
                BuildAboutPage();
            }
        }

        private void BuildAppearancePage()
        {
            AddTitle("樣貌", "調整寵物的尺寸與顏色，預覽會立即套用。");
            AddCaption("整體大小");
            TrackBar size = new TrackBar();
            size.Location = new Point(190, 82);
            size.Size = new Size(300, 42);
            size.Minimum = 70;
            size.Maximum = 280;
            size.TickFrequency = 30;
            size.Value = _settings.Width == _settings.Height ? _settings.Width : Math.Min(_settings.Width, _settings.Height);
            _page.Controls.Add(size);
            Label sizeValue = AddValueLabel(size.Value + " px", 500, 87);

            _loading = true;
            size.ValueChanged += delegate
            {
                if (_loading)
                {
                    return;
                }

                if (SetDimensions(size.Value, size.Value))
                {
                    sizeValue.Text = size.Value + " px";
                }
                else
                {
                    _loading = true;
                    size.Value = Math.Min(_settings.Width, _settings.Height);
                    sizeValue.Text = size.Value + " px";
                    _loading = false;
                }
            };
            _loading = false;

            AddCaption("寬度", 139);
            NumericUpDown width = CreateDimensionInput(_settings.Width, 139);
            _page.Controls.Add(width);
            AddCaption("高度", 188);
            NumericUpDown height = CreateDimensionInput(_settings.Height, 210);
            _page.Controls.Add(height);

            width.ValueChanged += delegate
            {
                if (!_loading)
                {
                    UpdateDimensions((int)width.Value, (int)height.Value, size, sizeValue);
                }
            };
            height.ValueChanged += delegate
            {
                if (!_loading)
                {
                    UpdateDimensions((int)width.Value, (int)height.Value, size, sizeValue);
                }
            };

            AddCaption("球體顏色", 246);
            Button bodyColor = CreateColorButton(Color.FromArgb(_settings.BodyColorArgb), 248);
            _page.Controls.Add(bodyColor);
            bodyColor.Click += delegate
            {
                Color selected;
                if (ChooseColor(Color.FromArgb(_settings.BodyColorArgb), out selected))
                {
                    PetSettings changed = Copy(_settings);
                    changed.BodyColorArgb = selected.ToArgb();
                    if (Commit(changed))
                    {
                        bodyColor.BackColor = selected;
                    }
                }
            };

            AddCaption("陰影顏色", 304);
            Button shadowColor = CreateColorButton(Color.FromArgb(_settings.ShadowColorArgb), 306);
            _page.Controls.Add(shadowColor);
            shadowColor.Click += delegate
            {
                Color selected;
                if (ChooseColor(Color.FromArgb(_settings.ShadowColorArgb), out selected))
                {
                    PetSettings changed = Copy(_settings);
                    changed.ShadowColorArgb = Color.FromArgb(255, selected).ToArgb();
                    if (Commit(changed))
                    {
                        shadowColor.BackColor = Color.FromArgb(255, selected);
                    }
                }
            };

            AddCaption("陰影透明度", 362);
            TrackBar opacity = new TrackBar();
            opacity.Location = new Point(190, 357);
            opacity.Size = new Size(300, 42);
            opacity.Minimum = 0;
            opacity.Maximum = 100;
            opacity.TickFrequency = 10;
            opacity.Value = _settings.ShadowOpacity;
            _page.Controls.Add(opacity);
            Label opacityValue = AddValueLabel(opacity.Value + "%", 500, 363);
            opacity.ValueChanged += delegate
            {
                if (_loading)
                {
                    return;
                }

                PetSettings changed = Copy(_settings);
                changed.ShadowOpacity = opacity.Value;
                if (Commit(changed))
                {
                    opacityValue.Text = opacity.Value + "%";
                }
                else
                {
                    _loading = true;
                    opacity.Value = _settings.ShadowOpacity;
                    opacityValue.Text = opacity.Value + "%";
                    _loading = false;
                }
            };

            Button reset = new Button();
            reset.Location = new Point(190, 421);
            reset.Size = new Size(140, 34);
            reset.Text = "還原預設值";
            reset.Click += delegate { ResetAppearance(size, width, height, bodyColor, shadowColor, opacity, sizeValue, opacityValue); };
            _page.Controls.Add(reset);
        }

        private void BuildOtherSettingsPage()
        {
            AddTitle("其他設定", "管理 MeoPet 的啟動方式與互動行為。");

            CheckBox autoStart = new CheckBox();
            autoStart.Location = new Point(32, 106);
            autoStart.Size = new Size(390, 28);
            autoStart.Text = "登入 Windows 時自動啟動";
            autoStart.Checked = StartupSettings.IsEnabled();
            autoStart.CheckedChanged += delegate
            {
                if (_loading)
                {
                    return;
                }

                PetSettings changed = Copy(_settings);
                changed.AutoStart = autoStart.Checked;
                if (!Commit(changed))
                {
                    _loading = true;
                    autoStart.Checked = !autoStart.Checked;
                    _loading = false;
                }
            };
            _page.Controls.Add(autoStart);

            AddDescription(
                "開機後自動在桌面顯示你的小夥伴。",
                58,
                134);

            CheckBox squeeze = new CheckBox();
            squeeze.Location = new Point(32, 190);
            squeeze.Size = new Size(390, 28);
            squeeze.Text = "拖到螢幕邊緣時擠壓寵物";
            squeeze.Checked = _settings.SqueezeAtEdge;
            squeeze.CheckedChanged += delegate
            {
                if (_loading)
                {
                    return;
                }

                PetSettings changed = Copy(_settings);
                changed.SqueezeAtEdge = squeeze.Checked;
                if (!Commit(changed))
                {
                    _loading = true;
                    squeeze.Checked = !squeeze.Checked;
                    _loading = false;
                }
            };
            _page.Controls.Add(squeeze);
            AddDescription(
                "偵測目前螢幕的邊緣，貼住邊界時變形，拖開後彈回。",
                58,
                218);

            Button update = new Button();
            update.Location = new Point(32, 278);
            update.Size = new Size(150, 36);
            update.Text = "檢查更新";
            update.Click += CheckForUpdates;
            _page.Controls.Add(update);

            Button clearData = new Button();
            clearData.Location = new Point(32, 330);
            clearData.Size = new Size(150, 36);
            clearData.Text = "清空資料";
            clearData.Click += ClearApplicationData;
            _page.Controls.Add(clearData);

            Button deleteApplication = new Button();
            deleteApplication.Location = new Point(32, 378);
            deleteApplication.Size = new Size(150, 36);
            deleteApplication.Text = "刪除應用";
            deleteApplication.ForeColor = Color.FromArgb(165, 62, 70);
            deleteApplication.Click += DeleteApplication;
            _page.Controls.Add(deleteApplication);
        }

        private void CheckForUpdates(object sender, EventArgs e)
        {
            Button button = (Button)sender;
            button.Enabled = false;
            button.Text = "檢查中...";

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object workerSender, DoWorkEventArgs workerEvent)
            {
                workerEvent.Result = _downloadUpdate();
            };
            worker.RunWorkerCompleted += delegate(object workerSender, RunWorkerCompletedEventArgs workerEvent)
            {
                button.Enabled = true;
                button.Text = "檢查更新";
                worker.Dispose();

                if (IsDisposed)
                {
                    if (workerEvent.Error == null && workerEvent.Result is string)
                    {
                        CleanupUpdate((string)workerEvent.Result);
                    }

                    return;
                }

                if (workerEvent.Error != null)
                {
                    MessageBox.Show(
                        this,
                        "檢查或下載更新失敗：" + Environment.NewLine + workerEvent.Error.Message,
                        "MeoPet 更新",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                string stagedDirectory = workerEvent.Result as string;
                if (String.IsNullOrEmpty(stagedDirectory))
                {
                    MessageBox.Show(
                        this,
                        "目前已是最新版。",
                        "MeoPet 更新",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                DialogResult answer = MessageBox.Show(
                    this,
                    "已下載新版，現在重新啟動 MeoPet 以完成更新嗎？",
                    "MeoPet 更新",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    if (!_startUpdate(stagedDirectory))
                    {
                        CleanupUpdate(stagedDirectory);
                    }
                }
                else
                {
                    CleanupUpdate(stagedDirectory);
                }
            };
            worker.RunWorkerAsync();
        }

        private void CleanupUpdate(string stagedDirectory)
        {
            try
            {
                UpdateService.CleanupStaging(stagedDirectory);
            }
            catch (IOException ex)
            {
                MessageBox.Show(
                    this,
                    "無法清理更新暫存檔：" + ex.Message,
                    "MeoPet 更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(
                    this,
                    "沒有權限清理更新暫存檔：" + ex.Message,
                    "MeoPet 更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ClearApplicationData(object sender, EventArgs e)
        {
            DialogResult answer = MessageBox.Show(
                this,
                "這會清除寵物名字與設定，並關閉開機自動啟動；MeoPet 程式仍會保留。確定要清空資料嗎？",
                "清空 MeoPet 資料",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer == DialogResult.Yes && _clearData())
            {
                Close();
            }
        }

        private void DeleteApplication(object sender, EventArgs e)
        {
            DialogResult answer = MessageBox.Show(
                this,
                "這會將 AppData\\meopet 中的 MeoPet 程式與資料移至資源回收筒。確定要刪除應用嗎？",
                "刪除 MeoPet",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            MessageBox.Show(
                this,
                "您之後可隨時去 Github MeowStudio-Project 裡找 MeoPet 安裝回來！",
                "MeoPet",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            if (_deleteApplication())
            {
                Close();
            }
        }

        private void BuildAboutPage()
        {
            AddTitle("關於", "版本資訊");

            Label version = new Label();
            version.Location = new Point(32, 108);
            version.Size = new Size(300, 32);
            version.Text = "版本 " + Program.Version;
            version.Font = new Font("Microsoft JhengHei UI", 12F);
            version.ForeColor = Color.FromArgb(75, 85, 101);
            _page.Controls.Add(version);
        }

        private bool SetDimensions(int widthValue, int heightValue)
        {
            PetSettings changed = Copy(_settings);
            changed.Width = widthValue;
            changed.Height = heightValue;
            if (!Commit(changed))
            {
                return false;
            }

            _loading = true;
            foreach (Control control in _page.Controls)
            {
                NumericUpDown input = control as NumericUpDown;
                if (input == null)
                {
                    continue;
                }

                if (input.Tag as string == "width")
                {
                    input.Value = widthValue;
                }
                else if (input.Tag as string == "height")
                {
                    input.Value = heightValue;
                }
            }

            _loading = false;
            return true;
        }

        private void UpdateDimensions(int widthValue, int heightValue, TrackBar size, Label sizeValue)
        {
            PetSettings changed = Copy(_settings);
            changed.Width = widthValue;
            changed.Height = heightValue;
            if (Commit(changed))
            {
                size.Value = Math.Max(size.Minimum, Math.Min(size.Maximum, Math.Min(widthValue, heightValue)));
                sizeValue.Text = size.Value + " px";
            }
        }

        private void ResetAppearance(
            TrackBar size,
            NumericUpDown width,
            NumericUpDown height,
            Button bodyColor,
            Button shadowColor,
            TrackBar opacity,
            Label sizeValue,
            Label opacityValue)
        {
            PetSettings defaults = new PetSettings();
            PetSettings changed = Copy(_settings);
            changed.Width = defaults.Width;
            changed.Height = defaults.Height;
            changed.BodyColorArgb = defaults.BodyColorArgb;
            changed.ShadowColorArgb = defaults.ShadowColorArgb;
            changed.ShadowOpacity = defaults.ShadowOpacity;
            if (!Commit(changed))
            {
                return;
            }

            _loading = true;
            size.Value = defaults.Width;
            width.Value = defaults.Width;
            height.Value = defaults.Height;
            opacity.Value = defaults.ShadowOpacity;
            bodyColor.BackColor = Color.FromArgb(defaults.BodyColorArgb);
            shadowColor.BackColor = Color.FromArgb(255, Color.FromArgb(defaults.ShadowColorArgb));
            sizeValue.Text = defaults.Width + " px";
            opacityValue.Text = defaults.ShadowOpacity + "%";
            _loading = false;
        }

        private bool Commit(PetSettings changed)
        {
            if (!_applySettings(changed))
            {
                return false;
            }

            _settings.Width = changed.Width;
            _settings.Height = changed.Height;
            _settings.BodyColorArgb = changed.BodyColorArgb;
            _settings.ShadowColorArgb = changed.ShadowColorArgb;
            _settings.ShadowOpacity = changed.ShadowOpacity;
            _settings.AutoStart = changed.AutoStart;
            _settings.SqueezeAtEdge = changed.SqueezeAtEdge;
            return true;
        }

        private static NumericUpDown CreateDimensionInput(int value, int y)
        {
            NumericUpDown input = new NumericUpDown();
            input.Location = new Point(190, y);
            input.Size = new Size(110, 27);
            input.Minimum = 70;
            input.Maximum = 280;
            input.Increment = 5;
            input.Value = value;
            input.Tag = y == 139 ? "width" : "height";
            return input;
        }

        private void AddTitle(string titleText, string descriptionText)
        {
            Label title = new Label();
            title.Location = new Point(28, 22);
            title.Size = new Size(480, 34);
            title.Text = titleText;
            title.Font = new Font("Microsoft JhengHei UI", 18F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(48, 57, 71);
            _page.Controls.Add(title);
            AddDescription(descriptionText, 30, 58);
        }

        private void AddCaption(string text, int y = 86)
        {
            Label label = new Label();
            label.Location = new Point(32, y);
            label.Size = new Size(146, 26);
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.FromArgb(65, 75, 90);
            _page.Controls.Add(label);
        }

        private Label AddDescription(string text, int x, int y)
        {
            Label label = new Label();
            label.Location = new Point(x, y);
            label.Size = new Size(490, 25);
            label.Text = text;
            label.ForeColor = Color.FromArgb(129, 138, 151);
            _page.Controls.Add(label);
            return label;
        }

        private Label AddValueLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.Location = new Point(x, y);
            label.Size = new Size(65, 24);
            label.Text = text;
            label.ForeColor = Color.FromArgb(101, 111, 126);
            label.TextAlign = ContentAlignment.MiddleLeft;
            _page.Controls.Add(label);
            return label;
        }

        private static Button CreateColorButton(Color color, int y)
        {
            Button button = new Button();
            button.Location = new Point(190, y);
            button.Size = new Size(48, 31);
            button.BackColor = color;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(205, 211, 220);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private static bool ChooseColor(Color initialColor, out Color selectedColor)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = initialColor;
                dialog.FullOpen = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    selectedColor = dialog.Color;
                    return true;
                }
            }

            selectedColor = initialColor;
            return false;
        }
    }
}
