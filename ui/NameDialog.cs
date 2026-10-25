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
    internal sealed class NameDialog : Form
    {
        private readonly TextBox _nameInput;
        private readonly Label _validation;

        public string PetName
        {
            get { return _nameInput.Text.Trim(); }
        }

        public NameDialog(string currentName)
        {
            Text = "MeoPet";
            ClientSize = new Size(370, 228);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            Font = new Font("Microsoft JhengHei UI", 9F);
            BackColor = Color.White;

            Label title = new Label();
            title.AutoSize = false;
            title.Location = new Point(26, 20);
            title.Size = new Size(320, 35);
            title.Font = new Font("Microsoft JhengHei UI", 16F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(48, 57, 71);
            title.Text = String.IsNullOrEmpty(currentName) ? "認識新朋友" : "幫小夥伴改名字";
            Controls.Add(title);

            Label description = new Label();
            description.AutoSize = false;
            description.Location = new Point(28, 62);
            description.Size = new Size(320, 40);
            description.ForeColor = Color.FromArgb(112, 122, 137);
            description.Text = String.IsNullOrEmpty(currentName)
                ? "替牠取個名字，牠就會出現在桌面上陪你。"
                : "輸入新名字，再和牠繼續一起生活。";
            Controls.Add(description);

            Label nameLabel = new Label();
            nameLabel.AutoSize = true;
            nameLabel.Location = new Point(29, 111);
            nameLabel.ForeColor = Color.FromArgb(75, 85, 101);
            nameLabel.Text = "小夥伴的名字";
            Controls.Add(nameLabel);

            _nameInput = new TextBox();
            _nameInput.Location = new Point(28, 134);
            _nameInput.Size = new Size(315, 27);
            _nameInput.MaxLength = 16;
            _nameInput.Text = currentName;
            _nameInput.Font = new Font("Microsoft JhengHei UI", 10F);
            Controls.Add(_nameInput);

            _validation = new Label();
            _validation.AutoSize = false;
            _validation.Location = new Point(29, 164);
            _validation.Size = new Size(205, 22);
            _validation.ForeColor = Color.FromArgb(181, 85, 99);
            Controls.Add(_validation);

            Button submit = new Button();
            submit.Location = new Point(239, 169);
            submit.Size = new Size(104, 34);
            submit.Text = "確定";
            submit.DialogResult = DialogResult.None;
            submit.Click += SubmitName;
            Controls.Add(submit);

            AcceptButton = submit;
            CancelButton = new Button { DialogResult = DialogResult.Cancel };
            Shown += delegate { _nameInput.Focus(); };
        }

        private void SubmitName(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(_nameInput.Text))
            {
                _validation.Text = "請先幫牠取個名字。";
                _nameInput.Focus();
                DialogResult = DialogResult.None;
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
