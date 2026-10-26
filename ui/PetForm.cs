using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Diagnostics;
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
    internal sealed class PetForm : Form
    {
        private struct ShakeSample
        {
            public Point Position;
            public DateTime Time;
        }

        [Flags]
        private enum SqueezeEdges
        {
            None = 0,
            Left = 1,
            Right = 2,
            Top = 4,
            Bottom = 8
        }

        private readonly string _nameFile;
        private readonly string _settingsFile;
        private Bitmap _surface;
        private Graphics _surfaceGraphics;
        private PetSettings _settings;
        private string _petName;
        private readonly SpeechContent _speechContent;
        private readonly CpuUsageMonitor _cpuUsageMonitor;
        private SettingsForm _settingsWindow;
        private readonly List<SpeechBubbleForm> _speechBubbles = new List<SpeechBubbleForm>();
        private readonly System.Windows.Forms.Timer _animationTimer;
        private readonly System.Windows.Forms.Timer _cpuTimer;
        private readonly System.Windows.Forms.Timer _chatTimer;
        private readonly System.Windows.Forms.Timer _deferredAmbientTimer;
        private readonly Random _chatRandom = new Random();
        private readonly Queue<ShakeSample> _shakeSamples = new Queue<ShakeSample>();
        private readonly Queue<DateTime> _shakeReversals = new Queue<DateTime>();
        private readonly string[] _ambientExpressions =
        {
            "happy", "surprised", "shy", "thoughtful", "sleepy",
            "curious", "relaxed", "playful", "dreamy", "proud"
        };
        private bool _isDragging;
        private bool _dizzyTriggered;
        private int _clickCount;
        private double _dragDistance;
        private Point _dragOffset;
        private Point _lastPointer;
        private Point _shakeAnchor;
        private int _shakeAxis = -1;
        private int _shakeDirection;
        private DateTime _dragStartedAt;
        private float _tilt;
        private float _idlePhase;
        private float _bounce;
        private float _squeeze;
        private SqueezeEdges _squeezeEdges;
        private string _faceExpression;
        private DateTime _expressionUntil;
        private DateTime _nextAmbientExpressionAt;
        private DateTime _nextSqueezeSpeechAt;
        private bool _pendingIdleSpeech;
        private bool _pendingCpuSpeech;
        private bool _isContextMenuOpen;
        private bool _cpuWarningArmed = true;

        public PetForm(
            string petName,
            string nameFile,
            string settingsFile,
            PetSettings settings,
            SpeechContent speechContent)
        {
            _petName = petName;
            _nameFile = nameFile;
            _settingsFile = settingsFile;
            _settings = settings;
            _speechContent = speechContent;
            _cpuUsageMonitor = new CpuUsageMonitor();
            _faceExpression = _speechContent.GetExpression("normal");
            _nextAmbientExpressionAt = DateTime.Now.AddSeconds(_chatRandom.Next(2, 6));

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(200, 200);
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            Cursor = Cursors.SizeAll;

            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem options = new ToolStripMenuItem("選項");
            ToolStripMenuItem settingsItem = new ToolStripMenuItem("設定");
            ToolStripMenuItem rename = new ToolStripMenuItem("重新命名");
            ToolStripMenuItem exit = new ToolStripMenuItem("退出應用");
            ToolStripMenuItem help = new ToolStripMenuItem("說明和使用");
            ToolStripMenuItem muteSpeech = new ToolStripMenuItem("禁止講話");
            muteSpeech.CheckOnClick = true;
            muteSpeech.Checked = _settings.SpeechMuted;
            settingsItem.Click += OpenSettings;
            rename.Click += RenamePet;
            exit.Click += delegate { Close(); };
            help.Click += ShowHelp;
            muteSpeech.Click += ToggleSpeechMute;
            options.DropDownItems.Add(settingsItem);
            options.DropDownItems.Add(rename);
            options.DropDownItems.Add(new ToolStripSeparator());
            options.DropDownItems.Add(exit);
            menu.Items.Add(options);
            menu.Items.Add(help);
            menu.Items.Add(muteSpeech);
            menu.Opening += delegate
            {
                _isContextMenuOpen = true;
            };
            menu.Closed += delegate
            {
                _isContextMenuOpen = false;
                if (_pendingIdleSpeech || _pendingCpuSpeech)
                {
                    _deferredAmbientTimer.Start();
                }
            };
            ContextMenuStrip = menu;

            MouseDown += StartDrag;
            MouseMove += ContinueDrag;
            MouseUp += FinishDrag;

            CreateSurface();
            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                workArea.Left + (workArea.Width - Width) / 2,
                workArea.Top + (workArea.Height - Height) / 2);

            _animationTimer = new System.Windows.Forms.Timer();
            _animationTimer.Interval = 30;
            _animationTimer.Tick += Animate;
            _animationTimer.Start();

            _cpuTimer = new System.Windows.Forms.Timer();
            _cpuTimer.Interval = 2000;
            _cpuTimer.Tick += CheckCpuUsage;
            _cpuTimer.Start();

            _chatTimer = new System.Windows.Forms.Timer();
            _chatTimer.Interval = 45000;
            _chatTimer.Tick += StartCasualChat;
            _chatTimer.Start();

            _deferredAmbientTimer = new System.Windows.Forms.Timer();
            _deferredAmbientTimer.Interval = 250;
            _deferredAmbientTimer.Tick += ShowDeferredAmbientSpeech;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x00080000;
                return parameters;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            RenderSurface();
            ShowSpeech("greeting");
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            foreach (SpeechBubbleForm bubble in _speechBubbles)
            {
                if (!bubble.IsDisposed && bubble.Visible)
                {
                    bubble.UpdatePosition(this);
                }
            }
        }

        private void ShowSpeech(string category)
        {
            if (_settings.SpeechMuted)
            {
                return;
            }

            SpeechLine line = _speechContent.GetRandomLine(category);
            SetExpression(line.Expression, 6000);
            SpeechBubbleForm bubble = new SpeechBubbleForm();
            bubble.FormClosed += delegate
            {
                _speechBubbles.Remove(bubble);
                if (_pendingIdleSpeech || _pendingCpuSpeech)
                {
                    _deferredAmbientTimer.Start();
                }
            };
            _speechBubbles.Add(bubble);
            bubble.ShowMessage(line.Text, this);
            RenderSurface();
            if (category != "idle" && _chatTimer != null)
            {
                _chatTimer.Stop();
                _chatTimer.Interval = 45000;
                _chatTimer.Start();
            }
        }

        private void StartCasualChat(object sender, EventArgs e)
        {
            _chatTimer.Stop();
            _chatTimer.Interval = _chatRandom.Next(35000, 70001);
            _chatTimer.Start();
            if (_isContextMenuOpen || _isDragging || HasVisibleSpeechBubbles())
            {
                _pendingIdleSpeech = true;
                _deferredAmbientTimer.Start();
                return;
            }

            ShowSpeech("idle");
        }

        private void ShowDeferredAmbientSpeech(object sender, EventArgs e)
        {
            if (!_pendingIdleSpeech && !_pendingCpuSpeech)
            {
                _deferredAmbientTimer.Stop();
                return;
            }

            if (_isContextMenuOpen || _isDragging || HasVisibleSpeechBubbles())
            {
                return;
            }

            _deferredAmbientTimer.Stop();
            if (_pendingCpuSpeech)
            {
                _pendingCpuSpeech = false;
                ShowSpeech("cpu");
            }
            else
            {
                _pendingIdleSpeech = false;
                ShowSpeech("idle");
            }

            if (_pendingIdleSpeech || _pendingCpuSpeech)
            {
                _deferredAmbientTimer.Start();
            }
        }

        private bool HasVisibleSpeechBubbles()
        {
            foreach (SpeechBubbleForm bubble in _speechBubbles)
            {
                if (!bubble.IsDisposed && bubble.Visible)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetExpression(string name, int durationMilliseconds)
        {
            _faceExpression = _speechContent.GetExpression(name);
            _expressionUntil = DateTime.Now.AddMilliseconds(durationMilliseconds);
        }

        private void CheckCpuUsage(object sender, EventArgs e)
        {
            try
            {
                float usage;
                if (!_cpuUsageMonitor.TryGetUsage(out usage))
                {
                    return;
                }

                if (usage < 75F)
                {
                    _cpuWarningArmed = true;
                }
                else if (usage > 90F && _cpuWarningArmed)
                {
                    _cpuWarningArmed = false;
                    if (_isContextMenuOpen || _isDragging || HasVisibleSpeechBubbles())
                    {
                        _pendingCpuSpeech = true;
                        _deferredAmbientTimer.Start();
                    }
                    else
                    {
                        ShowSpeech("cpu");
                        RenderSurface();
                    }
                }
            }
            catch (Win32Exception ex)
            {
                _cpuTimer.Stop();
                MessageBox.Show(
                    this,
                    "無法讀取 CPU 使用率：" + ex.Message,
                    "MeoPet",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void RenderSurface()
        {
            Graphics graphics = _surfaceGraphics;
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            float bob = _isDragging ? -5F : (float)Math.Sin(_idlePhase) * 2F + _bounce;
            float horizontalScale = 1F;
            float verticalScale = 1F;
            bool touchesHorizontalEdge = (_squeezeEdges & (SqueezeEdges.Left | SqueezeEdges.Right)) != 0;
            bool touchesVerticalEdge = (_squeezeEdges & (SqueezeEdges.Top | SqueezeEdges.Bottom)) != 0;
            if (touchesHorizontalEdge && touchesVerticalEdge)
            {
                horizontalScale = 1F - _squeeze * 0.18F;
                verticalScale = 1F - _squeeze * 0.18F;
            }
            else if (touchesHorizontalEdge)
            {
                horizontalScale = 1F - _squeeze * 0.3F;
                verticalScale = 1F + _squeeze * 0.12F;
            }
            else if (touchesVerticalEdge)
            {
                horizontalScale = 1F + _squeeze * 0.12F;
                verticalScale = 1F - _squeeze * 0.3F;
            }

            RectangleF sphere = new RectangleF(
                25F + _tilt * 0.16F,
                25F + bob,
                _settings.Width,
                _settings.Height);
            PointF anchor = GetSqueezeAnchor(sphere);
            graphics.TranslateTransform(anchor.X, anchor.Y);
            graphics.ScaleTransform(horizontalScale, verticalScale);
            graphics.TranslateTransform(-anchor.X, -anchor.Y);

            DrawShadow(graphics, sphere);
            DrawSphere(graphics, sphere);
            DrawFace(graphics, sphere);

            graphics.ResetTransform();
            UpdateLayeredSurface();
        }

        private PointF GetSqueezeAnchor(RectangleF sphere)
        {
            float x = sphere.Left + sphere.Width / 2F;
            float y = sphere.Top + sphere.Height / 2F;

            if ((_squeezeEdges & SqueezeEdges.Left) != 0)
            {
                x = sphere.Left;
            }
            else if ((_squeezeEdges & SqueezeEdges.Right) != 0)
            {
                x = sphere.Right;
            }

            if ((_squeezeEdges & SqueezeEdges.Top) != 0)
            {
                y = sphere.Top;
            }
            else if ((_squeezeEdges & SqueezeEdges.Bottom) != 0)
            {
                y = sphere.Bottom;
            }

            return new PointF(x, y);
        }

        private void DrawSphere(Graphics graphics, RectangleF bounds)
        {
            using (GraphicsPath spherePath = new GraphicsPath())
            {
                spherePath.AddEllipse(bounds);
                using (PathGradientBrush body = new PathGradientBrush(spherePath))
                {
                    body.CenterPoint = new PointF(bounds.Left + bounds.Width * 0.34F, bounds.Top + bounds.Height * 0.27F);
                    Color baseColor = Color.FromArgb(_settings.BodyColorArgb);
                    body.CenterColor = Lighten(baseColor, 0.18F);
                    body.SurroundColors = new Color[] { Darken(baseColor, 0.25F) };
                    body.FocusScales = new PointF(0.72F, 0.69F);
                    graphics.FillPath(body, spherePath);
                }

                using (Pen edge = new Pen(Color.FromArgb(45, 116, 127, 143), 1F))
                {
                    graphics.DrawPath(edge, spherePath);
                }
            }

            RectangleF shineBounds = new RectangleF(
                bounds.Left + bounds.Width * 0.15F + _tilt * 0.18F,
                bounds.Top + bounds.Height * 0.07F,
                bounds.Width * 0.48F,
                bounds.Height * 0.34F);
            using (GraphicsPath shinePath = new GraphicsPath())
            {
                shinePath.AddEllipse(shineBounds);
                using (PathGradientBrush shine = new PathGradientBrush(shinePath))
                {
                    shine.CenterColor = Color.FromArgb(106, 255, 255, 255);
                    shine.SurroundColors = new Color[] { Color.FromArgb(0, 255, 255, 255) };
                    graphics.FillPath(shine, shinePath);
                }
            }

            RectangleF lowerShine = new RectangleF(
                bounds.Left + bounds.Width * 0.68F,
                bounds.Top + bounds.Height * 0.66F,
                bounds.Width * 0.16F,
                bounds.Height * 0.09F);
            using (SolidBrush reflection = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
            {
                graphics.FillEllipse(reflection, lowerShine);
            }
        }

        private void DrawShadow(Graphics graphics, RectangleF sphere)
        {
            int opacity = _settings.ShadowOpacity;
            if (opacity <= 0)
            {
                return;
            }

            Color selected = Color.FromArgb(_settings.ShadowColorArgb);
            int alpha = selected.A * opacity / 100;
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, selected)))
            {
                float width = sphere.Width * 0.68F;
                float height = Math.Max(5F, sphere.Height * 0.1F);
                graphics.FillEllipse(
                    brush,
                    sphere.Left + (sphere.Width - width) / 2F,
                    sphere.Bottom - height * 0.2F,
                    width,
                    height);
            }
        }

        private static Color Lighten(Color color, float amount)
        {
            return Color.FromArgb(
                color.A,
                (int)(color.R + (255 - color.R) * amount),
                (int)(color.G + (255 - color.G) * amount),
                (int)(color.B + (255 - color.B) * amount));
        }

        private static Color Darken(Color color, float amount)
        {
            return Color.FromArgb(
                color.A,
                (int)(color.R * (1F - amount)),
                (int)(color.G * (1F - amount)),
                (int)(color.B * (1F - amount)));
        }

        private void DrawFace(Graphics graphics, RectangleF sphere)
        {
            using (Brush brush = new SolidBrush(Color.FromArgb(47, 55, 68)))
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;

                float faceHeight = Math.Min(30F, sphere.Height * 0.42F);
                RectangleF faceBounds = new RectangleF(
                    sphere.Left + sphere.Width * 0.05F,
                    sphere.Top + (sphere.Height - faceHeight) / 2F,
                    sphere.Width * 0.9F,
                    faceHeight);
                Font font = new Font("Microsoft JhengHei UI", 11F, FontStyle.Regular, GraphicsUnit.Point);

                while (font.Size > 7F)
                {
                    SizeF textSize = graphics.MeasureString(_faceExpression, font, Int32.MaxValue, format);
                    if (textSize.Width <= faceBounds.Width - 4F && textSize.Height <= faceBounds.Height - 2F)
                    {
                        break;
                    }

                    float nextSize = font.Size - 0.25F;
                    font.Dispose();
                    font = new Font("Microsoft JhengHei UI", nextSize, FontStyle.Regular, GraphicsUnit.Point);
                }

                using (font)
                {
                    graphics.DrawString(_faceExpression, font, brush, faceBounds, format);
                }
            }
        }

        private void UpdateLayeredSurface()
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            IntPtr memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            IntPtr bitmapHandle = IntPtr.Zero;
            IntPtr previousBitmap = IntPtr.Zero;
            try
            {
                BitmapV4Header header = new BitmapV4Header();
                header.Size = (uint)Marshal.SizeOf(typeof(BitmapV4Header));
                header.Width = _surface.Width;
                header.Height = -_surface.Height;
                header.Planes = 1;
                header.BitCount = 32;
                header.Compression = 3;
                header.SizeImage = (uint)(_surface.Width * _surface.Height * 4);
                header.RedMask = 0x00FF0000;
                header.GreenMask = 0x0000FF00;
                header.BlueMask = 0x000000FF;
                header.AlphaMask = 0xFF000000;
                header.ColorSpaceType = 0x73524742;

                IntPtr pixels;
                bitmapHandle = CreateDIBSection(
                    screenDc,
                    ref header,
                    0,
                    out pixels,
                    IntPtr.Zero,
                    0);
                if (bitmapHandle == IntPtr.Zero || pixels == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                CopySurfacePixels(pixels);
                previousBitmap = SelectObject(memoryDc, bitmapHandle);
                if (previousBitmap == IntPtr.Zero || previousBitmap == new IntPtr(-1))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                Point destination = Location;
                Point source = Point.Empty;
                Size size = _surface.Size;
                BlendFunction blend = new BlendFunction();
                blend.BlendOp = 0;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = 1;

                if (!UpdateLayeredWindow(
                    Handle,
                    screenDc,
                    ref destination,
                    ref size,
                    memoryDc,
                    ref source,
                    0,
                    ref blend,
                    2))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                if (previousBitmap != IntPtr.Zero && previousBitmap != new IntPtr(-1))
                {
                    SelectObject(memoryDc, previousBitmap);
                }

                if (bitmapHandle != IntPtr.Zero)
                {
                    DeleteObject(bitmapHandle);
                }

                DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private void CopySurfacePixels(IntPtr destination)
        {
            _surfaceGraphics.Flush(FlushIntention.Sync);
            BitmapData data = _surface.LockBits(
                new Rectangle(0, 0, _surface.Width, _surface.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppPArgb);
            try
            {
                int rowLength = _surface.Width * 4;
                byte[] row = new byte[rowLength];
                int sourceStride = Math.Abs(data.Stride);

                for (int y = 0; y < _surface.Height; y++)
                {
                    int sourceRow = data.Stride >= 0 ? y : _surface.Height - 1 - y;
                    IntPtr source = IntPtr.Add(data.Scan0, sourceRow * sourceStride);
                    IntPtr target = IntPtr.Add(destination, y * rowLength);
                    Marshal.Copy(source, row, 0, rowLength);
                    Marshal.Copy(row, 0, target, rowLength);
                }
            }
            finally
            {
                _surface.UnlockBits(data);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapV4Header
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ColorsUsed;
            public uint ColorsImportant;
            public uint RedMask;
            public uint GreenMask;
            public uint BlueMask;
            public uint AlphaMask;
            public uint ColorSpaceType;
            public int EndpointRedX;
            public int EndpointRedY;
            public int EndpointRedZ;
            public int EndpointGreenX;
            public int EndpointGreenY;
            public int EndpointGreenZ;
            public int EndpointBlueX;
            public int EndpointBlueY;
            public int EndpointBlueZ;
            public uint GammaRed;
            public uint GammaGreen;
            public uint GammaBlue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr window,
            IntPtr destinationDc,
            ref Point destinationPoint,
            ref Size size,
            IntPtr sourceDc,
            ref Point sourcePoint,
            int colorKey,
            ref BlendFunction blend,
            int flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(
            IntPtr deviceContext,
            ref BitmapV4Header bitmapInfo,
            uint usage,
            out IntPtr bits,
            IntPtr section,
            uint offset);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr graphicsObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr graphicsObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr deviceContext);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

        private void StartDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _isDragging = true;
            _dizzyTriggered = false;
            _dragDistance = 0D;
            _squeezeEdges = SqueezeEdges.None;
            _dragOffset = e.Location;
            _lastPointer = Cursor.Position;
            _shakeAnchor = _lastPointer;
            _shakeAxis = -1;
            _shakeDirection = 0;
            _shakeSamples.Clear();
            _shakeReversals.Clear();
            _shakeSamples.Enqueue(new ShakeSample { Position = _lastPointer, Time = DateTime.UtcNow });
            _dragStartedAt = DateTime.Now;
            Capture = true;
            RenderSurface();
        }

        private void ContinueDrag(object sender, MouseEventArgs e)
        {
            if (!_isDragging || e.Button != MouseButtons.Left)
            {
                return;
            }

            Point pointer = Cursor.Position;
            int deltaX = pointer.X - _lastPointer.X;
            int deltaY = pointer.Y - _lastPointer.Y;
            _dragDistance += Math.Sqrt((double)deltaX * deltaX + (double)deltaY * deltaY);
            UpdateShakeDetection(pointer);
            _tilt = Math.Max(-11F, Math.Min(11F, deltaX * 0.7F));
            _lastPointer = pointer;

            Rectangle screen = Screen.FromPoint(pointer).WorkingArea;
            int x = pointer.X - _dragOffset.X;
            int y = pointer.Y - _dragOffset.Y;
            x = Math.Max(screen.Left - 25, Math.Min(screen.Right - Width + 25, x));
            y = Math.Max(screen.Top - 25, Math.Min(screen.Bottom - Height + 25, y));
            Location = new Point(x, y);
            SqueezeEdges previousSqueezeEdges = _squeezeEdges;
            _squeezeEdges = _settings.SqueezeAtEdge ? DetectSqueezeEdges(screen) : SqueezeEdges.None;
            if (previousSqueezeEdges == SqueezeEdges.None &&
                _squeezeEdges != SqueezeEdges.None &&
                DateTime.Now >= _nextSqueezeSpeechAt)
            {
                _nextSqueezeSpeechAt = DateTime.Now.AddSeconds(5);
                ShowSpeech("squeeze");
            }

            if (!_dizzyTriggered && IsVigorousShake())
            {
                _dizzyTriggered = true;
                ShowSpeech("dizzy");
            }

            RenderSurface();
        }

        private void UpdateShakeDetection(Point pointer)
        {
            DateTime now = DateTime.UtcNow;
            _shakeSamples.Enqueue(new ShakeSample { Position = pointer, Time = now });
            while (_shakeSamples.Count > 1 &&
                (now - _shakeSamples.Peek().Time).TotalMilliseconds > 1400)
            {
                _shakeSamples.Dequeue();
            }

            while (_shakeReversals.Count > 0 &&
                (now - _shakeReversals.Peek()).TotalMilliseconds > 1400)
            {
                _shakeReversals.Dequeue();
            }

            int accumulatedX = pointer.X - _shakeAnchor.X;
            int accumulatedY = pointer.Y - _shakeAnchor.Y;
            int axis = Math.Abs(accumulatedX) >= Math.Abs(accumulatedY) ? 0 : 1;
            int movement = axis == 0 ? accumulatedX : accumulatedY;
            if (Math.Abs(movement) < 24)
            {
                return;
            }

            int direction = movement < 0 ? -1 : 1;
            if (_shakeAxis == axis && _shakeDirection != 0 && direction != _shakeDirection)
            {
                _shakeReversals.Enqueue(now);
            }

            _shakeAxis = axis;
            _shakeDirection = direction;
            _shakeAnchor = pointer;
        }

        private bool IsVigorousShake()
        {
            if (_shakeReversals.Count < 4 || _shakeSamples.Count < 2)
            {
                return false;
            }

            ShakeSample[] samples = _shakeSamples.ToArray();
            double recentTravel = 0D;
            for (int i = 1; i < samples.Length; i++)
            {
                int dx = samples[i].Position.X - samples[i - 1].Position.X;
                int dy = samples[i].Position.Y - samples[i - 1].Position.Y;
                recentTravel += Math.Sqrt((double)dx * dx + (double)dy * dy);
            }

            return recentTravel >= 300D;
        }

        private SqueezeEdges DetectSqueezeEdges(Rectangle screen)
        {
            int sphereLeft = Left + 25;
            int sphereTop = Top + 25;
            int leftGap = Math.Abs(sphereLeft - screen.Left);
            int rightGap = Math.Abs(sphereLeft + _settings.Width - screen.Right);
            int topGap = Math.Abs(sphereTop - screen.Top);
            int bottomGap = Math.Abs(sphereTop + _settings.Height - screen.Bottom);

            const int edgeThreshold = 12;
            SqueezeEdges edges = SqueezeEdges.None;
            if (leftGap <= edgeThreshold)
            {
                edges |= SqueezeEdges.Left;
            }

            if (rightGap <= edgeThreshold)
            {
                edges |= SqueezeEdges.Right;
            }

            if (topGap <= edgeThreshold)
            {
                edges |= SqueezeEdges.Top;
            }

            if (bottomGap <= edgeThreshold)
            {
                edges |= SqueezeEdges.Bottom;
            }

            return edges;
        }

        private void FinishDrag(object sender, MouseEventArgs e)
        {
            if (!_isDragging || e.Button != MouseButtons.Left)
            {
                return;
            }

            bool wasClick = _dragDistance < 8D &&
                (DateTime.Now - _dragStartedAt).TotalMilliseconds < 700;
            _isDragging = false;
            _bounce = -5F;
            Capture = false;
            if (wasClick)
            {
                _clickCount++;

                if (_clickCount == 20)
                {
                    _clickCount = 0;
                    ShowSpeech("angry");
                }
                else
                {
                    ShowSpeech("click");
                }
            }

            RenderSurface();
        }

        private void Animate(object sender, EventArgs e)
        {
            if (_expressionUntil != DateTime.MinValue && DateTime.Now >= _expressionUntil)
            {
                _faceExpression = _speechContent.GetExpression("normal");
                _expressionUntil = DateTime.MinValue;
                _nextAmbientExpressionAt = DateTime.Now.AddSeconds(_chatRandom.Next(2, 6));
            }

            if (_expressionUntil == DateTime.MinValue &&
                DateTime.Now >= _nextAmbientExpressionAt &&
                !HasVisibleSpeechBubbles())
            {
                string expression = _ambientExpressions[_chatRandom.Next(_ambientExpressions.Length)];
                SetAmbientExpression(expression, _chatRandom.Next(1400, 3001));
            }

            if (!_isDragging)
            {
                _idlePhase += 0.045F;
                _tilt *= 0.76F;
                _bounce *= 0.83F;
                if (Math.Abs(_bounce) < 0.15F)
                {
                    _bounce = 0F;
                }
            }

            float targetSqueeze = _isDragging && _settings.SqueezeAtEdge && _squeezeEdges != SqueezeEdges.None
                ? 1F
                : 0F;
            _squeeze += (targetSqueeze - _squeeze) * 0.2F;
            if (Math.Abs(_squeeze) < 0.01F)
            {
                _squeeze = 0F;
                if (!_isDragging)
                {
                    _squeezeEdges = SqueezeEdges.None;
                }
            }

            RenderSurface();
        }

        private void SetAmbientExpression(string name, int durationMilliseconds)
        {
            _faceExpression = _speechContent.GetExpression(name);
            _expressionUntil = DateTime.Now.AddMilliseconds(durationMilliseconds);
        }

        private void OpenSettings(object sender, EventArgs e)
        {
            if (_settingsWindow == null || _settingsWindow.IsDisposed)
            {
                _settingsWindow = new SettingsForm(
                    _settings,
                    ApplySettings,
                    ClearApplicationData,
                    DownloadUpdate,
                    StartUpdate);
            }

            if (!_settingsWindow.Visible)
            {
                _settingsWindow.Show(this);
            }

            _settingsWindow.WindowState = FormWindowState.Normal;
            _settingsWindow.BringToFront();
        }

        private void ShowHelp(object sender, EventArgs e)
        {
            MessageBox.Show(
                this,
                "MeoPet 使用說明" + Environment.NewLine + Environment.NewLine +
                "• 按住寵物並拖曳，可以移動牠。" + Environment.NewLine +
                "• 快速點擊寵物，牠會做出反應並說話。" + Environment.NewLine +
                "• 快速大力來回晃動寵物，牠會感到頭暈。" + Environment.NewLine +
                "• 在寵物上按右鍵，可開啟本選單。" + Environment.NewLine +
                "• 從「選項」可開啟設定、重新命名或退出應用。" + Environment.NewLine +
                "• 選取「禁止講話」可切換是否顯示對話。" + Environment.NewLine +
                "• 需要關閉 MeoPet 時，請用右鍵選單中的「選項」→「退出應用」。",
                "說明和使用",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ToggleSpeechMute(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            PetSettings changed = new PetSettings
            {
                Width = _settings.Width,
                Height = _settings.Height,
                BodyColorArgb = _settings.BodyColorArgb,
                ShadowColorArgb = _settings.ShadowColorArgb,
                ShadowOpacity = _settings.ShadowOpacity,
                AutoStart = _settings.AutoStart,
                SqueezeAtEdge = _settings.SqueezeAtEdge,
                SpeechMuted = item.Checked
            };

            if (!ApplySettings(changed))
            {
                item.Checked = !item.Checked;
            }
            else if (item.Checked)
            {
                foreach (SpeechBubbleForm bubble in _speechBubbles.ToArray())
                {
                    if (!bubble.IsDisposed)
                    {
                        bubble.Close();
                    }
                }
            }
        }

        private string DownloadUpdate()
        {
            return UpdateService.DownloadUpdateIfAvailable(AppInstaller.GetInstallDirectory());
        }

        private bool StartUpdate(string stagedDirectory)
        {
            try
            {
                string workerDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "MeoPet-updater-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(workerDirectory);
                string workerExecutable = Path.Combine(workerDirectory, "MeoPetUpdater.exe");
                File.Copy(Application.ExecutablePath, workerExecutable);

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = workerExecutable;
                startInfo.Arguments = "--apply-update \"" +
                    stagedDirectory +
                    "\" \"" +
                    AppInstaller.GetInstallDirectory() +
                    "\"";
                startInfo.WorkingDirectory = workerDirectory;
                startInfo.UseShellExecute = true;
                if (Process.Start(startInfo) == null)
                {
                    throw new InvalidOperationException("更新程序沒有成功啟動。");
                }

                Application.Exit();
                return true;
            }
            catch (IOException ex)
            {
                ShowSettingsError("無法啟動更新", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowSettingsError("沒有權限啟動更新", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                ShowSettingsError("無法啟動更新", ex.Message);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                ShowSettingsError("無法啟動更新", ex.Message);
            }

            return false;
        }

        private bool ApplySettings(PetSettings changed)
        {
            bool startupChanged = changed.AutoStart != _settings.AutoStart;
            bool startupUpdated = false;
            try
            {
                changed.Validate();
                if (startupChanged)
                {
                    StartupSettings.SetEnabled(changed.AutoStart);
                    startupUpdated = true;
                }

                changed.Save(_settingsFile);
                _settings = changed;

                Point center = new Point(Left + Width / 2, Top + Height / 2);
                CreateSurface();
                Location = new Point(center.X - Width / 2, center.Y - Height / 2);
                RenderSurface();
                return true;
            }
            catch (IOException ex)
            {
                RestoreStartupSetting(startupUpdated, startupChanged);
                ShowSettingsError("無法儲存設定", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                RestoreStartupSetting(startupUpdated, startupChanged);
                ShowSettingsError("沒有權限儲存設定", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                RestoreStartupSetting(startupUpdated, startupChanged);
                ShowSettingsError("無法套用設定", ex.Message);
            }
            catch (SecurityException ex)
            {
                RestoreStartupSetting(startupUpdated, startupChanged);
                ShowSettingsError("沒有權限變更開機啟動設定", ex.Message);
            }

            return false;
        }

        private bool ClearApplicationData()
        {
            try
            {
                StartupSettings.SetEnabled(false);
                File.Delete(_nameFile);
                File.Delete(_settingsFile);
                _petName = String.Empty;
                _settings = new PetSettings();

                Point center = new Point(Left + Width / 2, Top + Height / 2);
                CreateSurface();
                Location = new Point(center.X - Width / 2, center.Y - Height / 2);
                RenderSurface();
                return true;
            }
            catch (IOException ex)
            {
                ShowSettingsError("無法清空資料", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowSettingsError("沒有權限清空資料", ex.Message);
            }
            catch (SecurityException ex)
            {
                ShowSettingsError("沒有權限清空資料", ex.Message);
            }

            return false;
        }

        private void RestoreStartupSetting(bool startupUpdated, bool startupChanged)
        {
            if (!startupUpdated || !startupChanged)
            {
                return;
            }

            try
            {
                StartupSettings.SetEnabled(_settings.AutoStart);
            }
            catch (IOException ex)
            {
                ShowSettingsError("無法還原開機啟動設定", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                ShowSettingsError("沒有權限還原開機啟動設定", ex.Message);
            }
            catch (SecurityException ex)
            {
                ShowSettingsError("沒有權限還原開機啟動設定", ex.Message);
            }
        }

        private void ShowSettingsError(string title, string message)
        {
            MessageBox.Show(
                this,
                message,
                "MeoPet — " + title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void CreateSurface()
        {
            if (_surfaceGraphics != null)
            {
                _surfaceGraphics.Dispose();
                _surface.Dispose();
            }

            ClientSize = new Size(_settings.Width + 50, _settings.Height + 50);
            _surface = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
            _surfaceGraphics = Graphics.FromImage(_surface);
        }

        private void RenamePet(object sender, EventArgs e)
        {
            using (NameDialog dialog = new NameDialog(_petName))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    PetStorage.SaveName(_nameFile, dialog.PetName);
                    _petName = dialog.PetName;
                    RenderSurface();
                }
                catch (IOException ex)
                {
                    MessageBox.Show(
                        this,
                        "無法儲存寵物名字：" + ex.Message,
                        "MeoPet",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show(
                        this,
                        "沒有權限儲存寵物名字：" + ex.Message,
                        "MeoPet",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _animationTimer != null)
            {
                _animationTimer.Stop();
                _animationTimer.Dispose();
                if (_chatTimer != null)
                {
                    _chatTimer.Stop();
                    _chatTimer.Dispose();
                }

                if (_cpuTimer != null)
                {
                    _cpuTimer.Stop();
                    _cpuTimer.Dispose();
                }

                if (_deferredAmbientTimer != null)
                {
                    _deferredAmbientTimer.Stop();
                    _deferredAmbientTimer.Dispose();
                }

                foreach (SpeechBubbleForm bubble in _speechBubbles.ToArray())
                {
                    if (!bubble.IsDisposed)
                    {
                        bubble.Close();
                        bubble.Dispose();
                    }
                }

                _speechBubbles.Clear();
                _surfaceGraphics.Dispose();
                _surface.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
