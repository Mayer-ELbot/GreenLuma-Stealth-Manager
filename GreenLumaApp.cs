using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace GreenLumaManager
{
    public class GameItem
    {
        public int appId { get; set; }
        public string name { get; set; }
        public string image { get; set; }
        public bool enabled { get; set; }
        public string type { get; set; } // "game" or "dlc"
        public bool isDLC { get; set; }
        public int dlcCount { get; set; }
        public bool familyShare { get; set; }

        public GameItem()
        {
            familyShare = true;
            enabled = true;
            type = "game";
        }

        public bool CheckIsDLC()
        {
            if (isDLC) return true;
            if (!string.IsNullOrEmpty(type))
            {
                string t = type.ToLower();
                if (t == "dlc" || t == "music" || t == "soundtrack" || t == "series") return true;
            }
            if (!string.IsNullOrEmpty(name))
            {
                string lower = name.ToLower();
                if (lower.Contains(" - dlc") || lower.Contains(" dlc ") || lower.EndsWith(" dlc") ||
                    lower.Contains("expansion") || lower.Contains("season pass") || lower.Contains("upgrade to") ||
                    lower.Contains("deluxe edition upgrade") || lower.Contains("soundtrack") || lower.Contains(" ost") ||
                    lower.Contains("hunter set") || lower.Contains("reaver set") || lower.Contains("collector set") ||
                    lower.Contains("skin pack") || lower.Contains("costume") || lower.Contains("character pack") ||
                    lower.Contains("bonus content") || lower.Contains("artbook") || lower.Contains("item pack") ||
                    lower.Contains("content pack") || lower.Contains(" starter pack"))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class SearchResultItem
    {
        public int id { get; set; }
        public string name { get; set; }
        public string tiny_image { get; set; }
        public string price_text { get; set; }
        public bool isDLC { get; set; }
        public bool familyShare { get; set; }
    }

    public class AppSettings
    {
        public string customSteamPath { get; set; }
        public string mode { get; set; } // "standard" or "families"
        public bool clearAppCache { get; set; }
        public string steamArgs { get; set; }
        public string lang { get; set; } // "en" or "ar"

        public AppSettings()
        {
            mode = "standard";
            steamArgs = "-inhibitbootstrap";
            lang = "en";
            clearAppCache = false;
        }
    }

    public class MainWindow : Window
    {
        public const string CURRENT_VERSION = "v1.3.0";
        public const string GITHUB_REPO = "marouanes/GreenLuma-Stealth-Manager";

        private string appDir;
        private string steamDir;
        private List<GameItem> gamesList = new List<GameItem>();
        private AppSettings settings = new AppSettings();
        private bool isInjected = false;
        private Thread monitorThread = null;
        private DispatcherTimer statusTimer;

        // Window Controls (Custom Frameless Title Bar)
        private Button btnWinMin;
        private Button btnWinMax;
        private Button btnWinClose;

        // UI Controls - Header
        private Border headerBorder;
        private TextBlock txtLogoSub;
        private TextBlock txtStatus;
        private Border borderStatusDot;
        private Border borderStatusPulseRing;
        private Button btnSettings;
        private Button btnLangToggle;
        private Button btnCheckUpdate;
        private Button btnCleanAll;
        private Border updateBannerBorder;
        private TextBlock txtUpdateBanner;
        private Button btnUpdateBanner;
        private string pendingUpdateUrl = "";

        // UI Controls - Control Deck
        private TextBlock lblControlDesc;
        private TextBlock lblMode;
        private ComboBox cmbMode;
        private CheckBox chkClearCache;
        private Button btnLaunch;
        private Button btnStop;
        private TextBlock lblSteamDirHeader;
        private TextBlock txtSteamPath;
        private TextBlock lblPayloadSourceHeader;
        private TextBlock txtPayloadSource;
        private Button btnOpenAppFolder;
        private TextBlock lblLaunchArgsHeader;
        private TextBox txtSteamArgs;
        private TextBlock lblActiveCountHeader;
        private TextBlock txtGamesCount;

        // UI Controls - Search & Discovery Deck
        private Border searchContainer;
        private TextBox txtGameInput;
        private TextBlock txtSearchPlaceholder;
        private Button btnAddInput;
        private Button btnBulk;
        private Popup searchPopup;
        private StackPanel searchResultsPanel;
        private TextBlock lblSearchPopupTitle;
        private DispatcherTimer searchDebounceTimer;
        private List<SearchResultItem> lastSearchResults = new List<SearchResultItem>();

        // UI Controls - Library Grid & Filter Tabs
        private TextBlock lblLibraryTitle;
        private string currentFilterTab = "all"; // "all", "games", "dlcs"
        private Button btnTabAll;
        private Button btnTabGames;
        private Button btnTabDlcs;
        private TextBox txtFilterInput;
        private Button btnToggleAll;
        private Button btnClearAll;
        private WrapPanel gamesWrapPanel;
        private TextBlock txtEmptyState;

        private static readonly int[] DEFAULT_SLOTS = new int[] {
            90, 205, 219, 310, 410, 570, 575, 635, 640, 740, 1213, 1273, 1840, 2145, 2403, 4270, 4940, 
            8680, 8710, 8730, 8770, 13180, 17505, 17515, 17525, 17535, 17555, 17575, 17585, 18010, 18030, 
            22150, 34120, 41005, 41015, 41040, 41080, 42300, 42320, 42750, 43210, 55280, 63220, 70010, 
            72310, 72780, 91720, 96810, 111710, 203300, 203600, 208050, 212542, 215350, 215360, 216280, 
            216840, 220070, 221410, 222840, 223160, 223240, 223250, 223350, 223910, 224620, 229950, 230030, 
            231390, 233780, 236600, 236650, 237410, 238670, 238690, 255470, 258680, 261020, 261140, 261310, 
            265360, 266910, 294420, 302530, 302550, 312070, 313250, 315420, 316000, 319070, 320420, 321770, 
            322050, 323010, 332850, 366490, 373300, 374980, 381690, 382030, 401530, 405270, 407350, 443030, 
            476580, 551410, 568880, 613220, 733580, 807210, 858280, 875860, 944490, 961940, 1042420, 1054830, 
            1070560, 1070910, 1113280, 1161040, 1182480, 1245040, 1391110, 1420170, 1493710, 1580130, 1628350, 
            1635560, 1826330, 1874900, 1887720, 1977700, 2180100, 2230260, 2348590, 2676230, 2738040, 2805730, 
            3029110, 3043620, 3086180, 3127680, 3340990, 3658110, 4183110, 4185400, 4333400, 4427310, 4628710, 
            4628740, 4690330, 4862110
        };

        public MainWindow()
        {
            appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            Title = "GreenLuma 2026 - Stealth Pro Hub (Standalone Edition)";
            Width = 1200;
            Height = 860;
            MinWidth = 1000;
            MinHeight = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(10, 13, 20)); // #0A0D14
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));

            // Custom Frameless Window Style
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResizeWithGrip;

            this.Icon = CreateAppIcon();

            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls; } catch { }

            BuildUI();

            LoadSettings();
            DetectSteamPath();
            LoadGames();
            UpdateStatusUI();
            ApplyLanguageUI();

            StartStatusDotAnimation();

            statusTimer = new DispatcherTimer();
            statusTimer.Interval = TimeSpan.FromSeconds(2.5);
            statusTimer.Tick += (s, e) => UpdateStatusUI();
            statusTimer.Start();

            searchDebounceTimer = new DispatcherTimer();
            searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(280);
            searchDebounceTimer.Tick += (s, e) => {
                searchDebounceTimer.Stop();
                string q = txtGameInput.Text.Trim();
                if (q.Length >= 2)
                {
                    ExecuteLiveSearch(q);
                }
                else
                {
                    if (searchPopup != null) searchPopup.IsOpen = false;
                }
            };

            CheckForUpdatesAsync(false);

            Closing += MainWindow_Closing;
        }

        private ImageSource CreateAppIcon()
        {
            try
            {
                string localLogo = Path.Combine(appDir, "assets", "logo.png");
                if (File.Exists(localLogo))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(localLogo, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    return bmp;
                }

                var asm = Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("logo.png"))
                {
                    if (s != null)
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = s;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        return bmp;
                    }
                }
            }
            catch { }

            try
            {
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRoundedRectangle(
                        new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                        new Pen(new SolidColorBrush(Color.FromRgb(16, 185, 129)), 2),
                        new Rect(0, 0, 32, 32), 6, 6);

                    var ft = new FormattedText(
                        "GL",
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                        14,
                        new SolidColorBrush(Color.FromRgb(16, 185, 129)));

                    dc.DrawText(ft, new Point(7, 6));
                }
                var rtb = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                return rtb;
            }
            catch { return null; }
        }

        private void StartStatusDotAnimation()
        {
            try
            {
                var scaleTransform = new ScaleTransform(1.0, 1.0);
                borderStatusPulseRing.RenderTransformOrigin = new Point(0.5, 0.5);
                borderStatusPulseRing.RenderTransform = scaleTransform;

                var daScale = new DoubleAnimation(1.0, 2.4, TimeSpan.FromSeconds(1.8)) {
                    AutoReverse = false,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                var daOp = new DoubleAnimation(0.8, 0.0, TimeSpan.FromSeconds(1.8)) {
                    AutoReverse = false,
                    RepeatBehavior = RepeatBehavior.Forever
                };

                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, daScale);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, daScale);
                borderStatusPulseRing.BeginAnimation(UIElement.OpacityProperty, daOp);
            }
            catch { }
        }

        private void ToggleMaximize()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                btnWinMax.Content = "□";
            }
            else
            {
                WindowState = WindowState.Maximized;
                btnWinMax.Content = "❐";
            }
        }

        private void BuildUI()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 0: Top Header / Custom Title Bar
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 1: Update Banner
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 2: Control Deck (Launch & Steam)
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 3: VIP Search & Discovery Bar
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // 4: Selected Library & DLCs

            // =========================================================================
            // --- 0. CUSTOM TITLE BAR & TOP HEADER (SEAMLESS CYBERPUNK INTEGRATION) ---
            // =========================================================================
            headerBorder = new Border() {
                Background = new SolidColorBrush(Color.FromRgb(12, 16, 24)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 8, 10, 8)
            };

            // Draggable window support on header
            headerBorder.MouseDown += (s, e) => {
                if (e.ChangedButton == MouseButton.Left)
                {
                    if (e.ClickCount == 2)
                    {
                        ToggleMaximize();
                    }
                    else
                    {
                        DragMove();
                    }
                }
            };

            var headerDock = new DockPanel();

            // Brand Logo & Subtitle
            var logoStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var imgLogo = new Image() {
                Source = this.Icon,
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            logoStack.Children.Add(imgLogo);

            var txtLogo = new TextBlock() {
                Text = "GREENLUMA 2026",
                FontSize = 20,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                VerticalAlignment = VerticalAlignment.Center
            };
            var txtBadgeVer = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            txtBadgeVer.Child = new TextBlock() { Text = CURRENT_VERSION, FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183)), FontWeight = FontWeights.Bold };

            txtLogoSub = new TextBlock() {
                Text = "Stealth Session & Standalone Manager",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center
            };
            logoStack.Children.Add(txtLogo);
            logoStack.Children.Add(txtBadgeVer);
            logoStack.Children.Add(txtLogoSub);
            DockPanel.SetDock(logoStack, Dock.Left);
            headerDock.Children.Add(logoStack);

            // Right Header Controls: Status Radar + Actions + Window Control Buttons
            var rightHeaderStack = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

            // Live Status Radar Indicator
            var statusPillBorder = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(40, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(10, 4, 12, 4),
                Margin = new Thickness(0, 0, 10, 0)
            };
            var statusPillStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var dotContainer = new Grid() { Width = 14, Height = 14, Margin = new Thickness(0, 0, 8, 0) };
            borderStatusPulseRing = new Border() {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Opacity = 0.5
            };
            borderStatusDot = new Border() {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            dotContainer.Children.Add(borderStatusPulseRing);
            dotContainer.Children.Add(borderStatusDot);
            statusPillStack.Children.Add(dotContainer);

            txtStatus = new TextBlock() {
                Text = "Steam Inactive",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusPillStack.Children.Add(txtStatus);
            statusPillBorder.Child = statusPillStack;
            rightHeaderStack.Children.Add(statusPillBorder);

            // Settings Button
            btnSettings = new Button() {
                Content = "⚙️ Settings",
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 4, 9, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnSettings.Click += (s, e) => OpenSettingsDialog();
            rightHeaderStack.Children.Add(btnSettings);

            // Language Toggle
            btnLangToggle = new Button() {
                Content = "🌐 EN | العربية",
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 4, 9, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnLangToggle.Click += (s, e) => ToggleLanguage();
            rightHeaderStack.Children.Add(btnLangToggle);

            // Check Updates Button
            btnCheckUpdate = new Button() {
                Content = "🔄 Update",
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 4, 9, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnCheckUpdate.Click += (s, e) => CheckForUpdatesAsync(true);
            rightHeaderStack.Children.Add(btnCheckUpdate);

            // Clean artifacts
            btnCleanAll = new Button() {
                Content = "🧹 Clean Steam",
                Background = new SolidColorBrush(Color.FromArgb(40, 239, 68, 68)),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 239, 68, 68)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 12, 0)
            };
            btnCleanAll.Click += (s, e) => PurgeAllArtifacts();
            rightHeaderStack.Children.Add(btnCleanAll);

            // === CUSTOM WINDOW CONTROLS (MIN / MAX / CLOSE) ===
            var winControlsStack = new StackPanel() { Orientation = Orientation.Horizontal };

            btnWinMin = new Button() {
                Content = "─",
                FontSize = 13,
                Width = 34,
                Height = 28,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnWinMin.MouseEnter += (s, e) => {
                btnWinMin.Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                btnWinMin.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            };
            btnWinMin.MouseLeave += (s, e) => {
                btnWinMin.Background = Brushes.Transparent;
                btnWinMin.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            };
            btnWinMin.Click += (s, e) => WindowState = WindowState.Minimized;
            winControlsStack.Children.Add(btnWinMin);

            btnWinMax = new Button() {
                Content = "□",
                FontSize = 14,
                Width = 34,
                Height = 28,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnWinMax.MouseEnter += (s, e) => {
                btnWinMax.Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                btnWinMax.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            };
            btnWinMax.MouseLeave += (s, e) => {
                btnWinMax.Background = Brushes.Transparent;
                btnWinMax.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            };
            btnWinMax.Click += (s, e) => ToggleMaximize();
            winControlsStack.Children.Add(btnWinMax);

            btnWinClose = new Button() {
                Content = "✕",
                FontSize = 13,
                Width = 38,
                Height = 28,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnWinClose.MouseEnter += (s, e) => {
                btnWinClose.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                btnWinClose.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            };
            btnWinClose.MouseLeave += (s, e) => {
                btnWinClose.Background = Brushes.Transparent;
                btnWinClose.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            };
            btnWinClose.Click += (s, e) => Close();
            winControlsStack.Children.Add(btnWinClose);

            rightHeaderStack.Children.Add(winControlsStack);

            headerDock.Children.Add(rightHeaderStack);
            headerBorder.Child = headerDock;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // ==========================================
            // --- 1. UPDATE BANNER ---
            // ==========================================
            updateBannerBorder = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(60, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 16, 185, 129)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 8, 20, 8),
                Visibility = Visibility.Collapsed
            };
            var updateDock = new DockPanel();
            txtUpdateBanner = new TextBlock() {
                Text = "🎉 New version available!",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(167, 243, 208)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(txtUpdateBanner, Dock.Left);
            updateDock.Children.Add(txtUpdateBanner);

            btnUpdateBanner = new Button() {
                Content = "Download Update (GitHub) ↗",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(12, 4, 12, 4),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnUpdateBanner.Click += (s, e) => {
                if (!string.IsNullOrEmpty(pendingUpdateUrl))
                {
                    try { Process.Start(pendingUpdateUrl); } catch { }
                }
            };
            updateDock.Children.Add(btnUpdateBanner);
            updateBannerBorder.Child = updateDock;
            Grid.SetRow(updateBannerBorder, 1);
            mainGrid.Children.Add(updateBannerBorder);

            // ==========================================
            // --- 2. CONTROL DECK (LAUNCH & STEAM) ---
            // ==========================================
            var controlBorder = new Border() {
                Background = new SolidColorBrush(Color.FromRgb(15, 20, 32)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(20, 12, 20, 8),
                Padding = new Thickness(16)
            };
            var controlGrid = new Grid();
            controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(2, GridUnitType.Star) });
            controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.3, GridUnitType.Star) });

            // Left column: Mode, options & Reactor Launch Button
            var leftCtrl = new StackPanel();
            lblControlDesc = new TextBlock() {
                Text = "GreenLuma stealth injection unlocks all items and cleans up automatically when finished.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            leftCtrl.Children.Add(lblControlDesc);

            var optionsRow = new WrapPanel() { Margin = new Thickness(0, 0, 0, 14) };
            lblMode = new TextBlock() { Text = "Mode: ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)) };
            cmbMode = new ComboBox() { Width = 230, Height = 28, Margin = new Thickness(0, 0, 14, 0) };
            cmbMode.Items.Add("Standard Stealth (user32.dll)");
            cmbMode.Items.Add("Steam Families (user32SF.dll)");
            cmbMode.SelectedIndex = 0;
            optionsRow.Children.Add(lblMode);
            optionsRow.Children.Add(cmbMode);

            chkClearCache = new CheckBox() {
                Content = "Clear AppCache on Launch",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 14, 0)
            };
            optionsRow.Children.Add(chkClearCache);
            leftCtrl.Children.Add(optionsRow);

            var actionBtnRow = new StackPanel() { Orientation = Orientation.Horizontal };

            // === VIOLENT REACTOR LAUNCH BUTTON ===
            btnLaunch = new Button() {
                Content = "⚡ LAUNCH GREENLUMA STEAM",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 14,
                Padding = new Thickness(26, 11, 26, 11),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 12, 0)
            };
            var launchGlow = new DropShadowEffect() {
                Color = Color.FromRgb(16, 185, 129),
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0.85
            };
            btnLaunch.Effect = launchGlow;

            var daBlur = new DoubleAnimation(12, 34, TimeSpan.FromSeconds(1.1)) {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            launchGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, daBlur);

            var daOp = new DoubleAnimation(0.55, 1.0, TimeSpan.FromSeconds(1.1)) {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            launchGlow.BeginAnimation(DropShadowEffect.OpacityProperty, daOp);

            var btnScale = new ScaleTransform(1.0, 1.0);
            btnLaunch.RenderTransformOrigin = new Point(0.5, 0.5);
            btnLaunch.RenderTransform = btnScale;
            btnLaunch.MouseEnter += (s, e) => {
                btnScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 1.04, TimeSpan.FromMilliseconds(140)) { DecelerationRatio = 0.5 });
                btnScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 1.04, TimeSpan.FromMilliseconds(140)) { DecelerationRatio = 0.5 });
            };
            btnLaunch.MouseLeave += (s, e) => {
                btnScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.04, 1.0, TimeSpan.FromMilliseconds(160)));
                btnScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.04, 1.0, TimeSpan.FromMilliseconds(160)));
            };
            btnLaunch.Click += (s, e) => LaunchSteamSession();

            // Stop Steam Button
            btnStop = new Button() {
                Content = "🛑 Stop Steam & Delete Files",
                Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                Padding = new Thickness(22, 10, 22, 10),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed
            };
            var stopGlow = new DropShadowEffect() {
                Color = Color.FromRgb(239, 68, 68),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.7
            };
            btnStop.Effect = stopGlow;
            btnStop.Click += (s, e) => StopSteamSession();

            actionBtnRow.Children.Add(btnLaunch);
            actionBtnRow.Children.Add(btnStop);
            leftCtrl.Children.Add(actionBtnRow);

            Grid.SetColumn(leftCtrl, 0);
            controlGrid.Children.Add(leftCtrl);

            // Right column: Steam info card & drop updates
            var rightCtrl = new Border() {
                Background = new SolidColorBrush(Color.FromRgb(11, 15, 24)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(16, 0, 0, 0)
            };
            var rightStack = new StackPanel();

            lblSteamDirHeader = new TextBlock() { Text = "Steam Directory:", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) };
            rightStack.Children.Add(lblSteamDirHeader);
            txtSteamPath = new TextBlock() {
                Text = "Detecting...",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                Margin = new Thickness(0, 2, 0, 6),
                TextWrapping = TextWrapping.Wrap,
                FlowDirection = FlowDirection.LeftToRight,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            rightStack.Children.Add(txtSteamPath);

            lblPayloadSourceHeader = new TextBlock() { Text = "Stealth Engine Files:", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) };
            rightStack.Children.Add(lblPayloadSourceHeader);

            var sourceRow = new DockPanel() { Margin = new Thickness(0, 2, 0, 6) };
            btnOpenAppFolder = new Button() {
                Content = "📂 Drop Updates",
                FontSize = 10.5,
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 2, 6, 2),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnOpenAppFolder.Click += (s, e) => {
                try { Process.Start("explorer.exe", appDir); } catch { }
            };
            DockPanel.SetDock(btnOpenAppFolder, Dock.Right);
            sourceRow.Children.Add(btnOpenAppFolder);

            txtPayloadSource = new TextBlock() {
                Text = "Embedded (Self-Contained)",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            sourceRow.Children.Add(txtPayloadSource);
            rightStack.Children.Add(sourceRow);

            lblLaunchArgsHeader = new TextBlock() { Text = "Launch Arguments:", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) };
            rightStack.Children.Add(lblLaunchArgsHeader);
            txtSteamArgs = new TextBox() {
                Text = "-inhibitbootstrap",
                FontSize = 11.5,
                Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)),
                Foreground = new SolidColorBrush(Color.FromRgb(6, 182, 212)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 2, 0, 6),
                FlowDirection = FlowDirection.LeftToRight,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            rightStack.Children.Add(txtSteamArgs);

            var countStack = new StackPanel() { Orientation = Orientation.Horizontal };
            lblActiveCountHeader = new TextBlock() { Text = "Active Unlocked Items: ", FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)) };
            txtGamesCount = new TextBlock() { Text = "0", FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)) };
            countStack.Children.Add(lblActiveCountHeader);
            countStack.Children.Add(txtGamesCount);
            rightStack.Children.Add(countStack);

            rightCtrl.Child = rightStack;
            Grid.SetColumn(rightCtrl, 1);
            controlGrid.Children.Add(rightCtrl);

            controlBorder.Child = controlGrid;
            Grid.SetRow(controlBorder, 2);
            mainGrid.Children.Add(controlBorder);

            // =========================================================================
            // --- 3. VIP SEARCH COMMAND BAR (UNCLUTTERED, NO QUICK DISCOVER) ---
            // =========================================================================
            var discoveryBorder = new Border() {
                Background = new SolidColorBrush(Color.FromRgb(13, 18, 28)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(20, 4, 20, 8),
                Padding = new Thickness(14)
            };

            searchContainer = new Border() {
                Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4)
            };
            var searchDock = new DockPanel();

            var searchIcon = new TextBlock() {
                Text = "🔍",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 0)
            };
            DockPanel.SetDock(searchIcon, Dock.Left);
            searchDock.Children.Add(searchIcon);

            var actionBtns = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            btnAddInput = new Button() {
                Content = "+ Add AppID",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(14, 5, 14, 5),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 6, 0)
            };
            btnAddInput.Click += (s, e) => AddGameFromInput();
            actionBtns.Children.Add(btnAddInput);

            btnBulk = new Button() {
                Content = "📥 Bulk Import",
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                Padding = new Thickness(12, 5, 12, 5),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btnBulk.Click += (s, e) => OpenBulkImportDialog();
            actionBtns.Children.Add(btnBulk);

            DockPanel.SetDock(actionBtns, Dock.Right);
            searchDock.Children.Add(actionBtns);

            var inputGrid = new Grid();
            txtGameInput = new TextBox() {
                Height = 32,
                FontSize = 13,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };
            txtSearchPlaceholder = new TextBlock() {
                Text = "Search any Steam game or DLC by name (e.g. Arc Raiders, Black Myth, Elden Ring) or enter AppID...",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                IsHitTestVisible = false
            };
            inputGrid.Children.Add(txtSearchPlaceholder);
            inputGrid.Children.Add(txtGameInput);
            searchDock.Children.Add(inputGrid);

            txtGameInput.TextChanged += (s, e) => {
                txtSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtGameInput.Text) ? Visibility.Visible : Visibility.Collapsed;
                searchDebounceTimer.Stop();
                searchDebounceTimer.Start();
            };

            txtGameInput.KeyDown += (s, e) => {
                if (e.Key == Key.Enter)
                {
                    searchDebounceTimer.Stop();
                    string raw = txtGameInput.Text.Trim();
                    if (string.IsNullOrEmpty(raw)) return;

                    int id;
                    if (int.TryParse(raw, out id) && id > 0)
                    {
                        if (searchPopup != null) searchPopup.IsOpen = false;
                        AddGameById(id, null);
                        txtGameInput.Text = "";
                    }
                    else
                    {
                        if (lastSearchResults != null && lastSearchResults.Count > 0)
                        {
                            var top = lastSearchResults[0];
                            if (searchPopup != null) searchPopup.IsOpen = false;
                            AddGameById(top.id, top.name);
                            txtGameInput.Text = "";
                        }
                        else
                        {
                            ExecuteLiveSearch(raw);
                        }
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    if (searchPopup != null) searchPopup.IsOpen = false;
                }
            };

            searchContainer.Child = searchDock;
            discoveryBorder.Child = searchContainer;
            Grid.SetRow(discoveryBorder, 3);
            mainGrid.Children.Add(discoveryBorder);

            // ==========================================
            // --- FLOATING AUTOCOMPLETE SEARCH POPUP ---
            // ==========================================
            searchPopup = new Popup() {
                PlacementTarget = searchContainer,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                HorizontalOffset = 0,
                VerticalOffset = 4
            };

            var popupBorder = new Border() {
                Width = 760,
                MaxHeight = 440,
                Background = new SolidColorBrush(Color.FromRgb(11, 16, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10)
            };
            var popupGlow = new DropShadowEffect() {
                Color = Color.FromRgb(16, 185, 129),
                BlurRadius = 24,
                ShadowDepth = 4,
                Opacity = 0.6
            };
            popupBorder.Effect = popupGlow;

            var popupContentStack = new StackPanel();
            var popupHeaderDock = new DockPanel() { Margin = new Thickness(4, 2, 4, 8) };
            lblSearchPopupTitle = new TextBlock() {
                Text = "⚡ Steam Store Results (Shows Family Sharing & DLC Status):",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183))
            };
            DockPanel.SetDock(lblSearchPopupTitle, Dock.Left);
            popupHeaderDock.Children.Add(lblSearchPopupTitle);

            var lblEscHint = new TextBlock() {
                Text = "ESC to close",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            popupHeaderDock.Children.Add(lblEscHint);
            popupContentStack.Children.Add(popupHeaderDock);

            var popupScroll = new ScrollViewer() {
                MaxHeight = 370,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            searchResultsPanel = new StackPanel();
            popupScroll.Content = searchResultsPanel;
            popupContentStack.Children.Add(popupScroll);

            popupBorder.Child = popupContentStack;
            searchPopup.Child = popupBorder;

            // =========================================================================
            // --- 4. SELECTED GAMES & DLCS (Library Grid with Filter Tabs) ---
            // =========================================================================
            var libraryContainer = new Grid() { Margin = new Thickness(20, 4, 20, 16) };
            libraryContainer.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            libraryContainer.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

            // Title, Tabs & Filter Header
            var libraryHeaderDock = new DockPanel() { Margin = new Thickness(0, 0, 0, 10) };
            lblLibraryTitle = new TextBlock() {
                Text = "🎮 UNLOCKED STEAM APPLIST:",
                FontSize = 14,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            };
            DockPanel.SetDock(lblLibraryTitle, Dock.Left);
            libraryHeaderDock.Children.Add(lblLibraryTitle);

            // Filter Tabs: [ All ] [ Games ] [ DLCs ]
            var tabsStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btnTabAll = CreateFilterTabButton("All (0)", "all", true);
            btnTabGames = CreateFilterTabButton("🎮 Games (0)", "games", false);
            btnTabDlcs = CreateFilterTabButton("🧩 DLCs (0)", "dlcs", false);
            tabsStack.Children.Add(btnTabAll);
            tabsStack.Children.Add(btnTabGames);
            tabsStack.Children.Add(btnTabDlcs);
            DockPanel.SetDock(tabsStack, Dock.Left);
            libraryHeaderDock.Children.Add(tabsStack);

            // Right tools: Search filter + Toggle All + Clear All
            var libToolsStack = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            txtFilterInput = new TextBox() {
                Width = 180,
                Height = 26,
                FontSize = 11.5,
                Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)),
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            txtFilterInput.TextChanged += (s, e) => RenderGamesUI();
            libToolsStack.Children.Add(txtFilterInput);

            btnToggleAll = new Button() {
                Content = "🔄 Toggle All",
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnToggleAll.Click += (s, e) => {
                bool anyDisabled = false;
                foreach (var g in gamesList) { if (!g.enabled) { anyDisabled = true; break; } }
                foreach (var g in gamesList) { g.enabled = anyDisabled; }
                SaveGames();
                RenderGamesUI();
            };
            libToolsStack.Children.Add(btnToggleAll);

            btnClearAll = new Button() {
                Content = "🗑 Clear All",
                Background = new SolidColorBrush(Color.FromArgb(30, 239, 68, 68)),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 239, 68, 68)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.Hand
            };
            btnClearAll.Click += (s, e) => {
                if (gamesList.Count == 0) return;
                bool isAr = (settings.lang == "ar");
                if (MessageBox.Show(
                    isAr ? "هل أنت متأكد من رغبتك في حذف جميع الألعاب من القائمة؟" : "Are you sure you want to clear your entire unlocked library?",
                    isAr ? "تأكيد المسح" : "Clear Library",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    gamesList.Clear();
                    SaveGames();
                    RenderGamesUI();
                }
            };
            libToolsStack.Children.Add(btnClearAll);

            libraryHeaderDock.Children.Add(libToolsStack);

            Grid.SetRow(libraryHeaderDock, 0);
            libraryContainer.Children.Add(libraryHeaderDock);

            var scroll = new ScrollViewer() {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var gamesContainerGrid = new Grid();
            gamesWrapPanel = new WrapPanel() { Orientation = Orientation.Horizontal };
            gamesContainerGrid.Children.Add(gamesWrapPanel);

            txtEmptyState = new TextBlock() {
                Text = "No games selected yet.\nSearch above by game title or enter AppID to unlock it in your library.",
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 60, 0, 0),
                Visibility = Visibility.Collapsed
            };
            gamesContainerGrid.Children.Add(txtEmptyState);

            scroll.Content = gamesContainerGrid;
            Grid.SetRow(scroll, 1);
            libraryContainer.Children.Add(scroll);

            Grid.SetRow(libraryContainer, 4);
            mainGrid.Children.Add(libraryContainer);

            Content = mainGrid;
        }

        private Button CreateFilterTabButton(string text, string tabKey, bool isActive)
        {
            var btn = new Button() {
                Content = text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Background = isActive ? new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Foreground = isActive ? new SolidColorBrush(Color.FromRgb(110, 231, 183)) : new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderBrush = isActive ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            btn.Click += (s, e) => {
                currentFilterTab = tabKey;
                UpdateTabButtonStyles();
                RenderGamesUI();
            };
            return btn;
        }

        private void UpdateTabButtonStyles()
        {
            int total = gamesList.Count;
            int games = 0;
            int dlcs = 0;
            foreach (var g in gamesList)
            {
                if (g.CheckIsDLC()) dlcs++;
                else games++;
            }

            bool isAr = (settings.lang == "ar");
            btnTabAll.Content = isAr ? string.Format("الكل ({0})", total) : string.Format("All ({0})", total);
            btnTabGames.Content = isAr ? string.Format("🎮 ألعاب ({0})", games) : string.Format("🎮 Games ({0})", games);
            btnTabDlcs.Content = isAr ? string.Format("🧩 إضافات ({0})", dlcs) : string.Format("🧩 DLCs ({0})", dlcs);

            SetTabActive(btnTabAll, currentFilterTab == "all");
            SetTabActive(btnTabGames, currentFilterTab == "games");
            SetTabActive(btnTabDlcs, currentFilterTab == "dlcs");
        }

        private void SetTabActive(Button btn, bool active)
        {
            btn.Background = active ? new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            btn.Foreground = active ? new SolidColorBrush(Color.FromRgb(110, 231, 183)) : new SolidColorBrush(Color.FromRgb(148, 163, 184));
            btn.BorderBrush = active ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        }

        // =========================================================================
        // --- SETTINGS PREFERENCES DIALOG (STEAM PATH, MODES, ARGS) ---
        // =========================================================================
        private void OpenSettingsDialog()
        {
            bool isAr = (settings.lang == "ar");

            var win = new Window() {
                Title = isAr ? "إعدادات وتفضيلات البرنامج" : "GreenLuma Settings & Preferences",
                Width = 560,
                Height = 440,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(14, 19, 29)),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                FlowDirection = isAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            var sp = new StackPanel() { Margin = new Thickness(20) };

            // Title
            var txtTitle = new TextBlock() {
                Text = isAr ? "⚙️ إعدادات GreenLuma المتقدمة" : "⚙️ Advanced Preferences",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Margin = new Thickness(0, 0, 0, 16)
            };
            sp.Children.Add(txtTitle);

            // 1. Steam Directory Row
            sp.Children.Add(new TextBlock() {
                Text = isAr ? "مسار تثبيت Steam (مجلد steam.exe):" : "Steam Installation Directory:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 4)
            });

            var steamPathDock = new DockPanel() { Margin = new Thickness(0, 0, 0, 14) };
            var btnBrowse = new Button() {
                Content = isAr ? "📁 استعراض..." : "📁 Browse...",
                Padding = new Thickness(12, 5, 12, 5),
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            DockPanel.SetDock(btnBrowse, Dock.Right);
            steamPathDock.Children.Add(btnBrowse);

            var txtCustomSteam = new TextBox() {
                Text = string.IsNullOrEmpty(settings.customSteamPath) ? steamDir : settings.customSteamPath,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)),
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 8, 0),
                FlowDirection = FlowDirection.LeftToRight
            };
            steamPathDock.Children.Add(txtCustomSteam);
            sp.Children.Add(steamPathDock);

            btnBrowse.Click += (s, e) => {
                var dlg = new OpenFileDialog();
                dlg.Title = isAr ? "اختر ملف steam.exe" : "Select steam.exe in your Steam folder";
                dlg.Filter = "Steam Executable (steam.exe)|steam.exe";
                if (dlg.ShowDialog(win) == true)
                {
                    txtCustomSteam.Text = Path.GetDirectoryName(dlg.FileName);
                }
            };

            // 2. Default Mode
            sp.Children.Add(new TextBlock() {
                Text = isAr ? "وضع التخفي الافتراضي:" : "Default Injection Mode:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var cmbSetMode = new ComboBox() { Height = 28, Margin = new Thickness(0, 0, 0, 14) };
            cmbSetMode.Items.Add(isAr ? "🛡️ التخفي العادي (user32.dll)" : "Standard Stealth (user32.dll)");
            cmbSetMode.Items.Add(isAr ? "👥 عائلات ستيم (user32SF.dll)" : "Steam Families (user32SF.dll)");
            cmbSetMode.SelectedIndex = (settings.mode == "families") ? 1 : 0;
            sp.Children.Add(cmbSetMode);

            // 3. Steam Launch Arguments
            sp.Children.Add(new TextBlock() {
                Text = isAr ? "معلمات التشغيل الإضافية (Arguments):" : "Steam Launch Arguments:",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var txtSetArgs = new TextBox() {
                Text = settings.steamArgs,
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)),
                Foreground = new SolidColorBrush(Color.FromRgb(6, 182, 212)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 14),
                FlowDirection = FlowDirection.LeftToRight
            };
            sp.Children.Add(txtSetArgs);

            // 4. Clear AppCache Option
            var chkSetClearCache = new CheckBox() {
                Content = isAr ? "مسح كاش ستيم تلقائياً عند كل تشغيل (Clear AppCache)" : "Always clear AppCache on launch",
                IsChecked = settings.clearAppCache,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Margin = new Thickness(0, 0, 0, 20)
            };
            sp.Children.Add(chkSetClearCache);

            // Action Buttons Row (Save / Cancel)
            var btnRow = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnCancel = new Button() {
                Content = isAr ? "إلغاء" : "Cancel",
                Padding = new Thickness(14, 6, 14, 6),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnCancel.Click += (s, e) => win.Close();
            btnRow.Children.Add(btnCancel);

            var btnSave = new Button() {
                Content = isAr ? "💾 حفظ وتطبيق" : "💾 Save & Apply",
                Padding = new Thickness(18, 6, 18, 6),
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnSave.Click += (s, e) => {
                string custom = txtCustomSteam.Text.Trim();
                if (!string.IsNullOrEmpty(custom) && Directory.Exists(custom))
                {
                    settings.customSteamPath = custom;
                    steamDir = custom;
                    txtSteamPath.Text = steamDir;
                }
                settings.mode = (cmbSetMode.SelectedIndex == 1) ? "families" : "standard";
                cmbMode.SelectedIndex = cmbSetMode.SelectedIndex;
                settings.steamArgs = txtSetArgs.Text.Trim();
                txtSteamArgs.Text = settings.steamArgs;
                settings.clearAppCache = (chkSetClearCache.IsChecked == true);
                chkClearCache.IsChecked = settings.clearAppCache;

                SaveSettings();
                win.Close();
                MessageBox.Show(
                    isAr ? "تم حفظ الإعدادات وتطبيقها بنجاح!" : "Preferences saved and applied successfully!",
                    isAr ? "تم الحفظ" : "Settings Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            };
            btnRow.Children.Add(btnSave);

            sp.Children.Add(btnRow);
            win.Content = sp;
            win.ShowDialog();
        }

        // ==========================================
        // --- LIVE STEAM STORE SEARCH ENGINE ---
        // ==========================================
        private void ExecuteLiveSearch(string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length < 2)
            {
                if (searchPopup != null) searchPopup.IsOpen = false;
                return;
            }

            ThreadPool.QueueUserWorkItem((state) => {
                var results = new List<SearchResultItem>();
                try
                {
                    string url = string.Format("https://store.steampowered.com/api/storesearch/?term={0}&l=english&cc=US", Uri.EscapeDataString(query));
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                    req.Timeout = 4000;
                    using (var resp = req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        var js = new JavaScriptSerializer();
                        var dict = js.Deserialize<Dictionary<string, object>>(json);
                        if (dict != null && dict.ContainsKey("items"))
                        {
                            var arr = dict["items"] as ArrayList;
                            if (arr != null)
                            {
                                foreach (var obj in arr)
                                {
                                    var itemDict = obj as Dictionary<string, object>;
                                    if (itemDict == null) continue;

                                    int id = itemDict.ContainsKey("id") ? Convert.ToInt32(itemDict["id"]) : 0;
                                    string name = itemDict.ContainsKey("name") ? itemDict["name"].ToString() : "";
                                    string tinyImg = itemDict.ContainsKey("tiny_image") ? itemDict["tiny_image"].ToString() : "";
                                    
                                    string priceStr = "Store";
                                    if (itemDict.ContainsKey("price"))
                                    {
                                        var pObj = itemDict["price"] as Dictionary<string, object>;
                                        if (pObj != null && pObj.ContainsKey("final"))
                                        {
                                            int finalPrice = Convert.ToInt32(pObj["final"]);
                                            priceStr = (finalPrice == 0) ? "Free" : string.Format("${0:0.00}", finalPrice / 100.0);
                                        }
                                    }

                                    bool isDlcMatch = false;
                                    string lowerName = name.ToLower();
                                    if (lowerName.Contains("dlc") || lowerName.Contains("expansion") || lowerName.Contains("pack") ||
                                        lowerName.Contains("pass") || lowerName.Contains("upgrade") || lowerName.Contains("set") ||
                                        lowerName.Contains("soundtrack") || lowerName.Contains("ost"))
                                    {
                                        isDlcMatch = true;
                                    }

                                    // Family Sharing Assessment
                                    bool isFs = true;
                                    if (id == 1174180 || id == 271590 || id == 553850 || id == 359550 || id == 1222670) isFs = false;
                                    if (lowerName.Contains("rockstar") || lowerName.Contains("ea sports") || lowerName.Contains("ubisoft")) isFs = false;

                                    results.Add(new SearchResultItem() {
                                        id = id,
                                        name = name,
                                        tiny_image = tinyImg,
                                        price_text = priceStr,
                                        isDLC = isDlcMatch,
                                        familyShare = isFs
                                    });
                                }
                            }
                        }
                    }
                }
                catch { }

                Dispatcher.Invoke((Action)(() => {
                    lastSearchResults = results;
                    RenderSearchResultsUI(results, query);
                }));
            });
        }

        private void RenderSearchResultsUI(List<SearchResultItem> results, string query)
        {
            searchResultsPanel.Children.Clear();
            bool isAr = (settings.lang == "ar");

            if (results.Count == 0)
            {
                var txtNone = new TextBlock() {
                    Text = isAr ? string.Format("لم يتم العثور على ألعاب مطابقة لـ \"{0}\"", query) : string.Format("No matching titles found on Steam Store for \"{0}\"", query),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Padding = new Thickness(10)
                };
                searchResultsPanel.Children.Add(txtNone);
                searchPopup.IsOpen = true;
                return;
            }

            foreach (var item in results)
            {
                var rowBorder = new Border() {
                    Height = 48,
                    Margin = new Thickness(0, 2, 0, 3),
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromRgb(18, 24, 38)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Padding = new Thickness(6, 4, 8, 4)
                };

                var rowDock = new DockPanel();

                // Image Thumbnail
                if (!string.IsNullOrEmpty(item.tiny_image))
                {
                    var imgThumb = new Image() {
                        Width = 62,
                        Height = 32,
                        Stretch = Stretch.UniformToFill,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(item.tiny_image, UriKind.RelativeOrAbsolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        imgThumb.Source = bmp;
                    }
                    catch { }
                    DockPanel.SetDock(imgThumb, Dock.Left);
                    rowDock.Children.Add(imgThumb);
                }

                // Add Button (Right side)
                var btnAddRow = new Button() {
                    Content = isAr ? "+ إضافة" : "+ Add to Library",
                    Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                    Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Padding = new Thickness(10, 4, 10, 4),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                int rowId = item.id;
                string rowName = item.name;
                btnAddRow.Click += (s, e) => {
                    searchPopup.IsOpen = false;
                    txtGameInput.Text = "";
                    AddGameById(rowId, rowName);
                };
                DockPanel.SetDock(btnAddRow, Dock.Right);
                rowDock.Children.Add(btnAddRow);

                // Price Tag
                var txtPrice = new TextBlock() {
                    Text = item.price_text,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(167, 243, 208)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                DockPanel.SetDock(txtPrice, Dock.Right);
                rowDock.Children.Add(txtPrice);

                // Center Info: Title + ID Pill + Type Pill + Family Share Badge
                var centerStack = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
                var txtTitle = new TextBlock() {
                    Text = item.name,
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                centerStack.Children.Add(txtTitle);

                var metaRow = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
                var idPill = new TextBlock() {
                    Text = "ID: " + item.id,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(6, 182, 212)),
                    FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                metaRow.Children.Add(idPill);

                // Type Badge (GAME vs DLC)
                var typeBadge = new Border() {
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 0, 4, 0),
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = item.isDLC ? new SolidColorBrush(Color.FromArgb(50, 245, 158, 11)) : new SolidColorBrush(Color.FromArgb(50, 16, 185, 129))
                };
                typeBadge.Child = new TextBlock() {
                    Text = item.isDLC ? "🧩 DLC" : "🎮 GAME",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = item.isDLC ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(16, 185, 129))
                };
                metaRow.Children.Add(typeBadge);

                // Family Share indicator in search results
                if (!item.isDLC)
                {
                    var fsBadge = new Border() {
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 0, 4, 0),
                        Background = item.familyShare ? new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(160, 239, 68, 68))
                    };
                    fsBadge.Child = new TextBlock() {
                        Text = item.familyShare ? (isAr ? "👥 عائلي" : "👥 Family Share") : (isAr ? "🚫 بدون عائلي" : "🚫 No Share"),
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = item.familyShare ? new SolidColorBrush(Color.FromRgb(110, 231, 183)) : new SolidColorBrush(Color.FromRgb(255, 255, 255))
                    };
                    metaRow.Children.Add(fsBadge);
                }

                centerStack.Children.Add(metaRow);
                rowDock.Children.Add(centerStack);
                rowBorder.Child = rowDock;

                // Hover physics
                rowBorder.MouseEnter += (s, e) => {
                    rowBorder.Background = new SolidColorBrush(Color.FromRgb(28, 38, 58));
                    rowBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                };
                rowBorder.MouseLeave += (s, e) => {
                    rowBorder.Background = new SolidColorBrush(Color.FromRgb(18, 24, 38));
                    rowBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                };

                rowBorder.MouseDown += (s, e) => {
                    searchPopup.IsOpen = false;
                    txtGameInput.Text = "";
                    AddGameById(rowId, rowName);
                };

                searchResultsPanel.Children.Add(rowBorder);
            }

            searchPopup.IsOpen = true;
        }

        private void ToggleLanguage()
        {
            settings.lang = (settings.lang == "ar") ? "en" : "ar";
            SaveSettings();
            ApplyLanguageUI();
        }

        private void ApplyLanguageUI()
        {
            bool isAr = (settings.lang == "ar");
            FlowDirection = isAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            txtLogoSub.Text = isAr ? "مدير جلسات التخفي ومكتبة الألعاب والإضافات" : "Stealth Session & Standalone Manager";
            btnSettings.Content = isAr ? "⚙️ الإعدادات" : "⚙️ Settings";
            btnLangToggle.Content = isAr ? "🌐 English" : "🌐 العربية";
            btnCheckUpdate.Content = isAr ? "🔄 التحديثات" : "🔄 Update";
            btnCleanAll.Content = isAr ? "🧹 تنظيف ستيم" : "🧹 Clean Steam";

            lblControlDesc.Text = isAr ? 
                "يقوم نظام GreenLuma بحقن ملفات التخفي بأمان لفتح جميع الألعاب والإضافات وحذفها تلقائياً فور إغلاق ستيم." :
                "GreenLuma stealth injection unlocks all items and cleans up automatically when finished.";

            lblMode.Text = isAr ? "وضع التشغيل: " : "Mode: ";
            cmbMode.Items.Clear();
            cmbMode.Items.Add(isAr ? "🛡️ التخفي العادي (user32.dll)" : "Standard Stealth (user32.dll)");
            cmbMode.Items.Add(isAr ? "👥 عائلات ستيم (user32SF.dll)" : "Steam Families (user32SF.dll)");
            cmbMode.SelectedIndex = (settings.mode == "families") ? 1 : 0;

            chkClearCache.Content = isAr ? "مسح كاش ستيم (AppCache)" : "Clear AppCache on Launch";
            btnLaunch.Content = isAr ? "⚡ تشغيل ستيم مع GREENLUMA" : "⚡ LAUNCH GREENLUMA STEAM";
            btnStop.Content = isAr ? "🛑 إيقاف ستيم ومسح الملفات" : "🛑 Stop Steam & Delete Files";

            lblSteamDirHeader.Text = isAr ? "مسار تثبيت ستيم:" : "Steam Directory:";
            lblPayloadSourceHeader.Text = isAr ? "ملفات التخفي:" : "Stealth Engine Files:";
            btnOpenAppFolder.Content = isAr ? "📂 وضع التحديثات" : "📂 Drop Updates";
            RefreshPayloadSourceUI();

            lblLaunchArgsHeader.Text = isAr ? "معلمات التشغيل:" : "Launch Arguments:";
            lblActiveCountHeader.Text = isAr ? "العناصر المفعلة: " : "Active Unlocked Items: ";

            txtSearchPlaceholder.Text = isAr ? 
                "ابحث عن أي لعبة أو إضافة على ستيم بالاسم (مثال: Arc Raiders, GTA V, Elden Ring) أو AppID..." : 
                "Search any Steam game or DLC by name (e.g. Arc Raiders, Black Myth, Elden Ring) or enter AppID...";
            btnAddInput.Content = isAr ? "+ إضافة AppID" : "+ Add AppID";
            btnBulk.Content = isAr ? "📥 استيراد دفعة" : "📥 Bulk Import";

            lblLibraryTitle.Text = isAr ? "🎮 قائمة العناصر المفتوحة في APPLIST:" : "🎮 UNLOCKED STEAM APPLIST:";
            txtFilterInput.ToolTip = isAr ? "تصفية الألعاب المضافة..." : "Filter library...";
            btnToggleAll.Content = isAr ? "🔄 تبديل الكل" : "🔄 Toggle All";
            btnClearAll.Content = isAr ? "🗑 مسح الكل" : "🗑 Clear All";

            txtEmptyState.Text = isAr ? 
                "لا توجد عناصر مختارة حتى الآن.\nاستخدم شريط البحث بالأعلى للبحث عن أي لعبة بالاسم أو AppID لإضافتها إلى مكتبتك." : 
                "No games selected yet.\nSearch above by game title or enter AppID to unlock it in your library.";

            UpdateTabButtonStyles();
            UpdateStatusUI();
            RenderGamesUI();
        }

        // ==========================================
        // --- LIBRARY CARDS & CASCADE ANIMATION ---
        // ==========================================
        private void RenderGamesUI()
        {
            gamesWrapPanel.Children.Clear();
            int enabledCount = 0;
            string filter = txtFilterInput != null ? txtFilterInput.Text.Trim().ToLower() : "";

            if (gamesList.Count == 0)
            {
                txtEmptyState.Visibility = Visibility.Visible;
            }
            else
            {
                txtEmptyState.Visibility = Visibility.Collapsed;
            }

            int cardIndex = 0;
            foreach (var g in gamesList)
            {
                if (g.enabled) enabledCount++;

                bool isDlc = g.CheckIsDLC();

                // Tab Filter
                if (currentFilterTab == "games" && isDlc) continue;
                if (currentFilterTab == "dlcs" && !isDlc) continue;

                // Text Search Filter
                if (!string.IsNullOrEmpty(filter))
                {
                    if (!g.name.ToLower().Contains(filter) && !g.appId.ToString().Contains(filter))
                        continue;
                }

                var card = CreateGameCard(g);
                gamesWrapPanel.Children.Add(card);

                // === VIOLENT WATERFALL ENTRANCE ANIMATION ===
                var tg = card.RenderTransform as TransformGroup;
                if (tg != null)
                {
                    TranslateTransform tt = null;
                    foreach (var t in tg.Children)
                    {
                        if (t is TranslateTransform) { tt = (TranslateTransform)t; break; }
                    }

                    if (tt != null)
                    {
                        tt.Y = 22;
                        card.Opacity = 0.0;

                        var timeDelay = TimeSpan.FromMilliseconds(Math.Min(cardIndex * 25, 300));
                        var daY = new DoubleAnimation(22, 0, TimeSpan.FromMilliseconds(240)) {
                            BeginTime = timeDelay,
                            DecelerationRatio = 0.6
                        };
                        tt.BeginAnimation(TranslateTransform.YProperty, daY);

                        var daOp = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(240)) {
                            BeginTime = timeDelay
                        };
                        card.BeginAnimation(UIElement.OpacityProperty, daOp);
                    }
                }
                cardIndex++;
            }

            txtGamesCount.Text = enabledCount.ToString();
        }

        private Border CreateGameCard(GameItem game)
        {
            bool isAr = (settings.lang == "ar");
            bool isActuallyDlc = game.CheckIsDLC();

            var card = new Border() {
                Width = 268,
                Margin = new Thickness(0, 0, 14, 14),
                Background = new SolidColorBrush(Color.FromRgb(14, 19, 30)),
                BorderBrush = isActuallyDlc ? 
                    new SolidColorBrush(Color.FromArgb(70, 245, 158, 11)) : 
                    new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                ClipToBounds = true
            };

            // === CARD HOVER PHYSICS & GLOW ANIMATION ===
            var tg = new TransformGroup();
            var st = new ScaleTransform(1.0, 1.0);
            var tt = new TranslateTransform(0, 0);
            tg.Children.Add(st);
            tg.Children.Add(tt);
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            card.RenderTransform = tg;

            var cardShadow = new DropShadowEffect() {
                BlurRadius = 0,
                ShadowDepth = 2,
                Opacity = 0.85,
                Color = isActuallyDlc ? Color.FromRgb(245, 158, 11) : Color.FromRgb(16, 185, 129)
            };
            card.Effect = cardShadow;

            card.MouseEnter += (s, e) => {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 1.038, TimeSpan.FromMilliseconds(140)) { DecelerationRatio = 0.5 });
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 1.038, TimeSpan.FromMilliseconds(140)) { DecelerationRatio = 0.5 });
                cardShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(0, 22, TimeSpan.FromMilliseconds(140)));
                card.BorderBrush = isActuallyDlc ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(16, 185, 129));
            };

            card.MouseLeave += (s, e) => {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.038, 1.0, TimeSpan.FromMilliseconds(160)) { AccelerationRatio = 0.5 });
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.038, 1.0, TimeSpan.FromMilliseconds(160)) { AccelerationRatio = 0.5 });
                cardShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(22, 0, TimeSpan.FromMilliseconds(160)));
                card.BorderBrush = isActuallyDlc ? new SolidColorBrush(Color.FromArgb(70, 245, 158, 11)) : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            };

            var stack = new StackPanel();

            // Image Container with Badges Overlay
            var imgContainer = new Grid() { Height = 120, Background = new SolidColorBrush(Color.FromRgb(5, 8, 12)) };
            var img = new Image() { Stretch = Stretch.UniformToFill };
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(game.image, UriKind.RelativeOrAbsolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                img.Source = bmp;
            }
            catch { }
            imgContainer.Children.Add(img);

            // Badges Overlay Row
            var badgesRow = new DockPanel() { Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Top };

            // 1. CLICKABLE TYPE BADGE (Game vs DLC with instant 1-click toggle!)
            var typeBadge = new Border() {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Background = isActuallyDlc ? 
                    new SolidColorBrush(Color.FromRgb(245, 158, 11)) : // Amber for DLC
                    new SolidColorBrush(Color.FromRgb(16, 185, 129)),  // Emerald for Game
                Cursor = Cursors.Hand,
                ToolTip = isAr ? "انقر للتبديل الفوري بين لعبة كاملة وإضافة (DLC)" : "Click to instantly toggle between Game and DLC"
            };
            var txtBadgeType = new TextBlock() {
                Text = isActuallyDlc ? (isAr ? "🧩 DLC إضافة" : "🧩 DLC") : (isAr ? "🎮 لعبة كاملة" : "🎮 GAME"),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10))
            };
            typeBadge.Child = txtBadgeType;

            // One-click toggle between Game & DLC!
            typeBadge.MouseDown += (s, e) => {
                game.isDLC = !isActuallyDlc;
                game.type = game.isDLC ? "dlc" : "game";
                SaveGames();
                UpdateTabButtonStyles();
                RenderGamesUI();
            };

            DockPanel.SetDock(typeBadge, isAr ? Dock.Right : Dock.Left);
            badgesRow.Children.Add(typeBadge);

            // 2. Family Sharing indicator (Only on Base Games)
            if (!isActuallyDlc)
            {
                var fsCardBadge = new Border() {
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(7, 3, 7, 3),
                    Background = game.familyShare ?
                        new SolidColorBrush(Color.FromArgb(200, 15, 23, 42)) :
                        new SolidColorBrush(Color.FromRgb(239, 68, 68)), // Red if No Family Share!
                    BorderBrush = game.familyShare ?
                        new SolidColorBrush(Color.FromArgb(120, 16, 185, 129)) :
                        new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = isAr ? HorizontalAlignment.Left : HorizontalAlignment.Right
                };
                var txtFsCard = new TextBlock() {
                    Text = game.familyShare ?
                        (isAr ? "👥 عائلي" : "👥 Family") :
                        (isAr ? "🚫 بدون عائلي" : "🚫 No Share"),
                    FontSize = 9.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = game.familyShare ?
                        new SolidColorBrush(Color.FromRgb(110, 231, 183)) :
                        new SolidColorBrush(Color.FromRgb(255, 255, 255))
                };
                fsCardBadge.Child = txtFsCard;
                badgesRow.Children.Add(fsCardBadge);
            }

            imgContainer.Children.Add(badgesRow);
            stack.Children.Add(imgContainer);

            // Details Container
            var detailsStack = new StackPanel() { Margin = new Thickness(12) };
            var txtCardTitle = new TextBlock() {
                Text = game.name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 6)
            };
            detailsStack.Children.Add(txtCardTitle);

            var metaRow = new DockPanel() { Margin = new Thickness(0, 0, 0, 10) };
            
            // AppID Pill
            var pillId = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(30, 6, 182, 212)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 6, 182, 212)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            pillId.Child = new TextBlock() {
                Text = "ID: " + game.appId,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(6, 182, 212)),
                FontFamily = new FontFamily("Consolas")
            };
            DockPanel.SetDock(pillId, Dock.Left);
            metaRow.Children.Add(pillId);

            // Add DLCs Button (Strictly Base Games ONLY)
            if (!isActuallyDlc)
            {
                var btnDlc = new Button() {
                    Content = isAr ? "+ إضافة الـ DLCs" : "+ Add DLCs",
                    FontSize = 10.5,
                    Background = new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)),
                    Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(60, 245, 158, 11)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6, 2, 6, 2),
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                btnDlc.Click += (s, e) => FetchAndAddDLCs(game.appId, game.name);
                metaRow.Children.Add(btnDlc);
            }
            else
            {
                var dlcTag = new Border() {
                    Background = new SolidColorBrush(Color.FromArgb(30, 245, 158, 11)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(60, 245, 158, 11)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                dlcTag.Child = new TextBlock() {
                    Text = isAr ? "إضافة تابعة" : "Linked DLC",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36))
                };
                metaRow.Children.Add(dlcTag);
            }

            detailsStack.Children.Add(metaRow);

            // Actions row: Active Switch + Remove Button
            var actionRow = new DockPanel();
            var chkEnable = new CheckBox() {
                IsChecked = game.enabled,
                Content = game.enabled ? (isAr ? "مفعل" : "Active") : (isAr ? "معطل" : "Disabled"),
                Foreground = game.enabled ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                VerticalAlignment = VerticalAlignment.Center
            };
            chkEnable.Checked += (s, e) => {
                game.enabled = true;
                chkEnable.Content = isAr ? "مفعل" : "Active";
                chkEnable.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                SaveGames();
                UpdateStatusUI();
            };
            chkEnable.Unchecked += (s, e) => {
                game.enabled = false;
                chkEnable.Content = isAr ? "معطل" : "Disabled";
                chkEnable.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                SaveGames();
                UpdateStatusUI();
            };
            DockPanel.SetDock(chkEnable, Dock.Left);
            actionRow.Children.Add(chkEnable);

            var btnDel = new Button() {
                Content = isAr ? "🗑 حذف" : "🗑 Remove",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(4, 2, 4, 2)
            };
            btnDel.Click += (s, e) => {
                gamesList.Remove(game);
                SaveGames();
                UpdateTabButtonStyles();
                RenderGamesUI();
            };
            actionRow.Children.Add(btnDel);

            detailsStack.Children.Add(actionRow);
            stack.Children.Add(detailsStack);
            card.Child = stack;

            return card;
        }

        private void AddGameFromInput()
        {
            string raw = txtGameInput.Text.Trim();
            if (string.IsNullOrEmpty(raw)) return;

            int id;
            if (int.TryParse(raw, out id) && id > 0)
            {
                AddGameById(id, null);
                txtGameInput.Text = "";
            }
            else
            {
                ExecuteLiveSearch(raw);
            }
        }

        private void AddGameById(int appId, string hintName)
        {
            bool isAr = (settings.lang == "ar");

            foreach (var g in gamesList)
            {
                if (g.appId == appId)
                {
                    MessageBox.Show(
                        isAr ? ("اللعبة برقم " + appId + " موجودة بالفعل في مكتبتك.") : ("Game with AppID " + appId + " is already in your library."),
                        isAr ? "موجودة مسبقاً" : "Already Exists",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            ThreadPool.QueueUserWorkItem((state) => {
                string name = hintName;
                string headerImg = string.Format("https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{0}/header.jpg", appId);
                bool isDlcType = false;
                bool isFs = true;

                if (appId == 1174180 || appId == 271590 || appId == 553850 || appId == 359550 || appId == 1222670) isFs = false;

                try
                {
                    string url = string.Format("https://store.steampowered.com/api/appdetails?appids={0}&l=english", appId);
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = "Mozilla/5.0";
                    req.Timeout = 5000;
                    using (var resp = req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        var js = new JavaScriptSerializer();
                        var dict = js.Deserialize<Dictionary<string, object>>(json);
                        if (dict != null && dict.ContainsKey(appId.ToString()))
                        {
                            var appObj = dict[appId.ToString()] as Dictionary<string, object>;
                            if (appObj != null && appObj.ContainsKey("success") && (bool)appObj["success"])
                            {
                                var data = appObj["data"] as Dictionary<string, object>;
                                if (data != null)
                                {
                                    if (data.ContainsKey("name")) name = data["name"].ToString();
                                    if (data.ContainsKey("header_image")) headerImg = data["header_image"].ToString();
                                    
                                    if (data.ContainsKey("type"))
                                    {
                                        string t = data["type"].ToString().ToLower();
                                        if (t == "dlc" || t == "music" || t == "soundtrack" || t == "series") isDlcType = true;
                                    }
                                    if (data.ContainsKey("fullgame")) isDlcType = true;

                                    string notice = (data.ContainsKey("ext_user_account_notice") ? data["ext_user_account_notice"].ToString() : "") + " " +
                                                    (data.ContainsKey("drm_notice") ? data["drm_notice"].ToString() : "");
                                    notice = notice.ToLower();
                                    if (notice.Contains("rockstar") || notice.Contains("ea app") || notice.Contains("ubisoft") || notice.Contains("playstation network"))
                                    {
                                        isFs = false;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(name)) name = "App " + appId;

                string lowerCheck = name.ToLower();
                if (lowerCheck.Contains("dlc") || lowerCheck.Contains("expansion") || lowerCheck.Contains("pack") ||
                    lowerCheck.Contains("pass") || lowerCheck.Contains("upgrade") || lowerCheck.Contains("set") ||
                    lowerCheck.Contains("soundtrack") || lowerCheck.Contains("ost"))
                {
                    isDlcType = true;
                }

                Dispatcher.Invoke((Action)(() => {
                    gamesList.Insert(0, new GameItem() {
                        appId = appId,
                        name = name,
                        image = headerImg,
                        enabled = true,
                        isDLC = isDlcType,
                        type = isDlcType ? "dlc" : "game",
                        familyShare = isFs
                    });
                    SaveGames();
                    UpdateTabButtonStyles();
                    RenderGamesUI();
                }));
            });
        }

        private void FetchAndAddDLCs(int parentAppId, string parentName)
        {
            bool isAr = (settings.lang == "ar");

            ThreadPool.QueueUserWorkItem((state) => {
                var dlcIds = new List<int>();
                try
                {
                    string url = string.Format("https://store.steampowered.com/api/appdetails?appids={0}&l=english", parentAppId);
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = "Mozilla/5.0";
                    req.Timeout = 5000;
                    using (var resp = req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        var js = new JavaScriptSerializer();
                        var dict = js.Deserialize<Dictionary<string, object>>(json);
                        if (dict != null && dict.ContainsKey(parentAppId.ToString()))
                        {
                            var appObj = dict[parentAppId.ToString()] as Dictionary<string, object>;
                            if (appObj != null && (bool)appObj["success"])
                            {
                                var data = appObj["data"] as Dictionary<string, object>;
                                if (data != null && data.ContainsKey("dlc"))
                                {
                                    var arr = data["dlc"] as ArrayList;
                                    if (arr != null)
                                    {
                                        foreach (var item in arr)
                                        {
                                            dlcIds.Add(Convert.ToInt32(item));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                Dispatcher.Invoke((Action)(() => {
                    if (dlcIds.Count == 0)
                    {
                        MessageBox.Show(
                            isAr ? ("لم يتم العثور على أي إضافات (DLCs) لـ " + parentName + " على متجر ستيم.") : ("No DLCs found for " + parentName + " on Steam Store."),
                            isAr ? "إضافات اللعبة" : "DLCs",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    int added = 0;
                    foreach (var dlcId in dlcIds)
                    {
                        bool exists = false;
                        foreach (var g in gamesList) { if (g.appId == dlcId) { exists = true; break; } }
                        if (!exists)
                        {
                            gamesList.Insert(0, new GameItem() {
                                appId = dlcId,
                                name = parentName + " - DLC (" + dlcId + ")",
                                image = string.Format("https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{0}/header.jpg", dlcId),
                                enabled = true,
                                isDLC = true,
                                type = "dlc"
                            });
                            added++;
                        }
                    }

                    SaveGames();
                    UpdateTabButtonStyles();
                    RenderGamesUI();
                    MessageBox.Show(
                        isAr ? string.Format("تم إضافة وتفعيل {0} إضافة (DLC) بنجاح لـ {1}!", added, parentName) : string.Format("Successfully unlocked and added {0} DLCs for {1}!", added, parentName),
                        isAr ? "تمت إضافة الـ DLCs" : "DLCs Added",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }));
            });
        }

        private void OpenBulkImportDialog()
        {
            bool isAr = (settings.lang == "ar");

            var win = new Window() {
                Title = isAr ? "استيراد دفعة ألعاب وإضافات (Bulk Import)" : "Bulk Import AppIDs",
                Width = 520,
                Height = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(15, 20, 30)),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                FlowDirection = isAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
            };

            var sp = new StackPanel() { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock() {
                Text = isAr ? "الصق أرقام AppIDs مفصولة بفواصل أو مسافات أو أسطر جديدة:" : "Paste AppIDs separated by commas, spaces, or new lines:",
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            });

            var tb = new TextBox() {
                Height = 180,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)),
                Foreground = new SolidColorBrush(Color.FromRgb(167, 243, 208)),
                FontFamily = new FontFamily("Consolas"),
                Padding = new Thickness(8),
                FlowDirection = FlowDirection.LeftToRight
            };
            sp.Children.Add(tb);

            var btnImport = new Button() {
                Content = isAr ? "استيراد كل الـ AppIDs الآن" : "Import AppIDs Now",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 12, 0, 0),
                Cursor = Cursors.Hand
            };
            btnImport.Click += (s, e) => {
                var matches = Regex.Matches(tb.Text, @"\b\d{2,8}\b");
                int added = 0;
                foreach (Match m in matches)
                {
                    int aid = int.Parse(m.Value);
                    bool exists = false;
                    foreach (var g in gamesList) { if (g.appId == aid) { exists = true; break; } }
                    if (!exists)
                    {
                        gamesList.Insert(0, new GameItem() {
                            appId = aid,
                            name = "App " + aid,
                            image = string.Format("https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{0}/header.jpg", aid),
                            enabled = true,
                            type = "game"
                        });
                        added++;
                    }
                }
                SaveGames();
                UpdateTabButtonStyles();
                RenderGamesUI();
                win.Close();
                MessageBox.Show(
                    isAr ? string.Format("تم استيراد {0} عنصر بنجاح!", added) : string.Format("Successfully imported {0} items!", added),
                    isAr ? "اكتمل الاستيراد" : "Bulk Import",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            };
            sp.Children.Add(btnImport);

            win.Content = sp;
            win.ShowDialog();
        }

        // =========================================================================
        // --- SMART PAYLOAD RESOLUTION (DISK OVERRIDE -> EMBEDDED FALLBACK) ---
        // =========================================================================
        private byte[] GetPayloadBytes(string resourceName, out string sourceDesc)
        {
            string directFile = Path.Combine(appDir, resourceName);
            if (File.Exists(directFile))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(directFile);
                    if (data != null && data.Length > 0)
                    {
                        sourceDesc = "Local Override (" + resourceName + ")";
                        return data;
                    }
                }
                catch { }
            }

            string stealthFolderFile = Path.Combine(appDir, "StealthMode", resourceName);
            if (File.Exists(stealthFolderFile))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(stealthFolderFile);
                    if (data != null && data.Length > 0)
                    {
                        sourceDesc = "Local StealthMode/" + resourceName;
                        return data;
                    }
                }
                catch { }
            }

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        byte[] data = new byte[stream.Length];
                        stream.Read(data, 0, data.Length);
                        sourceDesc = "Embedded (Self-Contained)";
                        return data;
                    }
                }
            }
            catch { }

            sourceDesc = "Not Found";
            return null;
        }

        private void RefreshPayloadSourceUI()
        {
            if (txtPayloadSource == null) return;
            string src;
            GetPayloadBytes("user32.dll", out src);
            txtPayloadSource.Text = src;
            if (src.StartsWith("Local"))
            {
                txtPayloadSource.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
            }
            else
            {
                txtPayloadSource.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
        }

        // =========================================================================
        // --- LAUNCH & INJECTION ENGINE ---
        // =========================================================================
        private void LaunchSteamSession()
        {
            bool isAr = (settings.lang == "ar");

            if (string.IsNullOrEmpty(steamDir) || !Directory.Exists(steamDir))
            {
                MessageBox.Show(
                    isAr ? "لم يتم العثور على مجلد Steam! يرجى التأكد من تثبيت Steam أو تحديده في الإعدادات." : "Steam directory was not found! Please verify your path in Settings.",
                    isAr ? "خطأ في المسار" : "Path Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string steamExe = Path.Combine(steamDir, "steam.exe");
            if (!File.Exists(steamExe))
            {
                MessageBox.Show(
                    isAr ? ("تعذر العثور على steam.exe في: " + steamDir) : ("Could not find steam.exe at: " + steamDir),
                    isAr ? "ملف غير موجود" : "File Missing",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 1. Terminate existing Steam instances
            if (IsSteamRunning())
            {
                StopSteamProcess();
            }

            // 2. Clear AppCache if selected
            if (chkClearCache.IsChecked == true)
            {
                string cacheDir = Path.Combine(steamDir, "appcache");
                try
                {
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                    }
                }
                catch { }

                string src;
                byte[] delCacheExe = GetPayloadBytes("DeleteSteamAppCache.exe", out src);
                if (delCacheExe != null)
                {
                    string tmpExe = Path.Combine(steamDir, "DeleteSteamAppCache.exe");
                    try
                    {
                        File.WriteAllBytes(tmpExe, delCacheExe);
                        var p = Process.Start(tmpExe);
                        p.WaitForExit(3000);
                        if (File.Exists(tmpExe)) File.Delete(tmpExe);
                    }
                    catch { }
                }
            }

            // 3. Sync AppList to Steam/AppList
            SyncAppListIni();
            string localAppList = Path.Combine(appDir, "AppList");
            string steamAppList = Path.Combine(steamDir, "AppList");
            try
            {
                if (Directory.Exists(steamAppList)) Directory.Delete(steamAppList, true);
                Directory.CreateDirectory(steamAppList);

                if (Directory.Exists(localAppList))
                {
                    foreach (var file in Directory.GetFiles(localAppList))
                    {
                        File.Copy(file, Path.Combine(steamAppList, Path.GetFileName(file)), true);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    isAr ? ("فشل نسخ مجلد AppList إلى Steam: " + ex.Message) : ("Failed copying AppList to Steam: " + ex.Message),
                    isAr ? "خطأ في الإعداد" : "Setup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 4. Inject Stealth DLL
            bool isFamilies = (cmbMode.SelectedIndex == 1);
            string targetDllName = isFamilies ? "user32SF.dll" : "user32.dll";
            string srcDesc;
            byte[] dllBytes = GetPayloadBytes(targetDllName, out srcDesc);

            if (dllBytes == null || dllBytes.Length == 0)
            {
                MessageBox.Show(
                    isAr ? string.Format("تعذر استخراج ملف الحقن {0}! تأكد من وجوده بجانب البرنامج.", targetDllName) : string.Format("Failed to extract stealth payload {0}! Make sure the file exists.", targetDllName),
                    isAr ? "خطأ في الملفات" : "Payload Missing",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string destDll = Path.Combine(steamDir, "user32.dll");
            try
            {
                File.WriteAllBytes(destDll, dllBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    isAr ? ("فشل حقن user32.dll في مجلد Steam: " + ex.Message) : ("Failed injecting user32.dll into Steam folder: " + ex.Message),
                    isAr ? "خطأ في الحقن" : "Injection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 5. Launch Steam cleanly
            string args = txtSteamArgs.Text.Trim();
            try
            {
                Process.Start(steamExe, args);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    isAr ? ("فشل تشغيل Steam: " + ex.Message) : ("Failed to launch Steam: " + ex.Message),
                    isAr ? "خطأ في التشغيل" : "Launch Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            isInjected = true;
            UpdateStatusUI();

            // 6. Background Monitor Thread (Auto cleanup when Steam exits)
            monitorThread = new Thread(() => {
                for (int i = 0; i < 25; i++)
                {
                    if (IsSteamRunning()) break;
                    Thread.Sleep(500);
                }

                while (true)
                {
                    if (!isInjected) break;
                    if (!IsSteamRunning())
                    {
                        Thread.Sleep(1500);
                        DeleteInjectedFiles();
                        isInjected = false;
                        Dispatcher.Invoke((Action)(() => UpdateStatusUI()));
                        break;
                    }
                    Thread.Sleep(1000);
                }
            });
            monitorThread.IsBackground = true;
            monitorThread.Start();
        }

        private void StopSteamSession()
        {
            bool isAr = (settings.lang == "ar");
            isInjected = false;
            StopSteamProcess();
            DeleteInjectedFiles();
            UpdateStatusUI();
            MessageBox.Show(
                isAr ? "تم إغلاق Steam وحذف جميع ملفات الحقن فوراً. مجلد Steam نظيف 100%." : "Steam was closed and all GreenLuma files were removed. Your Steam folder is 100% clean.",
                isAr ? "انتهت الجلسة بأمان" : "Session Finished",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteInjectedFiles()
        {
            if (string.IsNullOrEmpty(steamDir)) return;

            string user32 = Path.Combine(steamDir, "user32.dll");
            string user32sf = Path.Combine(steamDir, "user32SF.dll");
            string appList = Path.Combine(steamDir, "AppList");

            for (int i = 0; i < 6; i++)
            {
                try
                {
                    if (File.Exists(user32)) File.Delete(user32);
                    if (File.Exists(user32sf)) File.Delete(user32sf);
                    if (Directory.Exists(appList)) Directory.Delete(appList, true);
                    break;
                }
                catch
                {
                    Thread.Sleep(800);
                }
            }
        }

        private void PurgeAllArtifacts()
        {
            bool isAr = (settings.lang == "ar");
            if (string.IsNullOrEmpty(steamDir)) return;

            string confirmMsg = isAr ? 
                "سيتم إغلاق Steam وحذف أي ملفات متعلقة بـ GreenLuma من مجلد Steam تماماً. هل تريد المتابعة؟" : 
                "This will close Steam and purge any GreenLuma files (both Stealth and Normal mode) from your Steam folder. Continue?";

            if (MessageBox.Show(confirmMsg, isAr ? "تنظيف شامل" : "Purge Artifacts", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            StopSteamProcess();

            string[] artifacts = new string[] {
                "user32.dll", "user32SF.dll", "AppList", "DLLInjector.exe", "DLLInjector.ini",
                "GreenLumaSettings_2026.exe", "GreenLuma_2026_x64.dll", "GreenLuma_2026_x86.dll",
                "GreenLuma_2026.log", "GreenLuma2026_Files", "NoQuestion.bin", "DeleteSteamAppCache.exe"
            };

            int purged = 0;
            foreach (var item in artifacts)
            {
                string target = Path.Combine(steamDir, item);
                try
                {
                    if (File.Exists(target)) { File.Delete(target); purged++; }
                    else if (Directory.Exists(target)) { Directory.Delete(target, true); purged++; }
                }
                catch { }
            }

            isInjected = false;
            UpdateStatusUI();
            MessageBox.Show(
                isAr ? string.Format("تم تنظيف مجلد Steam بنجاح! تم حذف {0} عناصر.", purged) : string.Format("Steam directory cleaned successfully! Purged {0} items.", purged),
                isAr ? "اكتمل التنظيف" : "Clean Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ==========================================
        // --- GITHUB RELEASE UPDATE CHECKER ---
        // ==========================================
        private void CheckForUpdatesAsync(bool manualUserClick)
        {
            ThreadPool.QueueUserWorkItem((s) => {
                string latestTag = "";
                string releaseUrl = "https://github.com/" + GITHUB_REPO + "/releases";

                try
                {
                    string apiUrl = "https://api.github.com/repos/" + GITHUB_REPO + "/releases/latest";
                    var req = (HttpWebRequest)WebRequest.Create(apiUrl);
                    req.UserAgent = "GreenLumaManager/" + CURRENT_VERSION;
                    req.Timeout = 6000;
                    using (var resp = req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        var js = new JavaScriptSerializer();
                        var dict = js.Deserialize<Dictionary<string, object>>(json);
                        if (dict != null && dict.ContainsKey("tag_name"))
                        {
                            latestTag = dict["tag_name"].ToString().Trim();
                            if (dict.ContainsKey("html_url")) releaseUrl = dict["html_url"].ToString();
                        }
                    }
                }
                catch { }

                Dispatcher.Invoke((Action)(() => {
                    bool isAr = (settings.lang == "ar");

                    if (!string.IsNullOrEmpty(latestTag) && latestTag != CURRENT_VERSION)
                    {
                        pendingUpdateUrl = releaseUrl;
                        txtUpdateBanner.Text = isAr ? 
                            string.Format("🎉 يتوفر إصدار جديد ({0}) على GitHub! إصدارك الحالي هو {1}.", latestTag, CURRENT_VERSION) :
                            string.Format("🎉 New update available ({0}) on GitHub! You are running {1}.", latestTag, CURRENT_VERSION);
                        updateBannerBorder.Visibility = Visibility.Visible;

                        if (manualUserClick)
                        {
                            if (MessageBox.Show(
                                isAr ? string.Format("يتوفر إصدار أحدث ({0}). هل تود فتح صفحة التحميل على GitHub؟", latestTag) :
                                       string.Format("A newer release ({0}) is available. Open download page on GitHub?", latestTag),
                                isAr ? "تحديث متاح" : "Update Available",
                                MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                            {
                                try { Process.Start(releaseUrl); } catch { }
                            }
                        }
                    }
                    else if (manualUserClick)
                    {
                        MessageBox.Show(
                            isAr ? string.Format("أنت تستخدم أحدث إصدار ({0}). برنامجك محدث بالكامل!", CURRENT_VERSION) :
                                   string.Format("You are running the latest version ({0}). Your software is up to date!", CURRENT_VERSION),
                            isAr ? "لا توجد تحديثات" : "Up to Date",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }));
            });
        }

        private void DetectSteamPath()
        {
            if (!string.IsNullOrEmpty(settings.customSteamPath) && Directory.Exists(settings.customSteamPath))
            {
                steamDir = settings.customSteamPath;
                txtSteamPath.Text = steamDir;
                return;
            }

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                    {
                        string val = key.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(val))
                        {
                            val = Path.GetFullPath(val);
                            if (File.Exists(Path.Combine(val, "steam.exe")))
                            {
                                steamDir = val;
                                txtSteamPath.Text = steamDir;
                                return;
                            }
                        }
                    }
                }
            }
            catch { }

            string[] fallbacks = new string[] { @"E:\steam", @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam", @"D:\Steam" };
            foreach (var fb in fallbacks)
            {
                if (File.Exists(Path.Combine(fb, "steam.exe")))
                {
                    steamDir = fb;
                    txtSteamPath.Text = steamDir;
                    return;
                }
            }

            txtSteamPath.Text = (settings.lang == "ar") ? "غير مكتشف (يرجى التحديد في الإعدادات)" : "Not detected (Specify in Settings)";
        }

        private bool IsSteamRunning()
        {
            var procs = Process.GetProcessesByName("steam");
            return procs.Length > 0;
        }

        private bool IsSteamInjected()
        {
            if (isInjected) return true;
            if (string.IsNullOrEmpty(steamDir)) return false;

            string[] markers = new string[] {
                "user32.dll",
                "user32SF.dll",
                "GreenLuma_2026_x64.dll",
                "GreenLuma_2026_x86.dll",
                "DLLInjector.exe",
                "DLLInjector.ini"
            };

            foreach (var m in markers)
            {
                if (File.Exists(Path.Combine(steamDir, m))) return true;
            }

            string appListIni = Path.Combine(steamDir, "AppList", "AppList.ini");
            if (File.Exists(appListIni)) return true;

            string lumaFiles = Path.Combine(steamDir, "GreenLuma2026_Files");
            if (Directory.Exists(lumaFiles)) return true;

            return false;
        }

        private void StopSteamProcess()
        {
            if (!IsSteamRunning()) return;

            if (!string.IsNullOrEmpty(steamDir))
            {
                string exe = Path.Combine(steamDir, "steam.exe");
                if (File.Exists(exe))
                {
                    try { Process.Start(exe, "-shutdown"); } catch { }
                }
            }

            for (int i = 0; i < 8; i++)
            {
                if (!IsSteamRunning()) return;
                Thread.Sleep(500);
            }

            foreach (var p in Process.GetProcessesByName("steam"))
            {
                try { p.Kill(); } catch { }
            }
            Thread.Sleep(500);
        }

        private void LoadSettings()
        {
            string file = Path.Combine(appDir, "settings.json");
            if (File.Exists(file))
            {
                try
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    var js = new JavaScriptSerializer();
                    settings = js.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch { settings = new AppSettings(); }
            }

            if (settings.mode == "families") cmbMode.SelectedIndex = 1;
            else cmbMode.SelectedIndex = 0;

            chkClearCache.IsChecked = settings.clearAppCache;
            if (!string.IsNullOrEmpty(settings.steamArgs)) txtSteamArgs.Text = settings.steamArgs;
            if (string.IsNullOrEmpty(settings.lang)) settings.lang = "en";
        }

        private void SaveSettings()
        {
            settings.mode = cmbMode.SelectedIndex == 1 ? "families" : "standard";
            settings.clearAppCache = chkClearCache.IsChecked == true;
            settings.steamArgs = txtSteamArgs.Text.Trim();

            string file = Path.Combine(appDir, "settings.json");
            try
            {
                var js = new JavaScriptSerializer();
                File.WriteAllText(file, js.Serialize(settings), Encoding.UTF8);
            }
            catch { }
        }

        private void LoadGames()
        {
            string file = Path.Combine(appDir, "games.json");
            if (File.Exists(file))
            {
                try
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    var js = new JavaScriptSerializer();
                    gamesList = js.Deserialize<List<GameItem>>(json) ?? new List<GameItem>();
                }
                catch { gamesList = new List<GameItem>(); }
            }

            UpdateTabButtonStyles();
            RenderGamesUI();
        }

        private void SaveGames()
        {
            string file = Path.Combine(appDir, "games.json");
            try
            {
                var js = new JavaScriptSerializer();
                File.WriteAllText(file, js.Serialize(gamesList), Encoding.UTF8);
            }
            catch { }

            SyncAppListIni();
        }

        private void SyncAppListIni()
        {
            var enabledIds = new List<int>();
            foreach (var g in gamesList)
            {
                if (g.enabled) enabledIds.Add(g.appId);
            }

            var sb = new StringBuilder();
            sb.AppendLine("[AppList]");

            for (int i = 0; i < Math.Min(enabledIds.Count, DEFAULT_SLOTS.Length); i++)
            {
                sb.AppendLine(string.Format("{0} = {1}", DEFAULT_SLOTS[i], enabledIds[i]));
            }

            if (enabledIds.Count > DEFAULT_SLOTS.Length)
            {
                for (int i = DEFAULT_SLOTS.Length; i < enabledIds.Count; i++)
                {
                    sb.AppendLine(string.Format("{0} = {0}", enabledIds[i]));
                }
            }

            string content = sb.ToString();

            string localAppListDir = Path.Combine(appDir, "AppList");
            try
            {
                Directory.CreateDirectory(localAppListDir);
                File.WriteAllText(Path.Combine(localAppListDir, "AppList.ini"), content, Encoding.UTF8);
            }
            catch { }

            if (!string.IsNullOrEmpty(steamDir))
            {
                string steamAppList = Path.Combine(steamDir, "AppList", "AppList.ini");
                if (File.Exists(steamAppList))
                {
                    try { File.WriteAllText(steamAppList, content, Encoding.UTF8); } catch { }
                }
            }
        }

        private void UpdateStatusUI()
        {
            bool isAr = (settings.lang == "ar");
            bool running = IsSteamRunning();
            bool injected = IsSteamInjected();

            if (injected)
            {
                borderStatusDot.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                borderStatusPulseRing.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                txtStatus.Text = running ? 
                    (isAr ? "🟢 ستيم يعمل (محقون بـ GreenLuma)" : "🟢 Steam Running (GreenLuma Injected)") : 
                    (isAr ? "🟡 الملفات محقونة (في انتظار فتح ستيم)" : "🟡 Files Injected (Waiting for Steam)");
                btnLaunch.Visibility = Visibility.Collapsed;
                btnStop.Visibility = Visibility.Visible;
            }
            else
            {
                borderStatusDot.Background = running ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));
                borderStatusPulseRing.Background = running ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));
                txtStatus.Text = running ? 
                    (isAr ? "ستيم يعمل (أصلي ونظيف)" : "Steam Running (Clean / Original)") : 
                    (isAr ? "ستيم مغلق (المجلد نظيف 100%)" : "Steam Inactive (Folder Clean)");
                btnLaunch.Visibility = Visibility.Visible;
                btnStop.Visibility = Visibility.Collapsed;
            }

            RefreshPayloadSourceUI();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isInjected)
            {
                DeleteInjectedFiles();
            }
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            var app = new Application();
            var window = new MainWindow();
            app.Run(window);
        }
    }
}
