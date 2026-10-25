using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MeoPet
{
    internal sealed class SpeechBubbleForm : Form
    {
        private const int BubbleWidth = 320;
        private const int BubbleHeight = 112;
        private const int CornerRadius = 14;
        private const int TailHalfWidth = 10;
        private const int TailDepth = 18;

        private readonly System.Windows.Forms.Timer _hideTimer;
        private string _message = String.Empty;
        private bool _tailAtTop;
        private int _tailCenter = BubbleWidth / 2;

        public SpeechBubbleForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            ShowIcon = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(BubbleWidth, BubbleHeight);
            BackColor = Color.FromArgb(255, 255, 253, 247);
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            UpdateBubbleRegion();

            _hideTimer = new System.Windows.Forms.Timer();
            _hideTimer.Interval = 5000;
            _hideTimer.Tick += delegate
            {
                _hideTimer.Stop();
                Close();
            };
        }

        public void ShowMessage(string message, Form pet)
        {
            _message = message;
            UpdatePosition(pet);
            Show();
            BringToFront();
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        public void UpdatePosition(Form pet)
        {
            Point petCenter = pet.PointToScreen(
                new Point(pet.ClientSize.Width / 2, pet.ClientSize.Height / 2));
            Rectangle workingArea = Screen.FromPoint(petCenter).WorkingArea;
            int sphereTop = pet.Top + 25;
            int sphereBottom = sphereTop + pet.ClientSize.Height - 50;
            int spaceAbove = sphereTop - workingArea.Top;
            int spaceBelow = workingArea.Bottom - sphereBottom;
            bool tailAtTop = spaceAbove < BubbleHeight - 5 && spaceBelow > spaceAbove;

            int x = petCenter.X - BubbleWidth / 2;
            int y = tailAtTop ? sphereBottom - 5 : sphereTop - BubbleHeight + 5;
            x = Math.Max(workingArea.Left, Math.Min(workingArea.Right - BubbleWidth, x));
            y = Math.Max(workingArea.Top, Math.Min(workingArea.Bottom - BubbleHeight, y));
            int tailCenter = Math.Max(
                30,
                Math.Min(BubbleWidth - 30, petCenter.X - x));

            if (_tailAtTop != tailAtTop || _tailCenter != tailCenter)
            {
                _tailAtTop = tailAtTop;
                _tailCenter = tailCenter;
                UpdateBubbleRegion();
                Invalidate();
            }

            Location = new Point(x, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (GraphicsPath bubble = CreateBubblePath())
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(255, 255, 253, 247)))
            using (Pen outline = new Pen(Color.FromArgb(226, 224, 216), 1.4F))
            {
                graphics.FillPath(fill, bubble);
                graphics.DrawPath(outline, bubble);
            }

            using (Font font = new Font("Microsoft JhengHei UI", 13F, FontStyle.Regular))
            using (Brush brush = new SolidBrush(Color.FromArgb(57, 65, 78)))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisWord;
                RectangleF textBounds = _tailAtTop
                    ? new RectangleF(20, 35, Width - 40, 61)
                    : new RectangleF(20, 12, Width - 40, 61);
                graphics.DrawString(_message, font, brush, textBounds, format);
            }
        }

        private void UpdateBubbleRegion()
        {
            using (GraphicsPath bubble = CreateBubblePath())
            {
                Region oldRegion = Region;
                Region = new Region(bubble);
                if (oldRegion != null)
                {
                    oldRegion.Dispose();
                }
            }
        }

        private GraphicsPath CreateBubblePath()
        {
            GraphicsPath bubble = new GraphicsPath();
            float left = 4F;
            float right = Width - 4F;
            float top = _tailAtTop ? TailDepth + 1F : 5F;
            float bottom = _tailAtTop ? Height - 5F : Height - TailDepth - 6F;
            float radius = CornerRadius;
            float diameter = radius * 2F;
            float tailLeft = _tailCenter - TailHalfWidth;
            float tailRight = _tailCenter + TailHalfWidth;

            bubble.StartFigure();
            bubble.AddLine(left + radius, top, _tailAtTop ? tailLeft : right - radius, top);
            if (_tailAtTop)
            {
                bubble.AddLine(tailLeft, top, _tailCenter, 5F);
                bubble.AddLine(_tailCenter, 5F, tailRight, top);
                bubble.AddLine(tailRight, top, right - radius, top);
            }

            bubble.AddArc(right - diameter, top, diameter, diameter, 270F, 90F);
            bubble.AddLine(right, top + radius, right, bottom - radius);
            bubble.AddArc(right - diameter, bottom - diameter, diameter, diameter, 0F, 90F);

            if (_tailAtTop)
            {
                bubble.AddLine(right - radius, bottom, left + radius, bottom);
            }
            else
            {
                bubble.AddLine(right - radius, bottom, tailRight, bottom);
                bubble.AddLine(tailRight, bottom, _tailCenter, Height - 5F);
                bubble.AddLine(_tailCenter, Height - 5F, tailLeft, bottom);
                bubble.AddLine(tailLeft, bottom, left + radius, bottom);
            }

            bubble.AddArc(left, bottom - diameter, diameter, diameter, 90F, 90F);
            bubble.AddLine(left, bottom - radius, left, top + radius);
            bubble.AddArc(left, top, diameter, diameter, 180F, 90F);
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
