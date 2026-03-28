using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SortMyMedia
{
    public sealed class ModeSelectionForm : Form
    {
        private readonly PictureBox logoBox = new();
        private readonly Label titleLabel = new();
        private readonly Label subtitleLabel = new();
        private readonly ModeCardPanel mediaCard;
        private readonly ModeCardPanel ocrCard;
        private readonly ModeCardPanel faceCard;

        public ModeSelectionForm()
        {
            Text = "SortMyMedia 2.0";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(1260, 800);
            BackColor = Color.FromArgb(12, 16, 24);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            Icon = AppIconHelper.CreateAppIcon();

            logoBox.BackColor = Color.Transparent;
            logoBox.Image = Properties.Resources.image_1_1770914179217;
            logoBox.SizeMode = PictureBoxSizeMode.Zoom;
            Controls.Add(logoBox);

            titleLabel.Text = "SortMyMedia 2.0";
            titleLabel.Font = new Font("Segoe UI", 28f, FontStyle.Bold);
            titleLabel.ForeColor = Color.White;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.AutoSize = false;
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            titleLabel.Height = 64;
            Controls.Add(titleLabel);

            subtitleLabel.Text = "Choose a mode to start";
            subtitleLabel.Font = new Font("Segoe UI", 12f, FontStyle.Regular);
            subtitleLabel.ForeColor = Color.FromArgb(200, 215, 240);
            subtitleLabel.BackColor = Color.Transparent;
            subtitleLabel.AutoSize = false;
            subtitleLabel.TextAlign = ContentAlignment.MiddleCenter;
            subtitleLabel.Height = 30;
            Controls.Add(subtitleLabel);

            mediaCard = CreateModeCard(
                "Media Sorter",
                "Organize photos and videos automatically by date.",
                new[]
                {
                    "• Extremely fast processing",
                    "• Advanced metadata extraction (EXIF, XMP, HEIC, MP4)",
                    "• Intelligent date detection with filename fallback",
                    "• Google Photos JSON fallback",
                    "• Handles thousands of files per run"
                },
                ColorTranslator.FromHtml("#4A90E2"),
                IllustrationKind.MediaSorter,
                AppMode.MediaSorter);

            ocrCard = CreateModeCard(
                "OCR Mode",
                "Extract text from images and documents.",
                new[]
                {
                    "• GPU-accelerated OCR",
                    "• Supports photos, scans, and PDF pages",
                    "• Automatic language detection",
                    "• Clean TXT output",
                    "• Ideal for receipts, documents, and screenshots"
                },
                ColorTranslator.FromHtml("#9B59B6"),
                IllustrationKind.OcrMode,
                AppMode.OCR);

            faceCard = CreateModeCard(
                "Face Mode",
                "Detect people and organize photos per person.",
                new[]
                {
                    "• Fast and accurate face detection",
                    "• Automatic person clustering",
                    "• Per-person date sorting",
                    "• Manual corrections supported",
                    "• Ideal for family photos and events"
                },
                ColorTranslator.FromHtml("#1ABC9C"),
                IllustrationKind.FaceMode,
                AppMode.FaceGrouping);

            Controls.Add(mediaCard);
            Controls.Add(ocrCard);
            Controls.Add(faceCard);

            LayoutCards();
            Resize += (_, _) => LayoutCards();
        }

        private ModeCardPanel CreateModeCard(
            string title,
            string tagline,
            string[] features,
            Color accentColor,
            IllustrationKind illustrationKind,
            AppMode mode)
        {
            var card = new ModeCardPanel();
            var illustrationPanel = CreateIllustrationPanel(illustrationKind);

            var cardTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#222222"),
                BackColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                AutoEllipsis = true
            };

            var descriptionLabel = new Label
            {
                Text = tagline,
                Font = new Font("Segoe UI", 9.8f, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#444444"),
                BackColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                AutoEllipsis = true
            };

            var featuresLabel = new Label
            {
                Text = string.Join(Environment.NewLine, features),
                Font = new Font("Segoe UI", 9.1f, FontStyle.Regular),
                ForeColor = ColorTranslator.FromHtml("#555555"),
                BackColor = Color.White,
                TextAlign = ContentAlignment.TopLeft,
                AutoSize = false,
                AutoEllipsis = true
            };

            var startButton = new RoundedStartButton
            {
                Text = "Start",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = accentColor,
                UseVisualStyleBackColor = false,
                FlatStyle = FlatStyle.Flat,
                TabStop = true
            };
            startButton.FlatAppearance.BorderSize = 0;
            startButton.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(accentColor, 0.08f);
            startButton.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accentColor, 0.14f);
            startButton.Click += (_, _) => SelectMode(mode);

            card.Controls.Add(illustrationPanel);
            card.Controls.Add(cardTitle);
            card.Controls.Add(descriptionLabel);
            card.Controls.Add(featuresLabel);
            card.Controls.Add(startButton);

            card.SetLayoutAction(() =>
            {
                const int illustrationHeight = 200;
                illustrationPanel.SetBounds(18, 18, card.Width - 36, illustrationHeight);

                cardTitle.SetBounds(18, 226, card.Width - 36, 32);
                descriptionLabel.SetBounds(18, 262, card.Width - 36, 40);
                featuresLabel.SetBounds(18, 306, card.Width - 36, 150);
                startButton.SetBounds(18, card.Height - 56, card.Width - 36, 38);
            });

            return card;
        }

        private void LayoutCards()
        {
            logoBox.SetBounds((ClientSize.Width - 128) / 2, 12, 128, 128);
            titleLabel.SetBounds(0, 144, ClientSize.Width, 64);
            subtitleLabel.SetBounds(0, 210, ClientSize.Width, 30);

            const int cardWidth = 360;
            const int cardHeight = 520;
            const int gap = 30;

            int totalWidth = cardWidth * 3 + gap * 2;
            int startX = (ClientSize.Width - totalWidth) / 2;
            int y = 256;

            mediaCard.SetBounds(startX, y, cardWidth, cardHeight);
            ocrCard.SetBounds(startX + cardWidth + gap, y, cardWidth, cardHeight);
            faceCard.SetBounds(startX + (cardWidth + gap) * 2, y, cardWidth, cardHeight);
        }

        private static Panel CreateIllustrationPanel(IllustrationKind kind)
        {
            return kind switch
            {
                IllustrationKind.MediaSorter => new MediaSorterIllustrationPanel(),
                IllustrationKind.OcrMode => new OcrIllustrationPanel(),
                _ => new FaceIllustrationPanel()
            };
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var gradient = new LinearGradientBrush(
                ClientRectangle,
                Color.FromArgb(12, 16, 24),
                Color.FromArgb(0, 120, 212),
                LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(gradient, ClientRectangle);
        }

        private void SelectMode(AppMode mode)
        {
            AppModeState.CurrentMode = mode;
            DialogResult = DialogResult.OK;
            Close();
        }

        private enum IllustrationKind
        {
            MediaSorter,
            OcrMode,
            FaceMode
        }

        private sealed class MediaSorterIllustrationPanel : Panel
        {
            public MediaSorterIllustrationPanel()
            {
                BackColor = Color.White;
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.White);

                int cx = Width / 2;
                int cy = Height / 2;

                // Folder body
                var folderRect = new Rectangle(cx - 120, cy - 28, 240, 100);
                var tabRect = new Rectangle(folderRect.Left + 18, folderRect.Top - 16, 72, 20);

                // Soft shadow
                using (var shadowBrush = new SolidBrush(Color.FromArgb(18, 0, 0, 0)))
                    g.FillEllipse(shadowBrush, folderRect.Left + 16, folderRect.Bottom - 2, folderRect.Width - 32, 14);
                using (var shadowBrush2 = new SolidBrush(Color.FromArgb(10, 0, 0, 0)))
                    g.FillEllipse(shadowBrush2, folderRect.Left + 8, folderRect.Bottom + 2, folderRect.Width - 16, 10);

                // Folder tab with gradient
                using (var tabPath = CreateRoundRect(tabRect, 7))
                using (var tabGrad = new LinearGradientBrush(tabRect, Color.FromArgb(236, 179, 58), Color.FromArgb(218, 156, 38), LinearGradientMode.Vertical))
                    g.FillPath(tabGrad, tabPath);

                // Folder body with gradient
                using (var folderPath = CreateRoundRect(folderRect, 14))
                using (var folderGrad = new LinearGradientBrush(folderRect, Color.FromArgb(252, 211, 96), Color.FromArgb(240, 186, 62), LinearGradientMode.Vertical))
                {
                    g.FillPath(folderGrad, folderPath);

                    // Subtle top highlight
                    var highlightRect = new Rectangle(folderRect.Left + 2, folderRect.Top, folderRect.Width - 4, folderRect.Height / 3);
                    using var highlightBrush = new SolidBrush(Color.FromArgb(28, 255, 255, 255));
                    using var highlightPath = CreateRoundRect(highlightRect, 14);
                    g.FillPath(highlightBrush, highlightPath);
                }

                // Photo cards inside folder (polaroid style)
                DrawPhotoCard(g, new Rectangle(folderRect.Left + 16, folderRect.Top + 18, 58, 62),
                    Color.FromArgb(86, 156, 238), Color.FromArgb(62, 134, 218), -6f);
                DrawPhotoCard(g, new Rectangle(cx - 34, folderRect.Top + 10, 66, 70),
                    Color.FromArgb(236, 126, 152), Color.FromArgb(220, 100, 132), 2f);
                DrawPhotoCard(g, new Rectangle(folderRect.Right - 80, folderRect.Top + 22, 60, 58),
                    Color.FromArgb(92, 194, 136), Color.FromArgb(68, 174, 112), 5f);

                // Small calendar icon (date sorting hint) bottom-right of folder
                DrawCalendarIcon(g, folderRect.Right - 10, folderRect.Bottom - 14);
            }

            private static void DrawPhotoCard(Graphics g, Rectangle rect, Color topColor, Color bottomColor, float rotation)
            {
                var state = g.Save();
                g.TranslateTransform(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
                g.RotateTransform(rotation);

                var cardRect = new Rectangle(-rect.Width / 2, -rect.Height / 2, rect.Width, rect.Height);

                // Card shadow
                using (var shadowBrush = new SolidBrush(Color.FromArgb(22, 0, 0, 0)))
                using (var shadowPath = CreateRoundRect(new Rectangle(cardRect.X + 2, cardRect.Y + 2, cardRect.Width, cardRect.Height), 6))
                    g.FillPath(shadowBrush, shadowPath);

                // White card border (polaroid)
                using (var cardPath = CreateRoundRect(cardRect, 6))
                using (var whiteBrush = new SolidBrush(Color.FromArgb(252, 252, 252)))
                    g.FillPath(whiteBrush, cardPath);

                // Image area
                var imageRect = new Rectangle(cardRect.X + 4, cardRect.Y + 4, cardRect.Width - 8, cardRect.Height - 16);
                using (var imagePath = CreateRoundRect(imageRect, 4))
                using (var imageGrad = new LinearGradientBrush(imageRect, topColor, bottomColor, LinearGradientMode.ForwardDiagonal))
                    g.FillPath(imageGrad, imagePath);

                // Tiny mountain scene on the image
                int mBase = imageRect.Bottom - 2;
                using var mountainBrush = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
                var mountainPoints = new[]
                {
                    new PointF(imageRect.Left + 2, mBase),
                    new PointF(imageRect.Left + imageRect.Width * 0.3f, imageRect.Top + imageRect.Height * 0.45f),
                    new PointF(imageRect.Left + imageRect.Width * 0.55f, imageRect.Top + imageRect.Height * 0.65f),
                    new PointF(imageRect.Left + imageRect.Width * 0.75f, imageRect.Top + imageRect.Height * 0.35f),
                    new PointF(imageRect.Right - 2, mBase)
                };
                g.FillPolygon(mountainBrush, mountainPoints);

                // Tiny sun
                using var sunBrush = new SolidBrush(Color.FromArgb(80, 255, 255, 255));
                g.FillEllipse(sunBrush, imageRect.Right - 16, imageRect.Top + 4, 10, 10);

                g.Restore(state);
            }

            private static void DrawCalendarIcon(Graphics g, int x, int y)
            {
                using var calBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
                using var calPath = CreateRoundRect(new Rectangle(x - 10, y - 10, 20, 20), 4);
                g.FillPath(calBrush, calPath);

                using var headerBrush = new SolidBrush(Color.FromArgb(218, 156, 38));
                g.FillRectangle(headerBrush, x - 8, y - 8, 16, 6);

                using var dotBrush = new SolidBrush(Color.FromArgb(180, 120, 80, 40));
                for (int r = 0; r < 2; r++)
                    for (int c = 0; c < 3; c++)
                        g.FillRectangle(dotBrush, x - 6 + c * 5, y + 1 + r * 5, 3, 3);
            }
        }

        private sealed class OcrIllustrationPanel : Panel
        {
            public OcrIllustrationPanel()
            {
                BackColor = Color.White;
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.White);

                int cx = Width / 2;
                int cy = Height / 2;

                // Document page
                var docRect = new Rectangle(cx - 92, cy - 74, 160, 148);

                // Soft page shadow
                using (var shadowBrush = new SolidBrush(Color.FromArgb(14, 0, 0, 0)))
                using (var shadowPath = CreateRoundRect(new Rectangle(docRect.X + 4, docRect.Y + 4, docRect.Width, docRect.Height), 10))
                    g.FillPath(shadowBrush, shadowPath);
                using (var shadowBrush2 = new SolidBrush(Color.FromArgb(8, 0, 0, 0)))
                using (var shadowPath2 = CreateRoundRect(new Rectangle(docRect.X + 7, docRect.Y + 7, docRect.Width, docRect.Height), 10))
                    g.FillPath(shadowBrush2, shadowPath2);

                // Page with subtle border
                using (var docPath = CreateRoundRect(docRect, 10))
                {
                    using var pageBrush = new SolidBrush(Color.FromArgb(253, 253, 255));
                    g.FillPath(pageBrush, docPath);
                    using var borderPen = new Pen(Color.FromArgb(225, 230, 238), 1.2f);
                    g.DrawPath(borderPen, docPath);
                }

                // Text lines with varying widths and a header line
                int lineLeft = docRect.Left + 16;
                int lineTop = docRect.Top + 18;

                // Title line (wider, darker)
                var titleRect = new Rectangle(lineLeft, lineTop, docRect.Width - 32, 10);
                using (var titlePath = CreateRoundRect(titleRect, 5))
                using (var titleBrush = new SolidBrush(Color.FromArgb(195, 202, 212)))
                    g.FillPath(titleBrush, titlePath);

                // Body text lines
                float[] lineWidths = { 1.0f, 0.85f, 0.92f, 0.78f, 1.0f, 0.7f, 0.88f };
                int highlightLine = 3;
                for (int i = 0; i < lineWidths.Length; i++)
                {
                    int ly = lineTop + 20 + i * 15;
                    int lw = (int)((docRect.Width - 32) * lineWidths[i]);
                    var lineRect = new Rectangle(lineLeft, ly, lw, 6);
                    using var linePath = CreateRoundRect(lineRect, 3);

                    if (i == highlightLine)
                    {
                        // Highlighted scanning line
                        using var hlBrush = new SolidBrush(Color.FromArgb(50, 74, 144, 226));
                        var hlBg = new Rectangle(lineLeft - 4, ly - 3, docRect.Width - 24, 12);
                        using var hlBgPath = CreateRoundRect(hlBg, 3);
                        g.FillPath(hlBrush, hlBgPath);

                        using var scanBrush = new SolidBrush(Color.FromArgb(100, 160, 220));
                        g.FillPath(scanBrush, linePath);
                    }
                    else
                    {
                        using var lineBrush = new SolidBrush(Color.FromArgb(218, 224, 232));
                        g.FillPath(lineBrush, linePath);
                    }
                }

                // Magnifying glass
                int glassX = docRect.Right + 8;
                int glassY = cy + 4;
                int glassR = 26;

                // Glass shadow
                using (var glassShadow = new SolidBrush(Color.FromArgb(16, 0, 0, 0)))
                    g.FillEllipse(glassShadow, glassX - glassR + 3, glassY - glassR + 3, glassR * 2, glassR * 2);

                // Glass fill (subtle lens)
                using (var lensGrad = new LinearGradientBrush(
                    new Rectangle(glassX - glassR, glassY - glassR, glassR * 2, glassR * 2),
                    Color.FromArgb(20, 140, 200, 255), Color.FromArgb(6, 80, 140, 255), LinearGradientMode.ForwardDiagonal))
                    g.FillEllipse(lensGrad, glassX - glassR, glassY - glassR, glassR * 2, glassR * 2);

                // Glass rim with gradient
                using var rimPen = new Pen(Color.FromArgb(86, 152, 236), 5f) { LineJoin = LineJoin.Round };
                g.DrawEllipse(rimPen, glassX - glassR, glassY - glassR, glassR * 2, glassR * 2);

                // Highlight arc on glass
                using var glintPen = new Pen(Color.FromArgb(60, 255, 255, 255), 2.5f);
                g.DrawArc(glintPen, glassX - glassR + 5, glassY - glassR + 4, glassR * 2 - 14, glassR * 2 - 14, 200, 60);

                // Handle with gradient
                var state = g.Save();
                g.TranslateTransform(glassX + 17, glassY + 20);
                g.RotateTransform(40f);
                var handleRect = new Rectangle(0, -4, 38, 10);
                using (var handlePath = CreateRoundRect(handleRect, 5))
                using (var handleGrad = new LinearGradientBrush(handleRect, Color.FromArgb(86, 152, 236), Color.FromArgb(58, 120, 206), LinearGradientMode.Vertical))
                    g.FillPath(handleGrad, handlePath);
                g.Restore(state);
            }
        }

        private sealed class FaceIllustrationPanel : Panel
        {
            public FaceIllustrationPanel()
            {
                BackColor = Color.White;
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.White);

                int cx = Width / 2;
                int cy = Height / 2;

                var nodes = new[]
                {
                    (X: cx - 72, Y: cy - 46),   // top-left
                    (X: cx,      Y: cy - 62),   // top-center
                    (X: cx + 72, Y: cy - 46),   // top-right
                    (X: cx - 42, Y: cy + 26),   // bottom-left
                    (X: cx + 42, Y: cy + 28)    // bottom-right
                };

                // Connection lines (draw behind avatars)
                var connections = new[] { (0, 1), (1, 2), (0, 3), (1, 3), (1, 4), (2, 4), (3, 4) };
                foreach (var (a, b) in connections)
                {
                    DrawConnectionLine(g, nodes[a].X, nodes[a].Y, nodes[b].X, nodes[b].Y);
                }

                // Avatars
                var avatarColors = new[]
                {
                    (Head: Color.FromArgb(255, 218, 190), Body: Color.FromArgb(120, 172, 240), BodyDark: Color.FromArgb(90, 148, 220)),
                    (Head: Color.FromArgb(255, 228, 200), Body: Color.FromArgb(162, 148, 230), BodyDark: Color.FromArgb(138, 122, 210)),
                    (Head: Color.FromArgb(250, 214, 188), Body: Color.FromArgb(108, 202, 168), BodyDark: Color.FromArgb(82, 180, 144)),
                    (Head: Color.FromArgb(255, 222, 196), Body: Color.FromArgb(240, 158, 178), BodyDark: Color.FromArgb(222, 132, 156)),
                    (Head: Color.FromArgb(246, 208, 178), Body: Color.FromArgb(238, 196, 120), BodyDark: Color.FromArgb(220, 176, 96))
                };

                for (int i = 0; i < nodes.Length; i++)
                {
                    bool highlighted = (i == 1); // center-top avatar is highlighted
                    DrawRefinedAvatar(g, nodes[i].X, nodes[i].Y,
                        avatarColors[i].Head, avatarColors[i].Body, avatarColors[i].BodyDark, highlighted);
                }
            }

            private static void DrawConnectionLine(Graphics g, int x1, int y1, int x2, int y2)
            {
                using var pen = new Pen(Color.FromArgb(56, 190, 200, 215), 1.8f)
                {
                    DashStyle = DashStyle.Dot,
                    DashCap = DashCap.Round,
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawLine(pen, x1, y1 + 6, x2, y2 + 6);
            }

            private static void DrawRefinedAvatar(Graphics g, int cx, int cy,
                Color headColor, Color bodyColor, Color bodyDarkColor, bool highlighted)
            {
                int avatarRadius = highlighted ? 28 : 24;

                // Glow ring for highlighted avatar
                if (highlighted)
                {
                    for (int i = 3; i >= 1; i--)
                    {
                        using var glowPen = new Pen(Color.FromArgb(20 + i * 8, 86, 156, 240), 2f + i);
                        g.DrawEllipse(glowPen, cx - avatarRadius - i * 2, cy - avatarRadius - i * 2,
                            (avatarRadius + i * 2) * 2, (avatarRadius + i * 2) * 2);
                    }
                }

                // Circle background with subtle shadow
                using (var shadowBrush = new SolidBrush(Color.FromArgb(16, 0, 0, 0)))
                    g.FillEllipse(shadowBrush, cx - avatarRadius + 2, cy - avatarRadius + 3, avatarRadius * 2, avatarRadius * 2);

                // Outer ring
                using (var ringBrush = new SolidBrush(highlighted ? Color.FromArgb(86, 156, 240) : Color.FromArgb(230, 234, 242)))
                    g.FillEllipse(ringBrush, cx - avatarRadius, cy - avatarRadius, avatarRadius * 2, avatarRadius * 2);

                // Inner circle (white background)
                int inner = avatarRadius - 3;
                using (var innerBrush = new SolidBrush(Color.FromArgb(248, 250, 255)))
                    g.FillEllipse(innerBrush, cx - inner, cy - inner, inner * 2, inner * 2);

                // Clip to inner circle for avatar silhouette
                var state = g.Save();
                using (var clipPath = new GraphicsPath())
                {
                    clipPath.AddEllipse(cx - inner, cy - inner, inner * 2, inner * 2);
                    g.SetClip(clipPath);

                    // Body (shoulders)
                    int bodyW = (int)(inner * 1.5f);
                    int bodyH = inner;
                    var bodyRect = new Rectangle(cx - bodyW / 2, cy + inner / 4, bodyW, bodyH);
                    using (var bodyGrad = new LinearGradientBrush(bodyRect, bodyColor, bodyDarkColor, LinearGradientMode.Vertical))
                        g.FillEllipse(bodyGrad, bodyRect);

                    // Head
                    int headR = (int)(inner * 0.44f);
                    using var headBrush = new SolidBrush(headColor);
                    g.FillEllipse(headBrush, cx - headR, cy - headR - inner / 6, headR * 2, headR * 2);
                }
                g.Restore(state);
            }
        }

        private sealed class ModeCardPanel : Panel
        {
            private bool isHovered;
            private Action? performLayoutAction;

            public void SetLayoutAction(Action action)
            {
                performLayoutAction = action;
            }

            public ModeCardPanel()
            {
                BackColor = Color.White;
                DoubleBuffered = true;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

                MouseEnter += (_, _) => { isHovered = true; Invalidate(); };
                MouseLeave += (_, _) => { isHovered = false; Invalidate(); };
                ControlAdded += (_, e) =>
                {
                    if (e.Control != null)
                        WireHoverPropagation(e.Control);
                };
            }

            protected override void OnResize(EventArgs eventargs)
            {
                base.OnResize(eventargs);
                using (var clipPath = CreateRoundRect(new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1)), 12))
                {
                    Region?.Dispose();
                    Region = new Region(clipPath);
                }

                performLayoutAction?.Invoke();
                Invalidate();
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // Resolve the gradient background color at this card's vertical position
                Color parentBg = Color.FromArgb(0, 120, 212);
                if (Parent != null && Parent.ClientSize.Height > 0)
                {
                    float t = Math.Clamp((Top + Height / 2f) / Parent.ClientSize.Height, 0f, 1f);
                    int r = (int)(12 + (0 - 12) * t);
                    int g = (int)(16 + (120 - 16) * t);
                    int b = (int)(24 + (212 - 24) * t);
                    parentBg = Color.FromArgb(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
                }

                using var bgBrush = new SolidBrush(parentBg);
                e.Graphics.FillRectangle(bgBrush, ClientRectangle);

                var cardRect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));

                using var cardPath = CreateRoundRect(cardRect, 12);
                using var fillBrush = new SolidBrush(isHovered ? Color.FromArgb(245, 249, 255) : ColorTranslator.FromHtml("#FFFFFF"));
                e.Graphics.FillPath(fillBrush, cardPath);
            }

            private void WireHoverPropagation(Control control)
            {
                if (control == null)
                    return;

                control.MouseEnter += (_, _) => { isHovered = true; Invalidate(); };
                control.MouseLeave += (_, _) =>
                {
                    if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
                    {
                        isHovered = false;
                        Invalidate();
                    }
                };
            }
        }

        private sealed class RoundedStartButton : Button
        {
            public RoundedStartButton()
            {
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                using var path = CreateRoundRect(new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)), 9);
                Region?.Dispose();
                Region = new Region(path);
            }
        }

        private static GraphicsPath CreateRoundRect(Rectangle rect, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
