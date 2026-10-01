using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickLook.Common.Plugin;
using SlbViewer;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Drawing.Color;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace QuickLook.Plugin.SlbViewer
{
    public class SlbViewerControl : Grid, IDisposable
    {
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private enum ViewMode
        {
            Grid,
            Detail
        }

        private const int PageSize = 24; // 每次加载 24 个（约 4 行），快速首屏秒开

        private readonly string _filePath;
        private readonly ContextObject _context;
        private readonly bool _isSlb;
        private readonly SlbReader _reader = new SlbReader();

        private List<string> _allSlideNames = new List<string>();
        private List<string> _filteredSlideNames = new List<string>();
        private int _loadedCount = 0;
        private int _currentIndex = 0;
        private bool _isDarkBackground = true;
        private ViewMode _currentMode;

        // 异步缩略图队列与缓存
        private CancellationTokenSource? _thumbnailCts;
        private readonly object _queueLock = new object();
        private readonly Queue<string> _thumbnailQueue = new Queue<string>();
        private bool _isThumbnailWorkerRunning = false;
        private readonly Dictionary<string, ImageSource> _thumbnailCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        // Views
        private Grid _gridView = null!;
        private Grid _detailView = null!;

        // Grid View Controls
        private TextBlock _txtGridTitle = null!;
        private TextBlock _txtGridCount = null!;
        private Border _searchBorder = null!;
        private System.Windows.Shapes.Path _searchIcon = null!;
        private TextBlock _txtPlaceholder = null!;
        private TextBox _txtSearch = null!;
        private Button _btnClearSearch = null!;
        private ScrollViewer _scrollViewer = null!;
        private UniformGrid _uniformGrid = null!;
        private Button _btnGridTheme = null!;
        // Detail View Controls
        private Grid _detailImageContainer = null!;
        private Image _detailImage = null!;
        private readonly TransformGroup _imageTransformGroup = new TransformGroup();
        private readonly ScaleTransform _imageScaleTransform = new ScaleTransform(1.0, 1.0);
        private readonly TranslateTransform _imageTranslateTransform = new TranslateTransform(0, 0);

        private bool _isPanning = false;
        private Point _lastPanPoint;
        private Point _panStartPoint;
        private bool _hasPanned = false;

        private TextBlock _txtDetailError = null!;
        private Border _detailToolbar = null!;
        private Button _btnBackToGrid = null!;
        private Button _btnPrev = null!;
        private Button _btnNext = null!;
        private TextBlock _txtDetailCounter = null!;
        private ComboBox _cmbSlides = null!;
        private Border _sepZoom = null!;
        private Border _sepTheme = null!;
        private Button _btnZoomOut = null!;
        private Button _btnZoomReset = null!;
        private Button _btnZoomIn = null!;
        private Button _btnDetailTheme = null!;
        private Bitmap? _currentDetailBitmap;
        // Theme Brushes
        private static readonly Brush DarkWindowBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 26));
        private static readonly Brush LightWindowBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 247));
        private static readonly Brush DarkCardBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 36, 38));
        private static readonly Brush LightCardBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
        private static readonly Brush DarkCardHoverBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 48, 52));
        private static readonly Brush LightCardHoverBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 243, 248));
        private static readonly Brush DarkCardBorder = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 255, 255));
        private static readonly Brush LightCardBorder = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 0, 0, 0));
        private static readonly Brush DarkToolbarBg = new SolidColorBrush(System.Windows.Media.Color.FromArgb(215, 32, 32, 34));
        private static readonly Brush LightToolbarBg = new SolidColorBrush(System.Windows.Media.Color.FromArgb(225, 255, 255, 255));
        private static readonly Brush TextPrimaryDark = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
        private static readonly Brush TextPrimaryLight = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));
        private static readonly Brush TextSecondaryDark = new SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 160, 160));
        private static readonly Brush TextSecondaryLight = new SolidColorBrush(System.Windows.Media.Color.FromRgb(110, 110, 110));
        private static readonly Brush AccentBlueBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));
        private static readonly Brush DarkSearchBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 35));
        private static readonly Brush LightSearchBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 243));
        private static readonly Brush DarkSearchFocusBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 45));
        private static readonly Brush LightSearchFocusBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
        public SlbViewerControl(string filePath, ContextObject context)
        {
            _filePath = filePath;
            _context = context;
            _isSlb = string.Equals(Path.GetExtension(filePath), ".slb", StringComparison.OrdinalIgnoreCase);

            Focusable = true;
            ClipToBounds = true;

            InitializeViews();

            // Default Mode: SLB opens in Grid; SLD opens in Detail
            _currentMode = _isSlb ? ViewMode.Grid : ViewMode.Detail;
            UpdateViewVisibility();
            ApplyTheme();

            Loaded += (s, e) => Focus();
            MouseDown += (s, e) => Focus();
            PreviewMouseDown += OnPreviewMouseDown;
            PreviewKeyDown += OnPreviewKeyDown;
            PreviewMouseWheel += OnPreviewMouseWheel;

            CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseBack, (s, e) =>
            {
                if (_currentMode == ViewMode.Detail && _isSlb)
                {
                    SwitchToGridView();
                    e.Handled = true;
                }
            }));
            CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseForward, (s, e) =>
            {
                if (_currentMode == ViewMode.Grid && _isSlb)
                {
                    SwitchToDetailView(_currentIndex);
                    e.Handled = true;
                }
                else if (_currentMode == ViewMode.Detail && _isSlb)
                {
                    NextSlide();
                    e.Handled = true;
                }
            }));
        }

        private void InitializeViews()
        {
            // ================= Grid View (Gallery) =================
            _gridView = new Grid();
            _gridView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Top bar
            _gridView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Scroll area

            // Top Bar
            var topBarBorder = new Border
            {
                Padding = new Thickness(16, 10, 16, 10),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = DarkCardBorder
            };

            var topBar = new Grid();
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // File Info
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var iconText = new TextBlock
            {
                Text = "📚",
                FontSize = 16,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _txtGridTitle = new TextBlock
            {
                Text = Path.GetFileName(_filePath),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            _txtGridCount = new TextBlock
            {
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            titlePanel.Children.Add(iconText);
            titlePanel.Children.Add(_txtGridTitle);
            titlePanel.Children.Add(_txtGridCount);
            Grid.SetColumn(titlePanel, 0);
            topBar.Children.Add(titlePanel);
            // Search Box (Fluent Pill Design)
            _searchBorder = new Border
            {
                Width = 220,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(1),
                BorderBrush = DarkCardBorder,
                Background = DarkSearchBg,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 12, 0),
                Padding = new Thickness(10, 0, 8, 0),
                Cursor = Cursors.IBeam
            };
            _searchBorder.MouseDown += (s, e) =>
            {
                _txtSearch.Focus();
                e.Handled = true;
            };

            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _searchIcon = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M6,1 A5,5 0 1,0 6,11 A5,5 0 1,0 6,1 M9.5,9.5 L13.5,13.5"),
                Stroke = TextSecondaryDark,
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 14,
                Height = 14
            };
            Grid.SetColumn(_searchIcon, 0);
            searchGrid.Children.Add(_searchIcon);

            var inputGrid = new Grid();

            _txtPlaceholder = new TextBlock
            {
                Text = "搜索幻灯片...",
                FontSize = 12,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI"),
                Foreground = TextSecondaryDark,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
                IsHitTestVisible = false
            };
            inputGrid.Children.Add(_txtPlaceholder);

            _txtSearch = new TextBox
            {
                FontSize = 12,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI"),
                Foreground = TextPrimaryDark,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FocusVisualStyle = null
            };

            var template = new ControlTemplate(typeof(TextBox));
            var factory = new FrameworkElementFactory(typeof(ScrollViewer));
            factory.Name = "PART_ContentHost";
            factory.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            factory.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            template.VisualTree = factory;
            _txtSearch.Template = template;

            Action updateSearchState = () =>
            {
                bool hasText = !string.IsNullOrEmpty(_txtSearch.Text);
                bool isFocused = _txtSearch.IsKeyboardFocused;

                // 获得焦点或已有文字时，彻底隐藏占位文字，避免与中文输入法或英文按键重叠
                _txtPlaceholder.Visibility = (!hasText && !isFocused) ? Visibility.Visible : Visibility.Collapsed;
                _btnClearSearch.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;

                if (isFocused)
                {
                    _searchBorder.BorderBrush = AccentBlueBrush;
                    _searchBorder.Background = _isDarkBackground ? DarkSearchFocusBg : LightSearchFocusBg;
                }
                else
                {
                    _searchBorder.BorderBrush = _isDarkBackground ? DarkCardBorder : LightCardBorder;
                    _searchBorder.Background = _isDarkBackground ? DarkSearchBg : LightSearchBg;
                }
            };

            _txtSearch.TextChanged += (s, e) =>
            {
                updateSearchState();
                FilterThumbnails();
            };

            _txtSearch.GotKeyboardFocus += (s, e) => updateSearchState();
            _txtSearch.LostKeyboardFocus += (s, e) => updateSearchState();
            _txtSearch.PreviewKeyDown += (s, e) =>
            {
                _txtPlaceholder.Visibility = Visibility.Collapsed;
            };

            inputGrid.Children.Add(_txtSearch);
            Grid.SetColumn(inputGrid, 1);
            searchGrid.Children.Add(inputGrid);

            _btnClearSearch = new Button
            {
                Content = "✕",
                ToolTip = "清空搜索",
                Background = Brushes.Transparent,
                Foreground = TextSecondaryDark,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 0, 2, 0),
                Cursor = Cursors.Hand,
                FontSize = 10,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _btnClearSearch.Click += (s, e) =>
            {
                _txtSearch.Text = "";
                _txtSearch.Focus();
            };
            Grid.SetColumn(_btnClearSearch, 2);
            searchGrid.Children.Add(_btnClearSearch);

            _searchBorder.Child = searchGrid;
            Grid.SetColumn(_searchBorder, 1);
            topBar.Children.Add(_searchBorder);

            // Theme Button in Grid top bar
            _btnGridTheme = CreateToolbarButton("🌓", "切换底色 (B)");
            _btnGridTheme.Click += (s, e) => ToggleTheme();
            Grid.SetColumn(_btnGridTheme, 2);
            topBar.Children.Add(_btnGridTheme);

            topBarBorder.Child = topBar;
            Grid.SetRow(topBarBorder, 0);
            _gridView.Children.Add(topBarBorder);

            // Scroll Area with UniformGrid (Equally divided width)
            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(14, 12, 14, 12)
            };

            _uniformGrid = new UniformGrid
            {
                Columns = 5,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _scrollViewer.Content = _uniformGrid;

            // Responsive Layout & Auto-load on scroll
            _scrollViewer.ScrollChanged += (s, e) =>
            {
                if (e.ViewportWidthChange != 0)
                {
                    UpdateGridLayout(e.ViewportWidth);
                }

                // 滚动接近底部时自动加载下一页
                if (_currentMode == ViewMode.Grid && _loadedCount < _filteredSlideNames.Count)
                {
                    if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 200)
                    {
                        LoadNextPage();
                    }
                }
            };
            _scrollViewer.SizeChanged += (s, e) =>
            {
                double w = _scrollViewer.ViewportWidth > 0 ? _scrollViewer.ViewportWidth : e.NewSize.Width;
                UpdateGridLayout(w);
            };

            Grid.SetRow(_scrollViewer, 1);
            _gridView.Children.Add(_scrollViewer);

            Children.Add(_gridView);

            // ================= Detail View (Full View) =================
            _detailView = new Grid();

            _imageTransformGroup.Children.Add(_imageScaleTransform);
            _imageTransformGroup.Children.Add(_imageTranslateTransform);

            _detailImage = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 12, 12, 60),
                RenderTransform = _imageTransformGroup
            };
            RenderOptions.SetBitmapScalingMode(_detailImage, BitmapScalingMode.HighQuality);

            _detailImageContainer = new Grid
            {
                ClipToBounds = true,
                Background = Brushes.Transparent
            };
            _detailImageContainer.Children.Add(_detailImage);

            _detailImageContainer.MouseDown += OnDetailMouseDown;
            _detailImageContainer.MouseMove += OnDetailMouseMove;
            _detailImageContainer.MouseUp += OnDetailMouseUp;
            _detailView.Children.Add(_detailImageContainer);

            _txtDetailError = new TextBlock
            {
                FontSize = 14,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _detailView.Children.Add(_txtDetailError);

            // Back Button (Top Left in Detail View)
            _btnBackToGrid = new Button
            {
                Content = "◀ 全部幻灯片",
                ToolTip = "返回缩略图网格 (鼠标侧键后退 / 双击 / Backspace / Alt+←)",
                Background = DarkToolbarBg,
                Foreground = TextPrimaryDark,
                BorderBrush = DarkCardBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),
                FontSize = 12,
                Focusable = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(16, 14, 0, 0),
                Visibility = _isSlb ? Visibility.Visible : Visibility.Collapsed
            };
            _btnBackToGrid.Click += (s, e) => SwitchToGridView();
            _detailView.Children.Add(_btnBackToGrid);

            // Detail Bottom Toolbar
            _detailToolbar = new Border
            {
                Background = DarkToolbarBg,
                BorderBrush = DarkCardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(12, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 14)
            };

            var detailPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _btnPrev = CreateToolbarButton("◀", "上一张 (Left / PageUp)");
            _btnPrev.Click += (s, e) => PrevSlide();
            detailPanel.Children.Add(_btnPrev);

            _txtDetailCounter = new TextBlock
            {
                Foreground = TextPrimaryDark,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };
            detailPanel.Children.Add(_txtDetailCounter);

            _cmbSlides = new ComboBox
            {
                MaxDropDownHeight = 320,
                Width = 140,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Focusable = false
            };
            _cmbSlides.SelectionChanged += (s, e) =>
            {
                if (_cmbSlides.SelectedIndex >= 0 && _cmbSlides.SelectedIndex != _currentIndex)
                {
                    _currentIndex = _cmbSlides.SelectedIndex;
                    RenderCurrentDetailSlide();
                }
            };
            detailPanel.Children.Add(_cmbSlides);

            _btnNext = CreateToolbarButton("▶", "下一张 (D / Right / Space)");
            _btnNext.Click += (s, e) => NextSlide();
            _btnNext.Margin = new Thickness(0, 0, 8, 0);
            detailPanel.Children.Add(_btnNext);

            _sepZoom = new Border
            {
                Width = 1,
                Height = 14,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 255, 255)),
                Margin = new Thickness(4, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            detailPanel.Children.Add(_sepZoom);

            _btnZoomOut = CreateToolbarButton("➖", "缩小 (- / 滚轮下滚)");
            _btnZoomOut.Click += (s, e) => ZoomAt(1.0 / 1.25, new Point(_detailImageContainer.ActualWidth / 2, _detailImageContainer.ActualHeight / 2));
            detailPanel.Children.Add(_btnZoomOut);

            _btnZoomReset = new Button
            {
                Content = "100%",
                ToolTip = "自适应 / 还原 100% (点击还原，双击图像也可还原)",
                Background = Brushes.Transparent,
                Foreground = TextPrimaryDark,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 2, 6, 2),
                Cursor = Cursors.Hand,
                FontSize = 11,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Consolas"),
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            _btnZoomReset.Click += (s, e) => ResetZoom();
            detailPanel.Children.Add(_btnZoomReset);

            _btnZoomIn = CreateToolbarButton("➕", "放大 (+ / 滚轮上滚)");
            _btnZoomIn.Click += (s, e) => ZoomAt(1.25, new Point(_detailImageContainer.ActualWidth / 2, _detailImageContainer.ActualHeight / 2));
            detailPanel.Children.Add(_btnZoomIn);

            _sepTheme = new Border
            {
                Width = 1,
                Height = 14,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 255, 255)),
                Margin = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            detailPanel.Children.Add(_sepTheme);

            _btnDetailTheme = CreateToolbarButton("🌓", "切换底色 (B)");
            _btnDetailTheme.Click += (s, e) => ToggleTheme();
            detailPanel.Children.Add(_btnDetailTheme);
            _detailToolbar.Child = detailPanel;
            _detailView.Children.Add(_detailToolbar);

            if (!_isSlb)
            {
                _btnPrev.Visibility = Visibility.Collapsed;
                _btnNext.Visibility = Visibility.Collapsed;
                _txtDetailCounter.Visibility = Visibility.Collapsed;
                _cmbSlides.Visibility = Visibility.Collapsed;
            }

            Children.Add(_detailView);
        }

        private Button CreateToolbarButton(string text, string tooltip)
        {
            return new Button
            {
                Content = text,
                ToolTip = tooltip,
                Background = Brushes.Transparent,
                Foreground = TextPrimaryDark,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 2, 6, 2),
                Cursor = Cursors.Hand,
                FontSize = 13,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private void UpdateGridLayout(double viewportWidth)
        {
            if (viewportWidth <= 100) return;

            double contentWidth = viewportWidth - 28;
            if (contentWidth <= 100) return;

            int cols = (int)Math.Max(2, Math.Round(contentWidth / 155.0));
            _uniformGrid.Columns = cols;

            double cellWidth = contentWidth / cols;
            double cardHeight = Math.Max(100, Math.Round(cellWidth * 0.78));

            foreach (UIElement child in _uniformGrid.Children)
            {
                if (child is Border card)
                {
                    card.Height = cardHeight;
                }
            }
        }

        private void ApplyTheme()
        {
            Background = _isDarkBackground ? DarkWindowBg : LightWindowBg;

            Brush primaryText = _isDarkBackground ? TextPrimaryDark : TextPrimaryLight;
            Brush secondaryText = _isDarkBackground ? TextSecondaryDark : TextSecondaryLight;
            Brush toolbarBg = _isDarkBackground ? DarkToolbarBg : LightToolbarBg;
            Brush cardBorder = _isDarkBackground ? DarkCardBorder : LightCardBorder;

            _txtGridTitle.Foreground = primaryText;
            _txtGridCount.Foreground = secondaryText;
            _btnGridTheme.Foreground = primaryText;
            _btnGridTheme.Content = _isDarkBackground ? "🌓" : "☀️";
            _btnDetailTheme.Content = _isDarkBackground ? "🌓" : "☀️";
            _searchBorder.Background = _isDarkBackground ? DarkSearchBg : LightSearchBg;
            _searchBorder.BorderBrush = cardBorder;
            _searchIcon.Stroke = secondaryText;
            _txtPlaceholder.Foreground = secondaryText;
            _txtSearch.Foreground = primaryText;
            _txtSearch.CaretBrush = primaryText;
            _btnClearSearch.Foreground = secondaryText;

            _detailToolbar.Background = toolbarBg;
            _detailToolbar.BorderBrush = cardBorder;
            _btnBackToGrid.Background = toolbarBg;
            _btnBackToGrid.Foreground = primaryText;
            _btnBackToGrid.BorderBrush = cardBorder;
            _btnPrev.Foreground = primaryText;
            _btnNext.Foreground = primaryText;
            _btnDetailTheme.Foreground = primaryText;

            if (_btnZoomOut != null) _btnZoomOut.Foreground = primaryText;
            if (_btnZoomReset != null) _btnZoomReset.Foreground = primaryText;
            if (_btnZoomIn != null) _btnZoomIn.Foreground = primaryText;
            if (_sepZoom != null) _sepZoom.Background = cardBorder;
            if (_sepTheme != null) _sepTheme.Background = cardBorder;

            // Update existing cards in uniformGrid
            foreach (UIElement child in _uniformGrid.Children)
            {
                if (child is Border card)
                {
                    card.Background = _isDarkBackground ? DarkCardBg : LightCardBg;
                    card.BorderBrush = cardBorder;
                    if (card.Child is Grid cardGrid && cardGrid.Children.Count >= 2 && cardGrid.Children[1] is TextBlock tb)
                    {
                        tb.Foreground = primaryText;
                    }
                }
            }
        }

        private void UpdateViewVisibility()
        {
            if (_currentMode == ViewMode.Grid)
            {
                _gridView.Visibility = Visibility.Visible;
                _detailView.Visibility = Visibility.Collapsed;
                _context.Title = $"{Path.GetFileName(_filePath)} (共 {_allSlideNames.Count} 张幻灯片)";
            }
            else
            {
                _gridView.Visibility = Visibility.Collapsed;
                _detailView.Visibility = Visibility.Visible;
            }
        }

        public void LoadContent()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    ShowError($"文件不存在: {_filePath}");
                    return;
                }

                if (_isSlb)
                {
                    _allSlideNames = _reader.ReadSlideNames(_filePath);
                    _context.Title = $"{Path.GetFileName(_filePath)} (共 {_allSlideNames.Count} 张幻灯片)";

                    _cmbSlides.Items.Clear();
                    foreach (var name in _allSlideNames)
                    {
                        _cmbSlides.Items.Add(name);
                    }

                    _filteredSlideNames = new List<string>(_allSlideNames);

                    // 初始仅载入首批幻灯片（首屏秒开）
                    ResetAndLoadFirstPage();
                }
                else
                {
                    // Single SLD directly loads detail
                    _currentMode = ViewMode.Detail;
                    UpdateViewVisibility();
                    RenderCurrentDetailSlide();
                }
            }
            catch (Exception ex)
            {
                ShowError($"加载幻灯片失败: {ex.Message}");
            }
            finally
            {
                _context.IsBusy = false;
            }
        }

        private void ResetAndLoadFirstPage()
        {
            // 取消正在进行的缩略图后台任务
            _thumbnailCts?.Cancel();
            _thumbnailCts = new CancellationTokenSource();
            lock (_queueLock)
            {
                _thumbnailQueue.Clear();
                _isThumbnailWorkerRunning = false;
            }

            _uniformGrid.Children.Clear();
            _loadedCount = 0;

            LoadNextPage();

            // 若窗口较大首批未铺满滚动区，按需补充加载
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_currentMode == ViewMode.Grid && _loadedCount < _filteredSlideNames.Count)
                {
                    if (_scrollViewer.ExtentHeight <= _scrollViewer.ViewportHeight + 20)
                    {
                        LoadNextPage();
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private bool LoadNextPage()
        {
            if (_loadedCount >= _filteredSlideNames.Count)
            {
                UpdateGridCount();
                return false;
            }

            int countToLoad = Math.Min(PageSize, _filteredSlideNames.Count - _loadedCount);
            var newBatch = new List<string>(countToLoad);

            double currentWidth = _scrollViewer.ViewportWidth > 0 ? _scrollViewer.ViewportWidth : _scrollViewer.ActualWidth;
            double contentWidth = currentWidth > 100 ? currentWidth - 28 : 800;
            int cols = (int)Math.Max(2, Math.Round(contentWidth / 155.0));
            double cellWidth = contentWidth / cols;
            double cardHeight = Math.Max(100, Math.Round(cellWidth * 0.78));

            for (int i = 0; i < countToLoad; i++)
            {
                string slideName = _filteredSlideNames[_loadedCount + i];
                newBatch.Add(slideName);
                int originalIndex = _allSlideNames.IndexOf(slideName);

                var card = CreateSlideCard(slideName, originalIndex);
                card.Height = cardHeight;
                _uniformGrid.Children.Add(card);
            }

            _loadedCount += countToLoad;

            // 重新适配网格列
            UpdateGridLayout(currentWidth);

            // 更新状态
            UpdateGridCount();
            // 启动该批次的后台缩略图渲染
            EnqueueBatchThumbnailLoading(newBatch);

            return true;
        }

        private void UpdateGridCount()
        {
            if (_filteredSlideNames.Count == 0)
            {
                _txtGridCount.Text = "(共 0 张)";
            }
            else if (_loadedCount < _filteredSlideNames.Count)
            {
                _txtGridCount.Text = $"(已加载 {_loadedCount} / 共 {_filteredSlideNames.Count} 张)";
            }
            else
            {
                _txtGridCount.Text = $"(共 {_filteredSlideNames.Count} 张)";
            }
        }

        private Border CreateSlideCard(string slideName, int slideIndex)
        {
            var card = new Border
            {
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(7),
                Background = _isDarkBackground ? DarkCardBg : LightCardBg,
                BorderBrush = _isDarkBackground ? DarkCardBorder : LightCardBorder,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                Tag = slideIndex
            };

            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Image fills available space
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Text caption at bottom

            // Thumbnail Image Area
            var img = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 5, 5, 2)
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

            // If thumbnail already cached, set it immediately
            if (_thumbnailCache.TryGetValue(slideName, out var cachedSource))
            {
                img.Source = cachedSource;
            }

            Grid.SetRow(img, 0);
            cardGrid.Children.Add(img);

            // Title caption
            var tb = new TextBlock
            {
                Text = slideName,
                FontSize = 11,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Microsoft YaHei UI"),
                Foreground = _isDarkBackground ? TextPrimaryDark : TextPrimaryLight,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(6, 2, 6, 6),
                ToolTip = slideName
            };
            Grid.SetRow(tb, 1);
            cardGrid.Children.Add(tb);

            card.Child = cardGrid;

            // Hover effects
            card.MouseEnter += (s, e) =>
            {
                card.BorderBrush = AccentBlueBrush;
                card.BorderThickness = new Thickness(1.5);
                card.Background = _isDarkBackground ? DarkCardHoverBg : LightCardHoverBg;
            };
            card.MouseLeave += (s, e) =>
            {
                card.BorderBrush = _isDarkBackground ? DarkCardBorder : LightCardBorder;
                card.BorderThickness = new Thickness(1);
                card.Background = _isDarkBackground ? DarkCardBg : LightCardBg;
            };

            // Click to open detail view!
            card.MouseLeftButtonUp += (s, e) =>
            {
                SwitchToDetailView(slideIndex);
            };

            return card;
        }

        private void FilterThumbnails()
        {
            string keyword = _txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                _filteredSlideNames = new List<string>(_allSlideNames);
            }
            else
            {
                _filteredSlideNames = _allSlideNames
                    .Where(n => n.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            ResetAndLoadFirstPage();
        }

        private void EnqueueBatchThumbnailLoading(List<string> slides)
        {
            lock (_queueLock)
            {
                foreach (var slide in slides)
                {
                    if (!_thumbnailCache.ContainsKey(slide) && !_thumbnailQueue.Contains(slide))
                    {
                        _thumbnailQueue.Enqueue(slide);
                    }
                }

                if (!_isThumbnailWorkerRunning && _thumbnailQueue.Count > 0)
                {
                    _isThumbnailWorkerRunning = true;
                    StartThumbnailWorker();
                }
            }
        }

        private void StartThumbnailWorker()
        {
            var cts = _thumbnailCts;
            if (cts == null) return;
            var token = cts.Token;

            string filePath = _filePath;
            DpiScale dpi = GetCurrentDpi();
            float dpiFactor = (float)dpi.DpiScaleX;
            int thumbW = (int)Math.Round(180 * dpiFactor);
            int thumbH = (int)Math.Round(120 * dpiFactor);

            Task.Run(() =>
            {
                var reader = new SlbReader();

                while (true)
                {
                    string? slideName = null;
                    bool isDark = false;

                    lock (_queueLock)
                    {
                        if (token.IsCancellationRequested || _thumbnailQueue.Count == 0)
                        {
                            _isThumbnailWorkerRunning = false;
                            break;
                        }
                        slideName = _thumbnailQueue.Dequeue();
                        isDark = _isDarkBackground;
                    }

                    if (slideName == null) break;

                    if (_thumbnailCache.ContainsKey(slideName)) continue;

                    try
                    {
                        Color bg = isDark ? Color.FromArgb(24, 24, 24) : Color.White;
                        Color fg = isDark ? Color.White : Color.Black;

                        byte[]? data = reader.ReadSlideData(filePath, slideName);
                        if (data != null && data.Length > 0)
                        {
                            var parser = new SlideParser();
                            if (parser.Parse(data))
                            {
                                var renderer = new SlideRenderer(parser, bg, fg)
                                {
                                    DpiScale = dpiFactor
                                };
                                using (var bmp = renderer.Render(thumbW, thumbH))
                                using (var ms = new MemoryStream())
                                {
                                    bmp.Save(ms, ImageFormat.Png);
                                    ms.Position = 0;
                                    var bi = new BitmapImage();
                                    bi.BeginInit();
                                    bi.StreamSource = ms;
                                    bi.CacheOption = BitmapCacheOption.OnLoad;
                                    bi.EndInit();
                                    bi.Freeze();

                                    _thumbnailCache[slideName] = bi;

                                    Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        UpdateCardImage(slideName, bi);
                                    }));
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore individual thumbnail failure
                    }
                }
            }, token);
        }

        private void UpdateCardImage(string slideName, ImageSource imageSource)
        {
            foreach (UIElement child in _uniformGrid.Children)
            {
                if (child is Border card && card.Child is Grid cardGrid && cardGrid.Children.Count >= 2)
                {
                    if (cardGrid.Children[1] is TextBlock tb && string.Equals(tb.Text, slideName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (cardGrid.Children[0] is Image img)
                        {
                            img.Source = imageSource;
                        }
                        break;
                    }
                }
            }
        }

        private void SwitchToDetailView(int slideIndex)
        {
            if (_txtSearch.IsFocused)
            {
                Keyboard.ClearFocus();
            }

            _currentIndex = Math.Max(0, Math.Min(slideIndex, _allSlideNames.Count - 1));
            _currentMode = ViewMode.Detail;
            UpdateViewVisibility();
            RenderCurrentDetailSlide();

            Focus();
            Keyboard.Focus(this);
        }

        private void SwitchToGridView()
        {
            if (!_isSlb) return;

            // 确保如果当前大图索引超出网格已加载范围，补充加载使得该卡片存在
            while (_loadedCount <= _currentIndex && _loadedCount < _filteredSlideNames.Count)
            {
                LoadNextPage();
            }

            _currentMode = ViewMode.Grid;
            UpdateViewVisibility();
            _context.Title = $"{Path.GetFileName(_filePath)} (共 {_allSlideNames.Count} 张幻灯片)";

            // 滚动到当前选中的卡片
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string currentName = _allSlideNames[_currentIndex];
                foreach (UIElement child in _uniformGrid.Children)
                {
                    if (child is Border card && card.Child is Grid cardGrid && cardGrid.Children.Count >= 2)
                    {
                        if (cardGrid.Children[1] is TextBlock tb && string.Equals(tb.Text, currentName, StringComparison.OrdinalIgnoreCase))
                        {
                            card.BringIntoView();
                            break;
                        }
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Background);

            Focus();
        }

        private void RenderCurrentDetailSlide()
        {
            try
            {
                byte[]? slideData = null;
                string slideTitle = Path.GetFileNameWithoutExtension(_filePath);

                if (_isSlb)
                {
                    if (_currentIndex < 0 || _currentIndex >= _allSlideNames.Count)
                        return;

                    slideTitle = _allSlideNames[_currentIndex];
                    slideData = _reader.ReadSlideData(_filePath, slideTitle);
                    if (slideData == null || slideData.Length == 0)
                    {
                        ShowDetailError($"未能读取幻灯片数据: {slideTitle}");
                        return;
                    }

                    _txtDetailCounter.Text = $"{_currentIndex + 1} / {_allSlideNames.Count}";
                    _cmbSlides.SelectedIndex = _currentIndex;
                    _btnPrev.IsEnabled = _currentIndex > 0;
                    _btnNext.IsEnabled = _currentIndex < _allSlideNames.Count - 1;
                    _context.Title = $"[{_currentIndex + 1}/{_allSlideNames.Count}] {slideTitle} - {Path.GetFileName(_filePath)}";
                }
                else
                {
                    slideData = File.ReadAllBytes(_filePath);
                    _context.Title = $"{Path.GetFileName(_filePath)}";
                }

                var parser = new SlideParser();
                bool parseOk = parser.Parse(slideData);
                if (!parseOk)
                {
                    ShowDetailError($"幻灯片解析失败: {slideTitle}");
                    return;
                }

                DpiScale dpi = GetCurrentDpi();
                float dpiFactor = (float)dpi.DpiScaleX;

                double viewW = _detailImageContainer.ActualWidth > 100 ? _detailImageContainer.ActualWidth : 960;
                double viewH = _detailImageContainer.ActualHeight > 100 ? _detailImageContainer.ActualHeight : 640;

                int renderW = (int)Math.Max(2400, Math.Round(viewW * dpiFactor * 2.0));
                int renderH = (int)Math.Max(1800, Math.Round(viewH * dpiFactor * 2.0));

                Color bg = _isDarkBackground ? Color.FromArgb(24, 24, 24) : Color.White;
                Color fg = _isDarkBackground ? Color.White : Color.Black;

                var renderer = new SlideRenderer(parser, bg, fg)
                {
                    DpiScale = dpiFactor
                };
                Bitmap newBitmap = renderer.Render(renderW, renderH);

                _currentDetailBitmap?.Dispose();
                _currentDetailBitmap = newBitmap;

                _detailImage.Source = BitmapToImageSource(newBitmap);
                _detailImage.Visibility = Visibility.Visible;
                _txtDetailError.Visibility = Visibility.Collapsed;
                ResetZoom();
                string dimInfo = parser.Header.Width > 0 && parser.Header.Height > 0
                    ? $"{parser.Header.Width} × {parser.Header.Height}"
                    : "矢量图形";

                if (!_isSlb)
                {
                    _context.Title = $"{Path.GetFileName(_filePath)} ({dimInfo})";
                }
            }
            catch (Exception ex)
            {
                ShowDetailError($"渲染出错: {ex.Message}");
            }
        }

        private void PrevSlide()
        {
            if (_isSlb && _currentIndex > 0)
            {
                _currentIndex--;
                RenderCurrentDetailSlide();
            }
        }

        private void NextSlide()
        {
            if (_isSlb && _currentIndex < _allSlideNames.Count - 1)
            {
                _currentIndex++;
                RenderCurrentDetailSlide();
            }
        }

        private void ToggleTheme()
        {
            _isDarkBackground = !_isDarkBackground;
            ApplyTheme();

            // 清理旧底色缓存，重新触发当前已加载卡片的渲染
            _thumbnailCache.Clear();
            if (_isSlb)
            {
                _thumbnailCts?.Cancel();
                _thumbnailCts = new CancellationTokenSource();
                lock (_queueLock)
                {
                    _thumbnailQueue.Clear();
                    _isThumbnailWorkerRunning = false;
                }

                var visibleBatch = _filteredSlideNames.Take(_loadedCount).ToList();
                EnqueueBatchThumbnailLoading(visibleBatch);
            }

            if (_currentMode == ViewMode.Detail)
            {
                RenderCurrentDetailSlide();
            }
        }

        private void ZoomAt(double zoomFactor, Point center)
        {
            double oldScale = _imageScaleTransform.ScaleX;
            double newScale = Math.Max(0.1, Math.Min(oldScale * zoomFactor, 30.0));
            double factor = newScale / oldScale;

            _imageTranslateTransform.X = center.X - (center.X - _imageTranslateTransform.X) * factor;
            _imageTranslateTransform.Y = center.Y - (center.Y - _imageTranslateTransform.Y) * factor;

            _imageScaleTransform.ScaleX = newScale;
            _imageScaleTransform.ScaleY = newScale;

            UpdateZoomButtonText();
        }

        private void ResetZoom()
        {
            _imageScaleTransform.ScaleX = 1.0;
            _imageScaleTransform.ScaleY = 1.0;
            _imageTranslateTransform.X = 0;
            _imageTranslateTransform.Y = 0;
            UpdateZoomButtonText();
        }

        private void UpdateZoomButtonText()
        {
            if (_btnZoomReset != null)
            {
                int pct = (int)Math.Round(_imageScaleTransform.ScaleX * 100);
                _btnZoomReset.Content = $"{pct}%";
            }
        }

        private void OnDetailMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentMode != ViewMode.Detail) return;

            if (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = true;
                _hasPanned = false;
                _lastPanPoint = e.GetPosition(_detailImageContainer);
                _panStartPoint = _lastPanPoint;
                _detailImageContainer.CaptureMouse();
            }
        }

        private void OnDetailMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning && (e.LeftButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed))
            {
                Point current = e.GetPosition(_detailImageContainer);
                Vector delta = current - _lastPanPoint;

                if (!_hasPanned && (Math.Abs(current.X - _panStartPoint.X) > 3 || Math.Abs(current.Y - _panStartPoint.Y) > 3))
                {
                    _hasPanned = true;
                    _detailImageContainer.Cursor = Cursors.SizeAll;
                }

                if (_hasPanned)
                {
                    _imageTranslateTransform.X += delta.X;
                    _imageTranslateTransform.Y += delta.Y;
                }

                _lastPanPoint = current;
            }
        }

        private void OnDetailMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning && (e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Middle))
            {
                _isPanning = false;
                _detailImageContainer.ReleaseMouseCapture();
                _detailImageContainer.Cursor = Cursors.Arrow;
            }
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_currentMode == ViewMode.Detail)
            {
                Point mousePos = e.GetPosition(_detailImageContainer);
                double zoomDelta = e.Delta > 0 ? 1.15 : (1.0 / 1.15);
                ZoomAt(zoomDelta, mousePos);
                e.Handled = true;
            }
        }
        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();

            // 鼠标后退键 (XButton1)
            if (e.ChangedButton == MouseButton.XButton1)
            {
                if (_currentMode == ViewMode.Detail && _isSlb)
                {
                    SwitchToGridView();
                    e.Handled = true;
                    return;
                }
            }
            // 鼠标前进键 (XButton2)
            else if (e.ChangedButton == MouseButton.XButton2)
            {
                if (_currentMode == ViewMode.Grid && _isSlb)
                {
                    SwitchToDetailView(_currentIndex);
                    e.Handled = true;
                    return;
                }
                else if (_currentMode == ViewMode.Detail && _isSlb)
                {
                    NextSlide();
                    e.Handled = true;
                    return;
                }
            }
            // 在大图视图下双击背景或图片，根据当前缩放状态：已放大则还原100%，已是100%则返回网格视图
            else if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                if (_currentMode == ViewMode.Detail)
                {
                    if (!IsDescendantOf(e.OriginalSource as DependencyObject, _detailToolbar) &&
                        !IsDescendantOf(e.OriginalSource as DependencyObject, _btnBackToGrid))
                    {
                        if (_imageScaleTransform.ScaleX > 1.05 || _imageScaleTransform.ScaleX < 0.95 ||
                            Math.Abs(_imageTranslateTransform.X) > 1 || Math.Abs(_imageTranslateTransform.Y) > 1)
                        {
                            ResetZoom();
                        }
                        else if (_isSlb)
                        {
                            SwitchToGridView();
                        }
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        private static bool IsDescendantOf(DependencyObject? node, DependencyObject parent)
        {
            while (node != null)
            {
                if (node == parent) return true;
                node = VisualTreeHelper.GetParent(node);
            }
            return false;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = e.Key;
            if (key == Key.ImeProcessed)
            {
                key = e.ImeProcessedKey;
            }
            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            if (key == Key.B && !_txtSearch.IsFocused)
            {
                ToggleTheme();
                e.Handled = true;
                return;
            }

            if (_currentMode == ViewMode.Detail)
            {
                // 退格键 或 Alt + Left 返回网格，不拦截 Escape 键以防干扰关闭 QuickLook
                if (key == Key.Back || (key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt))
                {
                    if (_isSlb)
                    {
                        SwitchToGridView();
                        e.Handled = true;
                        return;
                    }
                }

                if (!_isSlb) return;

                switch (key)
                {
                    case Key.A:
                    case Key.Left:
                    case Key.PageUp:
                        PrevSlide();
                        e.Handled = true;
                        break;

                    case Key.D:
                    case Key.Right:
                    case Key.PageDown:
                    case Key.Space:
                        NextSlide();
                        e.Handled = true;
                        break;

                    case Key.OemPlus:
                    case Key.Add:
                        ZoomAt(1.2, new Point(_detailImageContainer.ActualWidth / 2, _detailImageContainer.ActualHeight / 2));
                        e.Handled = true;
                        break;

                    case Key.OemMinus:
                    case Key.Subtract:
                        ZoomAt(1.0 / 1.2, new Point(_detailImageContainer.ActualWidth / 2, _detailImageContainer.ActualHeight / 2));
                        e.Handled = true;
                        break;

                    case Key.D0:
                    case Key.NumPad0:
                    case Key.F:
                        ResetZoom();
                        e.Handled = true;
                        break;

                    case Key.Home:
                        if (_currentIndex != 0)
                        {
                            _currentIndex = 0;
                            RenderCurrentDetailSlide();
                        }
                        e.Handled = true;
                        break;

                    case Key.End:
                        if (_currentIndex != _allSlideNames.Count - 1)
                        {
                            _currentIndex = _allSlideNames.Count - 1;
                            RenderCurrentDetailSlide();
                        }
                        e.Handled = true;
                        break;
                }
            }
            else if (_currentMode == ViewMode.Grid)
            {
                if (key == Key.Enter && !_txtSearch.IsFocused)
                {
                    if (_filteredSlideNames.Count > 0)
                    {
                        int index = _allSlideNames.IndexOf(_filteredSlideNames[0]);
                        if (index >= 0)
                        {
                            SwitchToDetailView(index);
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        private void ShowError(string message)
        {
            _txtDetailError.Text = message;
            _txtDetailError.Visibility = Visibility.Visible;
            _currentMode = ViewMode.Detail;
            UpdateViewVisibility();
        }

        private void ShowDetailError(string message)
        {
            _txtDetailError.Text = message;
            _txtDetailError.Visibility = Visibility.Visible;
            _detailImage.Visibility = Visibility.Collapsed;
                // Detail error
        }

        private static ImageSource BitmapToImageSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
        private DpiScale GetCurrentDpi()
        {
            try
            {
                return VisualTreeHelper.GetDpi(this);
            }
            catch
            {
                return new DpiScale(1.0, 1.0);
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);

            if (_currentMode == ViewMode.Detail)
            {
                RenderCurrentDetailSlide();
            }
            else if (_currentMode == ViewMode.Grid)
            {
                double currentWidth = _scrollViewer.ViewportWidth > 0 ? _scrollViewer.ViewportWidth : ActualWidth;
                UpdateGridLayout(currentWidth);
            }
        }


        public void Dispose()
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts = null;
            lock (_queueLock)
            {
                _thumbnailQueue.Clear();
                _isThumbnailWorkerRunning = false;
            }
            _thumbnailCache.Clear();

            _currentDetailBitmap?.Dispose();
            _currentDetailBitmap = null;
            _detailImage.Source = null;
        }
    }
}
