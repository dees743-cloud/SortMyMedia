using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SortMyMedia.FaceRecognition
{
    public sealed class FaceThumbnail : IDisposable
    {
        public static Image PlaceholderImage { get; } = CreatePlaceholderImage();

        public FaceThumbnail(int faceIndex, string imagePath)
        {
            FaceIndex = faceIndex;
            ImagePath = imagePath;
            FaceId = Guid.NewGuid().ToString();
        }

        public int FaceIndex { get; }
        public string ImagePath { get; }
        public string FaceId { get; set; }
        public Rectangle CropRectangle { get; set; }
        public Image? Thumbnail { get; set; }

        public bool EnsureThumbnailLoaded()
        {
            if (Thumbnail != null)
                return true;

            Thumbnail = LoadThumbnailImage(ImagePath);
            if (Thumbnail == null)
                return false;

            if (CropRectangle.Width <= 0 || CropRectangle.Height <= 0)
                CropRectangle = new Rectangle(0, 0, Thumbnail.Width, Thumbnail.Height);

            return true;
        }

        public void Dispose()
        {
            Thumbnail?.Dispose();
            Thumbnail = null;
        }

        private static Image? LoadThumbnailImage(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var image = Image.FromStream(fs);
                return new Bitmap(image);
            }
            catch
            {
                return null;
            }
        }

        private static Image CreatePlaceholderImage()
        {
            var bmp = new Bitmap(64, 64);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(228, 232, 238));
            using var pen = new Pen(Color.FromArgb(168, 176, 189), 2f);
            g.DrawRectangle(pen, 8, 8, 48, 48);
            g.DrawLine(pen, 8, 56, 56, 8);
            return bmp;
        }
    }

    public sealed class FaceOverviewCluster : IDisposable
    {
        private const int DefaultPreviewFaceLimit = 24;

        public FaceOverviewCluster(int clusterId, IEnumerable<int> faceIndices)
        {
            ClusterId = clusterId;
            FaceIndices = faceIndices.Distinct().ToList();
            Thumbnails = new List<FaceThumbnail>();
        }

        public FaceOverviewCluster(int clusterId, IEnumerable<int> faceIndices, IReadOnlyList<string> alignedPaths)
        {
            ClusterId = clusterId;
            FaceIndices = faceIndices.Distinct().ToList();
            Thumbnails = FaceIndices
                .Where(i => i >= 0 && i < alignedPaths.Count)
                .Take(DefaultPreviewFaceLimit)
                .Select(i => new FaceThumbnail(i, alignedPaths[i]))
                .ToList();

            RepresentativeImage = LoadRepresentativeImage();
        }

        public int ClusterId { get; }
        public List<int> FaceIndices { get; }
        public List<FaceThumbnail> Thumbnails { get; }
        public Bitmap? RepresentativeImage { get; private set; }
        public string Name { get; set; } = string.Empty;
        public Rectangle DropZone { get; set; }
        public bool IsSelected { get; set; }
        private string? RepresentativeImagePath { get; set; }

        public void RebuildFromIndices(IReadOnlyList<string> alignedPaths)
        {
            var currentByIndex = Thumbnails.ToDictionary(t => t.FaceIndex);
            var next = new List<FaceThumbnail>();

            foreach (int idx in FaceIndices.Where(i => i >= 0 && i < alignedPaths.Count).Take(DefaultPreviewFaceLimit))
            {
                if (currentByIndex.Remove(idx, out var existing))
                {
                    next.Add(existing);
                    continue;
                }

                next.Add(new FaceThumbnail(idx, alignedPaths[idx]));
            }

            // Keep removed thumbnails alive during interactive regrouping to avoid
            // stale/disposed image references in UI caches during drag/drop.

            Thumbnails.Clear();
            Thumbnails.AddRange(next);

            string? nextRepresentativePath = Thumbnails.Select(t => t.ImagePath).FirstOrDefault(File.Exists);
            if (!string.Equals(nextRepresentativePath, RepresentativeImagePath, StringComparison.OrdinalIgnoreCase))
            {
                RepresentativeImage?.Dispose();
                RepresentativeImage = string.IsNullOrWhiteSpace(nextRepresentativePath) ? null : LoadBitmapSafe(nextRepresentativePath);
                RepresentativeImagePath = nextRepresentativePath;
            }
        }

        private Bitmap? LoadRepresentativeImage()
        {
            string? path = Thumbnails.Select(t => t.ImagePath).FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(path))
                return null;

            RepresentativeImagePath = path;

            return LoadBitmapSafe(path);
        }

        public void Dispose()
        {
            RepresentativeImage?.Dispose();
            RepresentativeImage = null;

            foreach (var thumbnail in Thumbnails)
                thumbnail.Dispose();

            Thumbnails.Clear();
        }

        private static Bitmap? LoadBitmapSafe(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var img = Image.FromStream(fs);
                return new Bitmap(img);
            }
            catch
            {
                return null;
            }
        }
    }

    public sealed class FaceOverviewResult
    {
        public FaceOverviewResult(IReadOnlyList<FaceOverviewCluster> clusters, bool skippedOverview)
        {
            Clusters = clusters;
            SkippedOverview = skippedOverview;
        }

        public IReadOnlyList<FaceOverviewCluster> Clusters { get; }
        public bool SkippedOverview { get; }
    }

    public sealed class FaceOverviewGroupingService
    {
        internal static bool _layoutFrozen;
        internal static readonly List<Action> _pendingMutations = new();

        public FaceOverviewResult Run(
            IReadOnlyList<FaceOverviewCluster> initialClusters,
            IReadOnlyList<string> alignedPaths,
            IReadOnlyList<string> existingNames)
        {
            if (!CanUseWinForms())
                return BuildAutoNamedResult(initialClusters, alignedPaths);

            using var form = new FaceOverviewGroupingForm(initialClusters, alignedPaths, existingNames);
            DialogResult dialogResult = form.ShowDialog();
            if (dialogResult != DialogResult.OK)
                return BuildAutoNamedResult(initialClusters, alignedPaths);

            return form.Result;
        }

        public FaceOverviewResult Run(
            IReadOnlyList<List<int>> initialClusters,
            IReadOnlyList<string> alignedPaths,
            IReadOnlyList<string> existingNames)
        {
            if (!CanUseWinForms())
                return BuildAutoNamedResult(initialClusters, alignedPaths);

            using var form = new FaceOverviewGroupingForm(initialClusters, alignedPaths, existingNames);
            DialogResult dialogResult = form.ShowDialog();
            if (dialogResult != DialogResult.OK)
                return BuildAutoNamedResult(initialClusters, alignedPaths);

            return form.Result;
        }

        private static FaceOverviewResult BuildAutoNamedResult(IReadOnlyList<FaceOverviewCluster> initialClusters, IReadOnlyList<string> alignedPaths)
        {
            var clusters = new List<FaceOverviewCluster>(initialClusters.Count);
            for (int i = 0; i < initialClusters.Count; i++)
            {
                FaceOverviewCluster source = initialClusters[i];
                clusters.Add(new FaceOverviewCluster(source.ClusterId, source.FaceIndices)
                {
                    Name = string.IsNullOrWhiteSpace(source.Name) ? $"Person_{i + 1:00}" : source.Name
                });
            }

            return new FaceOverviewResult(clusters, skippedOverview: true);
        }

        private static FaceOverviewResult BuildAutoNamedResult(IReadOnlyList<List<int>> initialClusters, IReadOnlyList<string> alignedPaths)
        {
            var clusters = initialClusters
                .Select((c, idx) => new FaceOverviewCluster(idx + 1, c)
                {
                    Name = $"Person_{idx + 1:00}"
                })
                .ToList();

            return new FaceOverviewResult(clusters, skippedOverview: true);
        }

        private static bool CanUseWinForms()
        {
            try
            {
                return Environment.UserInteractive && Application.MessageLoop;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class FaceOverviewLayout
    {
        public const int CardWidth = 296;
        public const int CardHeight = 290;
        public const int CardSpacing = 10;
        public const int CardInnerLeft = 12;
        public const int ContentWidth = 272;
        public const int TextInsetLeft = 18;
        public const int TextWidth = 260;
    }

    internal sealed class ResponsiveClusterGridPanel : Panel
    {
        private const int MinSpacing = 12;

        public ResponsiveClusterGridPanel()
        {
            AutoScroll = true;
            HorizontalScroll.Enabled = false;
            HorizontalScroll.Visible = false;
            BorderStyle = BorderStyle.None;
            DoubleBuffered = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        public void ReflowCards()
        {
            Debug.WriteLine($"[FaceOverview][Diag] ReflowCards start controls={Controls.Count}, autoScrollMinSize={AutoScrollMinSize}, scrollY={VerticalScroll.Value}, frozen={FaceOverviewGroupingService._layoutFrozen}");

            if (FaceOverviewGroupingService._layoutFrozen)
            {
                Debug.WriteLine("[FaceOverview][Diag] ReflowCards skipped (layout frozen)");
                return;
            }

            if (Controls.Count == 0)
                return;

            SuspendLayout();

            int availableWidth = ClientSize.Width - Padding.Horizontal;
            int cardWidth = Controls[0].Width;
            int cardHeight = Controls[0].Height;

            int cardsPerRow = Math.Max(1, (availableWidth + MinSpacing) / (cardWidth + MinSpacing));
            int horizontalGap = cardsPerRow == 1
                ? 0
                : Math.Max(MinSpacing, (availableWidth - cardsPerRow * cardWidth) / (cardsPerRow - 1));

            int x = Padding.Left;
            int y = Padding.Top;
            int col = 0;

            foreach (Control card in Controls.Cast<Control>().OrderBy(c => c.TabIndex))
            {
                card.Location = new Point(x, y);

                col++;
                if (col >= cardsPerRow)
                {
                    col = 0;
                    x = Padding.Left;
                    y += cardHeight + MinSpacing;
                }
                else
                {
                    x += cardWidth + horizontalGap;
                }
            }

            AutoScrollMinSize = new Size(0, y + cardHeight + Padding.Bottom);
            HorizontalScroll.Enabled = false;
            HorizontalScroll.Visible = false;
            ResumeLayout();

            Debug.WriteLine($"[FaceOverview][Diag] ReflowCards end autoScrollMinSize={AutoScrollMinSize}, scrollY={VerticalScroll.Value}");
        }
    }

    internal sealed class ThumbnailStripPanel : Panel
    {
        private const int LeftPadding = 4;
        private const int Spacing = 4;
        private const int MinThumbnailSize = 64;
        private const int MaxThumbnailSize = 192;
        private readonly HScrollBar scrollBar = new();
        private readonly System.Windows.Forms.Timer scrollAnimation = new();
        private int horizontalOffset;
        private float animatedOffset;
        private bool suppressScrollBarValueChange;
        private Control? highlightedControl;
        private bool hasInitialCentering;

        public event Action<Control>? LeftmostControlChanged;

        public ThumbnailStripPanel()
        {
            Height = 72;
            BorderStyle = BorderStyle.None;
            DoubleBuffered = true;
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            scrollBar.Dock = DockStyle.Bottom;
            scrollBar.Height = 16;
            scrollBar.SmallChange = 16;
            scrollBar.LargeChange = 64;
            scrollBar.Minimum = 0;
            scrollBar.ValueChanged += (_, _) =>
            {
                if (suppressScrollBarValueChange)
                    return;

                horizontalOffset = scrollBar.Value;
                StartScrollAnimation();
            };

            scrollAnimation.Interval = 16;
            scrollAnimation.Tick += (_, _) => AnimateScrollStep();

            Controls.Add(scrollBar);
        }

        public void LayoutThumbnails()
        {
            var thumbnailControls = Controls.Cast<Control>()
                .Where(c => !ReferenceEquals(c, scrollBar))
                .OrderBy(c => c.TabIndex)
                .ToList();

            if (thumbnailControls.Count == 0)
            {
                hasInitialCentering = false;
                horizontalOffset = 0;
                animatedOffset = 0;
                scrollBar.Enabled = false;
                scrollBar.Visible = false;
                return;
            }

            int viewportWidth = Math.Max(1, ClientSize.Width - 1);
            int availableHeight = Math.Max(0, ClientSize.Height - scrollBar.Height - 8);
            int maxVisibleSize = Math.Max(MinThumbnailSize, Math.Min(MaxThumbnailSize, availableHeight));
            int slotWidth = maxVisibleSize;
            int slotPitch = slotWidth + Spacing;
            int centerPadding = Math.Max(LeftPadding, (viewportWidth - slotWidth) / 2);
            int contentWidth = GetContentWidth(thumbnailControls.Count, centerPadding, slotPitch);
            int maxOffset = Math.Max(0, contentWidth - viewportWidth + 1);

            if (horizontalOffset > maxOffset)
                horizontalOffset = maxOffset;
            if (animatedOffset > maxOffset)
                animatedOffset = maxOffset;

            scrollBar.Maximum = maxOffset + scrollBar.LargeChange;
            scrollBar.Enabled = maxOffset > 0;
            scrollBar.Visible = maxOffset > 0;

            if (!hasInitialCentering)
            {
                horizontalOffset = 0;
                animatedOffset = 0;
                hasInitialCentering = true;
            }

            scrollBar.SmallChange = Math.Max(2, slotPitch / 20);
            scrollBar.LargeChange = Math.Max(12, slotPitch / 3);

            if (scrollBar.Value != horizontalOffset)
            {
                suppressScrollBarValueChange = true;
                try
                {
                    scrollBar.Value = horizontalOffset;
                }
                finally
                {
                    suppressScrollBarValueChange = false;
                }
            }

            float viewportCenter = animatedOffset + (viewportWidth / 2f);
            int baseTop = 4;
            int arcDepth = 20;

            for (int i = 0; i < thumbnailControls.Count; i++)
            {
                Control control = thumbnailControls[i];
                int slotLeft = centerPadding + i * slotPitch;
                float slotCenter = slotLeft + (slotWidth / 2f);
                float distanceInSlots = Math.Abs(slotCenter - viewportCenter) / slotPitch;
                float proximity = Math.Max(0f, 1f - Math.Min(1f, distanceInSlots));
                float eased = proximity * proximity * (3f - 2f * proximity);
                int size = MinThumbnailSize + (int)Math.Round((maxVisibleSize - MinThumbnailSize) * eased);

                control.Width = size;
                control.Height = size;
                control.Left = (int)Math.Round(slotLeft - animatedOffset + ((slotWidth - size) / 2f));
                control.Top = baseTop + ((maxVisibleSize - size) / 2) + (int)Math.Round((1f - eased) * arcDepth);

                int cornerRadius = size >= 96 ? 8 : 6;
                if (control is RoundedThumbnailBox rounded)
                {
                    rounded.CornerRadius = cornerRadius;
                    rounded.Invalidate();
                }
            }

            EvaluateHighlightedControl();
        }

        public void ScrollHorizontal(int delta)
        {
            int count = Controls.Cast<Control>().Count(c => !ReferenceEquals(c, scrollBar));
            int viewportWidth = Math.Max(1, ClientSize.Width - 1);
            int availableHeight = Math.Max(0, ClientSize.Height - scrollBar.Height - 8);
            int slotWidth = Math.Max(MinThumbnailSize, Math.Min(MaxThumbnailSize, availableHeight));
            int slotPitch = slotWidth + Spacing;
            int centerPadding = Math.Max(LeftPadding, (viewportWidth - slotWidth) / 2);
            int maxOffset = Math.Max(0, GetContentWidth(count, centerPadding, slotPitch) - ClientSize.Width + 1);
            int next = Math.Max(0, Math.Min(maxOffset, horizontalOffset + delta));
            if (next == horizontalOffset)
                return;

            horizontalOffset = next;
            if (scrollBar.Value != horizontalOffset)
            {
                suppressScrollBarValueChange = true;
                try
                {
                    scrollBar.Value = horizontalOffset;
                }
                finally
                {
                    suppressScrollBarValueChange = false;
                }
            }

            StartScrollAnimation();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollHorizontal(-(e.Delta / 28));
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            LayoutThumbnails();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEHWHEEL = 0x020E;
            if (m.Msg == WM_MOUSEHWHEEL)
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                ScrollHorizontal(-(delta / 28));
                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var baseBrush = new SolidBrush(UiPaint.ResolveOpaqueBackground(this)))
                e.Graphics.FillRectangle(baseBrush, ClientRectangle);

            var rect = new Rectangle(0, 0, Width - 1, Math.Max(1, scrollBar.Top - 1));
            if (rect.Width <= 1 || rect.Height <= 1)
                return;

            using var panelPath = UiPaint.CreateRoundRect(rect, 10);
            using var glassBrush = new SolidBrush(Color.FromArgb(92, 245, 250, 255));
            using var edgePen = new Pen(Color.FromArgb(110, 172, 204, 236), 1.2f);
            e.Graphics.FillPath(glassBrush, panelPath);
            e.Graphics.DrawPath(edgePen, panelPath);
        }

        private static int GetContentWidth(int count, int centerPadding, int slotPitch)
        {
            if (count == 0)
                return 0;

            return (centerPadding * 2) + (count * slotPitch) - Spacing;
        }

        private void EvaluateHighlightedControl()
        {
            float viewportCenter = ClientSize.Width / 2f;
            var candidate = Controls.Cast<Control>()
                .Where(c => !ReferenceEquals(c, scrollBar))
                .Where(c => c.Right > 0 && c.Left < ClientSize.Width)
                .OrderBy(c => Math.Abs(((c.Left + c.Right) / 2f) - viewportCenter))
                .ThenByDescending(c => c.Width)
                .FirstOrDefault();

            if (candidate == null || ReferenceEquals(candidate, highlightedControl))
                return;

            highlightedControl = candidate;
            LeftmostControlChanged?.Invoke(candidate);
        }

        private void StartScrollAnimation()
        {
            if (!scrollAnimation.Enabled)
                scrollAnimation.Start();

            if (Math.Abs(animatedOffset - horizontalOffset) < 0.25f)
            {
                animatedOffset = horizontalOffset;
                LayoutThumbnails();
            }
        }

        private void AnimateScrollStep()
        {
            float delta = horizontalOffset - animatedOffset;
            if (Math.Abs(delta) < 0.25f)
            {
                animatedOffset = horizontalOffset;
                scrollAnimation.Stop();
            }
            else
            {
                animatedOffset += delta * 0.12f;
            }

            LayoutThumbnails();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                scrollAnimation.Dispose();

            base.Dispose(disposing);
        }
    }

    internal sealed class RoundedThumbnailBox : PictureBox
    {
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get; set; } = 8;

        public RoundedThumbnailBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            SizeMode = PictureBoxSizeMode.Zoom;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            pe.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            pe.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            pe.Graphics.CompositingQuality = CompositingQuality.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width <= 1 || rect.Height <= 1)
                return;

            int radius = Math.Min(CornerRadius, Math.Min(rect.Width, rect.Height) / 2);
            using var clipPath = UiPaint.CreateRoundRect(rect, radius);
            Region?.Dispose();
            Region = new Region(clipPath);

            pe.Graphics.SetClip(clipPath);
            Color background = UiPaint.ResolveOpaqueBackground(this);
            using (var bgBrush = new SolidBrush(background))
                pe.Graphics.FillRectangle(bgBrush, rect);

            if (Image != null)
            {
                Rectangle target = GetZoomRect(Image.Size, rect);
                pe.Graphics.DrawImage(Image, target);
            }

            pe.Graphics.ResetClip();
            using var edgePen = new Pen(Color.FromArgb(118, 160, 220), 2.0f);
            pe.Graphics.DrawPath(edgePen, clipPath);
        }

        private static Rectangle GetZoomRect(Size imageSize, Rectangle bounds)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
                return bounds;

            float scale = Math.Min(bounds.Width / (float)imageSize.Width, bounds.Height / (float)imageSize.Height);
            int drawWidth = Math.Max(1, (int)Math.Round(imageSize.Width * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(imageSize.Height * scale));

            int x = bounds.X + (bounds.Width - drawWidth) / 2;
            int y = bounds.Y + (bounds.Height - drawHeight) / 2;
            return new Rectangle(x, y, drawWidth, drawHeight);
        }
    }

    internal sealed class ClusterCardControl : UserControl
    {
        private readonly TextBox nameBox = new();
        private readonly ThumbnailStripPanel strip = new();
        private readonly Dictionary<int, RoundedThumbnailBox> thumbnailBoxes = new();
        private bool isHovered;
        private bool isDragHover;
        private Color cardFillColor = Color.White;

        private readonly Action<int> selectCluster;
        private readonly Func<int, int, object> payloadFactory;
        private readonly MouseEventHandler thumbnailMouseDown;
        private readonly MouseEventHandler thumbnailMouseUp;
        private readonly MouseEventHandler clusterMouseDown;
        private readonly EventHandler thumbnailClick;
        private readonly DragEventHandler cardDragEnter;
        private readonly DragEventHandler cardDragOver;
        private readonly DragEventHandler cardDragDrop;
        private readonly EventHandler cardDragLeave;
        private readonly KeyEventHandler nameBoxKeyDown;
        private readonly EventHandler nameBoxLeave;

        public ClusterCardControl(
            FaceOverviewCluster cluster,
            Action<int> selectCluster,
            Func<int, int, object> payloadFactory,
            MouseEventHandler thumbnailMouseDown,
            MouseEventHandler thumbnailMouseUp,
            MouseEventHandler clusterMouseDown,
            EventHandler thumbnailClick,
            DragEventHandler cardDragEnter,
            DragEventHandler cardDragOver,
            DragEventHandler cardDragDrop,
            EventHandler cardDragLeave,
            KeyEventHandler nameBoxKeyDown,
            EventHandler nameBoxLeave)
        {
            this.selectCluster = selectCluster;
            this.payloadFactory = payloadFactory;
            this.thumbnailMouseDown = thumbnailMouseDown;
            this.thumbnailMouseUp = thumbnailMouseUp;
            this.clusterMouseDown = clusterMouseDown;
            this.thumbnailClick = thumbnailClick;
            this.cardDragEnter = cardDragEnter;
            this.cardDragOver = cardDragOver;
            this.cardDragDrop = cardDragDrop;
            this.cardDragLeave = cardDragLeave;
            this.nameBoxKeyDown = nameBoxKeyDown;
            this.nameBoxLeave = nameBoxLeave;

            Width = FaceOverviewLayout.CardWidth;
            Height = FaceOverviewLayout.CardHeight;
            AutoSize = false;
            MinimumSize = new Size(FaceOverviewLayout.CardWidth, FaceOverviewLayout.CardHeight);
            MaximumSize = new Size(FaceOverviewLayout.CardWidth, FaceOverviewLayout.CardHeight);
            Margin = new Padding(10);
            BorderStyle = BorderStyle.None;
            BackColor = Color.Transparent;
            AllowDrop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);

            MouseEnter += (_, _) => { isHovered = true; Invalidate(); };
            MouseLeave += (_, _) => { isHovered = false; Invalidate(); };

            Click += (_, _) => this.selectCluster(Cluster.ClusterId);
            MouseDown += this.clusterMouseDown;
            DragEnter += this.cardDragEnter;
            DragOver += this.cardDragOver;
            DragDrop += this.cardDragDrop;
            DragLeave += this.cardDragLeave;

            nameBox.Left = FaceOverviewLayout.TextInsetLeft;
            nameBox.Top = 250;
            nameBox.Width = FaceOverviewLayout.TextWidth;
            nameBox.Height = 22;
            nameBox.AutoSize = false;
            nameBox.Multiline = false;
            nameBox.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            nameBox.BorderStyle = BorderStyle.None;
            nameBox.BackColor = Color.White;
            nameBox.ForeColor = Color.FromArgb(34, 34, 34);
            nameBox.PlaceholderText = "Name";
            nameBox.AllowDrop = true;
            nameBox.KeyDown += this.nameBoxKeyDown;
            nameBox.Leave += this.nameBoxLeave;
            nameBox.DragEnter += this.cardDragEnter;
            nameBox.DragOver += this.cardDragOver;
            nameBox.DragDrop += this.cardDragDrop;
            nameBox.DragLeave += this.cardDragLeave;
            Controls.Add(nameBox);

            strip.Left = FaceOverviewLayout.CardInnerLeft;
            strip.Top = 10;
            strip.Width = FaceOverviewLayout.ContentWidth;
            strip.Height = 228;
            strip.AllowDrop = true;
            strip.Click += (_, _) => this.selectCluster(Cluster.ClusterId);
            strip.MouseDown += this.clusterMouseDown;
            strip.DragEnter += this.cardDragEnter;
            strip.DragOver += this.cardDragOver;
            strip.DragDrop += this.cardDragDrop;
            strip.DragLeave += this.cardDragLeave;
            Controls.Add(strip);

            RefreshCluster(cluster);
        }

        private static void ReplacePictureImage(PictureBox box, Image? source)
        {
            var old = box.Image;
            box.Image = source == null ? null : new Bitmap(source);
            old?.Dispose();
        }

        public FaceOverviewCluster Cluster { get; private set; } = null!;

        public void RefreshCluster(FaceOverviewCluster cluster)
        {
            Cluster = cluster;
            Tag = cluster;

            cardFillColor = cluster.IsSelected ? Color.FromArgb(235, 245, 255) : Color.White;
            BackColor = Color.Transparent;

            nameBox.Tag = cluster;
            strip.Tag = cluster;
            if (!string.Equals(nameBox.Text, cluster.Name, StringComparison.Ordinal))
                nameBox.Text = cluster.Name;

            var desiredFaces = new HashSet<int>(cluster.Thumbnails.Select(t => t.FaceIndex));
            foreach (var existing in thumbnailBoxes.Keys.Where(k => !desiredFaces.Contains(k)).ToList())
            {
                var oldBox = thumbnailBoxes[existing];
                strip.Controls.Remove(oldBox);
                oldBox.Image?.Dispose();
                oldBox.Image = null;
                oldBox.Region?.Dispose();
                oldBox.Dispose();
                thumbnailBoxes.Remove(existing);
            }

            var validFaces = cluster.Thumbnails
                .Where(face => !string.IsNullOrEmpty(face.FaceId))
                .ToList();

            for (int i = 0; i < validFaces.Count; i++)
            {
                var thumbnail = validFaces[i];
                if (!thumbnailBoxes.TryGetValue(thumbnail.FaceIndex, out var thumbBox))
                {
                    thumbBox = new RoundedThumbnailBox
                    {
                        Width = 58,
                        Height = 58,
                        Margin = new Padding(3),
                        CornerRadius = 5
                    };

                    thumbBox.MouseDown += thumbnailMouseDown;
                    thumbBox.MouseUp += thumbnailMouseUp;
                    thumbnailBoxes[thumbnail.FaceIndex] = thumbBox;
                    strip.Controls.Add(thumbBox);
                }

                thumbBox.Width = 58;
                thumbBox.Height = 58;
                thumbBox.CornerRadius = 5;
                thumbBox.Invalidate();

                thumbBox.Tag = payloadFactory(cluster.ClusterId, thumbnail.FaceIndex);
                ReplacePictureImage(thumbBox, thumbnail.Thumbnail ?? FaceThumbnail.PlaceholderImage);
            }

            strip.LayoutThumbnails();
        }

        public void SetDragHover(bool hover)
        {
            isDragHover = hover;
            Invalidate();
        }

        public void ConfigureAutoComplete(AutoCompleteStringCollection source)
        {
            nameBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            nameBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            nameBox.AutoCompleteCustomSource = source;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (Parent != null)
                Cluster.DropZone = Bounds;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            UiPaint.DrawCard(e.Graphics, ClientRectangle, isHovered, isDragHover, cardFillColor);

            var inputRect = new Rectangle(FaceOverviewLayout.CardInnerLeft, 242, FaceOverviewLayout.ContentWidth, 32);
            using var inputPath = UiPaint.CreateRoundRect(inputRect, 7);
            using var inputShadowBrush = new SolidBrush(Color.FromArgb(26, 0, 0, 0));
            using var inputFillBrush = new SolidBrush(Color.White);
            using var inputBorderPen = new Pen(Color.FromArgb(142, 174, 220), 1.5f);
            using var inputShadowPath = UiPaint.CreateRoundRect(new Rectangle(inputRect.X, inputRect.Y + 1, inputRect.Width, inputRect.Height), 7);

            e.Graphics.FillPath(inputShadowBrush, inputShadowPath);
            e.Graphics.FillPath(inputFillBrush, inputPath);
            e.Graphics.DrawPath(inputBorderPen, inputPath);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Color background = UiPaint.ResolveOpaqueBackground(this);
            using var brush = new SolidBrush(background);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var box in thumbnailBoxes.Values)
                {
                    box.Image?.Dispose();
                    box.Image = null;
                    box.Region?.Dispose();
                }

                thumbnailBoxes.Clear();
            }

            base.Dispose(disposing);
        }
    }

    internal sealed class UnknownCardControl : UserControl
    {
        private const int MaxVisibleUnknownFaces = 120;
        private readonly Label title = new();
        private readonly ThumbnailStripPanel strip = new();
        private readonly Dictionary<int, RoundedThumbnailBox> faceBoxes = new();

        private readonly Func<int, int, object> payloadFactory;
        private readonly MouseEventHandler faceMouseDown;
        private readonly MouseEventHandler faceMouseUp;
        private readonly EventHandler faceClick;
        private bool isHovered;
        private bool isDragHover;
        private static readonly Color UnknownCardFillColor = ColorTranslator.FromHtml("#F4F7FB");

        public UnknownCardControl(
            Func<int, int, object> payloadFactory,
            MouseEventHandler faceMouseDown,
            MouseEventHandler faceMouseUp,
            EventHandler faceClick,
            DragEventHandler dragEnter,
            DragEventHandler dragOver,
            DragEventHandler dragDrop,
            EventHandler dragLeave)
        {
            this.payloadFactory = payloadFactory;
            this.faceMouseDown = faceMouseDown;
            this.faceMouseUp = faceMouseUp;
            this.faceClick = faceClick;

            Width = FaceOverviewLayout.CardWidth;
            Height = FaceOverviewLayout.CardHeight;
            AutoSize = false;
            MinimumSize = new Size(FaceOverviewLayout.CardWidth, FaceOverviewLayout.CardHeight);
            MaximumSize = new Size(FaceOverviewLayout.CardWidth, FaceOverviewLayout.CardHeight);
            Margin = new Padding(10);
            BackColor = Color.Transparent;
            BorderStyle = BorderStyle.None;
            AllowDrop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);

            MouseEnter += (_, _) => { isHovered = true; Invalidate(); };
            MouseLeave += (_, _) => { isHovered = false; Invalidate(); };

            DragEnter += dragEnter;
            DragOver += dragOver;
            DragDrop += dragDrop;
            DragLeave += dragLeave;

            strip.Left = FaceOverviewLayout.CardInnerLeft;
            strip.Top = 10;
            strip.Width = FaceOverviewLayout.ContentWidth;
            strip.Height = 228;
            strip.AllowDrop = true;
            strip.DragEnter += dragEnter;
            strip.DragOver += dragOver;
            strip.DragDrop += dragDrop;
            strip.DragLeave += dragLeave;
            Controls.Add(strip);

            title.Left = FaceOverviewLayout.TextInsetLeft;
            title.Top = 250;
            title.Width = FaceOverviewLayout.TextWidth;
            title.Height = 22;
            title.Text = "Unknown Faces";
            title.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            title.ForeColor = Color.FromArgb(34, 34, 34);
            title.BackColor = Color.White;
            title.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(title);
        }

        private static void ReplacePictureImage(PictureBox box, Image? source)
        {
            var old = box.Image;
            box.Image = source == null ? null : new Bitmap(source);
            old?.Dispose();
        }

        public void RefreshFaces(IReadOnlyList<int> unknownFaces, IReadOnlyDictionary<int, Image> faceImages)
        {
            var visibleFaces = unknownFaces.Take(MaxVisibleUnknownFaces).ToList();
            title.Text = unknownFaces.Count > MaxVisibleUnknownFaces
                ? $"Unknown ({MaxVisibleUnknownFaces}/{unknownFaces.Count:N0} shown)"
                : unknownFaces.Count > 0
                    ? $"Unknown Faces ({unknownFaces.Count:N0})"
                    : "Unknown Faces";

            var desired = new HashSet<int>(visibleFaces);
            foreach (var existing in faceBoxes.Keys.Where(k => !desired.Contains(k)).ToList())
            {
                var old = faceBoxes[existing];
                strip.Controls.Remove(old);
                old.Image?.Dispose();
                old.Image = null;
                old.Region?.Dispose();
                old.Dispose();
                faceBoxes.Remove(existing);
            }

            foreach (int faceIndex in visibleFaces)
            {
                if (!faceBoxes.TryGetValue(faceIndex, out var faceBox))
                {
                    faceBox = new RoundedThumbnailBox
                    {
                        Width = 58,
                        Height = 58,
                        Margin = new Padding(3),
                        CornerRadius = 5
                    };

                    faceBox.MouseDown += faceMouseDown;
                    faceBox.MouseUp += faceMouseUp;
                    faceBoxes[faceIndex] = faceBox;
                    strip.Controls.Add(faceBox);
                }

                faceBox.Tag = payloadFactory(0, faceIndex);
                faceImages.TryGetValue(faceIndex, out var image);
                ReplacePictureImage(faceBox, image ?? FaceThumbnail.PlaceholderImage);

                faceBox.CornerRadius = 5;
                faceBox.Invalidate();
            }

            strip.LayoutThumbnails();
        }

        public void SetDragHover(bool hover)
        {
            isDragHover = hover;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            UiPaint.DrawCard(e.Graphics, ClientRectangle, isHovered, isDragHover, UnknownCardFillColor);

            var inputRect = new Rectangle(FaceOverviewLayout.CardInnerLeft, 242, FaceOverviewLayout.ContentWidth, 32);
            using var inputPath = UiPaint.CreateRoundRect(inputRect, 7);
            using var inputShadowBrush = new SolidBrush(Color.FromArgb(26, 0, 0, 0));
            using var inputFillBrush = new SolidBrush(Color.White);
            using var inputBorderPen = new Pen(Color.FromArgb(142, 174, 220), 1.5f);
            using var inputShadowPath = UiPaint.CreateRoundRect(new Rectangle(inputRect.X, inputRect.Y + 1, inputRect.Width, inputRect.Height), 7);

            e.Graphics.FillPath(inputShadowBrush, inputShadowPath);
            e.Graphics.FillPath(inputFillBrush, inputPath);
            e.Graphics.DrawPath(inputBorderPen, inputPath);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Color background = UiPaint.ResolveOpaqueBackground(this);
            using var brush = new SolidBrush(background);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var box in faceBoxes.Values)
                {
                    box.Image?.Dispose();
                    box.Image = null;
                    box.Region?.Dispose();
                }

                faceBoxes.Clear();
            }

            base.Dispose(disposing);
        }
    }

    internal sealed class FaceOverviewGroupingForm : Form
    {
        private const int MaxCardsPerPage = 17;
        private readonly IReadOnlyList<string> alignedPaths;
        private readonly List<FaceOverviewCluster> clusters;
        private readonly HashSet<string> knownNames;
        private readonly Dictionary<int, ClusterCardControl> clusterCards = new();
        private readonly Dictionary<int, Image> faceImageCache = new();
        private readonly List<int> unknownFaceIndices = new();

        private readonly ResponsiveClusterGridPanel clusterGrid = new();
        private readonly UnknownCardControl unknownCard;
        private readonly Button continueButton = new();
        private readonly Button skipButton = new();
        private readonly Button skipAndFinishButton = new();
        private readonly Button undoButton = new();
        private readonly Button redoButton = new();
        private readonly Button previousPageButton = new();
        private readonly Button nextPageButton = new();
        private readonly ComboBox pageSelector = new();
        private readonly ContextMenuStrip thumbnailContextMenu = new();
        private readonly ToolStripMenuItem previewFaceMenuItem = new("Preview face");
        private readonly ToolStripMenuItem splitFaceMenuItem = new("Split");
        private readonly ToolStripMenuItem moveToUnknownMenuItem = new("Unknown face");
        private readonly ToolStripMenuItem moveToClusterMenuItem = new("Move to cluster…");
        private readonly ConcurrentQueue<FaceThumbnail> thumbnailDecodeQueue = new();
        private readonly HashSet<int> queuedDecodeFaceIndices = new();
        private readonly System.Windows.Forms.Timer thumbnailDecodeTimer = new();
        private readonly Label globalCountersLabel = new();

        private int? selectedClusterId;
        private int? dragHoverClusterId;
        private int nextCardSlot;
        private bool clusterDragInProgress;
        private readonly List<FaceOverviewCluster> pendingClusterDisposals = new();
        private bool disposalFlushScheduled;
        private readonly Stack<OverviewUiState> undoStack = new();
        private readonly Stack<OverviewUiState> redoStack = new();
        private bool isHistoryReplay;
        private bool cardsInitialized;
        private int currentPageIndex;
        private int totalPageCount = 1;
        private readonly List<int> currentPageClusterIds = new();
        private int contextMenuSourceClusterId = -1;
        private int contextMenuFaceIndex = -1;
        private int decodeBatchNumber;
        private int decodeBatchTotal;
        private bool firstPagePrepared;
        private bool suppressPageSelectorChanged;
        private bool applyingPendingMutations;
        private int frozenScrollValue;
        private OverviewUiState? frozenBeforeState;
        private static readonly object diagLogSync = new();
        private static string DiagLogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "faceoverview-diag.log");

        private static bool IsLayoutFrozen => FaceOverviewGroupingService._layoutFrozen;
        private static List<Action> PendingMutations => FaceOverviewGroupingService._pendingMutations;
        private bool ShouldQueueMutation => IsLayoutFrozen && !applyingPendingMutations;

        private static void LogDiag(string message)
        {
            string line = $"[FaceOverview] {DateTime.Now:HH:mm:ss.fff} {message}";
            Debug.WriteLine(line);
            Trace.WriteLine(line);
            AppLog.Write?.Invoke(line);

            try
            {
                lock (diagLogSync)
                    File.AppendAllText(DiagLogPath, line + Environment.NewLine);
            }
            catch
            {
            }
        }

        private void LogLayoutDiag(string source)
        {
            LogDiag($"LAYOUT {source}: frozen={IsLayoutFrozen}, cards={currentPageClusterIds.Count}, controls={clusterGrid.Controls.Count}, autoMin={clusterGrid.AutoScrollMinSize}, scrollY={CaptureGridScrollValue()}");
        }

        private void LogScrollDiag(string source)
        {
            LogDiag($"SCROLL {source}: scrollY={CaptureGridScrollValue()}, visible={clusterGrid.VerticalScroll.Visible}, max={clusterGrid.VerticalScroll.Maximum}, large={clusterGrid.VerticalScroll.LargeChange}");
        }

        private void BeginLayoutFreeze()
        {
            if (!FaceOverviewGroupingService._layoutFrozen)
            {
                frozenScrollValue = CaptureGridScrollValue();
                if (!isHistoryReplay)
                    frozenBeforeState = CaptureState();
            }

            FaceOverviewGroupingService._layoutFrozen = true;
            LogDiag($"FREEZE ON: frozenScroll={frozenScrollValue}, pending={PendingMutations.Count}");
        }

        private void EndLayoutFreezeAndApplyPendingMutations()
        {
            if (!IsLayoutFrozen && PendingMutations.Count == 0)
                return;

            int scrollToRestore = IsLayoutFrozen ? frozenScrollValue : CaptureGridScrollValue();
            LogDiag($"FREEZE OFF start: pending={PendingMutations.Count}, restoreTarget={scrollToRestore}");

            var pending = PendingMutations.ToList();
            PendingMutations.Clear();

            applyingPendingMutations = true;
            try
            {
                foreach (var mutation in pending)
                    mutation();

                FlushPendingClusterDisposals();
            }
            finally
            {
                applyingPendingMutations = false;
            }

            RefreshUnknownCard();

            clusterGrid.SuspendLayout();
            try
            {
                FaceOverviewGroupingService._layoutFrozen = false;
                LayoutPageCardsFromIndex(0);
                RestoreGridScrollValue(scrollToRestore);
            }
            finally
            {
                clusterGrid.ResumeLayout();
            }

            if (frozenBeforeState != null && !isHistoryReplay)
            {
                var afterState = CaptureState();
                if (!StatesEqual(frozenBeforeState, afterState))
                {
                    undoStack.Push(frozenBeforeState);
                    redoStack.Clear();
                    UpdateHistoryButtons();
                }

                frozenBeforeState = null;
            }

            LogDiag($"FREEZE OFF end: pending={PendingMutations.Count}, scrollAfter={CaptureGridScrollValue()}");
        }

        private int CaptureGridScrollValue()
        {
            try
            {
                return clusterGrid.VerticalScroll.Visible ? clusterGrid.VerticalScroll.Value : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void RestoreGridScrollValue(int value)
        {
            try
            {
                int max = Math.Max(0, clusterGrid.VerticalScroll.Maximum - clusterGrid.VerticalScroll.LargeChange + 1);
                clusterGrid.VerticalScroll.Value = Math.Max(0, Math.Min(max, value));
                LogScrollDiag($"RestoreGridScrollValue({value})");
            }
            catch
            {
            }
        }

        private void PreserveGridScrollForLayoutMutation(Action mutation, string source)
        {
            int scrollToRestore = CaptureGridScrollValue();
            LogDiag($"SCROLL preserve start: source={source}, before={scrollToRestore}");

            clusterGrid.SuspendLayout();
            try
            {
                mutation();
            }
            finally
            {
                clusterGrid.ResumeLayout();
            }

            RestoreGridScrollValue(scrollToRestore);
            LogDiag($"SCROLL preserve end: source={source}, after={CaptureGridScrollValue()}");
        }

        private void QueuePendingClusterDisposal(FaceOverviewCluster cluster)
        {
            if (!pendingClusterDisposals.Contains(cluster))
            {
                pendingClusterDisposals.Add(cluster);
                LogDiag($"Queued cluster disposal: clusterId={cluster.ClusterId}");
            }
        }

        private void SchedulePendingClusterDisposalFlush()
        {
            if (IsDisposed || disposalFlushScheduled)
                return;

            disposalFlushScheduled = true;
            BeginInvoke(new Action(() =>
            {
                disposalFlushScheduled = false;
                FlushPendingClusterDisposals();
            }));
        }

        public FaceOverviewGroupingForm(IReadOnlyList<List<int>> initialClusters, IReadOnlyList<string> alignedPaths, IReadOnlyList<string> existingNames)
            : this(initialClusters
                .Select((c, idx) => new FaceOverviewCluster(idx + 1, c, alignedPaths))
                .ToList(), alignedPaths, existingNames)
        {
        }

        public FaceOverviewGroupingForm(IReadOnlyList<FaceOverviewCluster> initialClusters, IReadOnlyList<string> alignedPaths, IReadOnlyList<string> existingNames)
        {
            this.alignedPaths = alignedPaths;
            knownNames = new HashSet<string>(existingNames.Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);

            try
            {
                lock (diagLogSync)
                    File.WriteAllText(DiagLogPath, string.Empty);
            }
            catch
            {
            }

            unknownCard = new UnknownCardControl(
                (sourceClusterId, faceIndex) => new FaceDragPayload(sourceClusterId, faceIndex),
                ThumbBox_MouseDown,
                ThumbBox_MouseUp,
                ThumbBox_Click,
                Card_DragEnter,
                Card_DragOver,
                UnknownCard_DragDrop,
                Card_DragLeave);

            previewFaceMenuItem.Click += PreviewFaceMenuItem_Click;
            splitFaceMenuItem.Click += SplitFaceMenuItem_Click;
            moveToUnknownMenuItem.Click += MoveToUnknownMenuItem_Click;
            thumbnailContextMenu.Items.Add(previewFaceMenuItem);
            thumbnailContextMenu.Items.Add(splitFaceMenuItem);
            thumbnailContextMenu.Items.Add(moveToUnknownMenuItem);
            thumbnailContextMenu.Items.Add(moveToClusterMenuItem);

            thumbnailDecodeTimer.Interval = 30;
            thumbnailDecodeTimer.Tick += ThumbnailDecodeTimer_Tick;

            clusters = new List<FaceOverviewCluster>(initialClusters.Count);
            foreach (var source in initialClusters)
            {
                bool isUnknownCluster = string.Equals(source.Name, "Unknown", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(source.Name, "Unknown Faces", StringComparison.OrdinalIgnoreCase);

                if (isUnknownCluster)
                {
                    foreach (int idx in source.FaceIndices)
                    {
                        if (!unknownFaceIndices.Contains(idx))
                            unknownFaceIndices.Add(idx);
                    }

                    continue;
                }

                clusters.Add(new FaceOverviewCluster(source.ClusterId, source.FaceIndices, alignedPaths)
                {
                    Name = source.Name,
                    IsSelected = source.IsSelected,
                    DropZone = source.DropZone
                });
            }

            Text = "SortMyMedia - Face Overview & Grouping";
            Width = 1600;
            Height = 980;
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = ColorTranslator.FromHtml("#F7F9FC");

            try
            {
                var ownerForm = Application.OpenForms.Cast<Form>().FirstOrDefault();
                if (ownerForm?.Icon != null)
                    Icon = (Icon)ownerForm.Icon.Clone();
            }
            catch { }

            LogDiag($"Diagnostics file: {DiagLogPath}");

            BuildUi();
            Shown += (_, _) =>
            {
                if (cardsInitialized)
                    return;

                cardsInitialized = true;
                BeginInvoke(new Action(async () => await InitializeClusterCardsAsync()));
            };
        }

        public FaceOverviewResult Result { get; private set; } = new FaceOverviewResult(Array.Empty<FaceOverviewCluster>(), false);

        private void BuildUi()
        {
            globalCountersLabel.AutoSize = true;
            globalCountersLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            globalCountersLabel.ForeColor = Color.FromArgb(34, 34, 34);
            globalCountersLabel.BackColor = Color.White;
            globalCountersLabel.TextAlign = ContentAlignment.MiddleCenter;
            globalCountersLabel.Padding = new Padding(6, 4, 6, 4);

            var countersPanel = new Panel
            {
                Height = 32,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Top = 6
            };
            countersPanel.Controls.Add(globalCountersLabel);
            globalCountersLabel.Left = 8;
            globalCountersLabel.Top = 4;
            countersPanel.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, countersPanel.Width - 1, countersPanel.Height - 1);
                using var path = UiPaint.CreateRoundRect(rect, 8);
                using var fill = new SolidBrush(Color.White);
                using var border = new Pen(Color.FromArgb(142, 174, 220), 1.6f);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            };
            Controls.Add(countersPanel);
            UpdateGlobalCounters();

            pageSelector.Top = 8;
            pageSelector.Width = 120;
            pageSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            pageSelector.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pageSelector.SelectedIndexChanged += async (_, _) =>
            {
                if (suppressPageSelectorChanged || pageSelector.SelectedIndex < 0)
                    return;

                await RenderClusterPageAsync(pageSelector.SelectedIndex);
            };
            Controls.Add(pageSelector);

            previousPageButton.Width = 34;
            previousPageButton.Height = pageSelector.Height;
            previousPageButton.Top = pageSelector.Top;
            previousPageButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            previousPageButton.Text = "◀";
            previousPageButton.Click += async (_, _) => await GoToPreviousPageAsync();
            Controls.Add(previousPageButton);

            nextPageButton.Width = 34;
            nextPageButton.Height = pageSelector.Height;
            nextPageButton.Top = pageSelector.Top;
            nextPageButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            nextPageButton.Text = "▶";
            nextPageButton.Click += async (_, _) => await GoToNextPageAsync();
            Controls.Add(nextPageButton);

            clusterGrid.Left = 8;
            clusterGrid.Top = 48;
            clusterGrid.Width = ClientSize.Width - 16;
            clusterGrid.Height = ClientSize.Height - 150;
            clusterGrid.AllowDrop = true;
            clusterGrid.Padding = new Padding(12);
            clusterGrid.BackColor = ColorTranslator.FromHtml("#F7F9FC");
            clusterGrid.DragEnter += ClusterGrid_DragEnter;
            clusterGrid.DragDrop += ClusterGrid_DragDrop;
            clusterGrid.DragOver += Grid_DragOver;
            clusterGrid.Scroll += (_, e) => LogDiag($"SCROLL event: type={e.ScrollOrientation}, old={e.OldValue}, new={e.NewValue}, reason={e.Type}, frozen={IsLayoutFrozen}");
            Controls.Add(clusterGrid);

            unknownCard.TabIndex = 0;
            clusterGrid.Controls.Add(unknownCard);

            skipButton.Left = 770;
            skipButton.Top = 775;
            skipButton.Width = 240;
            skipButton.Height = 34;
            skipButton.Text = "Skip overview and auto-assign names";
            skipButton.Click += (_, _) => SkipOverviewAndAutoAssignNames();
            Controls.Add(skipButton);

            continueButton.Left = 1030;
            continueButton.Top = 775;
            continueButton.Width = 234;
            continueButton.Height = 34;
            continueButton.Text = "Continue";
            continueButton.Click += (_, _) => ContinueWithAutoNames();
            Controls.Add(continueButton);

            skipAndFinishButton.AutoSize = false;
            skipAndFinishButton.Padding = new Padding(16, 6, 16, 6);
            skipAndFinishButton.Text = "Finish";
            skipAndFinishButton.Anchor = AnchorStyles.Bottom;
            skipAndFinishButton.Font = new Font(Font.FontFamily, 10f, FontStyle.Bold);
            int skipTextWidth = TextRenderer.MeasureText(skipAndFinishButton.Text, skipAndFinishButton.Font).Width;
            skipAndFinishButton.Width = skipTextWidth + skipAndFinishButton.Padding.Horizontal + 18;
            skipAndFinishButton.Height = 38;
            skipAndFinishButton.Top = ClientSize.Height - skipAndFinishButton.Height - 12;
            skipAndFinishButton.FlatStyle = FlatStyle.Flat;
            skipAndFinishButton.FlatAppearance.BorderSize = 0;
            skipAndFinishButton.ForeColor = Color.FromArgb(16, 40, 76);
            skipAndFinishButton.Click += (_, _) => ContinueWithAutoNames();
            skipAndFinishButton.Paint += (_, e) => PaintPillButton(skipAndFinishButton, e.Graphics);
            skipAndFinishButton.MouseEnter += (_, _) => skipAndFinishButton.Invalidate();
            skipAndFinishButton.MouseLeave += (_, _) => skipAndFinishButton.Invalidate();
            Controls.Add(skipAndFinishButton);

            undoButton.Text = "◀ Undo";
            undoButton.Width = 80;
            undoButton.Height = 36;
            undoButton.Anchor = AnchorStyles.Bottom;
            undoButton.FlatStyle = FlatStyle.Flat;
            undoButton.FlatAppearance.BorderSize = 0;
            undoButton.BackColor = Color.Transparent;
            undoButton.ForeColor = Color.FromArgb(16, 40, 76);
            undoButton.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            undoButton.Paint += (_, e) => PaintNavButton(undoButton, e.Graphics);
            undoButton.Click += (_, _) => UndoLastAction();
            Controls.Add(undoButton);

            redoButton.Text = "Redo ▶";
            redoButton.Width = 80;
            redoButton.Height = 36;
            redoButton.Anchor = AnchorStyles.Bottom;
            redoButton.FlatStyle = FlatStyle.Flat;
            redoButton.FlatAppearance.BorderSize = 0;
            redoButton.BackColor = Color.Transparent;
            redoButton.ForeColor = Color.FromArgb(16, 40, 76);
            redoButton.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            redoButton.Paint += (_, e) => PaintNavButton(redoButton, e.Graphics);
            redoButton.Click += (_, _) => RedoLastAction();
            Controls.Add(redoButton);

            LayoutBottomControls();
            Resize += (_, _) => LayoutBottomControls();
            UpdateHistoryButtons();
        }

        private void LayoutBottomControls()
        {
            LogLayoutDiag("LayoutBottomControls(start)");
            if (IsLayoutFrozen)
                return;

            const int spacing = 10;
            int bottomY = ClientSize.Height - Math.Max(skipAndFinishButton.Height, undoButton.Height) - 12;

            int totalWidth = undoButton.Width + spacing + skipAndFinishButton.Width + spacing + redoButton.Width;
            int startX = (ClientSize.Width - totalWidth) / 2;

            undoButton.Left = startX;
            undoButton.Top = bottomY + (skipAndFinishButton.Height - undoButton.Height) / 2;

            skipAndFinishButton.Left = undoButton.Right + spacing;
            skipAndFinishButton.Top = bottomY;

            redoButton.Left = skipAndFinishButton.Right + spacing;
            redoButton.Top = undoButton.Top;

            // Position page nav buttons relative to the counters panel
            UpdateGlobalCounters();
            int navRight = (globalCountersLabel.Parent is Panel cp) ? cp.Left - 12 : ClientSize.Width - 200;
            nextPageButton.Left = navRight - nextPageButton.Width;
            pageSelector.Left = nextPageButton.Left - pageSelector.Width - 4;
            previousPageButton.Left = pageSelector.Left - previousPageButton.Width - 4;

            clusterGrid.Left = 8;
            clusterGrid.Top = 48;
            clusterGrid.Width = ClientSize.Width - 16;
            clusterGrid.Height = Math.Max(120, skipAndFinishButton.Top - clusterGrid.Top - 12);
            LayoutPageCardsFromIndex(0);
            LogLayoutDiag("LayoutBottomControls(end)");
        }

        private int GetCardsPerRow()
        {
            int availableWidth = Math.Max(1, clusterGrid.ClientSize.Width - clusterGrid.Padding.Horizontal);
            return Math.Max(1, (availableWidth + FaceOverviewLayout.CardSpacing) / (FaceOverviewLayout.CardWidth + FaceOverviewLayout.CardSpacing));
        }

        private int GetCardsPerPage()
        {
            return MaxCardsPerPage;
        }

        private void LayoutPageCardsFromIndex(int startIndex)
        {
            LogLayoutDiag($"LayoutPageCardsFromIndex(startIndex={startIndex})");
            if (IsLayoutFrozen)
                return;

            var validCurrentPageClusterIds = currentPageClusterIds
                .Where(clusterCards.ContainsKey)
                .ToList();

            if (validCurrentPageClusterIds.Count != currentPageClusterIds.Count)
            {
                currentPageClusterIds.Clear();
                currentPageClusterIds.AddRange(validCurrentPageClusterIds);
                startIndex = 0;
            }

            int cardsPerRow = GetCardsPerRow();
            const int spacing = FaceOverviewLayout.CardSpacing;
            int cardWidth = FaceOverviewLayout.CardWidth;
            int cardHeight = FaceOverviewLayout.CardHeight;

            int scrollOffsetX = clusterGrid.AutoScrollPosition.X;
            int scrollOffsetY = clusterGrid.AutoScrollPosition.Y;

            int unknownX = clusterGrid.Padding.Left + scrollOffsetX;
            int unknownY = clusterGrid.Padding.Top + scrollOffsetY;
            unknownCard.Location = new Point(unknownX, unknownY);
            unknownCard.TabIndex = 0;

            if (clusterCards.Count == 0 || currentPageClusterIds.Count == 0)
            {
                clusterGrid.AutoScrollMinSize = new Size(0, clusterGrid.Padding.Top + cardHeight + clusterGrid.Padding.Bottom);
                return;
            }

            int begin = Math.Max(0, startIndex);
            for (int i = begin; i < currentPageClusterIds.Count; i++)
            {
                int clusterId = currentPageClusterIds[i];
                var card = clusterCards[clusterId];

                int slotIndex = i + 1;
                int row = slotIndex / cardsPerRow;
                int col = slotIndex % cardsPerRow;
                int x = clusterGrid.Padding.Left + (col * (cardWidth + spacing)) + scrollOffsetX;
                int y = clusterGrid.Padding.Top + (row * (cardHeight + spacing)) + scrollOffsetY;
                card.Location = new Point(x, y);
                card.TabIndex = i + 1;
            }

            int totalCards = currentPageClusterIds.Count + 1;
            int rows = Math.Max(1, (totalCards + cardsPerRow - 1) / cardsPerRow);
            clusterGrid.AutoScrollMinSize = new Size(0, clusterGrid.Padding.Top + (rows * (cardHeight + spacing)) + clusterGrid.Padding.Bottom);
            LogLayoutDiag($"LayoutPageCardsFromIndex(end,startIndex={startIndex})");
        }

        private static void PaintPillButton(Button button, Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Math.Max(1, button.Width - 3), Math.Max(1, button.Height - 3));
            using var path = UiPaint.CreateRoundRect(rect, 20);
            using var brush = new LinearGradientBrush(rect,
                Color.FromArgb(198, 224, 255),
                Color.FromArgb(164, 202, 250),
                LinearGradientMode.Vertical);
            graphics.FillPath(brush, path);
            using var border = new Pen(Color.FromArgb(120, 160, 220));
            graphics.DrawPath(border, path);

            TextRenderer.DrawText(
                graphics,
                button.Text,
                button.Font,
                rect,
                button.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static void PaintNavButton(Button button, Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(2, 2, Math.Max(1, button.Width - 5), Math.Max(1, button.Height - 5));

            bool enabled = button.Enabled;

            if (enabled)
            {
                using (var shadowPathWide = UiPaint.CreateRoundRect(new Rectangle(rect.X, rect.Y + 7, rect.Width, rect.Height), 10))
                using (var shadowWideBrush = new SolidBrush(Color.FromArgb(42, 0, 0, 0)))
                {
                    graphics.FillPath(shadowWideBrush, shadowPathWide);
                }

                using (var shadowPathSoft = UiPaint.CreateRoundRect(new Rectangle(rect.X, rect.Y + 4, rect.Width, rect.Height), 9))
                using (var shadowSoftBrush = new SolidBrush(Color.FromArgb(26, 0, 0, 0)))
                {
                    graphics.FillPath(shadowSoftBrush, shadowPathSoft);
                }
            }

            using var path = UiPaint.CreateRoundRect(rect, 8);
            Color fill = enabled ? Color.FromArgb(244, 248, 255) : Color.FromArgb(230, 230, 230);
            Color border = enabled ? Color.FromArgb(120, 160, 220) : Color.FromArgb(190, 190, 190);
            Color text = enabled ? button.ForeColor : Color.FromArgb(160, 160, 160);

            using var fillBrush = new SolidBrush(fill);
            using var borderPen = new Pen(border, 2.2f);
            graphics.FillPath(fillBrush, path);
            graphics.DrawPath(borderPen, path);

            TextRenderer.DrawText(
                graphics,
                button.Text,
                button.Font,
                rect,
                text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private async Task InitializeClusterCardsAsync()
        {
            AppLog.Write?.Invoke("Preparing first page...");
            await RenderClusterPageAsync(0);
            QueueBackgroundWarmupPages();
            AppLog.Write?.Invoke("Face overview ready.");
            firstPagePrepared = true;
        }

        private void QueueBackgroundWarmupPages()
        {
            var orderedClusters = GetLayoutOrderedClusters();
            int cardsPerPage = GetCardsPerPage();
            var warmupClusters = orderedClusters
                .Skip(cardsPerPage)
                .Take(cardsPerPage * 2)
                .ToList();

            if (warmupClusters.Count == 0)
                return;

            BeginInvoke(new Action(() => QueueThumbnailsForDecode(warmupClusters)));
        }

        private async Task GoToNextPageAsync()
        {
            int target = totalPageCount <= 1 ? 0 : (currentPageIndex + 1) % totalPageCount;
            await RenderClusterPageAsync(target);
        }

        private async Task GoToPreviousPageAsync()
        {
            int target = totalPageCount <= 1 ? 0 : (currentPageIndex - 1 + totalPageCount) % totalPageCount;
            await RenderClusterPageAsync(target);
        }

        private void UpdatePageSelector()
        {
            suppressPageSelectorChanged = true;
            try
            {
                if (pageSelector.Items.Count != totalPageCount)
                {
                    pageSelector.Items.Clear();
                    for (int i = 0; i < totalPageCount; i++)
                        pageSelector.Items.Add($"Page {i + 1}");
                }

                if (totalPageCount > 0)
                    pageSelector.SelectedIndex = Math.Max(0, Math.Min(totalPageCount - 1, currentPageIndex));
            }
            finally
            {
                suppressPageSelectorChanged = false;
            }
        }

        private void QueueThumbnailsForDecode(IEnumerable<FaceOverviewCluster> pageClusters)
        {
            int queued = 0;
            lock (queuedDecodeFaceIndices)
            {
                foreach (var cluster in pageClusters)
                {
                    foreach (var thumb in cluster.Thumbnails)
                    {
                        if (thumb.Thumbnail != null)
                            continue;

                        if (queuedDecodeFaceIndices.Add(thumb.FaceIndex))
                        {
                            thumbnailDecodeQueue.Enqueue(thumb);
                            queued++;
                        }
                    }
                }

                if (queued > 0)
                {
                    int pending = queuedDecodeFaceIndices.Count;
                    decodeBatchNumber = 0;
                    decodeBatchTotal = Math.Max(1, (int)Math.Ceiling(pending / 8d));
                }
            }

            if (queued > 0 && !thumbnailDecodeTimer.Enabled)
                thumbnailDecodeTimer.Start();
        }

        private void ThumbnailDecodeTimer_Tick(object? sender, EventArgs e)
        {
            const int batchSize = 8;
            var decodedFaces = new List<int>(batchSize);

            for (int i = 0; i < batchSize; i++)
            {
                if (!thumbnailDecodeQueue.TryDequeue(out var thumb))
                    break;

                bool loaded = thumb.EnsureThumbnailLoaded();
                lock (queuedDecodeFaceIndices)
                    queuedDecodeFaceIndices.Remove(thumb.FaceIndex);

                if (loaded)
                    decodedFaces.Add(thumb.FaceIndex);
            }

            if (decodedFaces.Count > 0)
            {
                decodeBatchNumber++;
                AppLog.Write?.Invoke($"Decoding thumbnails batch {decodeBatchNumber}/{Math.Max(decodeBatchNumber, decodeBatchTotal)}...");

                var affectedClusterIds = clusterCards.Values
                    .Where(card => card.Cluster.Thumbnails.Any(t => decodedFaces.Contains(t.FaceIndex)))
                    .Select(card => card.Cluster.ClusterId)
                    .Distinct()
                    .ToList();

                foreach (int clusterId in affectedClusterIds)
                    RefreshClusterCard(clusterId);
            }

            if (thumbnailDecodeQueue.IsEmpty)
                thumbnailDecodeTimer.Stop();
        }

        private async Task RenderClusterPageAsync(int requestedPageIndex, bool preserveScrollPosition = false)
        {
            LogLayoutDiag($"RenderClusterPageAsync(start,request={requestedPageIndex})");
            if (IsLayoutFrozen)
                return;

            int scrollToRestore = preserveScrollPosition ? CaptureGridScrollValue() : 0;

            var orderedClusters = GetLayoutOrderedClusters();
            int cardsPerPage = GetCardsPerPage();
            totalPageCount = Math.Max(1, (int)Math.Ceiling(orderedClusters.Count / (double)cardsPerPage));
            currentPageIndex = Math.Max(0, Math.Min(totalPageCount - 1, requestedPageIndex));

            int skip = currentPageIndex * cardsPerPage;
            var pageClusters = orderedClusters
                .Skip(skip)
                .Take(cardsPerPage)
                .ToList();

            AppLog.Write?.Invoke("Rendering grid layout...");

            clusterGrid.SuspendLayout();
            try
            {
                clusterGrid.AutoScrollMinSize = Size.Empty;
                ResetGridScrollToTop();

                foreach (var card in clusterCards.Values.ToList())
                    card.Dispose();

                clusterGrid.Controls.Clear();
                unknownCard.TabIndex = 0;
                clusterGrid.Controls.Add(unknownCard);

                clusterCards.Clear();
                nextCardSlot = 0;
                currentPageClusterIds.Clear();

                const int batchSize = 20;
                int count = 0;
                foreach (var cluster in pageClusters)
                {
                    foreach (var thumb in cluster.Thumbnails)
                        thumb.EnsureThumbnailLoaded();

                    var card = CreateClusterCard(cluster);
                    card.TabIndex = ++nextCardSlot;
                    clusterCards[cluster.ClusterId] = card;
                    currentPageClusterIds.Add(cluster.ClusterId);
                    clusterGrid.Controls.Add(card);

                    count++;
                    if (count % batchSize == 0)
                    {
                        ResetGridScrollToTop();
                        LayoutPageCardsFromIndex(count - batchSize);
                        clusterGrid.ResumeLayout();
                        await Task.Yield();
                        clusterGrid.SuspendLayout();
                    }
                }
            }
            finally
            {
                clusterGrid.ResumeLayout();
            }

            previousPageButton.Enabled = totalPageCount > 1;
            nextPageButton.Enabled = totalPageCount > 1;
            UpdatePageSelector();

            RefreshUnknownCard();
            ResetGridScrollToTop();
            LayoutPageCardsFromIndex(0);
            if (preserveScrollPosition)
                RestoreGridScrollValue(scrollToRestore);
            else
                ResetGridScrollToTop();

            QueueThumbnailsForDecode(pageClusters);
            LogLayoutDiag($"RenderClusterPageAsync(end,page={currentPageIndex})");
        }

        private void ResetGridScrollToTop()
        {
            LogScrollDiag("ResetGridScrollToTop(before)");
            try
            {
                clusterGrid.AutoScrollPosition = new Point(0, 0);
                if (clusterGrid.VerticalScroll.Visible)
                    clusterGrid.VerticalScroll.Value = 0;
            }
            catch
            {
            }
            LogScrollDiag("ResetGridScrollToTop(after)");
        }

        private ClusterCardControl CreateClusterCard(FaceOverviewCluster cluster)
        {
            var card = new ClusterCardControl(
                cluster,
                SelectCluster,
                (clusterId, faceIndex) => new FaceDragPayload(clusterId, faceIndex),
                ThumbBox_MouseDown,
                ThumbBox_MouseUp,
                CardSurface_MouseDown,
                ThumbBox_Click,
                Card_DragEnter,
                Card_DragOver,
                Card_DragDrop,
                Card_DragLeave,
                NameBox_KeyDown,
                NameBox_Leave);

            card.ConfigureAutoComplete(CreateAutoCompleteSource());
            return card;
        }

        private Image? GetClusterDragPreviewImage(FaceOverviewCluster cluster)
        {
            if (cluster.RepresentativeImage != null)
                return cluster.RepresentativeImage;

            foreach (var thumbnail in cluster.Thumbnails)
            {
                if (thumbnail.Thumbnail != null)
                    return thumbnail.Thumbnail;

                if (faceImageCache.TryGetValue(thumbnail.FaceIndex, out var cached))
                    return cached;
            }

            return null;
        }

        private void DoDragDropWithPreview(Control dragSource, object payload, Image? previewImage)
        {
            using var preview = previewImage == null ? null : new DragPreviewForm(previewImage);
            using var timer = new System.Windows.Forms.Timer { Interval = 16 };

            if (preview != null)
            {
                timer.Tick += (_, _) => preview.UpdatePosition(Cursor.Position);
                preview.UpdatePosition(Cursor.Position);
                preview.Show();
                timer.Start();
            }

            try
            {
                dragSource.DoDragDrop(payload, DragDropEffects.Move);
            }
            finally
            {
                timer.Stop();
                preview?.Close();
            }
        }

        private void RefreshClusterCard(int clusterId)
        {
            if (!clusterCards.TryGetValue(clusterId, out var card))
                return;

            var cluster = clusters.FirstOrDefault(c => c.ClusterId == clusterId);
            if (cluster == null)
                return;

            card.RefreshCluster(cluster);
            card.ConfigureAutoComplete(CreateAutoCompleteSource());

            foreach (var thumb in cluster.Thumbnails)
            {
                if (thumb.Thumbnail != null)
                    UpdateFaceImageCache(thumb.FaceIndex, thumb.Thumbnail);
            }

            QueueThumbnailsForDecode(new[] { cluster });
        }

        private void RefreshUnknownCard()
        {
            LogDiag($"UNKNOWN refresh: unknownCount={unknownFaceIndices.Count}, imageCache={faceImageCache.Count}, frozen={IsLayoutFrozen}");
            unknownCard.RefreshFaces(unknownFaceIndices, faceImageCache);
            UpdateGlobalCounters();
        }

        private void UpdateFaceImageCache(int faceIndex, Image source)
        {
            Image clone;
            try
            {
                clone = new Bitmap(source);
            }
            catch
            {
                return;
            }

            if (faceImageCache.TryGetValue(faceIndex, out var existing))
                existing.Dispose();

            faceImageCache[faceIndex] = clone;
        }

        private static bool RemoveFaceIndex(List<int> faceIndices, int faceIndex)
        {
            return faceIndices.RemoveAll(i => i == faceIndex) > 0;
        }

        private OverviewUiState CaptureState()
        {
            return new OverviewUiState
            {
                Clusters = clusters
                    .Select(c => new ClusterState
                    {
                        ClusterId = c.ClusterId,
                        Name = c.Name,
                        IsSelected = c.IsSelected,
                        FaceIndices = c.FaceIndices.ToList()
                    })
                    .ToList(),
                UnknownFaceIndices = unknownFaceIndices.ToList(),
                SelectedClusterId = selectedClusterId,
                CardOrder = clusterGrid.Controls
                    .OfType<ClusterCardControl>()
                    .OrderBy(c => c.TabIndex)
                    .Select(c => c.Cluster.ClusterId)
                    .ToList()
            };
        }

        private static bool StatesEqual(OverviewUiState a, OverviewUiState b)
        {
            if (a.SelectedClusterId != b.SelectedClusterId)
                return false;

            if (!a.CardOrder.SequenceEqual(b.CardOrder))
                return false;

            if (!a.UnknownFaceIndices.SequenceEqual(b.UnknownFaceIndices))
                return false;

            if (a.Clusters.Count != b.Clusters.Count)
                return false;

            for (int i = 0; i < a.Clusters.Count; i++)
            {
                var x = a.Clusters[i];
                var y = b.Clusters[i];
                if (x.ClusterId != y.ClusterId || x.Name != y.Name || x.IsSelected != y.IsSelected)
                    return false;

                if (!x.FaceIndices.SequenceEqual(y.FaceIndices))
                    return false;
            }

            return true;
        }

        private async Task ExecuteTrackedDragDropAction(Func<Task> action)
        {
            if (isHistoryReplay)
            {
                await action();
                return;
            }

            if (IsLayoutFrozen)
            {
                await action();
                return;
            }

            var before = CaptureState();
            await action();
            var after = CaptureState();

            if (!StatesEqual(before, after))
            {
                undoStack.Push(before);
                redoStack.Clear();
                UpdateHistoryButtons();
            }
        }

        private void UndoLastAction()
        {
            if (undoStack.Count == 0)
                return;

            var current = CaptureState();
            var previous = undoStack.Pop();
            redoStack.Push(current);
            RestoreState(previous);
            UpdateHistoryButtons();
        }

        private void RedoLastAction()
        {
            if (redoStack.Count == 0)
                return;

            var current = CaptureState();
            var next = redoStack.Pop();
            undoStack.Push(current);
            RestoreState(next);
            UpdateHistoryButtons();
        }

        private void UpdateHistoryButtons()
        {
            undoButton.Enabled = undoStack.Count > 0;
            redoButton.Enabled = redoStack.Count > 0;
        }

        private void RestoreState(OverviewUiState state)
        {
            if (IsLayoutFrozen)
                return;

            isHistoryReplay = true;
            try
            {
                clusterGrid.SuspendLayout();
                int previousScroll = clusterGrid.VerticalScroll.Value;
                bool structureChanged = false;
                bool orderChanged = false;

                var targetById = state.Clusters.ToDictionary(c => c.ClusterId);
                var currentById = clusters.ToDictionary(c => c.ClusterId);

                foreach (var cluster in clusters.Where(c => !targetById.ContainsKey(c.ClusterId)).ToList())
                {
                    clusters.Remove(cluster);
                    if (clusterCards.TryGetValue(cluster.ClusterId, out var card))
                    {
                        clusterGrid.Controls.Remove(card);
                        clusterCards.Remove(cluster.ClusterId);
                        card.Dispose();
                    }

                    cluster.Dispose();
                    structureChanged = true;
                }

                foreach (var target in state.Clusters)
                {
                    if (!currentById.TryGetValue(target.ClusterId, out var existing))
                    {
                        var newCluster = new FaceOverviewCluster(target.ClusterId, target.FaceIndices, alignedPaths)
                        {
                            Name = target.Name,
                            IsSelected = target.IsSelected
                        };

                        // Apply cached thumbnails so restored cards show images immediately
                        foreach (var thumb in newCluster.Thumbnails)
                        {
                            if (thumb.Thumbnail == null && faceImageCache.TryGetValue(thumb.FaceIndex, out var cached))
                                thumb.Thumbnail = new Bitmap(cached);
                            else
                                thumb.EnsureThumbnailLoaded();
                        }

                        clusters.Add(newCluster);
                        var newCard = CreateClusterCard(newCluster);
                        newCard.TabIndex = ++nextCardSlot;
                        clusterCards[newCluster.ClusterId] = newCard;
                        clusterGrid.Controls.Add(newCard);
                        structureChanged = true;
                        continue;
                    }

                    bool changed = !existing.FaceIndices.SequenceEqual(target.FaceIndices)
                        || !string.Equals(existing.Name, target.Name, StringComparison.Ordinal)
                        || existing.IsSelected != target.IsSelected;

                    if (!changed)
                        continue;

                    existing.FaceIndices.Clear();
                    existing.FaceIndices.AddRange(target.FaceIndices);
                    existing.Name = target.Name;
                    existing.IsSelected = target.IsSelected;
                    existing.RebuildFromIndices(alignedPaths);

                    foreach (var thumb in existing.Thumbnails)
                    {
                        if (thumb.Thumbnail == null && faceImageCache.TryGetValue(thumb.FaceIndex, out var cached))
                            thumb.Thumbnail = new Bitmap(cached);
                        else
                            thumb.EnsureThumbnailLoaded();
                    }

                    RefreshClusterCard(existing.ClusterId);
                }

                unknownFaceIndices.Clear();
                unknownFaceIndices.AddRange(state.UnknownFaceIndices);
                RefreshUnknownCard();

                selectedClusterId = state.SelectedClusterId;

                var targetOrder = state.CardOrder.Where(clusterCards.ContainsKey).ToList();
                var currentOrder = clusterGrid.Controls
                    .OfType<ClusterCardControl>()
                    .OrderBy(c => c.TabIndex)
                    .Select(c => c.Cluster.ClusterId)
                    .ToList();

                orderChanged = !targetOrder.SequenceEqual(currentOrder);
                int tab = 1;
                foreach (int clusterId in targetOrder)
                {
                    if (clusterCards.TryGetValue(clusterId, out var card))
                        card.TabIndex = tab++;
                }

                foreach (var card in clusterGrid.Controls.OfType<ClusterCardControl>().OrderBy(c => c.TabIndex).ToList())
                {
                    if (!targetOrder.Contains(card.Cluster.ClusterId))
                        card.TabIndex = tab++;
                }

                nextCardSlot = tab;

                if (structureChanged || orderChanged)
                {
                    currentPageClusterIds.Clear();
                    currentPageClusterIds.AddRange(clusterGrid.Controls
                        .OfType<ClusterCardControl>()
                        .OrderBy(c => c.TabIndex)
                        .Select(c => c.Cluster.ClusterId));

                    LayoutPageCardsFromIndex(0);
                    int max = Math.Max(0, clusterGrid.VerticalScroll.Maximum - clusterGrid.VerticalScroll.LargeChange + 1);
                    clusterGrid.VerticalScroll.Value = Math.Max(0, Math.Min(previousScroll, max));
                }

                // Queue thumbnail decoding for all clusters on the current page
                // so restored/modified clusters show images instead of placeholders.
                var pageClusters = currentPageClusterIds
                    .Select(id => clusters.FirstOrDefault(c => c.ClusterId == id))
                    .Where(c => c != null)
                    .ToList();
                QueueThumbnailsForDecode(pageClusters!);
            }
            finally
            {
                clusterGrid.ResumeLayout();
                isHistoryReplay = false;
            }
        }

        private async void UnknownCard_DragDrop(object? sender, DragEventArgs e)
        {
            try
            {
                if (e.Data?.GetData(typeof(FaceDragPayload)) is not FaceDragPayload payload)
                {
                    if (e.Data?.GetData(typeof(ClusterDragPayload)) is not ClusterDragPayload clusterPayload)
                        return;

                    LogDiag($"Unknown drop cluster: source={clusterPayload.SourceClusterId}");
                    await ExecuteTrackedDragDropAction(() => MoveClusterToUnknownAsync(clusterPayload.SourceClusterId));
                    SetDragHoverCluster(null);
                    return;
                }

                LogDiag($"Unknown drop face: source={payload.SourceClusterId}, face={payload.FaceIndex}");
                await ExecuteTrackedDragDropAction(() => MoveFaceToUnknownAsync(payload.SourceClusterId, payload.FaceIndex));
                SetDragHoverCluster(null);
            }
            catch (Exception ex)
            {
                LogDiag($"UnknownCard_DragDrop EXCEPTION: {ex}");
                MessageBox.Show($"Drag/drop error (Unknown): {ex.Message}", "Face Overview", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (IsLayoutFrozen)
                    EndLayoutFreezeAndApplyPendingMutations();
            }
        }

        private void AddClusterCard(FaceOverviewCluster cluster)
        {
            if (ShouldQueueMutation)
            {
                PendingMutations.Add(() => AddClusterCard(cluster));
                return;
            }

            if (currentPageClusterIds.Contains(cluster.ClusterId))
                return;

            if (currentPageClusterIds.Count >= GetCardsPerPage())
            {
                totalPageCount = Math.Max(1, (int)Math.Ceiling(clusters.Count / (double)GetCardsPerPage()));
                previousPageButton.Enabled = totalPageCount > 1;
                nextPageButton.Enabled = totalPageCount > 1;
                return;
            }

            PreserveGridScrollForLayoutMutation(() =>
            {
                var card = CreateClusterCard(cluster);
                clusterCards[cluster.ClusterId] = card;
                currentPageClusterIds.Add(cluster.ClusterId);
                clusterGrid.Controls.Add(card);
                LayoutPageCardsFromIndex(currentPageClusterIds.Count - 1);
            }, $"AddClusterCard({cluster.ClusterId})");

            totalPageCount = Math.Max(1, (int)Math.Ceiling(clusters.Count / (double)GetCardsPerPage()));
            previousPageButton.Enabled = totalPageCount > 1;
            nextPageButton.Enabled = totalPageCount > 1;
            UpdateGlobalCounters();
        }

        private void RemoveClusterCard(int clusterId)
        {
            if (ShouldQueueMutation)
            {
                PendingMutations.Add(() => RemoveClusterCard(clusterId));
                return;
            }

            PreserveGridScrollForLayoutMutation(() =>
            {
                int removedIndex = currentPageClusterIds.IndexOf(clusterId);

                if (clusterCards.TryGetValue(clusterId, out var card))
                {
                    clusterGrid.Controls.Remove(card);
                    card.Dispose();
                    clusterCards.Remove(clusterId);
                }

                if (removedIndex >= 0)
                {
                    currentPageClusterIds.RemoveAt(removedIndex);
                    RefillCurrentPageFromNextPages();
                    LayoutPageCardsFromIndex(Math.Max(0, removedIndex));
                }
            }, $"RemoveClusterCard({clusterId})");

            totalPageCount = Math.Max(1, (int)Math.Ceiling(clusters.Count / (double)GetCardsPerPage()));
            currentPageIndex = Math.Min(currentPageIndex, totalPageCount - 1);
            previousPageButton.Enabled = totalPageCount > 1;
            nextPageButton.Enabled = totalPageCount > 1;
            UpdateGlobalCounters();
        }

        private void RefillCurrentPageFromNextPages()
        {
            int cardsPerPage = GetCardsPerPage();
            if (currentPageClusterIds.Count >= cardsPerPage)
                return;

            var ordered = GetLayoutOrderedClusters();
            var onPage = new HashSet<int>(currentPageClusterIds);
            int pageStart = currentPageIndex * cardsPerPage;

            foreach (var cluster in ordered.Skip(pageStart))
            {
                if (onPage.Contains(cluster.ClusterId))
                    continue;

                if (currentPageClusterIds.Count >= cardsPerPage)
                    break;

                var newCard = CreateClusterCard(cluster);
                newCard.TabIndex = ++nextCardSlot;
                clusterCards[cluster.ClusterId] = newCard;
                currentPageClusterIds.Add(cluster.ClusterId);
                clusterGrid.Controls.Add(newCard);
                onPage.Add(cluster.ClusterId);
            }
        }

        private void InvokeRefreshClusterCard(int clusterId)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
                BeginInvoke(new Action(() => RefreshClusterCard(clusterId)));
            else
                RefreshClusterCard(clusterId);
        }

        private IReadOnlyList<FaceOverviewCluster> GetLayoutOrderedClusters()
        {
            return clusters.ToList();
        }

        private void UpdateGlobalCounters()
        {
            int totalFaces = clusters.Sum(c => c.FaceIndices.Count) + unknownFaceIndices.Count;
            int totalPersons = clusters.Count;
            globalCountersLabel.Text = $"Faces: {totalFaces:N0}   Persons: {totalPersons:N0}";

            if (globalCountersLabel.Parent is Panel panel)
            {
                panel.Width = globalCountersLabel.PreferredWidth + 20;
                panel.Left = ClientSize.Width - panel.Width - 16;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                thumbnailDecodeTimer.Stop();
                thumbnailDecodeTimer.Dispose();
                thumbnailContextMenu.Dispose();

                foreach (var card in clusterCards.Values.ToArray())
                    card.Dispose();

                clusterCards.Clear();

                foreach (var image in faceImageCache.Values)
                    image.Dispose();

                faceImageCache.Clear();

                foreach (var cluster in clusters)
                    cluster.Dispose();

                foreach (var pending in pendingClusterDisposals)
                    pending.Dispose();

                pendingClusterDisposals.Clear();

                FaceOverviewGroupingService._layoutFrozen = false;
                FaceOverviewGroupingService._pendingMutations.Clear();
            }

            base.Dispose(disposing);
        }

        private void ThumbBox_Click(object? sender, EventArgs e)
        {
            // Intentionally left blank.
            // Thumbnail interaction is:
            // - Left mouse: drag/drop only
            // - Right mouse: context menu actions
        }

        private void ThumbBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (sender is not PictureBox pic || pic.Tag is not FaceDragPayload payload)
                return;

            if (pic.Parent?.Tag is FaceOverviewCluster cluster)
            {
                var face = cluster.Thumbnails.FirstOrDefault(f => f.FaceIndex == payload.FaceIndex);
                if (face == null || face.Thumbnail == null || string.IsNullOrEmpty(face.FaceId))
                    return;

                payload.FaceId = face.FaceId;
                payload.ImagePath = face.ImagePath;
                payload.CropRectangle = face.CropRectangle;
            }

            if (e.Button == MouseButtons.Right)
                return;

            if (e.Button != MouseButtons.Left)
                return;

            // Large thumbnail drag = whole-cluster drag, small thumbnail drag = single-face drag.
            if (payload.SourceClusterId > 0 && pic.Width >= 96 && pic.Height >= 96)
            {
                LogDiag($"DRAG START cluster: sourceCluster={payload.SourceClusterId}, size={pic.Width}x{pic.Height}");
                clusterDragInProgress = true;
                BeginLayoutFreeze();
                try
                {
                    var sourceCluster = clusters.FirstOrDefault(c => c.ClusterId == payload.SourceClusterId);
                    DoDragDropWithPreview(pic, new ClusterDragPayload(payload.SourceClusterId), pic.Image ?? (sourceCluster == null ? null : GetClusterDragPreviewImage(sourceCluster)));
                }
                finally
                {
                    clusterDragInProgress = false;
                    EndLayoutFreezeAndApplyPendingMutations();
                }

                return;
            }

            LogDiag($"DRAG START face: sourceCluster={payload.SourceClusterId}, face={payload.FaceIndex}, size={pic.Width}x{pic.Height}");
            clusterDragInProgress = true;
            BeginLayoutFreeze();

            try
            {
                DoDragDropWithPreview(pic, payload, pic.Image);
            }
            finally
            {
                clusterDragInProgress = false;
                EndLayoutFreezeAndApplyPendingMutations();
            }
        }

        private void ThumbBox_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (sender is not Control control || control.Tag is not FaceDragPayload payload)
                return;

            contextMenuSourceClusterId = payload.SourceClusterId;
            contextMenuFaceIndex = payload.FaceIndex;
            previewFaceMenuItem.Enabled = payload.FaceIndex >= 0 && payload.FaceIndex < alignedPaths.Count;
            splitFaceMenuItem.Enabled = payload.FaceIndex >= 0;
            moveToUnknownMenuItem.Enabled = payload.SourceClusterId > 0 && payload.FaceIndex >= 0;
            PopulateMoveToClusterMenuItems();

            thumbnailContextMenu.Show(control, e.Location);
        }

        private void CardSurface_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            if (sender is not Control control || control.Tag is not FaceOverviewCluster cluster)
                return;

            clusterDragInProgress = true;
            BeginLayoutFreeze();
            try
            {
                DoDragDropWithPreview(control, new ClusterDragPayload(cluster.ClusterId), GetClusterDragPreviewImage(cluster));
            }
            finally
            {
                clusterDragInProgress = false;
                EndLayoutFreezeAndApplyPendingMutations();
            }
        }

        private void Card_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(typeof(FaceDragPayload)) == true || e.Data?.GetDataPresent(typeof(ClusterDragPayload)) == true)
                e.Effect = DragDropEffects.Move;
        }

        private void Card_DragOver(object? sender, DragEventArgs e)
        {
            Grid_DragOver(sender, e);
        }

        private void Card_DragLeave(object? sender, EventArgs e)
        {
            SetDragHoverCluster(null);
        }

        private async void Card_DragDrop(object? sender, DragEventArgs e)
        {
            LogDiag($"DROP Card enter: frozen={IsLayoutFrozen}, pending={PendingMutations.Count}");
            try
            {
                if (sender is not Control card || card.Tag is not FaceOverviewCluster target)
                    return;

                if (e.Data?.GetData(typeof(FaceDragPayload)) is FaceDragPayload facePayload)
                {
                    LogDiag($"Card drop face: source={facePayload.SourceClusterId}, target={target.ClusterId}, face={facePayload.FaceIndex}");
                    await ExecuteTrackedDragDropAction(() => MoveFaceAsync(facePayload.SourceClusterId, facePayload.FaceIndex, target.ClusterId));
                    return;
                }

                if (e.Data?.GetData(typeof(ClusterDragPayload)) is ClusterDragPayload clusterPayload)
                {
                    LogDiag($"Card drop cluster: source={clusterPayload.SourceClusterId}, target={target.ClusterId}");
                    await ExecuteTrackedDragDropAction(() => MergeClustersAsync(clusterPayload.SourceClusterId, target.ClusterId));
                }

                SetDragHoverCluster(null);
            }
            catch (Exception ex)
            {
                LogDiag($"Card_DragDrop EXCEPTION: {ex}");
                MessageBox.Show($"Drag/drop error: {ex.Message}", "Face Overview", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClusterGrid_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(typeof(FaceDragPayload)) == true)
                e.Effect = DragDropEffects.Move;
        }

        private void Grid_DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(typeof(FaceDragPayload)) != true && e.Data?.GetDataPresent(typeof(ClusterDragPayload)) != true)
                return;

            e.Effect = DragDropEffects.Move;

            Point gridPoint = clusterGrid.PointToClient(new Point(e.X, e.Y));
            AutoScrollGridForDrag(gridPoint.Y);

            bool unknownHovered = unknownCard.Bounds.Contains(gridPoint);
            unknownCard.SetDragHover(unknownHovered);

            int? hoveredCluster = GetHoveredClusterId(gridPoint);
            SetDragHoverCluster(hoveredCluster);
        }

        private void AutoScrollGridForDrag(int y)
        {
            const int edge = 56;
            const int step = 24;

            int max = Math.Max(0, clusterGrid.VerticalScroll.Maximum - clusterGrid.VerticalScroll.LargeChange + 1);
            int current = Math.Max(0, Math.Min(max, clusterGrid.VerticalScroll.Value));
            int target = current;

            if (y < edge)
                target = Math.Max(0, current - step);
            else if (y > clusterGrid.ClientSize.Height - edge)
                target = Math.Min(max, current + step);

            if (target == current)
                return;

            LogDiag($"SCROLL drag-autoscroll: y={y}, current={current}, target={target}");
            clusterGrid.VerticalScroll.Value = target;
            clusterGrid.Invalidate();
        }

        private int? GetHoveredClusterId(Point gridPoint)
        {
            if (unknownCard.Bounds.Contains(gridPoint))
                return null;

            foreach (var card in clusterCards.Values)
            {
                if (card.Bounds.Contains(gridPoint))
                    return card.Cluster.ClusterId;
            }

            return null;
        }

        private void SetDragHoverCluster(int? clusterId)
        {
            if (dragHoverClusterId == clusterId)
                return;

            if (dragHoverClusterId.HasValue && clusterCards.TryGetValue(dragHoverClusterId.Value, out var previous))
                previous.SetDragHover(false);

            unknownCard.SetDragHover(false);

            dragHoverClusterId = clusterId;

            if (dragHoverClusterId.HasValue && clusterCards.TryGetValue(dragHoverClusterId.Value, out var current))
                current.SetDragHover(true);
        }

        private async void ClusterGrid_DragDrop(object? sender, DragEventArgs e)
        {
            LogDiag($"DROP Grid enter: frozen={IsLayoutFrozen}, pending={PendingMutations.Count}");
            try
            {
                if (e.Data?.GetData(typeof(FaceDragPayload)) is not FaceDragPayload payload)
                    return;

                Point p = clusterGrid.PointToClient(new Point(e.X, e.Y));
                Control? targetControl = clusterGrid.GetChildAtPoint(p);
                if (targetControl != null)
                    return;

                LogDiag($"Grid drop create cluster: source={payload.SourceClusterId}, face={payload.FaceIndex}");
                await ExecuteTrackedDragDropAction(() => CreateClusterFromFaceAsync(payload.SourceClusterId, payload.FaceIndex));
                SetDragHoverCluster(null);
            }
            catch (Exception ex)
            {
                LogDiag($"ClusterGrid_DragDrop EXCEPTION: {ex}");
                MessageBox.Show($"Drag/drop grid error: {ex.Message}", "Face Overview", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task MoveFaceAsync(int sourceClusterId, int faceIndex, int targetClusterId)
        {
            if (ShouldQueueMutation)
            {
                PendingMutations.Add(() => MoveFaceAsync(sourceClusterId, faceIndex, targetClusterId).GetAwaiter().GetResult());
                return;
            }

            LogDiag($"MoveFaceAsync START source={sourceClusterId}, target={targetClusterId}, face={faceIndex}");
            if (sourceClusterId == targetClusterId)
                return;

            var source = sourceClusterId == 0 ? null : clusters.FirstOrDefault(c => c.ClusterId == sourceClusterId);
            var target = clusters.FirstOrDefault(c => c.ClusterId == targetClusterId);
            if (target == null)
                return;

            if (sourceClusterId == 0)
            {
                if (unknownFaceIndices.RemoveAll(i => i == faceIndex) == 0)
                    return;
            }
            else
            {
                if (source == null || !RemoveFaceIndex(source.FaceIndices, faceIndex))
                    return;
            }

            if (!target.FaceIndices.Contains(faceIndex))
                target.FaceIndices.Add(faceIndex);

            if (source != null)
            {
                source.RebuildFromIndices(alignedPaths);
                target.RebuildFromIndices(alignedPaths);
            }
            else
            {
                target.RebuildFromIndices(alignedPaths);
            }

            RefreshSourceAndTargetCards(source, target);
            RefreshUnknownCard();

            if (source != null && source.FaceIndices.Count == 0)
            {
                clusters.Remove(source);
                if (clusterDragInProgress)
                {
                    QueuePendingClusterDisposal(source);
                    SchedulePendingClusterDisposalFlush();
                }
                else
                {
                    RemoveClusterCard(source.ClusterId);
                    source.Dispose();
                }

                LogDiag($"MoveFaceAsync source emptied: clusterId={source.ClusterId}");
                return;
            }
        }

        private async Task MoveFaceToUnknownAsync(int sourceClusterId, int faceIndex)
        {
            if (ShouldQueueMutation)
            {
                PendingMutations.Add(() => MoveFaceToUnknownAsync(sourceClusterId, faceIndex).GetAwaiter().GetResult());
                return;
            }

            LogDiag($"MoveFaceToUnknownAsync START source={sourceClusterId}, face={faceIndex}");
            if (sourceClusterId == 0)
                return;

            var source = clusters.FirstOrDefault(c => c.ClusterId == sourceClusterId);
            if (source == null || !RemoveFaceIndex(source.FaceIndices, faceIndex))
                return;

            var movedThumbnail = source.Thumbnails.FirstOrDefault(t => t.FaceIndex == faceIndex)?.Thumbnail;
            if (movedThumbnail != null)
                UpdateFaceImageCache(faceIndex, movedThumbnail);

            if (!unknownFaceIndices.Contains(faceIndex))
                unknownFaceIndices.Add(faceIndex);

            source.RebuildFromIndices(alignedPaths);

            if (source.FaceIndices.Count == 0)
            {
                clusters.Remove(source);
                if (clusterDragInProgress)
                {
                    QueuePendingClusterDisposal(source);
                    SchedulePendingClusterDisposalFlush();
                }
                else
                {
                    RemoveClusterCard(source.ClusterId);
                    source.Dispose();
                }
            }
            else
            {
                InvokeRefreshClusterCard(source.ClusterId);
            }

            RefreshUnknownCard();
        }

        private async Task MoveClusterToUnknownAsync(int sourceClusterId)
        {
            if (ShouldQueueMutation)
            {
                PendingMutations.Add(() => MoveClusterToUnknownAsync(sourceClusterId).GetAwaiter().GetResult());
                return;
            }

            if (sourceClusterId == 0)
                return;

            var source = clusters.FirstOrDefault(c => c.ClusterId == sourceClusterId);
            if (source == null)
                return;

            foreach (var thumbnail in source.Thumbnails)
            {
                if (thumbnail.Thumbnail != null)
                    UpdateFaceImageCache(thumbnail.FaceIndex, thumbnail.Thumbnail);
            }

            foreach (int faceIndex in source.FaceIndices)
            {
                if (!unknownFaceIndices.Contains(faceIndex))
                    unknownFaceIndices.Add(faceIndex);
            }

            clusters.Remove(source);

            if (clusterDragInProgress)
            {
                QueuePendingClusterDisposal(source);
                SchedulePendingClusterDisposalFlush();
                return;
            }

            RemoveClusterCard(source.ClusterId);
            source.Dispose();
            RefreshUnknownCard();
            LogDiag($"MoveClusterToUnknownAsync completed remove source={source.ClusterId}");
        }

        private async Task MergeClustersAsync(int sourceClusterId, int targetClusterId)
        {
            if (ShouldQueueMutation)
            {
                PendingMutations.Add(() => MergeClustersAsync(sourceClusterId, targetClusterId).GetAwaiter().GetResult());
                return;
            }

            LogDiag($"MergeClustersAsync START source={sourceClusterId}, target={targetClusterId}");
            if (sourceClusterId == targetClusterId)
                return;

            var source = clusters.FirstOrDefault(c => c.ClusterId == sourceClusterId);
            var target = clusters.FirstOrDefault(c => c.ClusterId == targetClusterId);
            if (source == null || target == null)
                return;

            foreach (int face in source.FaceIndices)
            {
                if (!target.FaceIndices.Contains(face))
                    target.FaceIndices.Add(face);
            }

            clusters.Remove(source);
            target.RebuildFromIndices(alignedPaths);
            RefreshSourceAndTargetCards(source, target);

            if (clusterDragInProgress)
            {
                QueuePendingClusterDisposal(source);
                SchedulePendingClusterDisposalFlush();
                return;
            }

            RemoveClusterCard(source.ClusterId);
            source.Dispose();
            LogDiag($"MergeClustersAsync completed remove source={source.ClusterId}");
        }

        private void RefreshSourceAndTargetCards(FaceOverviewCluster? source, FaceOverviewCluster? target)
        {
            if (target != null)
                RefreshClusterCard(target.ClusterId);

            if (source != null)
                RefreshClusterCard(source.ClusterId);
        }

        private void FlushPendingClusterDisposals()
        {
            if (ShouldQueueMutation)
            {
                PendingMutations.Add(FlushPendingClusterDisposals);
                return;
            }

            if (pendingClusterDisposals.Count == 0)
                return;

            foreach (var cluster in pendingClusterDisposals.ToList())
            {
                try
                {
                    LogDiag($"Flush dispose clusterId={cluster.ClusterId}");
                    RemoveClusterCard(cluster.ClusterId);
                    cluster.Dispose();
                }
                catch (Exception ex)
                {
                    LogDiag($"FlushPendingClusterDisposals EXCEPTION cluster={cluster.ClusterId}: {ex}");
                }
            }

            pendingClusterDisposals.Clear();
        }

        private async Task CreateClusterFromFaceAsync(int sourceClusterId, int faceIndex)
        {
            if (ShouldQueueMutation)
            {
                PendingMutations.Add(() => CreateClusterFromFaceAsync(sourceClusterId, faceIndex).GetAwaiter().GetResult());
                return;
            }

            var source = clusters.FirstOrDefault(c => c.ClusterId == sourceClusterId);
            if (source == null || !RemoveFaceIndex(source.FaceIndices, faceIndex))
                return;

            int nextId = clusters.Count == 0 ? 1 : clusters.Max(c => c.ClusterId) + 1;
            var newCluster = new FaceOverviewCluster(nextId, new[] { faceIndex }, alignedPaths);
            clusters.Insert(Math.Min(nextId, clusters.Count), newCluster);

            if (source.FaceIndices.Count == 0)
            {
                clusters.Remove(source);
                RemoveClusterCard(source.ClusterId);
                QueuePendingClusterDisposal(source);
            }
            else
            {
                RefreshClusterCard(source.ClusterId);
            }

            RefreshUnknownCard();

            int pageForNew = GetPageIndexForClusterId(newCluster.ClusterId);
            if (pageForNew == currentPageIndex)
            {
                AddClusterCard(newCluster);
                LayoutPageCardsFromIndex(0);
                UpdateGlobalCounters();
            }
            else
            {
                await RenderClusterPageAsync(pageForNew, preserveScrollPosition: false);
            }

            SelectCluster(newCluster.ClusterId);
            SchedulePendingClusterDisposalFlush();
        }

        private void SelectCluster(int clusterId)
        {
            int? previousSelection = selectedClusterId;
            selectedClusterId = clusterId;
            foreach (var cluster in clusters)
                cluster.IsSelected = cluster.ClusterId == clusterId;

            if (previousSelection.HasValue)
                RefreshClusterCard(previousSelection.Value);

            RefreshClusterCard(clusterId);
        }

        private void NameBox_Leave(object? sender, EventArgs e)
        {
            if (sender is TextBox nameBox && nameBox.Tag is FaceOverviewCluster cluster)
                ApplyClusterName(cluster, nameBox.Text);
        }

        private void NameBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            if (sender is TextBox nameBox && nameBox.Tag is FaceOverviewCluster cluster)
                ApplyClusterName(cluster, nameBox.Text);

            e.SuppressKeyPress = true;
        }

        private void ApplyClusterName(FaceOverviewCluster cluster, string rawName)
        {
            string name = rawName?.Trim() ?? string.Empty;
            cluster.Name = name;
            if (!string.IsNullOrWhiteSpace(name))
                knownNames.Add(name);

            RefreshClusterCard(cluster.ClusterId);
        }

        private AutoCompleteStringCollection CreateAutoCompleteSource()
        {
            var source = new AutoCompleteStringCollection();
            foreach (string name in knownNames.OrderBy(n => n))
                source.Add(name);

            return source;
        }

        private void PopulateMoveToClusterMenuItems()
        {
            moveToClusterMenuItem.DropDownItems.Clear();

            var namedClusters = clusters
                .Where(c => c.ClusterId != contextMenuSourceClusterId)
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.ClusterId)
                .ToList();

            if (namedClusters.Count == 0)
            {
                moveToClusterMenuItem.Enabled = false;
                return;
            }

            moveToClusterMenuItem.Enabled = true;

            foreach (var cluster in namedClusters)
            {
                int targetClusterId = cluster.ClusterId;
                string label = $"{cluster.Name}  (#{cluster.ClusterId})";
                var item = new ToolStripMenuItem(label)
                {
                    Tag = targetClusterId
                };

                item.Click += MoveToClusterMenuItem_Click;
                moveToClusterMenuItem.DropDownItems.Add(item);
            }
        }

        private async void MoveToClusterMenuItem_Click(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem menuItem || menuItem.Tag is not int targetClusterId)
                return;

            int sourceClusterId = contextMenuSourceClusterId;
            int faceIndex = contextMenuFaceIndex;
            if (faceIndex < 0 || sourceClusterId == targetClusterId)
                return;

            int sourcePageBefore = sourceClusterId > 0 ? GetPageIndexForClusterId(sourceClusterId) : currentPageIndex;
            int targetPageBefore = GetPageIndexForClusterId(targetClusterId);

            if (sourceClusterId > 0)
                await ExecuteTrackedDragDropAction(() => MergeClustersAsync(sourceClusterId, targetClusterId));
            else
                await ExecuteTrackedDragDropAction(() => MoveFaceAsync(sourceClusterId, faceIndex, targetClusterId));

            int sourcePageAfter = sourceClusterId > 0 ? GetPageIndexForClusterId(sourceClusterId) : sourcePageBefore;
            int targetPageAfter = GetPageIndexForClusterId(targetClusterId);

            await RefreshAffectedPagesAsync(
                sourcePageAfter >= 0 ? sourcePageAfter : sourcePageBefore,
                targetPageAfter >= 0 ? targetPageAfter : targetPageBefore);
        }

        private async void MoveToUnknownMenuItem_Click(object? sender, EventArgs e)
        {
            int sourceClusterId = contextMenuSourceClusterId;
            int faceIndex = contextMenuFaceIndex;
            if (sourceClusterId <= 0 || faceIndex < 0)
                return;

            await ExecuteTrackedDragDropAction(() => MoveFaceToUnknownAsync(sourceClusterId, faceIndex));
        }

        private int GetPageIndexForClusterId(int clusterId)
        {
            var ordered = GetLayoutOrderedClusters();
            int index = ordered
                .Select((cluster, idx) => (cluster, idx))
                .Where(x => x.cluster.ClusterId == clusterId)
                .Select(x => x.idx)
                .DefaultIfEmpty(-1)
                .First();

            if (index < 0)
                return -1;

            return index / GetCardsPerPage();
        }

        private async Task RefreshAffectedPagesAsync(int sourcePageIndex, int targetPageIndex)
        {
            var pages = new[] { sourcePageIndex, targetPageIndex }
                .Where(p => p >= 0)
                .Distinct()
                .ToList();

            if (pages.Count == 0)
                return;

            if (pages.Contains(currentPageIndex))
            {
                await RenderClusterPageAsync(currentPageIndex, preserveScrollPosition: true);
                return;
            }

            foreach (int page in pages)
                await RenderClusterPageAsync(page);
        }

        private async void SplitFaceMenuItem_Click(object? sender, EventArgs e)
        {
            int sourceClusterId = contextMenuSourceClusterId;
            int faceIndex = contextMenuFaceIndex;
            if (faceIndex < 0)
                return;

            await ExecuteTrackedDragDropAction(() => SplitFaceIntoNewClusterAsync(sourceClusterId, faceIndex));
        }

        private void PreviewFaceMenuItem_Click(object? sender, EventArgs e)
        {
            int faceIndex = contextMenuFaceIndex;
            if (faceIndex < 0 || faceIndex >= alignedPaths.Count)
                return;

            Image? previewImage = null;
            try
            {
                string path = alignedPaths[faceIndex];
                if (!File.Exists(path))
                    return;

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var img = Image.FromStream(fs);
                previewImage = new Bitmap(img);
            }
            catch
            {
                return;
            }

            if (previewImage == null)
                return;

            using var preview = new Form
            {
                Text = "Face Preview",
                Width = 480,
                Height = 520,
                StartPosition = FormStartPosition.CenterParent
            };

            var previewBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = previewImage
            };

            preview.Controls.Add(previewBox);
            preview.ShowDialog(this);
            previewBox.Image?.Dispose();
        }

        private async Task SplitFaceIntoNewClusterAsync(int sourceClusterId, int faceIndex)
        {
            FaceOverviewCluster? source = null;
            int insertIndex = clusters.Count;

            if (sourceClusterId == 0)
            {
                if (unknownFaceIndices.RemoveAll(i => i == faceIndex) == 0)
                    return;
                insertIndex = clusters.Count;
            }
            else
            {
                source = clusters.FirstOrDefault(c => c.ClusterId == sourceClusterId);
                if (source == null || !RemoveFaceIndex(source.FaceIndices, faceIndex))
                    return;

                source.RebuildFromIndices(alignedPaths);
                insertIndex = Math.Max(0, clusters.IndexOf(source) + 1);
            }

            int nextId = clusters.Count == 0 ? 1 : clusters.Max(c => c.ClusterId) + 1;
            var newCluster = new FaceOverviewCluster(nextId, new[] { faceIndex }, alignedPaths);
            clusters.Insert(Math.Min(insertIndex, clusters.Count), newCluster);

            if (source != null)
            {
                if (source.FaceIndices.Count == 0)
                {
                    clusters.Remove(source);
                    RemoveClusterCard(source.ClusterId);
                    QueuePendingClusterDisposal(source);
                }
                else
                {
                    RefreshClusterCard(source.ClusterId);
                }
            }

            RefreshUnknownCard();

            int pageForNew = GetPageIndexForClusterId(newCluster.ClusterId);
            if (pageForNew == currentPageIndex)
            {
                AddClusterCard(newCluster);
                LayoutPageCardsFromIndex(0);
                UpdateGlobalCounters();
            }
            else
            {
                await RenderClusterPageAsync(pageForNew, preserveScrollPosition: false);
            }

            SelectCluster(newCluster.ClusterId);
            SchedulePendingClusterDisposalFlush();
        }

        private void ContinueWithAutoNames()
        {
            AssignAutoNamesToUnnamed();
            var outputClusters = clusters
                .Select(c => new FaceOverviewCluster(c.ClusterId, c.FaceIndices)
                {
                    Name = c.Name,
                    IsSelected = c.IsSelected,
                    DropZone = c.DropZone
                })
                .ToList();

            if (unknownFaceIndices.Count > 0)
            {
                int unknownId = outputClusters.Count == 0 ? 1 : outputClusters.Max(c => c.ClusterId) + 1;
                outputClusters.Add(new FaceOverviewCluster(unknownId, unknownFaceIndices)
                {
                    Name = "Unknown Faces"
                });
            }

            Result = new FaceOverviewResult(outputClusters, skippedOverview: false);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void SkipOverviewAndAutoAssignNames()
        {
            int counter = 1;
            foreach (var cluster in clusters.OrderBy(c => c.ClusterId))
                cluster.Name = $"Person_{counter++:00}";

            var outputClusters = clusters
                .Select(c => new FaceOverviewCluster(c.ClusterId, c.FaceIndices)
                {
                    Name = c.Name,
                    IsSelected = c.IsSelected,
                    DropZone = c.DropZone
                })
                .ToList();

            if (unknownFaceIndices.Count > 0)
            {
                int unknownId = outputClusters.Count == 0 ? 1 : outputClusters.Max(c => c.ClusterId) + 1;
                outputClusters.Add(new FaceOverviewCluster(unknownId, unknownFaceIndices)
                {
                    Name = "Unknown Faces"
                });
            }

            Result = new FaceOverviewResult(outputClusters, skippedOverview: true);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void AssignAutoNamesToUnnamed()
        {
            var used = new HashSet<string>(clusters
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => c.Name.Trim()), StringComparer.OrdinalIgnoreCase);

            int index = 1;
            foreach (var cluster in clusters.OrderBy(c => c.ClusterId))
            {
                if (!string.IsNullOrWhiteSpace(cluster.Name))
                    continue;

                while (used.Contains($"Person_{index:00}"))
                    index++;

                cluster.Name = $"Person_{index:00}";
                used.Add(cluster.Name);
                index++;
            }
        }

        private sealed class ClusterState
        {
            public int ClusterId { get; init; }
            public string Name { get; init; } = string.Empty;
            public bool IsSelected { get; init; }
            public List<int> FaceIndices { get; init; } = new();
        }

        private sealed class OverviewUiState
        {
            public List<ClusterState> Clusters { get; init; } = new();
            public List<int> UnknownFaceIndices { get; init; } = new();
            public int? SelectedClusterId { get; init; }
            public List<int> CardOrder { get; init; } = new();
        }

        private sealed class DragPreviewForm : Form
        {
            private readonly PictureBox preview = new();

            public DragPreviewForm(Image source)
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                TopMost = true;
                Opacity = 0.8;
                Width = 112;
                Height = 112;
                BackColor = Color.Magenta;
                TransparencyKey = Color.Magenta;

                preview.Left = 8;
                preview.Top = 8;
                preview.Width = 96;
                preview.Height = 96;
                preview.SizeMode = PictureBoxSizeMode.Zoom;
                preview.Image = source;
                preview.Region = UiPaint.CreateRoundRegion(new Rectangle(0, 0, 96, 96), 8);
                Controls.Add(preview);
            }

            public void UpdatePosition(Point cursor)
            {
                Location = new Point(cursor.X + 16, cursor.Y + 16);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var shadowPath = UiPaint.CreateRoundRect(new Rectangle(4, 4, 104, 104), 10);
                using var shadowBrush = new SolidBrush(Color.FromArgb(64, 0, 0, 0));
                e.Graphics.FillPath(shadowBrush, shadowPath);
            }
        }

        private sealed class FaceDragPayload
        {
            public FaceDragPayload(int sourceClusterId, int faceIndex)
            {
                SourceClusterId = sourceClusterId;
                FaceIndex = faceIndex;
            }

            public int SourceClusterId { get; }
            public int FaceIndex { get; }
            public string FaceId { get; set; } = string.Empty;
            public string ImagePath { get; set; } = string.Empty;
            public Rectangle CropRectangle { get; set; }
        }

        private sealed class ClusterDragPayload
        {
            public ClusterDragPayload(int sourceClusterId)
            {
                SourceClusterId = sourceClusterId;
            }

            public int SourceClusterId { get; }
        }
    }

    internal static class UiPaint
    {
        public static Color ResolveOpaqueBackground(Control control)
        {
            Control? current = control.Parent;
            while (current != null)
            {
                Color color = current.BackColor;
                if (color.A == 255 && color != Color.Transparent)
                    return color;

                current = current.Parent;
            }

            return SystemColors.Control;
        }

        public static GraphicsPath CreateRoundRect(Rectangle rect, int radius)
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

        public static Region CreateRoundRegion(Rectangle rect, int radius)
        {
            using var path = CreateRoundRect(rect, radius);
            return new Region(path);
        }

        public static void DrawCard(Graphics g, Rectangle bounds, bool hovered, bool dragHover, Color fillColor)
        {
            var content = new Rectangle(3, 3, Math.Max(1, bounds.Width - 7), Math.Max(1, bounds.Height - 7));
            var borderColor = dragHover
                ? Color.FromArgb(102, 169, 255)
                : Color.FromArgb(120, 160, 220);
            float borderWidth = dragHover ? 3.2f : (hovered ? 2.6f : 2.2f);

            using (var shadowPathWide = CreateRoundRect(new Rectangle(content.X, content.Y + 7, content.Width, content.Height), 14))
            using (var shadowWideBrush = new SolidBrush(Color.FromArgb(42, 0, 0, 0)))
            {
                g.FillPath(shadowWideBrush, shadowPathWide);
            }

            using (var shadowPathSoft = CreateRoundRect(new Rectangle(content.X, content.Y + 4, content.Width, content.Height), 9))
            using (var shadowSoftBrush = new SolidBrush(Color.FromArgb(26, 0, 0, 0)))
            {
                g.FillPath(shadowSoftBrush, shadowPathSoft);
            }

            using (var cardPath = CreateRoundRect(content, 12))
            using (var cardBrush = new SolidBrush(fillColor))
            {
                g.FillPath(cardBrush, cardPath);
                using var borderPen = new Pen(borderColor, borderWidth);
                g.DrawPath(borderPen, cardPath);
            }
        }
    }
}
