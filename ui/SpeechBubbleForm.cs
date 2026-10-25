using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MeoPet
{
    internal sealed class SpeechBubbleForm : Form
    {
        private readonly System.Windows.Forms.Timer _hideTimer;
        private string _message = String.Empty;
        private bool _tailAtTop;

        public SpeechBubbleForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            ShowIcon = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(320, 112);
            BackColor = Color.FromArgb(255, 255, 253, 247);
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            using (GraphicsPath bubble = CreateBubblePath())
            {
                Region = new Region(bubble);
            }

            _hideTimer = new System.Windows.Forms.Timer();
            _hideTimer.Interval = 5000;
            _hideTimer.Tick += delegate
            {
                _hideTimer.Stop();
                Hide();
            };
        }

        public void ShowMessage(string message, Form pet)
        {
            _message = message;
            UpdatePosition(pet);
            Invalidate();
            Show();
            BringToFront();
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        public void UpdatePosition(Form pet)
        {
            Point petCenter = pet.PointToScreen(new Point(pet.ClientSize.Width / 2, pet.ClientSize.Height / 2));
            Rectangle workingArea = Screen.FromPoint(petCenter).WorkingArea;
            int sphereTop = pet.Top + 25;
            int sphereBottom = sphereTop + pet.ClientSize.Height - 50;
            int spaceAbove = sphereTop - workingArea.Top;
            int spaceBelow = workingArea.Bottom - sphereBottom;
            bool tailAtTop = spaceAbove < Height - 5 && spaceBelow > spaceAbove;
            if (tailAtTop != _tailAtTop)
            {
                if (Region != null)
                {
                    Region.Dispose();
                }

                _tailAtTop = tailAtTop;
                using (GraphicsPath bubble = CreateBubblePath())
                {
                    Region = new Region(bubble);
                }
            }

            int x = petCenter.X - Width / 2;
            int y = tailAtTop ? sphereBottom - 5 : sphereTop - Height + 5;
            x = Math.Max(workingArea.Left, Math.Min(workingArea.Right - Width, x));
            y = Math.Max(workingArea.Top, Math.Min(workingArea.Bottom - Height, y));
            Location = new Point(x, y);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (GraphicsPath bubble = CreateBubblePath())
            {
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(255, 255, 253, 247)))
                using (Pen outline = new Pen(Color.FromArgb(226, 224, 216), 1.4F))
                {
                    graphics.FillPath(fill, bubble);
                    graphics.DrawPath(outline, bubble);
                }
            }

            using (Font font = new Font("Microsoft JhengHei UI", 13F, FontStyle.Regular))
            using (Brush brush = new SolidBrush(Color.FromArgb(57, 65, 78)))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                float textTop = _tailAtTop ? 30F : 12F;
                float textHeight = _tailAtTop ? 56F : 70F;
                graphics.DrawString(_message, font, brush, new RectangleF(20, textTop, Width - 40, textHeight), format);
            }
        }

        private GraphicsPath CreateBubblePath()
        {
            GraphicsPath bubble = new GraphicsPath();
            if (_tailAtTop)
            {
                bubble.AddArc(8, 22, 22, 22, 180, 90);
                bubble.AddLine(19, 22, Width / 2 - 10, 22);
                bubble.AddLine(Width / 2 - 10, 22, Width / 2, 5);
                bubble.AddLine(Width / 2, 5, Width / 2 + 10, 22);
                bubble.AddLine(Width / 2 + 10, 22, Width - 19, 22);
                bubble.AddArc(Width - 30, 22, 22, 22, 270, 90);
                bubble.AddLine(Width - 8, 33, Width - 8, 79);
                bubble.AddArc(Width - 30, 68, 22, 22, 0, 90);
                bubble.AddLine(Width - 19, 90, 19, 90);
                bubble.AddArc(8, 68, 22, 22, 90, 90);
                bubble.AddLine(8, 79, 8, 33);
                bubble.CloseFigure();
                return bubble;
            }

            bubble.AddArc(8, 5, 22, 22, 180, 90);
            bubble.AddArc(Width - 30, 5, 22, 22, 270, 90);
            bubble.AddArc(Width - 30, 68, 22, 22, 0, 90);
            bubble.AddLine(Width - 8, 90, Width / 2 + 10, 90);
            bubble.AddLine(Width / 2 + 10, 90, Width / 2, 107);
            bubble.AddLine(Width / 2, 107, Width / 2 - 10, 90);
            bubble.AddLine(Width / 2 - 10, 90, 19, 90);
            bubble.AddArc(8, 68, 22, 22, 90, 90);
            bubble.CloseFigure();
            return bubble;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hideTimer.Stop();
                _hideTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
