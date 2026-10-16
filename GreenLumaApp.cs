using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
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
        public int parentAppId { get; set; }
        public string parentName { get; set; }

        public GameItem()
        {
            familyShare = true;
            enabled = true;
            type = "game";
            parentAppId = 0;
            parentName = "";
        }

        public bool CheckIsDLC()
        {
            if (isDLC) return true;
            if (parentAppId > 0) return true;
            if (!string.IsNullOrEmpty(parentName)) return true;
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
                    lower.Contains("deluxe edition upgrade") || lower.Contains("deluxe upgrade") ||
                    lower.Contains("soundtrack") || lower.Contains(" ost") ||
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

    public class InstalledGameInfo
    {
        public int appId { get; set; }
        public string name { get; set; }
        public string installDir { get; set; }
        public long sizeBytes { get; set; }
        public bool isAlreadyAdded { get; set; }
        public bool isSelected { get; set; }
    }

    public class AppSettings
    {
        public string customSteamPath { get; set; }
        public string mode { get; set; } // "standard" or "families"
        public bool clearAppCache { get; set; }
        public string steamArgs { get; set; }
        public string lang { get; set; } // "en" or "ar"
        public bool firstTimeGuideShown { get; set; }
        public string viewMode { get; set; } // "cards" or "list"
        public bool isMiniMode { get; set; }

        public AppSettings()
        {
            mode = "families";
            steamArgs = "";
            lang = "en";
            clearAppCache = true;
            firstTimeGuideShown = false;
            viewMode = "cards";
            isMiniMode = false;
        }
    }

    public class MainWindow : Window
    {
        public const string CURRENT_VERSION = "v1.6.0";
        public const string GITHUB_REPO = "Mayer-ELbot/GreenLuma-Stealth-Manager";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        public enum ToastType
        {
            Success,
            Info,
            Warning,
            Error
        }

        private string appDir;
        private string steamDir;
        private List<GameItem> gamesList = new List<GameItem>();
        private AppSettings settings = new AppSettings();
        private bool isInjected = false;
        private Thread monitorThread = null;
        private DispatcherTimer statusTimer;

        // Dual App Mode & View Mode State
        private bool isMiniMode = false;
        private string currentViewMode = "cards";
        private double savedMainWidth = 1140;
        private double savedMainHeight = 780;
        private double savedMainLeft = 100;
        private double savedMainTop = 100;

        // Custom Window Title Bar Controls
        private Border headerBorder;
        private Button btnWinMin;
        private Button btnWinMax;
        private Button btnWinClose;
        private Button btnModeToggle;

        // In-App Toast & Modal Infrastructure (Multi-Monitor Safe)
        private StackPanel toastNotificationHost;
        private Grid inAppModalHost;

        // Active Steam Account & Status Pill
        private Border unifiedStatusPill;
        private Border accountPillBorder;
        private TextBlock txtAccountIcon;
        private TextBlock txtAccountName;
        private string activeSteamPersona = "";
        private string activeSteamAccount = "";

        // Header Controls
        private Border txtBadgeVer;
        private TextBlock txtLogoSub;
        private TextBlock txtStatus;
        private Border borderStatusDot;
        private Border borderStatusPulseRing;
        private Button btnSettings;
        private Border updateBannerBorder;
        private TextBlock txtUpdateBanner;
        private Button btnUpdateBanner;
        private Button btnUpdateChangelog;
        private Button btnCloseUpdateBanner;
        private string pendingUpdateUrl = "";
        private string pendingExeDownloadUrl = "";
        private string pendingLatestTag = "";
        private List<KeyValuePair<string, string>> pendingOtherAssets = new List<KeyValuePair<string, string>>();

        // Control Deck (Streamlined 1-Click Launch & Stealth Actions)
        private Border badgeStealthMode;
        private TextBlock txtStealthMode;
        private Button btnToggleClearCache;
        private Button btnInjectFiles;
        private Button btnLaunch;
        private Button btnStop;
        private TextBlock lblTelemetryUnlocked;
        private TextBlock lblTelemetrySource;

        // Search Command Deck (100% In-App Overlay, Replaces Floating Win32 Popup)
        private Border searchContainer;
        private TextBox txtGameInput;
        private TextBlock txtSearchPlaceholder;
        private Button btnClearSearch;
        private Button btnAddInput;
        private Button btnBulk;
        private Border searchOverlayBorder;
        private Grid searchDismissBackdrop;
        private StackPanel searchResultsPanel;
        private TextBlock lblSearchPopupTitle;
        private DispatcherTimer searchDebounceTimer;
        private List<SearchResultItem> lastSearchResults = new List<SearchResultItem>();
        private int lastAddedAppId = 0;

        // Library & Filter Tabs
        private TextBlock lblLibraryTitle;
        private string currentFilterTab = "all";
        private Button btnTabAll;
        private Button btnTabGames;
        private Button btnTabDlcs;
        private TextBox txtFilterInput;
        private Button btnShareLibrary;
        private Button btnImportLibrary;
        private Button btnToggleAll;
        private Button btnClearAll;
        private Button btnViewCards;
        private Button btnViewList;
        private WrapPanel gamesWrapPanel;
        private StackPanel gamesListStack;
        private StackPanel emptyStatePanel;
        private TextBlock txtEmptyTitle;
        private TextBlock txtEmptySubtitle;

        // Mode Containers
        private Grid mainContentGrid;
        private Grid miniContentGrid;
        private StackPanel miniGamesStack;
        private TextBox txtMiniSearch;
        private Button btnMiniAdd;
        private Button btnMiniLaunch;
        private Button btnMiniStop;
        private TextBlock lblMiniStatus;
        private Button btnMiniDoctor;
        private Button btnMiniSettings;
        private Button btnMiniLang;

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
            try
            {
                string exeLocation = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(exeLocation))
                    appDir = Path.GetDirectoryName(exeLocation);
                else
                    appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            }
            catch
            {
                appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            }

            Title = "GreenLuma 2026 - Stealth Pro Hub (Standalone Edition)";
            Width = 1140;
            Height = 780;
            MinWidth = 760;
            MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(10, 13, 20)); // #0A0D14
            Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            this.SizeChanged += MainWindow_SizeChanged;

            // Custom Frameless Window without white borders or grip
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            BorderThickness = new Thickness(0);

            try
            {
                var chrome = new System.Windows.Shell.WindowChrome {
                    CaptionHeight = 0,
                    ResizeBorderThickness = new Thickness(6),
                    CornerRadius = new CornerRadius(0),
                    GlassFrameThickness = new Thickness(0),
                    UseAeroCaptionButtons = false
                };
                System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);
            }
            catch { }

            this.Icon = CreateAppIcon();

            // Inject Custom Cyberpunk 8px Dark Scrollbar Style
            ApplyCustomScrollbarTheme();

            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls; } catch { }

            this.PreviewKeyDown += (s, e) => {
                if (e.Key == Key.Escape)
                {
                    if (inAppModalHost != null && inAppModalHost.Visibility == Visibility.Visible)
                    {
                        CloseInAppModal();
                        e.Handled = true;
                    }
                    else if (searchOverlayBorder != null && searchOverlayBorder.Visibility == Visibility.Visible)
                    {
                        CloseSearchOverlay();
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    if (txtFilterInput != null && txtFilterInput.IsVisible)
                    {
                        txtFilterInput.Focus();
                        txtFilterInput.SelectAll();
                        e.Handled = true;
                    }
                }
            };

            BuildUI();

            LoadSettings();
            DetectSteamPath();
            LoadGames();
            UpdateStatusUI();
            ApplyLanguageUI();
            CheckAndWarnAppLocation();

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
                    CloseSearchOverlay();
                }
            };

            // Clean up any previous update leftovers and notify user if just updated
            bool justUpdated = false;
            try
            {
                var oldBaks = Directory.GetFiles(appDir, "GreenLumaManager*.old");
                if (oldBaks != null && oldBaks.Length > 0)
                {
                    justUpdated = true;
                    ThreadPool.QueueUserWorkItem((state) => {
                        for (int r = 0; r < 10; r++)
                        {
                            foreach (var f in Directory.GetFiles(appDir, "GreenLumaManager*.old"))
                            {
                                try { File.Delete(f); } catch { }
                            }
                            Thread.Sleep(500);
                        }
                    });
                }
                string oldDownload = Path.Combine(appDir, "GreenLumaManager.new");
                if (File.Exists(oldDownload)) File.Delete(oldDownload);
            }
            catch { }

            if (justUpdated)
            {
                Dispatcher.BeginInvoke((Action)(() => {
                    bool isAr = (settings.lang == "ar");
                    ShowToast(
                        isAr ? string.Format("🎉 تم تحديث البرنامج بنجاح إلى الإصدار {0}!", CURRENT_VERSION) :
                               string.Format("🎉 Program successfully updated to {0}!", CURRENT_VERSION),
                        ToastType.Success, 5000);
                }));
            }

            CheckForUpdatesAsync(false);

            // First-Time Step-by-Step Guide (Only on genuine 1st launch ever)
            if (IsFirstTimeUser())
            {
                MarkGuideAsShownPermanently();
                Dispatcher.BeginInvoke((Action)(() => OpenQuickGuideDialog()));
            }

            Closing += MainWindow_Closing;
        }

        private void ApplyCustomScrollbarTheme()
        {
            try
            {
                string xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Style TargetType='{x:Type ScrollBar}'>
        <Setter Property='Stylus.IsPressAndHoldEnabled' Value='false'/>
        <Setter Property='Stylus.IsFlicksEnabled' Value='false'/>
        <Setter Property='Width' Value='8'/>
        <Setter Property='MinWidth' Value='8'/>
        <Setter Property='Background' Value='Transparent'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='{x:Type ScrollBar}'>
                    <Grid Background='Transparent'>
                        <Track x:Name='PART_Track' IsDirectionReversed='true'>
                            <Track.Thumb>
                                <Thumb>
                                    <Thumb.Template>
                                        <ControlTemplate TargetType='{x:Type Thumb}'>
                                            <Border Background='#1E293B' CornerRadius='4' Margin='1,0,1,0'/>
                                        </ControlTemplate>
                                    </Thumb.Template>
                                </Thumb>
                            </Track.Thumb>
                        </Track>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='true'>
                            <Setter Property='Background' Value='#10B981'/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>";
                var dict = (ResourceDictionary)XamlReader.Parse(xaml);
                this.Resources.MergedDictionaries.Add(dict);
            }
            catch { }
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

        public void ShowToast(string message, ToastType type = ToastType.Info, int durationMs = 3500)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                if (toastNotificationHost == null) return;

                Color bgCol = Color.FromArgb(240, 15, 23, 42); // slate 900
                Color borderCol = Color.FromArgb(180, 51, 65, 85);
                Color textCol = Color.FromRgb(248, 250, 252);
                string icon = "💡";

                switch (type)
                {
                    case ToastType.Success:
                        borderCol = Color.FromRgb(16, 185, 129); // emerald
                        icon = "✅";
                        break;
                    case ToastType.Warning:
                        borderCol = Color.FromRgb(245, 158, 11); // amber
                        icon = "⚠️";
                        break;
                    case ToastType.Error:
                        borderCol = Color.FromRgb(239, 68, 68); // rose
                        icon = "❌";
                        break;
                    case ToastType.Info:
                    default:
                        borderCol = Color.FromRgb(6, 182, 212); // cyan
                        icon = "ℹ️";
                        break;
                }

                var toastBorder = new Border()
                {
                    Background = new SolidColorBrush(bgCol),
                    BorderBrush = new SolidColorBrush(borderCol),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 0, 0, 8),
                    MaxWidth = 420,
                    MinHeight = 44,
                    Effect = new DropShadowEffect()
                    {
                        Color = borderCol,
                        BlurRadius = 16,
                        ShadowDepth = 2,
                        Opacity = 0.45
                    },
                    Opacity = 0
                };

                var dock = new DockPanel() { LastChildFill = true };

                var btnClose = new TextBlock()
                {
                    Text = "✕",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(12, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(btnClose, Dock.Right);

                var contentStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var txtIcon = new TextBlock()
                {
                    Text = icon,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var txtMsg = new TextBlock()
                {
                    Text = message,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(textCol),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    MaxWidth = 330
                };
                contentStack.Children.Add(txtIcon);
                contentStack.Children.Add(txtMsg);

                dock.Children.Add(btnClose);
                dock.Children.Add(contentStack);
                toastBorder.Child = dock;

                toastNotificationHost.Children.Add(toastBorder);
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                toastBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                DispatcherTimer timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(durationMs) };
                Action dismiss = () =>
                {
                    timer.Stop();
                    var fadeOut = new DoubleAnimation(toastBorder.Opacity, 0, TimeSpan.FromMilliseconds(200));
                    fadeOut.Completed += (s, e) => {
                        try { toastNotificationHost.Children.Remove(toastBorder); } catch { }
                    };
                    toastBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                };

                timer.Tick += (s, e) => dismiss();
                timer.Start();

                btnClose.MouseDown += (s, e) => dismiss();
            }));
        }

        public void ShowInAppModal(FrameworkElement body, string title = null, Action onClose = null, double maxWidth = 700)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                if (inAppModalHost == null) return;
                inAppModalHost.Children.Clear();
                bool isAr = (settings.lang == "ar");

                var cardBorder = new Border()
                {
                    MaxWidth = maxWidth,
                    MaxHeight = 640,
                    Margin = new Thickness(24),
                    Background = new SolidColorBrush(Color.FromRgb(13, 18, 28)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(18),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Effect = new DropShadowEffect()
                    {
                        Color = Color.FromRgb(16, 185, 129),
                        BlurRadius = 30,
                        ShadowDepth = 4,
                        Opacity = 0.4
                    }
                };

                var dock = new DockPanel() { LastChildFill = true };

                var headerDock = new DockPanel() { Margin = new Thickness(0, 0, 0, 14) };
                var btnClose = new Button()
                {
                    Content = "✕",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8, 4, 8, 4),
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(btnClose, Dock.Right);

                Action doClose = () =>
                {
                    inAppModalHost.Visibility = Visibility.Collapsed;
                    inAppModalHost.Children.Clear();
                    if (onClose != null) onClose();
                };

                btnClose.Click += (s, e) => doClose();

                var txtTitle = new TextBlock()
                {
                    Text = title ?? "",
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                headerDock.Children.Add(btnClose);
                headerDock.Children.Add(txtTitle);
                DockPanel.SetDock(headerDock, Dock.Top);
                dock.Children.Add(headerDock);

                dock.Children.Add(body);
                cardBorder.Child = dock;

                inAppModalHost.Children.Add(cardBorder);
                inAppModalHost.Visibility = Visibility.Visible;
            }));
        }

        public void CloseInAppModal()
        {
            Dispatcher.Invoke((Action)(() =>
            {
                if (inAppModalHost != null)
                {
                    inAppModalHost.Visibility = Visibility.Collapsed;
                    inAppModalHost.Children.Clear();
                }
            }));
        }

        public void ShowSearchOverlay()
        {
            if (searchOverlayBorder != null)
            {
                searchOverlayBorder.Visibility = Visibility.Visible;
                var tg = searchOverlayBorder.RenderTransform as TransformGroup;
                if (tg == null)
                {
                    tg = new TransformGroup();
                    tg.Children.Add(new ScaleTransform(1.0, 1.0));
                    tg.Children.Add(new TranslateTransform(0, 0));
                    searchOverlayBorder.RenderTransformOrigin = new Point(0.5, 0.0);
                    searchOverlayBorder.RenderTransform = tg;
                }
                var st = tg.Children[0] as ScaleTransform;
                var tt = tg.Children[1] as TranslateTransform;

                if (st != null)
                {
                    st.ScaleY = 0.92;
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(180)) { DecelerationRatio = 0.6 });
                }
                if (tt != null)
                {
                    tt.Y = -18;
                    tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-18, 0, TimeSpan.FromMilliseconds(180)) { DecelerationRatio = 0.6 });
                }

                searchOverlayBorder.Opacity = 0.0;
                searchOverlayBorder.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160)));
            }

            if (searchDismissBackdrop != null)
            {
                searchDismissBackdrop.Visibility = Visibility.Visible;
                searchDismissBackdrop.Opacity = 0.0;
                searchDismissBackdrop.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 0.5, TimeSpan.FromMilliseconds(160)));
            }
        }

        public void CloseSearchOverlay()
        {
            if (searchOverlayBorder != null && searchOverlayBorder.Visibility == Visibility.Visible)
            {
                var tg = searchOverlayBorder.RenderTransform as TransformGroup;
                TranslateTransform tt = (tg != null && tg.Children.Count > 1) ? tg.Children[1] as TranslateTransform : null;
                if (tt != null)
                {
                    tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -12, TimeSpan.FromMilliseconds(110)) { AccelerationRatio = 0.5 });
                }

                var daOp = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(110));
                daOp.Completed += (s, e) => {
                    searchOverlayBorder.Visibility = Visibility.Collapsed;
                };
                searchOverlayBorder.BeginAnimation(UIElement.OpacityProperty, daOp);
            }

            if (searchDismissBackdrop != null && searchDismissBackdrop.Visibility == Visibility.Visible)
            {
                var daBackdrop = new DoubleAnimation(0.5, 0.0, TimeSpan.FromMilliseconds(110));
                daBackdrop.Completed += (s, e) => {
                    searchDismissBackdrop.Visibility = Visibility.Collapsed;
                };
                searchDismissBackdrop.BeginAnimation(UIElement.OpacityProperty, daBackdrop);
            }
        }

        private void DetectActiveSteamAccount()
        {
            if (string.IsNullOrEmpty(steamDir)) return;
            string vdfPath = Path.Combine(steamDir, "config", "loginusers.vdf");
            if (!File.Exists(vdfPath)) return;

            try
            {
                string[] lines = File.ReadAllLines(vdfPath);
                string currentPersona = "";
                string currentAccount = "";
                long currentTimestamp = 0;
                bool isMostRecent = false;

                string bestPersona = "";
                string bestAccount = "";
                long bestTimestamp = -1;

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("\"PersonaName\"", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(line, "\"PersonaName\"\\s+\"([^\"]+)\"");
                        if (match.Success) currentPersona = match.Groups[1].Value;
                    }
                    else if (line.StartsWith("\"AccountName\"", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(line, "\"AccountName\"\\s+\"([^\"]+)\"");
                        if (match.Success) currentAccount = match.Groups[1].Value;
                    }
                    else if (line.StartsWith("\"MostRecent\"", StringComparison.OrdinalIgnoreCase) || line.StartsWith("\"AutoLogin\"", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(line, "\"(?:MostRecent|AutoLogin)\"\\s+\"([^\"]+)\"");
                        if (match.Success && match.Groups[1].Value == "1")
                        {
                            isMostRecent = true;
                        }
                    }
                    else if (line.StartsWith("\"timestamp\"", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(line, "\"timestamp\"\\s+\"([^\"]+)\"");
                        if (match.Success) long.TryParse(match.Groups[1].Value, out currentTimestamp);
                    }
                    else if (line == "}")
                    {
                        if (!string.IsNullOrEmpty(currentPersona))
                        {
                            if (isMostRecent || currentTimestamp > bestTimestamp)
                            {
                                bestPersona = currentPersona;
                                bestAccount = currentAccount;
                                bestTimestamp = currentTimestamp;
                                if (isMostRecent) break;
                            }
                        }
                        currentPersona = "";
                        currentAccount = "";
                        currentTimestamp = 0;
                        isMostRecent = false;
                    }
                }

                if (!string.IsNullOrEmpty(bestPersona))
                {
                    activeSteamPersona = bestPersona;
                    activeSteamAccount = bestAccount;
                }
            }
            catch { }
        }

        private int KillZombieSteamProcesses()
        {
            int killed = 0;
            string[] targets = new string[] { "steam", "steamwebhelper", "gameoverlayui" };
            foreach (string name in targets)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try
                        {
                            p.Kill();
                            p.WaitForExit(1000);
                            killed++;
                        }
                        catch { }
                    }
                }
                catch { }
            }
            if (killed > 0)
            {
                Thread.Sleep(400);
            }
            return killed;
        }

        private string AutoScanSteamDrives()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                    {
                        string p = key.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(p) && File.Exists(Path.Combine(p, "steam.exe")))
                            return Path.GetFullPath(p);
                    }
                }
            }
            catch { }

            string[] subPaths = new string[] {
                @"Steam",
                @"Program Files (x86)\Steam",
                @"Program Files\Steam",
                @"Games\Steam"
            };

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string root = drive.RootDirectory.FullName;
                foreach (var sub in subPaths)
                {
                    try
                    {
                        string candidate = Path.Combine(root, sub);
                        if (File.Exists(Path.Combine(candidate, "steam.exe")))
                            return Path.GetFullPath(candidate);
                    }
                    catch { }
                }
            }
            return null;
        }

        private bool SanitizeAppListBOM()
        {
            bool fixedAny = false;
            List<string> iniFiles = new List<string>();
            iniFiles.Add(Path.Combine(appDir, "AppList.ini"));
            if (!string.IsNullOrEmpty(steamDir))
            {
                iniFiles.Add(Path.Combine(steamDir, "AppList", "AppList.ini"));
            }

            var utf8NoBom = new UTF8Encoding(false);
            foreach (string file in iniFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        byte[] bytes = File.ReadAllBytes(file);
                        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                        {
                            string text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
                            File.WriteAllText(file, text, utf8NoBom);
                            fixedAny = true;
                        }
                    }
                }
                catch { }
            }
            return fixedAny;
        }

        private bool WipeAndDummyAppCache()
        {
            if (string.IsNullOrEmpty(steamDir)) return false;
            try
            {
                string cacheDir = Path.Combine(steamDir, "appcache");
                if (Directory.Exists(cacheDir))
                {
                    string pkg = Path.Combine(cacheDir, "packageinfo.vdf");
                    if (File.Exists(pkg))
                    {
                        try { File.Delete(pkg); } catch { }
                        try { File.WriteAllBytes(pkg, new byte[] { 0x01 }); } catch { }
                    }
                    string appinfo = Path.Combine(cacheDir, "appinfo.vdf");
                    if (File.Exists(appinfo))
                    {
                        try { File.Delete(appinfo); } catch { }
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        private void ExportLibraryToClipboard()
        {
            bool isAr = (settings.lang == "ar");
            if (gamesList.Count == 0)
            {
                ShowToast(isAr ? "القائمة فارغة، أضف بعض الألعاب أولاً!" : "Library is empty, add games first!", ToastType.Warning);
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# GreenLuma Unlocked Library Export");
            foreach (var g in gamesList)
            {
                sb.AppendLine(string.Format("{0} = {1} [{2}]", g.appId, g.name, g.CheckIsDLC() ? "DLC" : "GAME"));
            }
            try
            {
                Clipboard.SetText(sb.ToString());
                ShowToast(
                    isAr ? string.Format("✅ تم نسخ {0} عناصر إلى الحافظة للمشاركة!", gamesList.Count) :
                           string.Format("✅ Exported {0} games to clipboard!", gamesList.Count),
                    ToastType.Success);
            }
            catch (Exception ex)
            {
                ShowToast(ex.Message, ToastType.Error);
            }
        }

        private void ImportLibraryFromClipboard()
        {
            bool isAr = (settings.lang == "ar");
            try
            {
                string text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text))
                {
                    ShowToast(isAr ? "الحافظة فارغة!" : "Clipboard is empty!", ToastType.Warning);
                    return;
                }

                var matches = Regex.Matches(text, @"\b(\d{3,8})\b");
                if (matches.Count == 0)
                {
                    ShowToast(isAr ? "لم يتم العثور على أرقام AppID صالحة في الحافظة!" : "No valid AppIDs found in clipboard!", ToastType.Warning);
                    return;
                }

                int added = 0;
                HashSet<int> existing = new HashSet<int>();
                foreach (var g in gamesList) existing.Add(g.appId);

                foreach (Match m in matches)
                {
                    int id;
                    if (int.TryParse(m.Groups[1].Value, out id))
                    {
                        if (!existing.Contains(id))
                        {
                            existing.Add(id);
                            gamesList.Add(new GameItem() {
                                appId = id,
                                name = "Steam App " + id,
                                image = string.Format("https://cdn.cloudflare.steamstatic.com/steam/apps/{0}/header.jpg", id),
                                enabled = true
                            });
                            added++;
                        }
                    }
                }

                if (added > 0)
                {
                    SaveGames();
                    UpdateTabButtonStyles();
                    RenderGamesUI();
                    ShowToast(
                        isAr ? string.Format("✅ تم استيراد {0} لعبة جديدة بنجاح!", added) :
                               string.Format("✅ Successfully imported {0} games!", added),
                        ToastType.Success);
                }
                else
                {
                    ShowToast(isAr ? "جميع الألعاب موجودة بالفعل في قائمتك!" : "All games are already in your library!", ToastType.Info);
                }
            }
            catch (Exception ex)
            {
                ShowToast(ex.Message, ToastType.Error);
            }
        }

        private void OpenSteamDoctorModal()
        {
            bool isAr = (settings.lang == "ar");
            var mainStack = new StackPanel() { Margin = new Thickness(4) };

            var descText = new TextBlock() {
                Text = isAr ? 
                    "يقوم طبيب ستيم بفحص العمليات العالقة في الذاكرة، وسلامة ملفات AppList، والكاش المتراكم، وملفات التخفي." :
                    "Steam Doctor automatically inspects zombie background processes, AppList BOM integrity, appcache locks, and stealth payload status.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            };
            mainStack.Children.Add(descText);

            var cardsContainer = new StackPanel();

            Func<string, string, bool, Border> createDiagRow = (title, details, isHealthy) =>
            {
                var row = new Border() {
                    Background = new SolidColorBrush(Color.FromRgb(18, 24, 38)),
                    BorderBrush = isHealthy ? 
                        new SolidColorBrush(Color.FromArgb(90, 16, 185, 129)) : 
                        new SolidColorBrush(Color.FromArgb(90, 239, 68, 68)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var rowDock = new DockPanel() { LastChildFill = true };

                var badge = new Border() {
                    Background = isHealthy ? new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(40, 239, 68, 68)),
                    BorderBrush = isHealthy ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(8, 2, 8, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(badge, Dock.Right);
                badge.Child = new TextBlock() {
                    Text = isHealthy ? (isAr ? "✅ سليم" : "✅ HEALTHY") : (isAr ? "⚠️ يحتاج إصلاح" : "⚠️ ISSUE DETECTED"),
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = isHealthy ? new SolidColorBrush(Color.FromRgb(110, 231, 183)) : new SolidColorBrush(Color.FromRgb(252, 165, 165))
                };
                rowDock.Children.Add(badge);

                var textStack = new StackPanel();
                textStack.Children.Add(new TextBlock() {
                    Text = title,
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249))
                });
                textStack.Children.Add(new TextBlock() {
                    Text = details,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Margin = new Thickness(0, 2, 0, 0)
                });
                rowDock.Children.Add(textStack);

                row.Child = rowDock;
                return row;
            };

            Action populateRows = () =>
            {
                cardsContainer.Children.Clear();

                // 1. Process Check
                int zombieCount = 0;
                foreach (var p in Process.GetProcessesByName("steam")) zombieCount++;
                foreach (var p in Process.GetProcessesByName("steamwebhelper")) zombieCount++;
                bool procHealthy = (zombieCount == 0 || !IsSteamRunning());
                cardsContainer.Children.Add(createDiagRow(
                    isAr ? "1. حالة عمليات ستيم (Steam Background Processes)" : "1. Steam Background Processes",
                    zombieCount > 0 ? (isAr ? string.Format("يوجد {0} عمليات قيد التشغيل قد تقفل ملفات user32.dll", zombieCount) : string.Format("{0} background processes detected", zombieCount)) :
                                      (isAr ? "لا توجد عمليات عالقة في الخلفية، الذاكرة نظيفة." : "Clean memory, no lingering processes."),
                    procHealthy
                ));

                // 2. Steam Path Check
                bool pathHealthy = !string.IsNullOrEmpty(steamDir) && Directory.Exists(steamDir) && File.Exists(Path.Combine(steamDir, "steam.exe"));
                cardsContainer.Children.Add(createDiagRow(
                    isAr ? "2. مسار مجلد ستيم (Steam Installation Directory)" : "2. Steam Installation Directory",
                    pathHealthy ? steamDir : (isAr ? "لم يتم العثور على مسار ستيم الصحيح!" : "Steam path missing or invalid!"),
                    pathHealthy
                ));

                // 3. BOM & AppList Check
                bool bomFound = false;
                string appIni = Path.Combine(appDir, "AppList.ini");
                if (File.Exists(appIni))
                {
                    byte[] b = File.ReadAllBytes(appIni);
                    if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) bomFound = true;
                }
                cardsContainer.Children.Add(createDiagRow(
                    isAr ? "3. ترميز ملفات AppList (UTF-8 No-BOM Integrity)" : "3. AppList UTF-8 No-BOM Integrity",
                    bomFound ? (isAr ? "تم اكتشاف علامة BOM غير مرغوبة قد تسبب فشل قراءة الألعاب" : "UTF-8 BOM detected in AppList.ini") :
                               (isAr ? "ترميز ملفات الألعاب نظيف 100% ومطابق لـ GreenLuma" : "Clean UTF-8 encoding without BOM."),
                    !bomFound
                ));

                // 4. AppCache State
                bool cacheIssue = false;
                if (!string.IsNullOrEmpty(steamDir))
                {
                    string pkg = Path.Combine(steamDir, "appcache", "packageinfo.vdf");
                    if (File.Exists(pkg))
                    {
                        var fi = new FileInfo(pkg);
                        if (fi.Length > 200000000) cacheIssue = true;
                    }
                }
                cardsContainer.Children.Add(createDiagRow(
                    isAr ? "4. حالة الكاش المؤقت (AppCache & packageinfo.vdf)" : "4. AppCache & packageinfo.vdf State",
                    cacheIssue ? (isAr ? "ملف packageinfo.vdf متضخم ومقفل، يفضل تصفيره" : "Bloated packageinfo.vdf cache file") :
                                 (isAr ? "الكاش مهيأ ومستعد لحقن الألعاب الجديدة" : "AppCache is ready for clean injection."),
                    !cacheIssue
                ));

                // 5. Payload Integrity
                string srcDesc;
                byte[] sf = GetPayloadBytes("user32SF.dll", out srcDesc);
                bool payloadHealthy = (sf != null && sf.Length > 0);
                cardsContainer.Children.Add(createDiagRow(
                    isAr ? "5. سلامة ملفات التخفي (Stealth Hook user32SF.dll)" : "5. Stealth Hook Integrity",
                    payloadHealthy ? (isAr ? "ملف التخفي جاهز وموجود في الذاكرة/المجلد" : "Payload verified and ready.") :
                                     (isAr ? "ملف التخفي مفقود أو محجوب بواسطة مضاد الفيروسات!" : "Payload missing or blocked by Defender!"),
                    payloadHealthy
                ));
            };

            populateRows();
            mainStack.Children.Add(cardsContainer);

            var actionDock = new DockPanel() { Margin = new Thickness(0, 16, 0, 0) };

            var btnAutoFix = new Button() {
                Content = isAr ? "⚡ إصلاح وتطهير كافة المشاكل بنقرة واحدة (Auto-Fix All)" : "⚡ Auto-Fix All Issues Now",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Padding = new Thickness(16, 10, 16, 10),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            btnAutoFix.Click += (s, e) => {
                int killed = KillZombieSteamProcesses();
                SanitizeAppListBOM();
                SyncAppListIni();
                WipeAndDummyAppCache();

                if (string.IsNullOrEmpty(steamDir) || !Directory.Exists(steamDir))
                {
                    string scanned = AutoScanSteamDrives();
                    if (!string.IsNullOrEmpty(scanned))
                    {
                        steamDir = scanned;
                        settings.customSteamPath = scanned;
                        SaveSettings();
                    }
                }

                populateRows();
                ShowToast(
                    isAr ? string.Format("✅ اكتمل الفحص والإصلاح الذكي! تم إنهاء {0} عمليات وتطهير الكاش والملفات.", killed) :
                           string.Format("✅ Auto-Fix complete! Terminated {0} processes, cleaned AppCache & BOM.", killed),
                    ToastType.Success,
                    4000
                );
            };
            DockPanel.SetDock(btnAutoFix, Dock.Left);
            actionDock.Children.Add(btnAutoFix);

            var btnDeepClean = new Button() {
                Content = isAr ? "🧹 تنظيف عميق لستيم" : "🧹 Deep Clean Steam",
                Background = new SolidColorBrush(Color.FromArgb(40, 239, 68, 68)),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 239, 68, 68)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 10, 12, 10),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnDeepClean.Click += (s, e) => {
                PurgeAllArtifacts();
                populateRows();
            };
            actionDock.Children.Add(btnDeepClean);

            mainStack.Children.Add(actionDock);

            ShowInAppModal(mainStack, isAr ? "🩺 طبيب ستيم وفحص المشاكل (Steam Doctor)" : "🩺 Steam Doctor & Diagnostic Hub", null, 680);
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
            // Root Container with Sharp Cyber Edge (Eliminates white borders!)
            var rootBorder = new Border() {
                Background = new SolidColorBrush(Color.FromRgb(10, 13, 20)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 30, 41, 59)),
                BorderThickness = new Thickness(1)
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 0: Top Header / Custom Title Bar
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 1: Update Banner
            mainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // 2: Active Mode Host (Full or Mini)

            // =========================================================================
            // --- 0. 3-COLUMN ZERO-COLLISION CUSTOM TITLE BAR & TOP HEADER ---
            // =========================================================================
            headerBorder = new Border() {
                Background = new SolidColorBrush(Color.FromRgb(12, 16, 24)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 6, 8, 6)
            };

            headerBorder.MouseDown += (s, e) => {
                if (e.ChangedButton == MouseButton.Left)
                {
                    if (e.ClickCount == 2 && !isMiniMode)
                    {
                        ToggleMaximize();
                    }
                    else
                    {
                        DragMove();
                    }
                }
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto }); // Col 0: Brand
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) }); // Col 1: Center Status Radar
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto }); // Col 2: Actions & Controls

            // Col 0: Brand Logo & Title
            var logoStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var imgLogo = new Image() {
                Source = this.Icon,
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            logoStack.Children.Add(imgLogo);

            var txtLogo = new TextBlock() {
                Text = "GREENLUMA 2026",
                FontSize = 17,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                VerticalAlignment = VerticalAlignment.Center
            };
            txtBadgeVer = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            txtBadgeVer.Child = new TextBlock() { Text = CURRENT_VERSION, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183)), FontWeight = FontWeights.Bold };

            txtLogoSub = new TextBlock() {
                Text = "STEALTH HUB",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                VerticalAlignment = VerticalAlignment.Center
            };
            logoStack.Children.Add(txtLogo);
            logoStack.Children.Add(txtBadgeVer);
            logoStack.Children.Add(txtLogoSub);
            Grid.SetColumn(logoStack, 0);
            headerGrid.Children.Add(logoStack);

            // Col 1: Center Live Unified Status & Active Steam Account Pill (Zero Overlap Guaranteed!)
            unifiedStatusPill = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(50, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14, 5, 14, 5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            var unifiedPillStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

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
            unifiedPillStack.Children.Add(dotContainer);

            txtStatus = new TextBlock() {
                Text = "Steam Inactive",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.None
            };
            unifiedPillStack.Children.Add(txtStatus);

            accountPillBorder = new Border() {
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            var accountStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var txtDivider = new TextBlock() {
                Text = "·",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            accountStack.Children.Add(txtDivider);
            txtAccountIcon = new TextBlock() { Text = "👤", FontSize = 11, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
            txtAccountName = new TextBlock() {
                Text = "Steam User",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(103, 232, 249)),
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 95,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            accountStack.Children.Add(txtAccountIcon);
            accountStack.Children.Add(txtAccountName);
            accountPillBorder.Child = accountStack;
            unifiedPillStack.Children.Add(accountPillBorder);

            unifiedStatusPill.Child = unifiedPillStack;
            unifiedStatusPill.MouseDown += (s, e) => {
                bool isAr = (settings.lang == "ar");
                if (!string.IsNullOrEmpty(activeSteamPersona))
                {
                    ShowToast(
                        isAr ? string.Format("الحساب النشط في ستيم: {0} ({1})", activeSteamPersona, activeSteamAccount) :
                               string.Format("Active Steam Account: {0} ({1})", activeSteamPersona, activeSteamAccount),
                        ToastType.Info
                    );
                }
                else
                {
                    ShowToast(
                        isAr ? "ستيم غير نشط أو لم يتم تسجيل الدخول بعد" :
                               "Steam is inactive or no active user detected",
                        ToastType.Info
                    );
                }
            };

            Grid.SetColumn(unifiedStatusPill, 1);
            headerGrid.Children.Add(unifiedStatusPill);

            // Col 2: Mode Toggle, Settings & Custom Window Controls
            var rightHeaderStack = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

            btnModeToggle = new Button() {
                Content = "⛶ Mini",
                ToolTip = "Switch between Main Window & Compact Tray Widget",
                Background = new SolidColorBrush(Color.FromArgb(35, 6, 182, 212)),
                Foreground = new SolidColorBrush(Color.FromRgb(103, 232, 249)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 6, 182, 212)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            AttachTactilePress(btnModeToggle);
            btnModeToggle.Click += (s, e) => ApplyAppMode(!isMiniMode);
            rightHeaderStack.Children.Add(btnModeToggle);

            btnSettings = new Button() {
                Content = "⚙️ Settings",
                ToolTip = "Settings & Utilities Hub",
                Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            AttachTactilePress(btnSettings);
            btnSettings.Click += (s, e) => OpenSettingsDialog();
            rightHeaderStack.Children.Add(btnSettings);

            // Min / Max / Close Window Buttons
            var winControlsStack = new StackPanel() { Orientation = Orientation.Horizontal };
            btnWinMin = new Button() {
                Content = "─",
                FontSize = 13,
                Width = 32,
                Height = 28,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnWinMin.Click += (s, e) => WindowState = WindowState.Minimized;
            winControlsStack.Children.Add(btnWinMin);

            btnWinMax = new Button() {
                Content = "□",
                FontSize = 14,
                Width = 32,
                Height = 28,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnWinMax.Click += (s, e) => ToggleMaximize();
            winControlsStack.Children.Add(btnWinMax);

            btnWinClose = new Button() {
                Content = "✕",
                FontSize = 13,
                Width = 36,
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
            Grid.SetColumn(rightHeaderStack, 2);
            headerGrid.Children.Add(rightHeaderStack);

            headerBorder.Child = headerGrid;
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

            var updateActionsStack = new StackPanel() {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            btnUpdateBanner = new Button() {
                Content = "⚡ Update & Restart Now",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(13, 5, 13, 5),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnUpdateBanner.Click += (s, e) => StartInAppUpdate();
            updateActionsStack.Children.Add(btnUpdateBanner);

            btnUpdateChangelog = new Button() {
                Content = "Release Notes ↗",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnUpdateChangelog.Click += (s, e) => {
                if (!string.IsNullOrEmpty(pendingUpdateUrl))
                {
                    try { Process.Start(pendingUpdateUrl); } catch { }
                }
            };
            updateActionsStack.Children.Add(btnUpdateChangelog);

            btnCloseUpdateBanner = new Button() {
                Content = "✕",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(6, 2, 6, 2)
            };
            btnCloseUpdateBanner.Click += (s, e) => {
                updateBannerBorder.Visibility = Visibility.Collapsed;
            };
            updateActionsStack.Children.Add(btnCloseUpdateBanner);

            updateDock.Children.Add(updateActionsStack);
            updateBannerBorder.Child = updateDock;
            Grid.SetRow(updateBannerBorder, 1);
            mainGrid.Children.Add(updateBannerBorder);

            // =========================================================================
            // --- 2. DUAL MODE HOSTS: FULL DASHBOARD & COMPACT TRAY FLYOUT ---
            // =========================================================================
            mainContentGrid = new Grid();
            mainContentGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // Row 0: Hero Command Deck
            mainContentGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // Row 1: Library Container
            Grid.SetRow(mainContentGrid, 2);
            mainGrid.Children.Add(mainContentGrid);

            // =========================================================================
            // --- HERO COMMAND DECK (2-TIER CLEAN ARCHITECTURE, ZERO COLLISION) ---
            // =========================================================================
            var heroCommandDeck = new Border() {
                Background = new SolidColorBrush(Color.FromRgb(13, 18, 27)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(20, 8, 20, 8),
                Padding = new Thickness(14, 12, 14, 12)
            };

            var deckMainStack = new StackPanel();

            // ---------------------------------------------------------
            // Tier 1: Action Command Bar & Telemetry Strip (No Search!)
            // ---------------------------------------------------------
            // ---------------------------------------------------------
            // Tier 1: Action Command Bar & Telemetry Strip (Priority 1 Action Grid)
            // ---------------------------------------------------------
            var deckTopGrid = new Grid();
            deckTopGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto }); // Col 0: Actions (Always 100% full width, never squeezed!)
            deckTopGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) }); // Col 1: Telemetry (flex)

            // Left: Action Stack (Launch, Inject, Stop)
            var leftActionStack = new StackPanel() { 
                Orientation = Orientation.Horizontal, 
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            btnLaunch = new Button() {
                Content = "🚀 تشغيل ستيم",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 13,
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 8, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnLaunch.Effect = new DropShadowEffect() {
                Color = Color.FromRgb(16, 185, 129),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.7
            };
            AttachTactilePress(btnLaunch);
            btnLaunch.Click += (s, e) => ExecuteSmartLaunch();
            leftActionStack.Children.Add(btnLaunch);

            btnInjectFiles = new Button() {
                Content = "📦 تجهيز الملفات",
                Background = new SolidColorBrush(Color.FromArgb(35, 2, 132, 199)),
                Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 2, 132, 199)),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11.5,
                Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            AttachTactilePress(btnInjectFiles);
            btnInjectFiles.Click += (s, e) => InjectFilesOnly(true);
            leftActionStack.Children.Add(btnInjectFiles);

            btnStop = new Button() {
                Content = "🛑 إيقاف وحذف",
                Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 0, 8, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed
            };
            btnStop.Effect = new DropShadowEffect() {
                Color = Color.FromRgb(239, 68, 68),
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.7
            };
            AttachTactilePress(btnStop);
            btnStop.Click += (s, e) => StopSteamSession();
            leftActionStack.Children.Add(btnStop);

            Grid.SetColumn(leftActionStack, 0);
            deckTopGrid.Children.Add(leftActionStack);

            // Right: Telemetry & Status Badges (Col 1, right-aligned)
            var telemetryRight = new StackPanel() { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right, 
                VerticalAlignment = VerticalAlignment.Center 
            };

            var badgeUnlocked = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            lblTelemetryUnlocked = new TextBlock() {
                Text = "🎯 0 Unlocked",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183))
            };
            badgeUnlocked.Child = lblTelemetryUnlocked;
            telemetryRight.Children.Add(badgeUnlocked);

            badgeStealthMode = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(35, 6, 182, 212)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 6, 182, 212)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            txtStealthMode = new TextBlock() {
                Text = "🛡️ user32SF",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(103, 232, 249))
            };
            lblTelemetrySource = txtStealthMode;
            badgeStealthMode.Child = txtStealthMode;
            telemetryRight.Children.Add(badgeStealthMode);

            btnToggleClearCache = new Button() {
                Content = "🔥 مسح الكاش: مفعّل",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 4, 8, 4),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            AttachTactilePress(btnToggleClearCache);
            btnToggleClearCache.Click += (s, e) => {
                settings.clearAppCache = !settings.clearAppCache;
                SaveSettings();
                UpdateClearCacheButton();
            };
            telemetryRight.Children.Add(btnToggleClearCache);

            Grid.SetColumn(telemetryRight, 1);
            deckTopGrid.Children.Add(telemetryRight);

            deckMainStack.Children.Add(deckTopGrid);

            // ---------------------------------------------------------
            // Tier 2: Search & Ingestion Bar (Dedicated Full Width)
            // ---------------------------------------------------------
            var deckSearchGrid = new Grid() { Margin = new Thickness(0, 10, 0, 0) };
            deckSearchGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) }); // Stretch search
            deckSearchGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto }); // Action buttons (+ Add, Bulk)

            searchContainer = new Border() {
                Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 3, 10, 3),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var searchTg = new TransformGroup();
            var searchSt = new ScaleTransform(1.0, 1.0);
            searchTg.Children.Add(searchSt);
            searchContainer.RenderTransformOrigin = new Point(0.5, 0.5);
            searchContainer.RenderTransform = searchTg;

            var searchDock = new DockPanel();
            var searchIcon = new TextBlock() {
                Text = "🔍",
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 8, 0)
            };
            var iconSt = new ScaleTransform(1.0, 1.0);
            searchIcon.RenderTransformOrigin = new Point(0.5, 0.5);
            searchIcon.RenderTransform = iconSt;

            DockPanel.SetDock(searchIcon, Dock.Left);
            searchDock.Children.Add(searchIcon);

            var inputGrid = new Grid();
            txtGameInput = new TextBox() {
                Height = 28,
                FontSize = 12,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 24, 0)
            };
            txtSearchPlaceholder = new TextBlock() {
                Text = "Search Steam game, DLC or enter AppID...",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                IsHitTestVisible = false
            };
            btnClearSearch = new Button() {
                Content = "✕",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                Visibility = Visibility.Collapsed
            };
            btnClearSearch.Click += (s, e) => {
                txtGameInput.Text = "";
                CloseSearchOverlay();
            };

            inputGrid.Children.Add(txtSearchPlaceholder);
            inputGrid.Children.Add(txtGameInput);
            inputGrid.Children.Add(btnClearSearch);
            searchDock.Children.Add(inputGrid);

            txtGameInput.TextChanged += (s, e) => {
                txtSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtGameInput.Text) ? Visibility.Visible : Visibility.Collapsed;
                btnClearSearch.Visibility = string.IsNullOrEmpty(txtGameInput.Text) ? Visibility.Collapsed : Visibility.Visible;
                searchDebounceTimer.Stop();
                searchDebounceTimer.Start();
            };

            txtGameInput.GotFocus += (s, e) => {
                searchContainer.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                searchContainer.Effect = new DropShadowEffect() {
                    Color = Color.FromRgb(16, 185, 129),
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.8
                };
                searchSt.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 1.014, TimeSpan.FromMilliseconds(130)) { DecelerationRatio = 0.5 });
                searchSt.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 1.014, TimeSpan.FromMilliseconds(130)) { DecelerationRatio = 0.5 });
                iconSt.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 1.25, TimeSpan.FromMilliseconds(150)) { AutoReverse = true });
                iconSt.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 1.25, TimeSpan.FromMilliseconds(150)) { AutoReverse = true });
            };

            txtGameInput.LostFocus += (s, e) => {
                searchContainer.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 16, 185, 129));
                searchContainer.Effect = null;
                searchSt.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.014, 1.0, TimeSpan.FromMilliseconds(150)) { AccelerationRatio = 0.5 });
                searchSt.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.014, 1.0, TimeSpan.FromMilliseconds(150)) { AccelerationRatio = 0.5 });
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
                        CloseSearchOverlay();
                        AddGameById(id, null);
                        txtGameInput.Text = "";
                    }
                    else
                    {
                        if (lastSearchResults != null && lastSearchResults.Count > 0)
                        {
                            var top = lastSearchResults[0];
                            CloseSearchOverlay();
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
                    CloseSearchOverlay();
                }
            };

            searchContainer.Child = searchDock;
            Grid.SetColumn(searchContainer, 0);
            deckSearchGrid.Children.Add(searchContainer);

            var searchButtonsStack = new StackPanel() { 
                Orientation = Orientation.Horizontal, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            btnAddInput = new Button() {
                Content = "+ Add",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Padding = new Thickness(14, 6, 14, 6),
                Height = 32,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            AttachTactilePress(btnAddInput);
            btnAddInput.Click += (s, e) => AddGameFromInput();
            searchButtonsStack.Children.Add(btnAddInput);

            btnBulk = new Button() {
                Content = "📥 Bulk",
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                FontSize = 11.5,
                Padding = new Thickness(12, 6, 12, 6),
                Height = 32,
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            AttachTactilePress(btnBulk);
            btnBulk.Click += (s, e) => OpenBulkImportDialog();
            searchButtonsStack.Children.Add(btnBulk);

            Grid.SetColumn(searchButtonsStack, 1);
            deckSearchGrid.Children.Add(searchButtonsStack);

            deckMainStack.Children.Add(deckSearchGrid);

            heroCommandDeck.Child = deckMainStack;
            Grid.SetRow(heroCommandDeck, 0);
            mainContentGrid.Children.Add(heroCommandDeck);

            // =========================================================================
            // --- COMPACT TRAY FLYOUT OPERATING MODE (MODE A: 380x620) ---
            // =========================================================================
            miniContentGrid = new Grid() {
                Margin = new Thickness(16, 10, 16, 12),
                Visibility = Visibility.Collapsed
            };
            miniContentGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 0: Launch / Stop Hero Button
            miniContentGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 1: Search & Add Bar
            miniContentGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) }); // 2: Scrollable Games List
            miniContentGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto }); // 3: Mini Footer Status & Actions

            // Mini Row 0: Launch / Stop Button
            var miniLaunchStack = new StackPanel() { Margin = new Thickness(0, 0, 0, 10) };
            btnMiniLaunch = new Button() {
                Content = "🚀 تشغيل ستيم مع GreenLuma",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 13.5,
                Height = 42,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnMiniLaunch.Effect = new DropShadowEffect() {
                Color = Color.FromRgb(16, 185, 129),
                BlurRadius = 14,
                ShadowDepth = 0,
                Opacity = 0.8
            };
            AttachTactilePress(btnMiniLaunch);
            btnMiniLaunch.Click += (s, e) => ExecuteSmartLaunch();
            miniLaunchStack.Children.Add(btnMiniLaunch);

            btnMiniStop = new Button() {
                Content = "🛑 إيقاف وحذف الملفات",
                Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Height = 38,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed
            };
            AttachTactilePress(btnMiniStop);
            btnMiniStop.Click += (s, e) => StopSteamSession();
            miniLaunchStack.Children.Add(btnMiniStop);

            Grid.SetRow(miniLaunchStack, 0);
            miniContentGrid.Children.Add(miniLaunchStack);

            // Mini Row 1: Search & Add AppID Bar
            var miniSearchBorder = new Border() {
                Background = new SolidColorBrush(Color.FromRgb(13, 18, 27)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var miniSearchDock = new DockPanel();

            btnMiniAdd = new Button() {
                Content = "+ Add",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Padding = new Thickness(10, 4, 10, 4),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            AttachTactilePress(btnMiniAdd);
            btnMiniAdd.Click += (s, e) => {
                string raw = txtMiniSearch.Text.Trim();
                int id;
                if (int.TryParse(raw, out id) && id > 0)
                {
                    AddGameById(id, null);
                    txtMiniSearch.Text = "";
                }
                else if (!string.IsNullOrEmpty(raw))
                {
                    ApplyAppMode(false);
                    txtGameInput.Text = raw;
                    ExecuteLiveSearch(raw);
                }
            };
            DockPanel.SetDock(btnMiniAdd, Dock.Right);
            miniSearchDock.Children.Add(btnMiniAdd);

            txtMiniSearch = new TextBox() {
                Height = 26,
                FontSize = 11.5,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            txtMiniSearch.KeyDown += (s, e) => {
                if (e.Key == Key.Enter)
                {
                    btnMiniAdd.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            };
            miniSearchDock.Children.Add(txtMiniSearch);
            miniSearchBorder.Child = miniSearchDock;
            Grid.SetRow(miniSearchBorder, 1);
            miniContentGrid.Children.Add(miniSearchBorder);

            // Mini Row 2: Scrollable Games List
            var miniScroll = new ScrollViewer() {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 8)
            };
            miniGamesStack = new StackPanel();
            miniScroll.Content = miniGamesStack;
            Grid.SetRow(miniScroll, 2);
            miniContentGrid.Children.Add(miniScroll);

            // Mini Row 3: Mini Footer Dock
            var miniFooterDock = new DockPanel() { Margin = new Thickness(0, 4, 0, 0) };

            lblMiniStatus = new TextBlock() {
                Text = "● Steam Inactive",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(lblMiniStatus, Dock.Left);
            miniFooterDock.Children.Add(lblMiniStatus);

            var miniActions = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            btnMiniDoctor = new Button() {
                Content = "🩺",
                ToolTip = "Steam Doctor",
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 3, 6, 3),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 4, 0)
            };
            btnMiniDoctor.Click += (s, e) => OpenSteamDoctorModal();
            miniActions.Children.Add(btnMiniDoctor);

            btnMiniSettings = new Button() {
                Content = "⚙️",
                ToolTip = "Settings",
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 3, 6, 3),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 4, 0)
            };
            btnMiniSettings.Click += (s, e) => OpenSettingsDialog();
            miniActions.Children.Add(btnMiniSettings);

            btnMiniLang = new Button() {
                Content = "🌐",
                ToolTip = "Toggle Language",
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 3, 6, 3),
                Cursor = Cursors.Hand
            };
            btnMiniLang.Click += (s, e) => ToggleLanguage();
            miniActions.Children.Add(btnMiniLang);

            DockPanel.SetDock(miniActions, Dock.Right);
            miniFooterDock.Children.Add(miniActions);

            Grid.SetRow(miniFooterDock, 3);
            miniContentGrid.Children.Add(miniFooterDock);

            Grid.SetRow(miniContentGrid, 2);
            mainGrid.Children.Add(miniContentGrid);

            // Transparent Dismiss Backdrop for In-App Search Overlay (Multi-Monitor Safe)
            searchDismissBackdrop = new Grid() {
                Background = Brushes.Transparent,
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(searchDismissBackdrop, 2);
            Panel.SetZIndex(searchDismissBackdrop, 1400);
            searchDismissBackdrop.MouseDown += (s, e) => CloseSearchOverlay();
            mainGrid.Children.Add(searchDismissBackdrop);

            // In-App Search Results Overlay (Docked inside Main Window)
            searchOverlayBorder = new Border() {
                Margin = new Thickness(20, 114, 20, 0),
                MaxHeight = 440,
                Background = new SolidColorBrush(Color.FromRgb(11, 16, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed
            };
            var popupGlow = new DropShadowEffect() {
                Color = Color.FromRgb(16, 185, 129),
                BlurRadius = 24,
                ShadowDepth = 4,
                Opacity = 0.6
            };
            searchOverlayBorder.Effect = popupGlow;

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

            searchOverlayBorder.Child = popupContentStack;
            Grid.SetRow(searchOverlayBorder, 2);
            Panel.SetZIndex(searchOverlayBorder, 1500);
            mainGrid.Children.Add(searchOverlayBorder);

            // =========================================================================
            // --- 4. SELECTED GAMES & DLCS (Library Grid with 2-Tier Header) ---
            // =========================================================================
            var libraryContainer = new Grid() { Margin = new Thickness(20, 4, 20, 16) };
            libraryContainer.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            libraryContainer.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

            var libraryHeaderStack = new StackPanel() { Margin = new Thickness(0, 0, 0, 10) };

            // --- Tier 1: Category Filter Tabs & Dual View Switcher ---
            var libNavDock = new DockPanel() { LastChildFill = false };

            // Right: View Switcher (Cards vs List)
            var viewSwitcherBorder = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(30, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(2),
                VerticalAlignment = VerticalAlignment.Center
            };
            var switcherStack = new StackPanel() { Orientation = Orientation.Horizontal };

            btnViewCards = new Button() {
                Content = "⊞ Cards",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(10, 3, 10, 3),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            AttachTactilePress(btnViewCards);
            btnViewCards.Click += (s, e) => SwitchViewMode("cards");

            btnViewList = new Button() {
                Content = "☰ List",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(10, 3, 10, 3),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            AttachTactilePress(btnViewList);
            btnViewList.Click += (s, e) => SwitchViewMode("list");

            switcherStack.Children.Add(btnViewCards);
            switcherStack.Children.Add(btnViewList);
            viewSwitcherBorder.Child = switcherStack;
            DockPanel.SetDock(viewSwitcherBorder, Dock.Right);
            libNavDock.Children.Add(viewSwitcherBorder);

            // Left: Title & Category Tabs
            var navLeftStack = new StackPanel() { 
                Orientation = Orientation.Horizontal, 
                VerticalAlignment = VerticalAlignment.Center 
            };
            lblLibraryTitle = new TextBlock() {
                Text = "🎮 APPLIST:",
                FontSize = 13.5,
                FontWeight = FontWeights.ExtraBold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            navLeftStack.Children.Add(lblLibraryTitle);

            var tabsStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btnTabAll = CreateFilterTabButton("All (0)", "all", true);
            btnTabGames = CreateFilterTabButton("🎮 Games (0)", "games", false);
            btnTabDlcs = CreateFilterTabButton("🧩 DLCs (0)", "dlcs", false);
            tabsStack.Children.Add(btnTabAll);
            tabsStack.Children.Add(btnTabGames);
            tabsStack.Children.Add(btnTabDlcs);
            navLeftStack.Children.Add(tabsStack);

            DockPanel.SetDock(navLeftStack, Dock.Left);
            libNavDock.Children.Add(navLeftStack);

            libraryHeaderStack.Children.Add(libNavDock);

            // --- Tier 2: Search Filter & Management Actions Toolbar ---
            var libToolbarDock = new DockPanel() { Margin = new Thickness(0, 8, 0, 0) };

            // Left: In-library search filter input with search icon container
            var filterContainer = new Border() {
                Width = 220,
                Height = 28,
                Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var filterTg = new TransformGroup();
            var filterSt = new ScaleTransform(1.0, 1.0);
            filterTg.Children.Add(filterSt);
            filterContainer.RenderTransformOrigin = new Point(0.5, 0.5);
            filterContainer.RenderTransform = filterTg;

            var filterDock = new DockPanel();
            var filterIcon = new TextBlock() {
                Text = "🔍",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            DockPanel.SetDock(filterIcon, Dock.Left);
            filterDock.Children.Add(filterIcon);

            var filterHint = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            filterHint.Child = new TextBlock() {
                Text = "Ctrl + F",
                FontSize = 9.5,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
            DockPanel.SetDock(filterHint, Dock.Right);
            filterDock.Children.Add(filterHint);

            txtFilterInput = new TextBox() {
                FontSize = 11.5,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            txtFilterInput.TextChanged += (s, e) => {
                filterHint.Visibility = string.IsNullOrEmpty(txtFilterInput.Text) ? Visibility.Visible : Visibility.Collapsed;
                RenderGamesUI();
            };
            txtFilterInput.GotFocus += (s, e) => {
                filterContainer.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                filterContainer.Effect = new DropShadowEffect() {
                    Color = Color.FromRgb(16, 185, 129),
                    BlurRadius = 14,
                    ShadowDepth = 0,
                    Opacity = 0.8
                };
                filterSt.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 1.025, TimeSpan.FromMilliseconds(120)) { DecelerationRatio = 0.5 });
                filterSt.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 1.025, TimeSpan.FromMilliseconds(120)) { DecelerationRatio = 0.5 });
            };
            txtFilterInput.LostFocus += (s, e) => {
                filterContainer.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                filterContainer.Effect = null;
                filterSt.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.025, 1.0, TimeSpan.FromMilliseconds(140)) { AccelerationRatio = 0.5 });
                filterSt.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.025, 1.0, TimeSpan.FromMilliseconds(140)) { AccelerationRatio = 0.5 });
            };
            filterDock.Children.Add(txtFilterInput);
            filterContainer.Child = filterDock;

            DockPanel.SetDock(filterContainer, Dock.Left);
            libToolbarDock.Children.Add(filterContainer);

            // Right: Utility Actions (Share, Import, Toggle All, Clear All)
            var libToolsStack = new StackPanel() { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center 
            };

            btnShareLibrary = new Button() {
                Content = "📋 Share",
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                Height = 28,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            AttachTactilePress(btnShareLibrary);
            btnShareLibrary.Click += (s, e) => ExportLibraryToClipboard();
            libToolsStack.Children.Add(btnShareLibrary);

            btnImportLibrary = new Button() {
                Content = "📥 Import",
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                Height = 28,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            AttachTactilePress(btnImportLibrary);
            btnImportLibrary.Click += (s, e) => ImportLibraryFromClipboard();
            libToolsStack.Children.Add(btnImportLibrary);

            btnToggleAll = new Button() {
                Content = "🔄 Toggle All",
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                Height = 28,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            AttachTactilePress(btnToggleAll);
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
                Padding = new Thickness(10, 4, 10, 4),
                Height = 28,
                Cursor = Cursors.Hand
            };
            AttachTactilePress(btnClearAll);
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
                    ShowToast(isAr ? "تم مسح كافة الألعاب من القائمة" : "Library cleared successfully", ToastType.Info);
                }
            };
            libToolsStack.Children.Add(btnClearAll);

            DockPanel.SetDock(libToolsStack, Dock.Right);
            libToolbarDock.Children.Add(libToolsStack);

            libraryHeaderStack.Children.Add(libToolbarDock);

            Grid.SetRow(libraryHeaderStack, 0);
            libraryContainer.Children.Add(libraryHeaderStack);

            var scroll = new ScrollViewer() {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var gamesContainerGrid = new Grid();
            gamesWrapPanel = new WrapPanel() { Orientation = Orientation.Horizontal };
            gamesListStack = new StackPanel() { Visibility = Visibility.Collapsed };
            gamesContainerGrid.Children.Add(gamesWrapPanel);
            gamesContainerGrid.Children.Add(gamesListStack);

            // Modern Empty State with Trending Quick-Add Chips
            emptyStatePanel = new StackPanel() {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 20),
                Visibility = Visibility.Collapsed
            };

            var emptyIconBorder = new Border() {
                Width = 64,
                Height = 64,
                CornerRadius = new CornerRadius(32),
                Background = new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };
            emptyIconBorder.Child = new TextBlock() {
                Text = "🎮",
                FontSize = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            emptyStatePanel.Children.Add(emptyIconBorder);

            txtEmptyTitle = new TextBlock() {
                Text = "Unlocked Library is Empty",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            emptyStatePanel.Children.Add(txtEmptyTitle);

            txtEmptySubtitle = new TextBlock() {
                Text = "Search by game title above, enter an AppID, or quick-add popular titles below:",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 18)
            };
            emptyStatePanel.Children.Add(txtEmptySubtitle);

            var chipsWrap = new WrapPanel() {
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 680
            };

            var popularGames = new Tuple<string, int>[] {
                Tuple.Create("⚡ Arc Raiders", 1808500),
                Tuple.Create("⚔️ Elden Ring", 1245620),
                Tuple.Create("🐒 Black Myth: Wukong", 2358720),
                Tuple.Create("💥 Helldivers 2", 553850),
                Tuple.Create("🏎️ Forza Horizon 5", 1551360),
                Tuple.Create("🤖 Cyberpunk 2077", 1091500)
            };

            foreach (var item in popularGames)
            {
                var chip = new Border() {
                    Background = new SolidColorBrush(Color.FromArgb(40, 15, 23, 42)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(4),
                    Cursor = Cursors.Hand
                };
                var chipStack = new StackPanel() { Orientation = Orientation.Horizontal };
                chipStack.Children.Add(new TextBlock() {
                    Text = item.Item1,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
                });
                chip.Child = chipStack;

                int curId = item.Item2;
                string curName = item.Item1;
                chip.MouseDown += (s, e) => {
                    AddGameById(curId, curName);
                };

                chip.MouseEnter += (s, e) => chip.Background = new SolidColorBrush(Color.FromArgb(80, 16, 185, 129));
                chip.MouseLeave += (s, e) => chip.Background = new SolidColorBrush(Color.FromArgb(40, 15, 23, 42));

                chipsWrap.Children.Add(chip);
            }
            emptyStatePanel.Children.Add(chipsWrap);
            gamesContainerGrid.Children.Add(emptyStatePanel);

            scroll.Content = gamesContainerGrid;
            Grid.SetRow(scroll, 1);
            libraryContainer.Children.Add(scroll);

            Grid.SetRow(libraryContainer, 1);
            mainContentGrid.Children.Add(libraryContainer);

            var rootGrid = new Grid();
            rootGrid.Children.Add(mainGrid);

            // In-App Frosted Modal Layer (Panel.ZIndex = 3000)
            inAppModalHost = new Grid() {
                Background = new SolidColorBrush(Color.FromArgb(195, 6, 9, 16)),
                Visibility = Visibility.Collapsed
            };
            Panel.SetZIndex(inAppModalHost, 3000);
            rootGrid.Children.Add(inAppModalHost);

            // In-App Toast Notifications Layer (Panel.ZIndex = 4000)
            toastNotificationHost = new StackPanel() {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 24, 20),
                IsHitTestVisible = true
            };
            Panel.SetZIndex(toastNotificationHost, 4000);
            rootGrid.Children.Add(toastNotificationHost);

            rootBorder.Child = rootGrid;
            Content = rootBorder;

            // Apply Saved Dual View Mode & Dual Operating Mode
            SwitchViewMode(string.IsNullOrEmpty(settings.viewMode) ? "cards" : settings.viewMode);
            if (settings.isMiniMode)
            {
                ApplyAppMode(true);
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (isMiniMode) return;
            bool isAr = (settings.lang == "ar");

            if (e.NewSize.Width < 980)
            {
                if (txtLogoSub != null) txtLogoSub.Visibility = Visibility.Collapsed;
                if (txtBadgeVer != null) txtBadgeVer.Visibility = Visibility.Collapsed;
                if (btnModeToggle != null) btnModeToggle.Content = "⛶";
                if (btnSettings != null) btnSettings.Content = "⚙️";
            }
            else
            {
                if (txtLogoSub != null) txtLogoSub.Visibility = Visibility.Visible;
                if (txtBadgeVer != null) txtBadgeVer.Visibility = Visibility.Visible;
                if (btnModeToggle != null) btnModeToggle.Content = isMiniMode ? (isAr ? "🗖 مكبر" : "🗖 Big") : (isAr ? "⛶ مصغر" : "⛶ Mini");
                if (btnSettings != null) btnSettings.Content = isAr ? "⚙️ الإعدادات" : "⚙️ Settings";
            }

            if (e.NewSize.Width < 840)
            {
                if (badgeStealthMode != null) badgeStealthMode.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (badgeStealthMode != null) badgeStealthMode.Visibility = Visibility.Visible;
            }
        }

        private void ApplyAppMode(bool mini)
        {
            isMiniMode = mini;
            settings.isMiniMode = mini;
            SaveSettings();

            bool isAr = (settings.lang == "ar");

            if (mini)
            {
                // Save geometry of dashboard
                if (WindowState == WindowState.Normal)
                {
                    savedMainWidth = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
                    savedMainHeight = this.ActualHeight > 0 ? this.ActualHeight : this.Height;
                    savedMainLeft = this.Left;
                    savedMainTop = this.Top;
                }

                if (WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                }

                this.MinWidth = 360;
                this.MinHeight = 480;
                this.Width = 380;
                this.Height = 620;

                // Dock near Windows system tray
                var workArea = SystemParameters.WorkArea;
                this.Left = Math.Max(0, workArea.Right - this.Width - 16);
                this.Top = Math.Max(0, workArea.Bottom - this.Height - 16);

                if (mainContentGrid != null) mainContentGrid.Visibility = Visibility.Collapsed;
                if (miniContentGrid != null) miniContentGrid.Visibility = Visibility.Visible;
                if (btnWinMax != null) btnWinMax.Visibility = Visibility.Collapsed;
                if (unifiedStatusPill != null) unifiedStatusPill.Visibility = Visibility.Collapsed;
                if (txtLogoSub != null) txtLogoSub.Visibility = Visibility.Collapsed;

                if (btnModeToggle != null)
                {
                    btnModeToggle.Content = isAr ? "🗖 مكبر" : "🗖 Big";
                    btnModeToggle.ToolTip = isAr ? "التبديل إلى لوحة التحكم الكاملة" : "Expand to Full Dashboard";
                }

                RenderMiniGamesList();
            }
            else
            {
                this.MinWidth = 760;
                this.MinHeight = 540;
                this.Width = (savedMainWidth >= 760) ? savedMainWidth : 1140;
                this.Height = (savedMainHeight >= 540) ? savedMainHeight : 780;

                var workArea = SystemParameters.WorkArea;
                if (savedMainLeft >= 0 && savedMainLeft < (workArea.Right - 100) &&
                    savedMainTop >= 0 && savedMainTop < (workArea.Bottom - 100))
                {
                    this.Left = savedMainLeft;
                    this.Top = savedMainTop;
                }
                else
                {
                    this.Left = Math.Max(workArea.Left + 40, (workArea.Width - this.Width) / 2);
                    this.Top = Math.Max(workArea.Top + 40, (workArea.Height - this.Height) / 2);
                }

                if (mainContentGrid != null) mainContentGrid.Visibility = Visibility.Visible;
                if (miniContentGrid != null) miniContentGrid.Visibility = Visibility.Collapsed;
                if (btnWinMax != null) btnWinMax.Visibility = Visibility.Visible;
                if (unifiedStatusPill != null) unifiedStatusPill.Visibility = Visibility.Visible;
                if (txtLogoSub != null) txtLogoSub.Visibility = Visibility.Visible;

                if (btnModeToggle != null)
                {
                    btnModeToggle.Content = isAr ? "⛶ مصغر" : "⛶ Mini";
                    btnModeToggle.ToolTip = isAr ? "التبديل إلى المنظر المصغر (نافذة صينية سريعة)" : "Switch to Compact Tray Mode";
                }

                RenderGamesUI();
            }
        }

        private void SwitchViewMode(string mode)
        {
            currentViewMode = (mode == "list") ? "list" : "cards";
            settings.viewMode = currentViewMode;
            SaveSettings();

            UpdateViewSwitcherStyles();

            if (gamesWrapPanel != null && gamesListStack != null)
            {
                if (currentViewMode == "list")
                {
                    gamesWrapPanel.Visibility = Visibility.Collapsed;
                    gamesListStack.Visibility = Visibility.Visible;
                }
                else
                {
                    gamesWrapPanel.Visibility = Visibility.Visible;
                    gamesListStack.Visibility = Visibility.Collapsed;
                }
            }

            AnimateTabSwitchTransition();
            RenderGamesUI();
        }

        private void UpdateViewSwitcherStyles()
        {
            bool isAr = (settings.lang == "ar");
            bool isCards = (currentViewMode == "cards");

            if (btnViewCards != null)
            {
                btnViewCards.Content = isAr ? "⊞ بطاقات" : "⊞ Cards";
                btnViewCards.Background = isCards ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : Brushes.Transparent;
                btnViewCards.Foreground = isCards ? new SolidColorBrush(Color.FromRgb(5, 15, 10)) : new SolidColorBrush(Color.FromRgb(148, 163, 184));
                btnViewCards.FontWeight = isCards ? FontWeights.Bold : FontWeights.Normal;
            }

            if (btnViewList != null)
            {
                btnViewList.Content = isAr ? "☰ قائمة" : "☰ List";
                btnViewList.Background = !isCards ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : Brushes.Transparent;
                btnViewList.Foreground = !isCards ? new SolidColorBrush(Color.FromRgb(5, 15, 10)) : new SolidColorBrush(Color.FromRgb(148, 163, 184));
                btnViewList.FontWeight = !isCards ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        private void AttachTactilePress(UIElement btn)
        {
            if (btn == null) return;
            var tg = btn.RenderTransform as TransformGroup;
            if (tg == null)
            {
                tg = new TransformGroup();
                tg.Children.Add(new ScaleTransform(1.0, 1.0));
                tg.Children.Add(new TranslateTransform(0, 0));
                btn.RenderTransformOrigin = new Point(0.5, 0.5);
                btn.RenderTransform = tg;
            }
            var st = tg.Children[0] as ScaleTransform;

            btn.PreviewMouseDown += (s, e) => {
                if (st != null)
                {
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 0.94, TimeSpan.FromMilliseconds(70)) { DecelerationRatio = 0.5 });
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 0.94, TimeSpan.FromMilliseconds(70)) { DecelerationRatio = 0.5 });
                }
            };
            btn.PreviewMouseUp += (s, e) => {
                if (st != null)
                {
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(100)) { DecelerationRatio = 0.5 });
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(100)) { DecelerationRatio = 0.5 });
                }
            };
        }

        private void AnimateTabSwitchTransition()
        {
            bool isAr = (settings.lang == "ar");
            FrameworkElement target = (currentViewMode == "list") ? (FrameworkElement)gamesListStack : (FrameworkElement)gamesWrapPanel;
            if (target != null)
            {
                var tg = target.RenderTransform as TransformGroup;
                if (tg == null)
                {
                    tg = new TransformGroup();
                    tg.Children.Add(new ScaleTransform(1.0, 1.0));
                    tg.Children.Add(new TranslateTransform(0, 0));
                    target.RenderTransformOrigin = new Point(0.5, 0.5);
                    target.RenderTransform = tg;
                }
                var tt = tg.Children[1] as TranslateTransform;
                if (tt != null)
                {
                    tt.X = isAr ? -35 : 35;
                    tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(isAr ? -35 : 35, 0, TimeSpan.FromMilliseconds(220)) { DecelerationRatio = 0.6 });
                }
                target.Opacity = 0.0;
                target.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)));
            }
        }

        private void AnimateAndRemoveCard(Border card, GameItem game)
        {
            if (card == null || game == null) return;
            card.IsHitTestVisible = false;

            var tg = card.RenderTransform as TransformGroup;
            ScaleTransform st = null;
            TranslateTransform tt = null;
            if (tg != null)
            {
                foreach (var t in tg.Children)
                {
                    if (t is ScaleTransform) st = (ScaleTransform)t;
                    else if (t is TranslateTransform) tt = (TranslateTransform)t;
                }
            }

            var shadow = card.Effect as DropShadowEffect;
            if (shadow != null)
            {
                shadow.Color = Color.FromRgb(239, 68, 68);
                shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(0, 32, TimeSpan.FromMilliseconds(160)));
            }

            bool isAr = (settings.lang == "ar");
            if (tt != null)
            {
                var daX = new DoubleAnimation(0, isAr ? -80 : 80, TimeSpan.FromMilliseconds(180)) {
                    AccelerationRatio = 0.6
                };
                tt.BeginAnimation(TranslateTransform.XProperty, daX);
            }
            if (st != null)
            {
                var daScale = new DoubleAnimation(1.0, 0.72, TimeSpan.FromMilliseconds(180)) {
                    AccelerationRatio = 0.6
                };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, daScale);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, daScale);
            }

            var daOp = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180)) {
                AccelerationRatio = 0.6
            };
            daOp.Completed += (s, e) => {
                gamesList.Remove(game);
                SaveGames();
                UpdateTabButtonStyles();
                RenderGamesUI();
                ShowToast(isAr ? ("تمت إزالة " + game.name) : ("Removed " + game.name), ToastType.Info);
            };
            card.BeginAnimation(UIElement.OpacityProperty, daOp);
        }

        private void AnimateAndRemoveRow(Border row, GameItem game)
        {
            if (row == null || game == null) return;
            row.IsHitTestVisible = false;

            var tg = row.RenderTransform as TransformGroup;
            ScaleTransform st = null;
            TranslateTransform tt = null;
            if (tg != null)
            {
                foreach (var t in tg.Children)
                {
                    if (t is ScaleTransform) st = (ScaleTransform)t;
                    else if (t is TranslateTransform) tt = (TranslateTransform)t;
                }
            }

            bool isAr = (settings.lang == "ar");
            if (tt != null)
            {
                var daX = new DoubleAnimation(0, isAr ? -70 : 70, TimeSpan.FromMilliseconds(160)) {
                    AccelerationRatio = 0.6
                };
                tt.BeginAnimation(TranslateTransform.XProperty, daX);
            }
            if (st != null)
            {
                var daScale = new DoubleAnimation(1.0, 0.8, TimeSpan.FromMilliseconds(160)) {
                    AccelerationRatio = 0.6
                };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, daScale);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, daScale);
            }

            var daOp = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(160)) {
                AccelerationRatio = 0.6
            };
            daOp.Completed += (s, e) => {
                gamesList.Remove(game);
                SaveGames();
                UpdateTabButtonStyles();
                RenderGamesUI();
                ShowToast(isAr ? ("تمت إزالة " + game.name) : ("Removed " + game.name), ToastType.Info);
            };
            row.BeginAnimation(UIElement.OpacityProperty, daOp);
        }

        private FrameworkElement CreateNeonToggle(bool initialValue, Action<bool> onToggled, string activeText = null, string inactiveText = null)
        {
            bool isAr = (settings.lang == "ar");
            string strActive = activeText ?? (isAr ? "مفعّل" : "Active");
            string strInactive = inactiveText ?? (isAr ? "معطّل" : "Disabled");

            var rootTg = new TransformGroup();
            var rootSt = new ScaleTransform(1.0, 1.0);
            rootTg.Children.Add(rootSt);

            var root = new Border() {
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = isAr ? "انقر للتشغيل أو التعطيل" : "Click to toggle status",
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = rootTg
            };

            var stack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Track
            var trackGlow = new DropShadowEffect() { BlurRadius = 0, ShadowDepth = 0, Color = Color.FromRgb(16, 185, 129), Opacity = 0.85 };
            var track = new Border() {
                Width = 34,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Effect = trackGlow
            };

            // Thumb inside Grid with translation
            var trackGrid = new Grid() { Width = 32, Height = 16 };
            var thumbTg = new TransformGroup();
            var thumbTt = new TranslateTransform(0, 0);
            thumbTg.Children.Add(thumbTt);

            var thumb = new Border() {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                RenderTransform = thumbTg,
                Effect = new DropShadowEffect() { BlurRadius = 4, ShadowDepth = 1, Opacity = 0.4 }
            };
            trackGrid.Children.Add(thumb);
            track.Child = trackGrid;

            // Label
            var txtLabel = new TextBlock() {
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            stack.Children.Add(track);
            stack.Children.Add(txtLabel);
            root.Child = stack;

            bool currentVal = initialValue;

            Action<bool, bool> applyState = (val, animate) => {
                if (val)
                {
                    track.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    track.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                    trackGlow.Color = Color.FromRgb(16, 185, 129);
                    txtLabel.Text = strActive;
                    txtLabel.Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183));
                    txtLabel.FontWeight = FontWeights.SemiBold;
                    if (animate)
                    {
                        thumbTt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(thumbTt.X, 18, TimeSpan.FromMilliseconds(130)) { DecelerationRatio = 0.6 });
                        trackGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(220)));
                    }
                    else
                    {
                        thumbTt.X = 18;
                    }
                }
                else
                {
                    track.Background = new SolidColorBrush(Color.FromArgb(160, 30, 41, 59));
                    track.BorderBrush = new SolidColorBrush(Color.FromArgb(200, 71, 85, 105));
                    trackGlow.Color = Color.FromRgb(100, 116, 139);
                    txtLabel.Text = strInactive;
                    txtLabel.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                    txtLabel.FontWeight = FontWeights.Normal;
                    if (animate)
                    {
                        thumbTt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(thumbTt.X, 2, TimeSpan.FromMilliseconds(130)) { DecelerationRatio = 0.6 });
                    }
                    else
                    {
                        thumbTt.X = 2;
                    }
                }
            };

            applyState(currentVal, false);

            root.MouseDown += (s, e) => {
                e.Handled = true;
                currentVal = !currentVal;
                rootSt.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 0.93, TimeSpan.FromMilliseconds(70)) { AutoReverse = true, DecelerationRatio = 0.5 });
                rootSt.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 0.93, TimeSpan.FromMilliseconds(70)) { AutoReverse = true, DecelerationRatio = 0.5 });
                applyState(currentVal, true);
                if (onToggled != null) onToggled(currentVal);
            };

            return root;
        }

        private void OpenGameDlcManagerDialog(GameItem baseGame)
        {
            if (baseGame == null) return;
            bool isAr = (settings.lang == "ar");

            var linkedDlcs = new List<GameItem>();
            foreach (var other in gamesList)
            {
                if (other.CheckIsDLC() && (other.parentAppId == baseGame.appId || (!string.IsNullOrEmpty(other.parentName) && other.parentName.Equals(baseGame.name, StringComparison.OrdinalIgnoreCase))))
                {
                    linkedDlcs.Add(other);
                }
            }

            var mainStack = new StackPanel();

            // Header Hero Banner with Base Game Info
            var headerBanner = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 245, 158, 11)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var bannerDock = new DockPanel();

            var thumbBorder = new Border() {
                Width = 64,
                Height = 36,
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromRgb(5, 8, 12)),
                Margin = new Thickness(0, 0, 10, 0)
            };
            var thumbImg = new Image() { Stretch = Stretch.UniformToFill };
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(baseGame.image, UriKind.RelativeOrAbsolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                thumbImg.Source = bmp;
            }
            catch { }
            thumbBorder.Child = thumbImg;
            DockPanel.SetDock(thumbBorder, Dock.Left);
            bannerDock.Children.Add(thumbBorder);

            var titleStack = new StackPanel() { VerticalAlignment = VerticalAlignment.Center };
            var baseTitle = new TextBlock() {
                Text = baseGame.name,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            titleStack.Children.Add(baseTitle);

            var subTitle = new TextBlock() {
                Text = isAr ? 
                    string.Format("إدارة الإضافات المرتبطة ({0} DLCs) | AppID: {1}", linkedDlcs.Count, baseGame.appId) : 
                    string.Format("Manage Linked DLCs ({0} DLCs) | AppID: {1}", linkedDlcs.Count, baseGame.appId),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            titleStack.Children.Add(subTitle);
            bannerDock.Children.Add(titleStack);

            headerBanner.Child = bannerDock;
            mainStack.Children.Add(headerBanner);

            // Management Toolbar
            var toolbarDock = new DockPanel() { Margin = new Thickness(0, 0, 0, 10) };

            var btnScanSteam = new Button() {
                Content = isAr ? "➕ فحص وتنزيل إضافات جديدة من Steam" : "➕ Scan Steam for More DLCs",
                Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            btnScanSteam.Click += (s, e) => {
                CloseInAppModal();
                FetchAndAddDLCs(baseGame.appId, baseGame.name);
            };
            DockPanel.SetDock(btnScanSteam, Dock.Left);
            toolbarDock.Children.Add(btnScanSteam);

            var quickTogglesStack = new StackPanel() { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right 
            };

            var btnEnableAll = new Button() {
                Content = isAr ? "🟢 تفعيل الكل" : "🟢 Enable All",
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 10.5,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };

            var btnDisableAll = new Button() {
                Content = isAr ? "⚪ تعطيل الكل" : "⚪ Disable All",
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 10.5,
                Cursor = Cursors.Hand
            };

            quickTogglesStack.Children.Add(btnEnableAll);
            quickTogglesStack.Children.Add(btnDisableAll);
            toolbarDock.Children.Add(quickTogglesStack);

            mainStack.Children.Add(toolbarDock);

            // DLC List ScrollViewer
            var scroll = new ScrollViewer() {
                MaxHeight = 340,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var listStack = new StackPanel();

            Action refreshDlcList = null;
            refreshDlcList = () => {
                listStack.Children.Clear();
                if (linkedDlcs.Count == 0)
                {
                    listStack.Children.Add(new TextBlock() {
                        Text = isAr ? "لا توجد إضافات مرتبطة بهذه اللعبة حالياً." : "No DLCs currently linked to this game.",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 25, 0, 25)
                    });
                    return;
                }

                foreach (var dlc in linkedDlcs)
                {
                    var itemRef = dlc;
                    var rowBorder = new Border() {
                        Background = new SolidColorBrush(Color.FromArgb(40, 15, 23, 42)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(8, 5, 8, 5),
                        Margin = new Thickness(0, 0, 0, 5)
                    };

                    var rowDock = new DockPanel();

                    // Right side: Neon Toggle + Remove Button
                    var rightStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    DockPanel.SetDock(rightStack, Dock.Right);

                    var neonToggle = CreateNeonToggle(itemRef.enabled, (val) => {
                        itemRef.enabled = val;
                        SaveGames();
                        UpdateStatusUI();
                    });
                    neonToggle.Margin = new Thickness(0, 0, 8, 0);
                    rightStack.Children.Add(neonToggle);

                    var btnDel = new Button() {
                        Content = "✕",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Background = Brushes.Transparent,
                        Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(6, 2, 6, 2),
                        Cursor = Cursors.Hand
                    };
                    btnDel.Click += (s, e) => {
                        gamesList.Remove(itemRef);
                        linkedDlcs.Remove(itemRef);
                        SaveGames();
                        UpdateTabButtonStyles();
                        RenderGamesUI();
                        refreshDlcList();
                    };
                    rightStack.Children.Add(btnDel);
                    rowDock.Children.Add(rightStack);

                    // Left side: AppID + DLC Name
                    var leftStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                    var idBadge = new Border() {
                        Background = new SolidColorBrush(Color.FromArgb(30, 245, 158, 11)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(60, 245, 158, 11)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    idBadge.Child = new TextBlock() {
                        Text = itemRef.appId.ToString(),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 10.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36))
                    };
                    leftStack.Children.Add(idBadge);

                    var txtTitle = new TextBlock() {
                        Text = itemRef.name,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = itemRef.enabled ? new SolidColorBrush(Color.FromRgb(241, 245, 249)) : new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 360,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    leftStack.Children.Add(txtTitle);
                    rowDock.Children.Add(leftStack);

                    rowBorder.Child = rowDock;
                    listStack.Children.Add(rowBorder);
                }
            };

            btnEnableAll.Click += (s, e) => {
                foreach (var d in linkedDlcs) d.enabled = true;
                SaveGames();
                UpdateStatusUI();
                refreshDlcList();
                RenderGamesUI();
            };

            btnDisableAll.Click += (s, e) => {
                foreach (var d in linkedDlcs) d.enabled = false;
                SaveGames();
                UpdateStatusUI();
                refreshDlcList();
                RenderGamesUI();
            };

            refreshDlcList();
            scroll.Content = listStack;
            mainStack.Children.Add(scroll);

            // Done Button
            var btnDone = new Button() {
                Content = isAr ? "إغلاق" : "Done",
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 7, 16, 7),
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand
            };
            btnDone.Click += (s, e) => {
                CloseInAppModal();
                UpdateTabButtonStyles();
                RenderGamesUI();
            };
            mainStack.Children.Add(btnDone);

            ShowInAppModal(mainStack, isAr ? ("🧩 إدارة إضافات: " + baseGame.name) : ("🧩 DLC Manager: " + baseGame.name), () => {
                UpdateTabButtonStyles();
                RenderGamesUI();
            }, 660);
        }

        private Border CreateGameRow(GameItem game)
        {
            bool isAr = (settings.lang == "ar");
            bool isActuallyDlc = game.CheckIsDLC();

            var rowBorder = new Border() {
                Height = 44,
                Margin = new Thickness(0, 0, 0, 5),
                Background = new SolidColorBrush(Color.FromRgb(14, 19, 30)),
                BorderBrush = isActuallyDlc ? 
                    new SolidColorBrush(Color.FromArgb(60, 245, 158, 11)) : 
                    new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 4, 10, 4)
            };

            var rowTg = new TransformGroup();
            var rowSt = new ScaleTransform(1.0, 1.0);
            var rowTt = new TranslateTransform(0, 0);
            rowTg.Children.Add(rowSt);
            rowTg.Children.Add(rowTt);
            rowBorder.RenderTransformOrigin = new Point(0.5, 0.5);
            rowBorder.RenderTransform = rowTg;

            var rowDock = new DockPanel() { LastChildFill = true };

            // Left: Thumbnail + AppID + Title + Type Badge
            var leftStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(leftStack, Dock.Left);

            // Mini Thumbnail
            var thumbBorder = new Border() {
                Width = 48,
                Height = 30,
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromRgb(5, 8, 12)),
                Margin = new Thickness(0, 0, 10, 0)
            };
            var thumbImg = new Image() { Stretch = Stretch.UniformToFill };
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(game.image, UriKind.RelativeOrAbsolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                thumbImg.Source = bmp;
            }
            catch { }
            thumbBorder.Child = thumbImg;
            leftStack.Children.Add(thumbBorder);

            // AppID Monospace Badge
            var badgeId = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(30, 6, 182, 212)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 6, 182, 212)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeId.Child = new TextBlock() {
                Text = game.appId.ToString(),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(103, 232, 249))
            };
            leftStack.Children.Add(badgeId);

            // Type Badge (Clickable to toggle!)
            var typeBadge = new Border() {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 10, 0),
                Background = isActuallyDlc ? 
                    new SolidColorBrush(Color.FromArgb(50, 245, 158, 11)) : 
                    new SolidColorBrush(Color.FromArgb(50, 16, 185, 129)),
                Cursor = Cursors.Hand,
                ToolTip = isAr ? "انقر للتبديل الفوري بين لعبة كاملة وإضافة (DLC)" : "Click to instantly toggle between Game and DLC",
                VerticalAlignment = VerticalAlignment.Center
            };
            typeBadge.Child = new TextBlock() {
                Text = isActuallyDlc ? "🧩 DLC" : "🎮 GAME",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = isActuallyDlc ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(16, 185, 129))
            };
            typeBadge.MouseDown += (s, e) => {
                game.type = isActuallyDlc ? "game" : "dlc";
                SaveGames();
                UpdateTabButtonStyles();
                RenderGamesUI();
            };
            leftStack.Children.Add(typeBadge);

            // Title
            var txtTitle = new TextBlock() {
                Text = game.name,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = game.enabled ? new SolidColorBrush(Color.FromRgb(241, 245, 249)) : new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 360
            };
            leftStack.Children.Add(txtTitle);

            if (isActuallyDlc)
            {
                string parentLabel = !string.IsNullOrEmpty(game.parentName) ? game.parentName : (game.parentAppId > 0 ? ("App " + game.parentAppId) : null);
                if (!string.IsNullOrEmpty(parentLabel))
                {
                    var parentChip = new Border() {
                        Background = new SolidColorBrush(Color.FromArgb(30, 6, 182, 212)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(60, 6, 182, 212)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(8, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = Cursors.Hand,
                        ToolTip = isAr ? ("انقر لتصفية مكتبتك حسب اللعبة الأساسية: " + parentLabel) : ("Click to filter library by base game: " + parentLabel)
                    };
                    parentChip.Child = new TextBlock() {
                        Text = "🔗 " + parentLabel,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(103, 232, 249))
                    };
                    parentChip.MouseDown += (s, e) => {
                        if (txtFilterInput != null) txtFilterInput.Text = parentLabel;
                    };
                    leftStack.Children.Add(parentChip);
                }
            }

            rowDock.Children.Add(leftStack);

            // Right: DLC Indicator + Enable Toggle + Remove Button
            var rightStack = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(rightStack, Dock.Right);

            // DLC Count if game has DLCs
            int linkedDlcCount = 0;
            if (!isActuallyDlc)
            {
                foreach (var other in gamesList)
                {
                    if (other.CheckIsDLC() && (other.parentAppId == game.appId || (!string.IsNullOrEmpty(other.parentName) && other.parentName.Equals(game.name, StringComparison.OrdinalIgnoreCase))))
                    {
                        linkedDlcCount++;
                    }
                }
            }

            if (linkedDlcCount > 0)
            {
                var dlcPill = new Border() {
                    Background = new SolidColorBrush(Color.FromArgb(35, 245, 158, 11)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(70, 245, 158, 11)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    ToolTip = isAr ? string.Format("انقر لإدارة {0} إضافات تابعة لـ {1}", linkedDlcCount, game.name) : string.Format("Click to manage {0} DLCs for {1}", linkedDlcCount, game.name)
                };
                dlcPill.Child = new TextBlock() {
                    Text = string.Format("🧩 {0} DLCs", linkedDlcCount),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36))
                };
                dlcPill.MouseDown += (s, e) => {
                    OpenGameDlcManagerDialog(game);
                };
                rightStack.Children.Add(dlcPill);
            }

            // Neon Toggle Switch
            var toggleRow = CreateNeonToggle(game.enabled, (val) => {
                game.enabled = val;
                SaveGames();
                UpdateStatusUI();
                RenderGamesUI();
            });
            toggleRow.Margin = new Thickness(0, 0, 8, 0);
            rightStack.Children.Add(toggleRow);

            // Remove Button
            var btnRemove = new Button() {
                Content = "✕",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 2, 6, 2),
                Cursor = Cursors.Hand
            };
            btnRemove.MouseEnter += (s, e) => btnRemove.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            btnRemove.MouseLeave += (s, e) => btnRemove.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            btnRemove.Click += (s, e) => {
                AnimateAndRemoveRow(rowBorder, game);
            };
            rightStack.Children.Add(btnRemove);

            rowDock.Children.Add(rightStack);
            rowBorder.Child = rowDock;

            rowBorder.MouseEnter += (s, e) => {
                rowBorder.Background = new SolidColorBrush(Color.FromRgb(24, 34, 52));
                rowBorder.BorderBrush = isActuallyDlc ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(16, 185, 129));
                rowTt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, isAr ? -6 : 6, TimeSpan.FromMilliseconds(120)) { DecelerationRatio = 0.5 });
                rowSt.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 1.008, TimeSpan.FromMilliseconds(120)));
            };
            rowBorder.MouseLeave += (s, e) => {
                rowBorder.Background = new SolidColorBrush(Color.FromRgb(14, 19, 30));
                rowBorder.BorderBrush = isActuallyDlc ? new SolidColorBrush(Color.FromArgb(60, 245, 158, 11)) : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                rowTt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(isAr ? -6 : 6, 0, TimeSpan.FromMilliseconds(140)));
                rowSt.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.008, 1.0, TimeSpan.FromMilliseconds(140)));
            };

            return rowBorder;
        }

        private void RenderMiniGamesList()
        {
            if (miniGamesStack == null) return;
            miniGamesStack.Children.Clear();

            bool isAr = (settings.lang == "ar");

            if (gamesList.Count == 0)
            {
                var emptyText = new TextBlock() {
                    Text = isAr ? "لا توجد ألعاب مضافة" : "No games in library",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 24, 0, 0)
                };
                miniGamesStack.Children.Add(emptyText);
                return;
            }

            foreach (var g in gamesList)
            {
                var gameItem = g;
                var row = new Border() {
                    Height = 36,
                    Margin = new Thickness(0, 0, 0, 4),
                    Background = new SolidColorBrush(Color.FromRgb(14, 19, 30)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 2, 8, 2),
                    Cursor = Cursors.Hand
                };

                var dock = new DockPanel() { LastChildFill = true };

                var btnRemove = new Button() {
                    Content = "✕",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Padding = new Thickness(4, 2, 4, 2)
                };
                btnRemove.MouseEnter += (s, e) => btnRemove.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                btnRemove.MouseLeave += (s, e) => btnRemove.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                btnRemove.Click += (s, e) => {
                    gamesList.Remove(gameItem);
                    SaveGames();
                    UpdateTabButtonStyles();
                    RenderGamesUI();
                    RenderMiniGamesList();
                };
                DockPanel.SetDock(btnRemove, Dock.Right);
                dock.Children.Add(btnRemove);

                var leftStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                var chkStatus = new TextBlock() {
                    Text = gameItem.enabled ? "●" : "○",
                    FontSize = 12,
                    Foreground = gameItem.enabled ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                leftStack.Children.Add(chkStatus);

                var titleText = new TextBlock() {
                    Text = gameItem.name,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = gameItem.enabled ? new SolidColorBrush(Color.FromRgb(241, 245, 249)) : new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 220,
                    VerticalAlignment = VerticalAlignment.Center
                };
                leftStack.Children.Add(titleText);
                dock.Children.Add(leftStack);

                row.Child = dock;

                row.MouseDown += (s, e) => {
                    if (e.OriginalSource != btnRemove)
                    {
                        gameItem.enabled = !gameItem.enabled;
                        SaveGames();
                        RenderGamesUI();
                        RenderMiniGamesList();
                    }
                };

                miniGamesStack.Children.Add(row);
            }
        }

        private bool IsRemoteVersionNewer(string remoteTag, string localTag)
        {
            if (string.IsNullOrEmpty(remoteTag) || string.IsNullOrEmpty(localTag)) return false;
            try
            {
                string rClean = remoteTag.Trim().TrimStart('v', 'V');
                string lClean = localTag.Trim().TrimStart('v', 'V');
                Version rVer, lVer;
                if (Version.TryParse(rClean, out rVer) && Version.TryParse(lClean, out lVer))
                {
                    return rVer > lVer;
                }

                string[] rParts = rClean.Split('.');
                string[] lParts = lClean.Split('.');
                int len = Math.Max(rParts.Length, lParts.Length);
                for (int i = 0; i < len; i++)
                {
                    int rNum = 0, lNum = 0;
                    if (i < rParts.Length) int.TryParse(rParts[i], out rNum);
                    if (i < lParts.Length) int.TryParse(lParts[i], out lNum);
                    if (rNum > lNum) return true;
                    if (rNum < lNum) return false;
                }
            }
            catch { }
            return false;
        }

        private void UpdateModeButtonStyles()
        {
            bool isAr = (settings.lang == "ar");
            if (txtStealthMode != null)
            {
                txtStealthMode.Text = "🛡️ user32SF";
                txtStealthMode.ToolTip = isAr ? "محرك التخفي الأحدث لستيم (متوافق مع نظام العائلات وجميع الألعاب)" : "Universal Steam Stealth Engine (Compatible with Families & all modern games)";
            }
        }

        private void UpdateClearCacheButton()
        {
            bool isAr = (settings.lang == "ar");
            if (settings.clearAppCache)
            {
                btnToggleClearCache.Content = isAr ? "🔥 مسح الكاش: مفعل" : "🔥 AppCache: ON";
                btnToggleClearCache.Background = new SolidColorBrush(Color.FromArgb(50, 245, 158, 11));
                btnToggleClearCache.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                btnToggleClearCache.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                btnToggleClearCache.ToolTip = isAr ? "سيتم مسح كاش ستيم تلقائياً عبر DeleteSteamAppCache.exe قبل الحقن" : "Steam app cache will be cleared via DeleteSteamAppCache.exe before injection";
            }
            else
            {
                btnToggleClearCache.Content = isAr ? "❄️ مسح الكاش: معطل" : "❄️ AppCache: OFF";
                btnToggleClearCache.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                btnToggleClearCache.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                btnToggleClearCache.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                btnToggleClearCache.ToolTip = isAr ? "لن يتم مسح كاش ستيم" : "Steam app cache will not be cleared";
            }
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
            AttachTactilePress(btn);
            btn.Click += (s, e) => {
                currentFilterTab = tabKey;
                UpdateTabButtonStyles();
                AnimateTabSwitchTransition();
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

            if (lblTelemetryUnlocked != null)
            {
                lblTelemetryUnlocked.Text = isAr ? 
                    string.Format("🎯 {0} مفتوح", total) : 
                    string.Format("🎯 {0} Unlocked", total);
            }
        }

        private void SetTabActive(Button btn, bool active)
        {
            btn.Background = active ? new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)) : new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            btn.Foreground = active ? new SolidColorBrush(Color.FromRgb(110, 231, 183)) : new SolidColorBrush(Color.FromRgb(148, 163, 184));
            btn.BorderBrush = active ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        }

        private List<InstalledGameInfo> ScanInstalledSteamGames()
        {
            var results = new List<InstalledGameInfo>();
            var seenAppIds = new HashSet<int>();
            var libraryPaths = new List<string>();
            var knownLibPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Action<string> addLibPath = (p) => {
                if (string.IsNullOrEmpty(p)) return;
                try
                {
                    string full = Path.GetFullPath(p);
                    if (Directory.Exists(full) && !knownLibPaths.Contains(full))
                    {
                        knownLibPaths.Add(full);
                        libraryPaths.Add(full);
                    }
                }
                catch { }
            };

            // 1. Primary Steam folder
            if (!string.IsNullOrEmpty(steamDir))
            {
                addLibPath(steamDir);
            }

            // 2. Discover libraries from libraryfolders.vdf
            try
            {
                string vdfPath = Path.Combine(steamDir, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    string content = File.ReadAllText(vdfPath);
                    var matches = Regex.Matches(content, @"""path""\s*""([^""]+)""");
                    foreach (Match m in matches)
                    {
                        if (m.Groups.Count > 1)
                        {
                            string raw = m.Groups[1].Value.Replace(@"\\", @"\");
                            addLibPath(raw);
                        }
                    }
                }
            }
            catch { }

            // 3. Fallback drive roots
            string[] fallbackRoots = new string[] { @"C:\SteamLibrary", @"D:\SteamLibrary", @"E:\SteamLibrary", @"F:\SteamLibrary", @"G:\SteamLibrary" };
            foreach (var fb in fallbackRoots)
            {
                addLibPath(fb);
            }

            // 4. Scan appmanifest_*.acf in each library's steamapps folder
            foreach (var lib in libraryPaths)
            {
                string steamappsDir = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(steamappsDir))
                {
                    steamappsDir = lib;
                }

                if (!Directory.Exists(steamappsDir)) continue;

                try
                {
                    var acfFiles = Directory.GetFiles(steamappsDir, "appmanifest_*.acf");
                    foreach (var acf in acfFiles)
                    {
                        try
                        {
                            string text = File.ReadAllText(acf);
                            var mId = Regex.Match(text, @"""appid""\s*""(\d+)""");
                            var mName = Regex.Match(text, @"""name""\s*""([^""]+)""");
                            var mDir = Regex.Match(text, @"""installdir""\s*""([^""]+)""");
                            var mSize = Regex.Match(text, @"""SizeOnDisk""\s*""(\d+)""");

                            if (mId.Success && mName.Success)
                            {
                                int id = int.Parse(mId.Groups[1].Value);
                                string name = mName.Groups[1].Value;

                                if (id == 228980 || id == 1070560 || id == 1391110 || id == 1493710 || id == 2180100)
                                    continue;
                                if (name.StartsWith("Steamworks", StringComparison.OrdinalIgnoreCase) ||
                                    name.StartsWith("Proton", StringComparison.OrdinalIgnoreCase) ||
                                    name.StartsWith("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                if (!seenAppIds.Contains(id))
                                {
                                    seenAppIds.Add(id);
                                    long sz = 0;
                                    if (mSize.Success) long.TryParse(mSize.Groups[1].Value, out sz);

                                    bool alreadyInLib = false;
                                    foreach (var g in gamesList)
                                    {
                                        if (g.appId == id) { alreadyInLib = true; break; }
                                    }

                                    results.Add(new InstalledGameInfo() {
                                        appId = id,
                                        name = name,
                                        installDir = mDir.Success ? mDir.Groups[1].Value : "",
                                        sizeBytes = sz,
                                        isAlreadyAdded = alreadyInLib,
                                        isSelected = !alreadyInLib
                                    });
                                }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // Sort: Not yet added first, then alphabetically by name
            results.Sort((a, b) => {
                if (a.isAlreadyAdded != b.isAlreadyAdded)
                    return a.isAlreadyAdded ? 1 : -1;
                return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });

            return results;
        }

        private void OpenInstalledGamesModal()
        {
            bool isAr = (settings.lang == "ar");
            var installedGames = ScanInstalledSteamGames();

            var mainStack = new StackPanel();

            // Header summary banner
            var headerBanner = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var bannerStack = new StackPanel();
            bannerStack.Children.Add(new TextBlock() {
                Text = isAr ? 
                    string.Format("تم العثور على {0} لعبة مثبتة على هذا الجهاز عبر مكتبات Steam.", installedGames.Count) : 
                    string.Format("Found {0} installed games across your local Steam libraries.", installedGames.Count),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249))
            });
            bannerStack.Children.Add(new TextBlock() {
                Text = isAr ? 
                    "اختر الألعاب التي ترغب في إضافتها إلى قائمة ألعاب GreenLuma بنقرة واحدة:" : 
                    "Select games you want to import into your GreenLuma manager:",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 3, 0, 0)
            });
            headerBanner.Child = bannerStack;
            mainStack.Children.Add(headerBanner);

            // Filter box & quick selection buttons
            var toolRow = new DockPanel() { Margin = new Thickness(0, 0, 0, 10) };

            var selectBtnsStack = new StackPanel() { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(selectBtnsStack, Dock.Right);

            var btnSelectAllNew = new Button() {
                Content = isAr ? "☑️ تحديد الجديد فقط" : "☑️ Select All New",
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };

            var btnDeselectAll = new Button() {
                Content = isAr ? "⚪ إلغاء التحديد" : "⚪ Deselect All",
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Cursor = Cursors.Hand
            };

            selectBtnsStack.Children.Add(btnSelectAllNew);
            selectBtnsStack.Children.Add(btnDeselectAll);
            toolRow.Children.Add(selectBtnsStack);

            // Search filter box
            var filterBox = new Border() {
                Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 10, 0)
            };
            var filterTb = new TextBox() {
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderThickness = new Thickness(0),
                FontSize = 11.5,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            filterBox.Child = filterTb;
            toolRow.Children.Add(filterBox);
            mainStack.Children.Add(toolRow);

            // Games ScrollViewer list
            var scroll = new ScrollViewer() {
                MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 12)
            };
            var listStack = new StackPanel();

            var chkMap = new Dictionary<InstalledGameInfo, CheckBox>();

            Action populateList = () => {
                listStack.Children.Clear();
                chkMap.Clear();
                string filterText = filterTb.Text.Trim().ToLowerInvariant();

                foreach (var g in installedGames)
                {
                    if (!string.IsNullOrEmpty(filterText))
                    {
                        if (!g.name.ToLowerInvariant().Contains(filterText) && !g.appId.ToString().Contains(filterText))
                            continue;
                    }

                    var rowBorder = new Border() {
                        Background = new SolidColorBrush(Color.FromArgb(40, 15, 23, 42)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(8, 6, 8, 6),
                        Margin = new Thickness(0, 0, 0, 5)
                    };

                    var rowDock = new DockPanel();

                    // Checkbox or "Already Added" badge
                    if (g.isAlreadyAdded)
                    {
                        var addedBadge = new Border() {
                            Background = new SolidColorBrush(Color.FromArgb(30, 16, 185, 129)),
                            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 16, 185, 129)),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        addedBadge.Child = new TextBlock() {
                            Text = isAr ? "✓ مضاف بالفعل" : "✓ In Library",
                            FontSize = 10,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183))
                        };
                        DockPanel.SetDock(addedBadge, Dock.Right);
                        rowDock.Children.Add(addedBadge);
                    }
                    else
                    {
                        var chk = new CheckBox() {
                            IsChecked = g.isSelected,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(8, 0, 0, 0)
                        };
                        var gameRef = g;
                        chk.Checked += (s, e) => gameRef.isSelected = true;
                        chk.Unchecked += (s, e) => gameRef.isSelected = false;
                        chkMap[g] = chk;
                        DockPanel.SetDock(chk, Dock.Right);
                        rowDock.Children.Add(chk);
                    }

                    // Left side: AppID badge + Title
                    var leftStack = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                    var idBadge = new Border() {
                        Background = new SolidColorBrush(Color.FromArgb(30, 6, 182, 212)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(60, 6, 182, 212)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    idBadge.Child = new TextBlock() {
                        Text = g.appId.ToString(),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 10.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(103, 232, 249))
                    };
                    leftStack.Children.Add(idBadge);

                    var txtTitle = new TextBlock() {
                        Text = g.name,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = g.isAlreadyAdded ? new SolidColorBrush(Color.FromRgb(148, 163, 184)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 420,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    leftStack.Children.Add(txtTitle);

                    rowDock.Children.Add(leftStack);
                    rowBorder.Child = rowDock;
                    listStack.Children.Add(rowBorder);
                }

                if (listStack.Children.Count == 0)
                {
                    listStack.Children.Add(new TextBlock() {
                        Text = isAr ? "لا توجد ألعاب مطابقة للبحث." : "No matching games found.",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 20)
                    });
                }
            };

            filterTb.TextChanged += (s, e) => populateList();
            btnSelectAllNew.Click += (s, e) => {
                foreach (var g in installedGames) { if (!g.isAlreadyAdded) g.isSelected = true; }
                foreach (var kv in chkMap) { kv.Value.IsChecked = true; }
            };
            btnDeselectAll.Click += (s, e) => {
                foreach (var g in installedGames) { g.isSelected = false; }
                foreach (var kv in chkMap) { kv.Value.IsChecked = false; }
            };

            populateList();
            scroll.Content = listStack;
            mainStack.Children.Add(scroll);

            // Bottom action buttons
            var actionsDock = new DockPanel();
            var btnImport = new Button() {
                Content = isAr ? "➕ استيراد الألعاب المحددة إلى القائمة" : "➕ Import Selected Games",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 8, 16, 8),
                Cursor = Cursors.Hand
            };
            btnImport.Click += (s, e) => {
                int countAdded = 0;
                foreach (var g in installedGames)
                {
                    if (!g.isAlreadyAdded && g.isSelected)
                    {
                        gamesList.Insert(0, new GameItem() {
                            appId = g.appId,
                            name = g.name,
                            image = string.Format("https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{0}/header.jpg", g.appId),
                            enabled = true,
                            type = "game"
                        });
                        countAdded++;
                    }
                }

                if (countAdded > 0)
                {
                    SaveGames();
                    UpdateTabButtonStyles();
                    RenderGamesUI();
                    CloseInAppModal();
                    ShowToast(
                        isAr ? string.Format("✅ تم استيراد {0} لعبة بنجاح!", countAdded) : string.Format("✅ Successfully imported {0} games!", countAdded),
                        ToastType.Success);
                }
                else
                {
                    CloseInAppModal();
                }
            };
            DockPanel.SetDock(btnImport, Dock.Left);
            actionsDock.Children.Add(btnImport);

            var btnClose = new Button() {
                Content = isAr ? "إلغاء" : "Cancel",
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s, e) => CloseInAppModal();
            actionsDock.Children.Add(btnClose);

            mainStack.Children.Add(actionsDock);

            ShowInAppModal(mainStack, isAr ? "🎮 فحص واستيراد الألعاب المثبتة على الجهاز" : "🎮 Scan & Import Installed Steam Games", null, 680);
        }

        // =========================================================================
        // --- CLEAN SETTINGS PREFERENCES DIALOG (IN-APP MODAL OVERLAY) ---
        // =========================================================================
        private void OpenSettingsDialog()
        {
            bool isAr = (settings.lang == "ar");

            var sp = new StackPanel() { Margin = new Thickness(4) };

            // Helper to create category group box
            Func<string, FrameworkElement, Border> createCategory = (catTitle, content) => {
                var groupBorder = new Border() {
                    Background = new SolidColorBrush(Color.FromArgb(40, 15, 23, 42)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                var groupStack = new StackPanel();
                var txtHead = new TextBlock() {
                    Text = catTitle,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                groupStack.Children.Add(txtHead);
                groupStack.Children.Add(content);
                groupBorder.Child = groupStack;
                return groupBorder;
            };

            // 1. Language & Appearance Category
            var langStack = new DockPanel();
            var txtLangInfo = new TextBlock() {
                Text = isAr ? "اللغة الحالية: العربية" : "Current Language: English",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(txtLangInfo, Dock.Left);
            langStack.Children.Add(txtLangInfo);

            var btnSwitchLang = new Button() {
                Content = isAr ? "🌐 Switch to English" : "🌐 التبديل إلى العربية",
                Padding = new Thickness(12, 5, 12, 5),
                Background = new SolidColorBrush(Color.FromArgb(40, 6, 182, 212)),
                Foreground = new SolidColorBrush(Color.FromRgb(103, 232, 249)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(6, 182, 212)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnSwitchLang.Click += (s, e) => {
                ToggleLanguage();
                CloseInAppModal();
                OpenSettingsDialog();
            };
            langStack.Children.Add(btnSwitchLang);
            sp.Children.Add(createCategory(isAr ? "🌐 اللغة والواجهة" : "🌐 Language & Interface", langStack));

            // 2. Tools & Diagnostics Category
            var toolsGrid = new Grid();
            toolsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            toolsGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            toolsGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            toolsGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

            var btnDoc = new Button() {
                Content = isAr ? "🩺 طبيب ستيم" : "🩺 Steam Doctor",
                ToolTip = isAr ? "فحص وتشخيص مشاكل ستيم والأخطاء" : "Diagnose Steam issues & running processes",
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 6)
            };
            btnDoc.Click += (s, e) => {
                CloseInAppModal();
                OpenSteamDoctorModal();
            };
            Grid.SetRow(btnDoc, 0);
            Grid.SetColumn(btnDoc, 0);
            toolsGrid.Children.Add(btnDoc);

            var btnScanLocal = new Button() {
                Content = isAr ? "🎮 فحص الألعاب المثبتة" : "🎮 Scan Installed Games",
                ToolTip = isAr ? "فحص كافة مكتبات Steam على أقراص جهازك واستيراد الألعاب المثبتة فوراً" : "Scan all Steam libraries on local drives & import games",
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromArgb(30, 16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 6)
            };
            btnScanLocal.Click += (s, e) => {
                CloseInAppModal();
                OpenInstalledGamesModal();
            };
            Grid.SetRow(btnScanLocal, 0);
            Grid.SetColumn(btnScanLocal, 1);
            toolsGrid.Children.Add(btnScanLocal);

            var btnPurge = new Button() {
                Content = isAr ? "🧹 تنظيف ستيم" : "🧹 Clean Steam",
                ToolTip = isAr ? "إعادة تعيين وحذف كافة ملفات GreenLuma من مجلد ستيم" : "Purge GreenLuma injection files from Steam",
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromArgb(30, 239, 68, 68)),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 239, 68, 68)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnPurge.Click += (s, e) => {
                CloseInAppModal();
                PurgeAllArtifacts();
            };
            Grid.SetRow(btnPurge, 1);
            Grid.SetColumn(btnPurge, 0);
            toolsGrid.Children.Add(btnPurge);

            var btnWipeCache = new Button() {
                Content = isAr ? "🗑 مسح الكاش الآن" : "🗑 Clear Cache Now",
                ToolTip = isAr ? "تشغيل أداة DeleteSteamAppCache.exe لمسح كاش ستيم فوراً" : "Run DeleteSteamAppCache.exe to clear cache immediately",
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromArgb(30, 245, 158, 11)),
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 245, 158, 11)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btnWipeCache.Click += (s, e) => {
                ExecuteManualDeleteAppCache();
            };
            Grid.SetRow(btnWipeCache, 1);
            Grid.SetColumn(btnWipeCache, 1);
            toolsGrid.Children.Add(btnWipeCache);

            sp.Children.Add(createCategory(isAr ? "🛠️ أدوات النظام والتشخيص" : "🛠️ System Tools & Diagnostics", toolsGrid));

            // 3. Updates & Community Help Category
            var updatesStack = new StackPanel();
            var updateBtns = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            var btnCheckUp = new Button() {
                Content = isAr ? "🔄 فحص التحديثات" : "🔄 Check Updates",
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnCheckUp.Click += (s, e) => {
                CloseInAppModal();
                CheckForUpdatesAsync(true);
            };
            updateBtns.Children.Add(btnCheckUp);

            var btnGuideModal = new Button() {
                Content = isAr ? "📖 دليل الاستخدام والأسئلة" : "📖 User Guide & FAQ",
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnGuideModal.Click += (s, e) => {
                CloseInAppModal();
                OpenQuickGuideDialog();
            };
            updateBtns.Children.Add(btnGuideModal);

            var btnCsForum = new Button() {
                Content = isAr ? "🌐 منتدى CS.RIN.RU ↗" : "🌐 CS.RIN.RU Topic ↗",
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromArgb(30, 6, 182, 212)),
                Foreground = new SolidColorBrush(Color.FromRgb(103, 232, 249)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 6, 182, 212)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btnCsForum.Click += (s, e) => {
                try { Process.Start(new ProcessStartInfo("https://cs.rin.ru/forum/viewtopic.php?f=10&t=140004") { UseShellExecute = true }); } catch { }
            };
            updateBtns.Children.Add(btnCsForum);
            updatesStack.Children.Add(updateBtns);
            sp.Children.Add(createCategory(isAr ? "🔄 التحديثات والمجتمع" : "🔄 Updates & Community", updatesStack));

            // 4. Steam Paths & Options Category
            var steamConfigStack = new StackPanel();

            steamConfigStack.Children.Add(new TextBlock() {
                Text = isAr ? "مسار تثبيت Steam (مجلد steam.exe):" : "Steam Installation Directory:",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 0, 0, 4)
            });

            var steamPathDock = new DockPanel() { Margin = new Thickness(0, 0, 0, 8) };
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
            steamConfigStack.Children.Add(steamPathDock);

            btnBrowse.Click += (s, e) => {
                var dlg = new OpenFileDialog();
                dlg.Title = isAr ? "اختر ملف steam.exe" : "Select steam.exe in your Steam folder";
                dlg.Filter = "Steam Executable (steam.exe)|steam.exe";
                if (dlg.ShowDialog(this) == true)
                {
                    txtCustomSteam.Text = Path.GetDirectoryName(dlg.FileName);
                }
            };

            steamConfigStack.Children.Add(new TextBlock() {
                Text = isAr ? "معلمات التشغيل الإضافية لـ Steam:" : "Steam Launch Arguments:",
                FontSize = 11,
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
                Margin = new Thickness(0, 0, 0, 8),
                FlowDirection = FlowDirection.LeftToRight
            };
            steamConfigStack.Children.Add(txtSetArgs);

            var btnOpenAppFolder = new Button() {
                Content = isAr ? "📂 فتح مجلد البرنامج" : "📂 Open App Folder",
                Padding = new Thickness(10, 4, 10, 4),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            btnOpenAppFolder.Click += (s, e) => {
                try { Process.Start("explorer.exe", appDir); } catch { }
            };
            steamConfigStack.Children.Add(btnOpenAppFolder);

            sp.Children.Add(createCategory(isAr ? "⚙️ إعدادات ومسارات Steam" : "⚙️ Steam Paths & Configuration", steamConfigStack));

            // Action Buttons Row (Save / Cancel)
            var btnRow = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
            var btnCancel = new Button() {
                Content = isAr ? "إلغاء" : "Cancel",
                Padding = new Thickness(14, 6, 14, 6),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnCancel.Click += (s, e) => CloseInAppModal();
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
                }
                settings.steamArgs = txtSetArgs.Text.Trim();
                SaveSettings();
                CloseInAppModal();
                ShowToast(
                    isAr ? "تم حفظ الإعدادات وتطبيقها بنجاح!" : "Preferences saved and applied successfully!",
                    ToastType.Success);
            };
            btnRow.Children.Add(btnSave);

            sp.Children.Add(btnRow);
            ShowInAppModal(sp, isAr ? "⚙️ مركز الإعدادات والأدوات" : "⚙️ Settings & Tools Hub", null, 660);
        }

        // ==========================================
        // --- LIVE STEAM STORE SEARCH ENGINE ---
        // ==========================================
        private void ExecuteLiveSearch(string query)
        {
            if (string.IsNullOrEmpty(query) || query.Length < 2)
            {
                CloseSearchOverlay();
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
                                    if (id == 1174180 || id == 271590 || id == 359550 || id == 1222670) isFs = false;
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
                ShowSearchOverlay();
                return;
            }

            int resIdx = 0;
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

                var tgRow = new TransformGroup();
                var stRow = new ScaleTransform(1.0, 1.0);
                var ttRow = new TranslateTransform(isAr ? 35 : -35, 0);
                tgRow.Children.Add(stRow);
                tgRow.Children.Add(ttRow);
                rowBorder.RenderTransformOrigin = new Point(0.5, 0.5);
                rowBorder.RenderTransform = tgRow;
                rowBorder.Opacity = 0.0;

                var delay = TimeSpan.FromMilliseconds(Math.Min(resIdx * 25, 200));
                var daX = new DoubleAnimation(isAr ? 35 : -35, 0, TimeSpan.FromMilliseconds(180)) {
                    BeginTime = delay,
                    DecelerationRatio = 0.6
                };
                ttRow.BeginAnimation(TranslateTransform.XProperty, daX);

                var daOp = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180)) {
                    BeginTime = delay
                };
                rowBorder.BeginAnimation(UIElement.OpacityProperty, daOp);

                var rowDock = new DockPanel();

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
                AttachTactilePress(btnAddRow);
                int rowId = item.id;
                string rowName = item.name;
                btnAddRow.Click += (s, e) => {
                    CloseSearchOverlay();
                    txtGameInput.Text = "";
                    AddGameById(rowId, rowName);
                };
                DockPanel.SetDock(btnAddRow, Dock.Right);
                rowDock.Children.Add(btnAddRow);

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

                rowBorder.MouseEnter += (s, e) => {
                    rowBorder.Background = new SolidColorBrush(Color.FromRgb(28, 38, 58));
                    rowBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    ttRow.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, isAr ? -6 : 6, TimeSpan.FromMilliseconds(110)) { DecelerationRatio = 0.5 });
                    stRow.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 1.018, TimeSpan.FromMilliseconds(110)));
                    stRow.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 1.018, TimeSpan.FromMilliseconds(110)));
                };
                rowBorder.MouseLeave += (s, e) => {
                    rowBorder.Background = new SolidColorBrush(Color.FromRgb(18, 24, 38));
                    rowBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                    ttRow.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(isAr ? -6 : 6, 0, TimeSpan.FromMilliseconds(140)) { AccelerationRatio = 0.5 });
                    stRow.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.018, 1.0, TimeSpan.FromMilliseconds(140)));
                    stRow.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.018, 1.0, TimeSpan.FromMilliseconds(140)));
                };

                AttachTactilePress(rowBorder);
                rowBorder.MouseDown += (s, e) => {
                    CloseSearchOverlay();
                    txtGameInput.Text = "";
                    AddGameById(rowId, rowName);
                };

                searchResultsPanel.Children.Add(rowBorder);
                resIdx++;
            }

            ShowSearchOverlay();
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

            txtLogoSub.Text = isAr ? "نظام التخفي" : "STEALTH HUB";
            if (btnSettings != null)
            {
                if (this.ActualWidth < 980 && this.ActualWidth > 0)
                {
                    btnSettings.Content = "⚙️";
                }
                else
                {
                    btnSettings.Content = isAr ? "⚙️ الإعدادات" : "⚙️ Settings";
                }
            }

            UpdateModeButtonStyles();
            UpdateClearCacheButton();

            if (btnLaunch != null)
            {
                btnLaunch.Content = isAr ? "🚀 تشغيل ستيم" : "🚀 Launch Steam";
                btnLaunch.ToolTip = isAr ? "تشغيل تلقائي متكامل مع GreenLuma: إغلاق ستيم، مسح الكاش، تجهيز الملفات وتشغيل ستيم فوراً" : "1-Click Smart Launch: Closes Steam, clears cache, injects files & starts Steam cleanly";
            }
            if (btnInjectFiles != null)
            {
                btnInjectFiles.Content = isAr ? "📦 تجهيز الملفات" : "📦 Inject Files";
                btnInjectFiles.ToolTip = isAr ? "تجهيز وحقن ملفات GreenLuma و AppList داخل ستيم دون تشغيله" : "Prepare GreenLuma files & AppList in Steam directory without launching";
            }
            if (btnStop != null)
            {
                btnStop.Content = isAr ? "🛑 إيقاف وحذف" : "🛑 Stop & Clean";
                btnStop.ToolTip = isAr ? "إغلاق ستيم وحذف user32.dll فوراً" : "Close Steam and remove user32.dll immediately";
            }

            txtSearchPlaceholder.Text = isAr ? 
                "ابحث عن أي لعبة أو إضافة على ستيم بالاسم (مثال: Arc Raiders, GTA V, Elden Ring) أو AppID..." : 
                "Search any Steam game or DLC by name (e.g. Arc Raiders, Black Myth, Elden Ring) or enter AppID...";
            btnAddInput.Content = isAr ? "+ إضافة AppID" : "+ Add AppID";
            btnBulk.Content = isAr ? "📥 استيراد دفعة" : "📥 Bulk Import";

            lblLibraryTitle.Text = isAr ? "🎮 قائمة العناصر:" : "🎮 APPLIST:";
            txtFilterInput.ToolTip = isAr ? "تصفية الألعاب المضافة..." : "Filter library...";
            btnToggleAll.Content = isAr ? "🔄 تبديل الكل" : "🔄 Toggle All";
            btnClearAll.Content = isAr ? "🗑 مسح الكل" : "🗑 Clear All";

            if (btnShareLibrary != null) btnShareLibrary.Content = isAr ? "📋 مشاركة" : "📋 Share";
            if (btnImportLibrary != null) btnImportLibrary.Content = isAr ? "📥 استيراد" : "📥 Import";

            if (txtEmptyTitle != null) txtEmptyTitle.Text = isAr ? "مكتبتك المفتوحة خالية حالياً" : "Unlocked Library is Empty";
            if (txtEmptySubtitle != null) txtEmptySubtitle.Text = isAr ? 
                "ابحث عن أي لعبة أو إضافة بالأعلى، أو أدخل الـ AppID، أو اختر من الألعاب الأكثر طلباً:" : 
                "Search by game title above, enter an AppID, or quick-add popular titles below:";

            if (btnModeToggle != null)
            {
                if (this.ActualWidth < 980 && this.ActualWidth > 0)
                {
                    btnModeToggle.Content = "⛶";
                }
                else
                {
                    btnModeToggle.Content = isMiniMode ? (isAr ? "🗖 مكبر" : "🗖 Big") : (isAr ? "⛶ مصغر" : "⛶ Mini");
                }
                btnModeToggle.ToolTip = isMiniMode ? (isAr ? "التبديل إلى لوحة التحكم الكاملة" : "Expand to Full Dashboard") : (isAr ? "التبديل إلى المنظر المصغر (نافذة صينية سريعة)" : "Switch to Compact Tray Mode");
            }
            if (btnMiniLaunch != null)
            {
                btnMiniLaunch.Content = isAr ? "🚀 تشغيل ستيم" : "🚀 Launch Steam";
            }
            if (btnMiniStop != null)
            {
                btnMiniStop.Content = isAr ? "🛑 إيقاف وحذف" : "🛑 Stop & Clean";
            }
            if (btnMiniAdd != null)
            {
                btnMiniAdd.Content = isAr ? "+ إضافة" : "+ Add";
            }
            UpdateViewSwitcherStyles();

            UpdateTabButtonStyles();
            UpdateStatusUI();
            RenderGamesUI();
        }

        // ==========================================
        // --- LIBRARY CARDS / LIST & CASCADE ANIMATION ---
        // ==========================================
        private void RenderGamesUI()
        {
            bool isAr = (settings.lang == "ar");
            if (gamesWrapPanel != null) gamesWrapPanel.Children.Clear();
            if (gamesListStack != null) gamesListStack.Children.Clear();
            int enabledCount = 0;
            string filter = txtFilterInput != null ? txtFilterInput.Text.Trim().ToLower() : "";

            if (gamesList.Count == 0)
            {
                if (emptyStatePanel != null) emptyStatePanel.Visibility = Visibility.Visible;
            }
            else
            {
                if (emptyStatePanel != null) emptyStatePanel.Visibility = Visibility.Collapsed;
            }

            bool isList = (currentViewMode == "list");

            int cardIndex = 0;
            foreach (var g in gamesList)
            {
                if (g.enabled) enabledCount++;

                bool isDlc = g.CheckIsDLC();

                // Tab Filter
                if (currentFilterTab == "games" && isDlc) continue;
                if (currentFilterTab == "dlcs" && !isDlc) continue;

                // Text Search Filter (Matches Title, AppID, or Parent Base Game)
                if (!string.IsNullOrEmpty(filter))
                {
                    bool matchName = g.name.ToLower().Contains(filter);
                    bool matchId = g.appId.ToString().Contains(filter);
                    bool matchParent = !string.IsNullOrEmpty(g.parentName) && g.parentName.ToLower().Contains(filter);
                    if (!matchName && !matchId && !matchParent)
                        continue;
                }

                if (isList)
                {
                    if (gamesListStack != null)
                    {
                        var row = CreateGameRow(g);
                        gamesListStack.Children.Add(row);

                        var tgRow = row.RenderTransform as TransformGroup;
                        if (tgRow != null)
                        {
                            TranslateTransform ttRow = null;
                            ScaleTransform stRow = null;
                            foreach (var t in tgRow.Children)
                            {
                                if (t is TranslateTransform) ttRow = (TranslateTransform)t;
                                else if (t is ScaleTransform) stRow = (ScaleTransform)t;
                            }

                            if (g.appId == lastAddedAppId && lastAddedAppId > 0)
                            {
                                if (stRow != null)
                                {
                                    var animScale = new DoubleAnimationUsingKeyFrames();
                                    animScale.KeyFrames.Add(new LinearDoubleKeyFrame(0.7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
                                    animScale.KeyFrames.Add(new SplineDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));
                                    animScale.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));
                                    stRow.BeginAnimation(ScaleTransform.ScaleXProperty, animScale);
                                    stRow.BeginAnimation(ScaleTransform.ScaleYProperty, animScale);
                                }
                                if (ttRow != null)
                                {
                                    ttRow.X = isAr ? 60 : -60;
                                    var daX = new DoubleAnimation(isAr ? 60 : -60, 0, TimeSpan.FromMilliseconds(260)) { DecelerationRatio = 0.7 };
                                    ttRow.BeginAnimation(TranslateTransform.XProperty, daX);
                                }
                                var shadow = new DropShadowEffect() { Color = Color.FromRgb(16, 185, 129), BlurRadius = 30, ShadowDepth = 0, Opacity = 0.9 };
                                row.Effect = shadow;
                                shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(30, 0, TimeSpan.FromMilliseconds(1200)));
                                row.Opacity = 0.0;
                                row.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)));
                            }
                            else if (ttRow != null)
                            {
                                ttRow.X = isAr ? 24 : -24;
                                row.Opacity = 0.0;
                                var timeDelay = TimeSpan.FromMilliseconds(Math.Min(cardIndex * 20, 260));
                                var daX = new DoubleAnimation(isAr ? 24 : -24, 0, TimeSpan.FromMilliseconds(220)) {
                                    BeginTime = timeDelay,
                                    DecelerationRatio = 0.6
                                };
                                ttRow.BeginAnimation(TranslateTransform.XProperty, daX);
                                var daOp = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220)) {
                                    BeginTime = timeDelay
                                };
                                row.BeginAnimation(UIElement.OpacityProperty, daOp);
                            }
                        }
                    }
                }
                else
                {
                    if (gamesWrapPanel != null)
                    {
                        var card = CreateGameCard(g);
                        gamesWrapPanel.Children.Add(card);

                        var tg = card.RenderTransform as TransformGroup;
                        if (tg != null)
                        {
                            TranslateTransform tt = null;
                            ScaleTransform st = null;
                            foreach (var t in tg.Children)
                            {
                                if (t is TranslateTransform) tt = (TranslateTransform)t;
                                else if (t is ScaleTransform) st = (ScaleTransform)t;
                            }

                            if (g.appId == lastAddedAppId && lastAddedAppId > 0)
                            {
                                if (st != null)
                                {
                                    var animScale = new DoubleAnimationUsingKeyFrames();
                                    animScale.KeyFrames.Add(new LinearDoubleKeyFrame(0.65, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
                                    animScale.KeyFrames.Add(new SplineDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
                                    animScale.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));
                                    st.BeginAnimation(ScaleTransform.ScaleXProperty, animScale);
                                    st.BeginAnimation(ScaleTransform.ScaleYProperty, animScale);
                                }
                                if (tt != null)
                                {
                                    tt.Y = 40;
                                    var daY = new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(280)) { DecelerationRatio = 0.7 };
                                    tt.BeginAnimation(TranslateTransform.YProperty, daY);
                                }
                                var shadow = card.Effect as DropShadowEffect;
                                if (shadow != null)
                                {
                                    shadow.Color = Color.FromRgb(16, 185, 129);
                                    shadow.BlurRadius = 36;
                                    shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(36, 0, TimeSpan.FromMilliseconds(1200)));
                                }
                                card.Opacity = 0.0;
                                card.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)));
                            }
                            else if (tt != null)
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
                    }
                }
                cardIndex++;
            }

            lastAddedAppId = 0;

            if (isMiniMode)
            {
                RenderMiniGamesList();
            }
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
                st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, 1.045, TimeSpan.FromMilliseconds(130)) { DecelerationRatio = 0.5 });
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, 1.045, TimeSpan.FromMilliseconds(130)) { DecelerationRatio = 0.5 });
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -6, TimeSpan.FromMilliseconds(130)) { DecelerationRatio = 0.5 });
                cardShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(0, 26, TimeSpan.FromMilliseconds(130)));
                card.BorderBrush = isActuallyDlc ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(16, 185, 129));
            };

            card.MouseLeave += (s, e) => {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.045, 1.0, TimeSpan.FromMilliseconds(160)) { AccelerationRatio = 0.5 });
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.045, 1.0, TimeSpan.FromMilliseconds(160)) { AccelerationRatio = 0.5 });
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-6, 0, TimeSpan.FromMilliseconds(160)) { AccelerationRatio = 0.5 });
                cardShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(26, 0, TimeSpan.FromMilliseconds(160)));
                card.BorderBrush = isActuallyDlc ? new SolidColorBrush(Color.FromArgb(70, 245, 158, 11)) : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            };

            var stack = new StackPanel();

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

            var vignetteBrush = new LinearGradientBrush() {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            vignetteBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 14, 19, 30), 0.0));
            vignetteBrush.GradientStops.Add(new GradientStop(Color.FromArgb(70, 14, 19, 30), 0.55));
            vignetteBrush.GradientStops.Add(new GradientStop(Color.FromRgb(14, 19, 30), 1.0));

            var imgTint = new Border() {
                Background = vignetteBrush
            };
            imgContainer.Children.Add(imgTint);

            var badgesRow = new DockPanel() { 
                Margin = new Thickness(8), 
                VerticalAlignment = VerticalAlignment.Top,
                LastChildFill = false 
            };

            // 1. Clickable Type Badge (1-click toggle!)
            var typeBadge = new Border() {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = isAr ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = isActuallyDlc ? 
                    new SolidColorBrush(Color.FromRgb(245, 158, 11)) : 
                    new SolidColorBrush(Color.FromRgb(16, 185, 129)),
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
                        new SolidColorBrush(Color.FromRgb(239, 68, 68)),
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
                fsCardBadge.Cursor = Cursors.Hand;
                fsCardBadge.ToolTip = isAr ? "انقر للتبديل الفوري لحالة المشاركة العائلية (Family Share)" : "Click to toggle Family Share status";
                fsCardBadge.MouseDown += (s, e) => {
                    game.familyShare = !game.familyShare;
                    SaveGames();
                    RenderGamesUI();
                };
                fsCardBadge.Child = txtFsCard;
                badgesRow.Children.Add(fsCardBadge);
            }

            imgContainer.Children.Add(badgesRow);
            stack.Children.Add(imgContainer);

            var detailsStack = new StackPanel() { Margin = new Thickness(12) };

            // If it is a DLC, display the parent relationship link chip at the very top of details!
            if (isActuallyDlc)
            {
                string parentLabel = !string.IsNullOrEmpty(game.parentName) ? game.parentName : (game.parentAppId > 0 ? ("App " + game.parentAppId) : null);
                if (!string.IsNullOrEmpty(parentLabel))
                {
                    var parentChip = new Border() {
                        Background = new SolidColorBrush(Color.FromArgb(35, 6, 182, 212)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(70, 6, 182, 212)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 0, 0, 5),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Cursor = Cursors.Hand,
                        ToolTip = isAr ? ("انقر لتصفية مكتبتك حسب اللعبة الأساسية: " + parentLabel) : ("Click to filter library by base game: " + parentLabel)
                    };
                    parentChip.Child = new TextBlock() {
                        Text = "🔗 " + parentLabel,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(103, 232, 249))
                    };
                    parentChip.MouseDown += (s, e) => {
                        if (txtFilterInput != null) txtFilterInput.Text = parentLabel;
                    };
                    detailsStack.Children.Add(parentChip);
                }
            }

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

            if (!isActuallyDlc)
            {
                // Count active DLCs in library
                int linkedDlcCount = 0;
                foreach (var other in gamesList)
                {
                    if (other.CheckIsDLC() && (other.parentAppId == game.appId || (!string.IsNullOrEmpty(other.parentName) && other.parentName.Equals(game.name, StringComparison.OrdinalIgnoreCase))))
                    {
                        linkedDlcCount++;
                    }
                }

                var btnDlc = new Button() {
                    Content = linkedDlcCount > 0 ? (isAr ? string.Format("🧩 {0} DLCs", linkedDlcCount) : string.Format("🧩 {0} DLCs", linkedDlcCount)) : (isAr ? "+ إضافة الـ DLCs" : "+ Add DLCs"),
                    ToolTip = linkedDlcCount > 0 ? (isAr ? string.Format("يوجد {0} إضافات مفعلة. انقر لإدارتها والتحكم بها", linkedDlcCount) : string.Format("{0} DLCs linked. Click to manage them", linkedDlcCount)) : (isAr ? "البحث عن وتنزيل إضافات اللعبة الرسمية" : "Search and add official DLCs"),
                    FontSize = 10.5,
                    Background = linkedDlcCount > 0 ? new SolidColorBrush(Color.FromArgb(50, 245, 158, 11)) : new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)),
                    Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 245, 158, 11)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6, 2, 6, 2),
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                if (linkedDlcCount > 0)
                {
                    btnDlc.Click += (s, e) => OpenGameDlcManagerDialog(game);
                }
                else
                {
                    btnDlc.Click += (s, e) => FetchAndAddDLCs(game.appId, game.name);
                }
                metaRow.Children.Add(btnDlc);
            }
            else
            {
                string pLabel = !string.IsNullOrEmpty(game.parentName) ? game.parentName : (game.parentAppId > 0 ? ("App " + game.parentAppId) : null);
                var dlcTag = new Border() {
                    Background = new SolidColorBrush(Color.FromArgb(30, 245, 158, 11)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(60, 245, 158, 11)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                dlcTag.Child = new TextBlock() {
                    Text = !string.IsNullOrEmpty(pLabel) ? (isAr ? ("إضافة لـ " + pLabel) : ("DLC for " + pLabel)) : (isAr ? "إضافة تابعة" : "Linked DLC"),
                    FontSize = 10,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 130,
                    Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36))
                };
                metaRow.Children.Add(dlcTag);
            }

            detailsStack.Children.Add(metaRow);

            var actionRow = new DockPanel();
            var toggleCard = CreateNeonToggle(game.enabled, (val) => {
                game.enabled = val;
                SaveGames();
                UpdateStatusUI();
            });
            DockPanel.SetDock(toggleCard, Dock.Left);
            actionRow.Children.Add(toggleCard);

            var btnDel = new Button() {
                Content = isAr ? "🗑 حذف" : "🗑 Remove",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(4, 2, 4, 2)
            };
            AttachTactilePress(btnDel);
            btnDel.Click += (s, e) => {
                AnimateAndRemoveCard(card, game);
            };
            actionRow.Children.Add(btnDel);

            detailsStack.Children.Add(actionRow);
            stack.Children.Add(detailsStack);
            card.Child = stack;

            var cm = new ContextMenu() {
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4)
            };

            var miStore = new MenuItem() {
                Header = isAr ? "🌐 عرض في متجر Steam" : "🌐 View on Steam Store"
            };
            miStore.Click += (s, e) => {
                try { Process.Start(new ProcessStartInfo("https://store.steampowered.com/app/" + game.appId) { UseShellExecute = true }); } catch { }
            };
            cm.Items.Add(miStore);

            var miSteamDb = new MenuItem() {
                Header = isAr ? "📊 فحص بيانات SteamDB" : "📊 View on SteamDB"
            };
            miSteamDb.Click += (s, e) => {
                try { Process.Start(new ProcessStartInfo("https://steamdb.info/app/" + game.appId) { UseShellExecute = true }); } catch { }
            };
            cm.Items.Add(miSteamDb);

            var miCopyId = new MenuItem() {
                Header = isAr ? "📋 نسخ الـ AppID" : "📋 Copy AppID"
            };
            miCopyId.Click += (s, e) => {
                try {
                    Clipboard.SetText(game.appId.ToString());
                    ShowToast(isAr ? ("تم نسخ AppID: " + game.appId) : ("Copied AppID: " + game.appId), ToastType.Success);
                } catch { }
            };
            cm.Items.Add(miCopyId);

            if (!isActuallyDlc)
            {
                var miFetchDlc = new MenuItem() {
                    Header = isAr ? "🧩 جلب وتفعيل كافة الـ DLCs" : "🧩 Fetch & Unlock All DLCs"
                };
                miFetchDlc.Click += (s, e) => FetchAndAddDLCs(game.appId, game.name);
                cm.Items.Add(miFetchDlc);
            }

            cm.Items.Add(new Separator());

            var miToggle = new MenuItem() {
                Header = game.enabled ? (isAr ? "⏸️ تعطيل من القائمة" : "⏸️ Disable") : (isAr ? "▶️ تفعيل في القائمة" : "▶️ Enable")
            };
            miToggle.Click += (s, e) => {
                game.enabled = !game.enabled;
                SaveGames();
                RenderGamesUI();
                UpdateStatusUI();
            };
            cm.Items.Add(miToggle);

            var miDel = new MenuItem() {
                Header = isAr ? "🗑️ حذف من القائمة" : "🗑️ Remove from Library",
                Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68))
            };
            miDel.Click += (s, e) => {
                AnimateAndRemoveCard(card, game);
            };
            cm.Items.Add(miDel);

            card.ContextMenu = cm;

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
                    ShowToast(
                        isAr ? ("العنصر برقم " + appId + " موجود بالفعل في مكتبتك.") : ("Item with AppID " + appId + " is already in your library."),
                        ToastType.Info);
                    return;
                }
            }

            ThreadPool.QueueUserWorkItem((state) => {
                string name = hintName;
                string headerImg = string.Format("https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{0}/header.jpg", appId);
                bool isDlcType = false;
                bool isFs = true;
                int parentId = 0;
                string parentTitle = "";

                if (appId == 1174180 || appId == 271590 || appId == 359550 || appId == 1222670) isFs = false;

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
                                    if (data.ContainsKey("fullgame"))
                                    {
                                        isDlcType = true;
                                        var fg = data["fullgame"] as Dictionary<string, object>;
                                        if (fg != null)
                                        {
                                            if (fg.ContainsKey("appid")) parentId = Convert.ToInt32(fg["appid"]);
                                            if (fg.ContainsKey("name")) parentTitle = fg["name"].ToString();
                                        }
                                    }

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

                // If it's a DLC and parent is not yet linked, try to find matching base game in gamesList
                if (isDlcType && parentId == 0)
                {
                    foreach (var bg in gamesList)
                    {
                        if (!bg.CheckIsDLC() && name.StartsWith(bg.name, StringComparison.OrdinalIgnoreCase))
                        {
                            parentId = bg.appId;
                            parentTitle = bg.name;
                            break;
                        }
                    }
                }

                Dispatcher.Invoke((Action)(() => {
                    lastAddedAppId = appId;
                    gamesList.Insert(0, new GameItem() {
                        appId = appId,
                        name = name,
                        image = headerImg,
                        enabled = true,
                        isDLC = isDlcType,
                        type = isDlcType ? "dlc" : "game",
                        familyShare = isFs,
                        parentAppId = parentId,
                        parentName = parentTitle
                    });
                    SaveGames();
                    UpdateTabButtonStyles();
                    RenderGamesUI();
                    ShowToast(
                        isAr ? string.Format("تمت إضافة {0} ({1}) بنجاح!", name, appId) :
                               string.Format("Added {0} ({1}) successfully!", name, appId),
                        ToastType.Success);
                }));
            });
        }

        private void FetchAndAddDLCs(int parentAppId, string parentName)
        {
            bool isAr = (settings.lang == "ar");

            ShowToast(
                isAr ? string.Format("جاري البحث عن وتنزيل إضافات (DLCs) لـ {0}...", parentName) :
                       string.Format("Searching and fetching official DLCs for {0}...", parentName),
                ToastType.Info, 3000);

            ThreadPool.QueueUserWorkItem((state) => {
                var dlcIds = new List<int>();
                try
                {
                    string url = string.Format("https://store.steampowered.com/api/appdetails?appids={0}&l=english", parentAppId);
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = "Mozilla/5.0";
                    req.Timeout = 6000;
                    using (var resp = req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        var js = new JavaScriptSerializer();
                        var dict = js.Deserialize<Dictionary<string, object>>(json);
                        if (dict != null && dict.ContainsKey(parentAppId.ToString()))
                        {
                            var appObj = dict[parentAppId.ToString()] as Dictionary<string, object>;
                            if (appObj != null && appObj.ContainsKey("success") && (bool)appObj["success"])
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

                if (dlcIds.Count == 0)
                {
                    Dispatcher.Invoke((Action)(() => {
                        ShowToast(
                            isAr ? string.Format("لم يتم العثور على أي إضافات (DLCs) مسجلة لـ {0} على ستيم.", parentName) :
                                   string.Format("No DLCs found for {0} on Steam Store.", parentName),
                            ToastType.Warning, 4000);
                    }));
                    return;
                }

                // Resolve real DLC titles and capsule covers from Steam to filter out junk/empty packages
                var resolvedItems = new List<GameItem>();
                foreach (var dlcId in dlcIds)
                {
                    bool exists = false;
                    foreach (var g in gamesList) { if (g.appId == dlcId) { exists = true; break; } }
                    if (exists) continue;

                    string dlcName = null;
                    string dlcImage = string.Format("https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{0}/header.jpg", dlcId);
                    bool isValidDlc = true;

                    try
                    {
                        string dUrl = string.Format("https://store.steampowered.com/api/appdetails?appids={0}&filters=basic&l=english", dlcId);
                        var dReq = (HttpWebRequest)WebRequest.Create(dUrl);
                        dReq.UserAgent = "Mozilla/5.0";
                        dReq.Timeout = 4000;
                        using (var dResp = dReq.GetResponse())
                        using (var dReader = new StreamReader(dResp.GetResponseStream()))
                        {
                            string dJson = dReader.ReadToEnd();
                            var js = new JavaScriptSerializer();
                            var dDict = js.Deserialize<Dictionary<string, object>>(dJson);
                            if (dDict != null && dDict.ContainsKey(dlcId.ToString()))
                            {
                                var dObj = dDict[dlcId.ToString()] as Dictionary<string, object>;
                                if (dObj != null && dObj.ContainsKey("success") && (bool)dObj["success"])
                                {
                                    var dData = dObj["data"] as Dictionary<string, object>;
                                    if (dData != null)
                                    {
                                        if (dData.ContainsKey("name")) dlcName = dData["name"].ToString();
                                        if (dData.ContainsKey("header_image")) dlcImage = dData["header_image"].ToString();
                                        if (dData.ContainsKey("type"))
                                        {
                                            string t = dData["type"].ToString().ToLower();
                                            if (t == "demo" || t == "hardware" || t == "tool" || t == "server" || t == "beta")
                                            {
                                                isValidDlc = false;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    if (!isValidDlc) continue;

                    if (string.IsNullOrEmpty(dlcName))
                    {
                        dlcName = string.Format("{0} - DLC ({1})", parentName, dlcId);
                    }

                    resolvedItems.Add(new GameItem() {
                        appId = dlcId,
                        name = dlcName,
                        image = dlcImage,
                        enabled = true,
                        isDLC = true,
                        type = "dlc",
                        parentAppId = parentAppId,
                        parentName = parentName
                    });
                }

                Dispatcher.Invoke((Action)(() => {
                    if (resolvedItems.Count == 0)
                    {
                        ShowToast(
                            isAr ? string.Format("كافة إضافات {0} مضافة بالفعل في مكتبتك!", parentName) :
                                   string.Format("All DLCs for {0} are already in your library!", parentName),
                            ToastType.Info, 4000);
                        return;
                    }

                    // Insert next to parent game in library
                    int insertIdx = 0;
                    for (int i = 0; i < gamesList.Count; i++)
                    {
                        if (gamesList[i].appId == parentAppId)
                        {
                            insertIdx = i + 1;
                            break;
                        }
                    }
                    gamesList.InsertRange(insertIdx, resolvedItems);

                    SaveGames();
                    UpdateTabButtonStyles();
                    RenderGamesUI();

                    ShowToast(
                        isAr ? string.Format("تمت إضافة وتفعيل {0} إضافات رسمية بنجاح لـ {1}!", resolvedItems.Count, parentName) :
                               string.Format("Successfully added and unlocked {0} official DLCs for {1}!", resolvedItems.Count, parentName),
                        ToastType.Success, 5000);
                }));
            });
        }

        private void OpenBulkImportDialog()
        {
            bool isAr = (settings.lang == "ar");

            var sp = new StackPanel() { Margin = new Thickness(4) };
            sp.Children.Add(new TextBlock() {
                Text = isAr ? "الصق أرقام AppIDs مفصولة بفواصل أو مسافات أو أسطر جديدة:" : "Paste AppIDs separated by commas, spaces, or new lines:",
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225))
            });

            var tb = new TextBox() {
                Height = 160,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)),
                Foreground = new SolidColorBrush(Color.FromRgb(167, 243, 208)),
                FontFamily = new FontFamily("Consolas"),
                Padding = new Thickness(8),
                FlowDirection = FlowDirection.LeftToRight
            };
            sp.Children.Add(tb);

            var btnScanInstalledBulk = new Button() {
                Content = isAr ? "🎮 أو فحص واستيراد الألعاب المثبتة على جهازك تلقائياً..." : "🎮 Or Scan & Import Installed Steam Games...",
                Background = new SolidColorBrush(Color.FromArgb(30, 16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 7, 12, 7),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 0),
                Cursor = Cursors.Hand
            };
            btnScanInstalledBulk.Click += (s, e) => {
                CloseInAppModal();
                OpenInstalledGamesModal();
            };
            sp.Children.Add(btnScanInstalledBulk);

            var btnImport = new Button() {
                Content = isAr ? "استيراد كل الـ AppIDs الآن" : "Import AppIDs Now",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 8, 0, 0),
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
                CloseInAppModal();
                ShowToast(
                    isAr ? string.Format("✅ تم استيراد {0} عنصر بنجاح!", added) : string.Format("✅ Successfully imported {0} items!", added),
                    ToastType.Success);
            };
            sp.Children.Add(btnImport);

            ShowInAppModal(sp, isAr ? "📥 استيراد دفعة ألعاب وإضافات (Bulk Import)" : "📥 Bulk Import AppIDs", null, 540);
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
            string targetDll = (settings.mode == "families") ? "user32SF.dll" : "user32.dll";
            string src;
            GetPayloadBytes(targetDll, out src);
            if (lblTelemetrySource != null)
            {
                string shortSrc = src.Contains("StealthMode") ? "StealthMode" : (src.Contains("Local") ? "Local" : "Core");
                lblTelemetrySource.Text = "🛡️ " + targetDll + " (" + shortSrc + ")";
                lblTelemetrySource.ToolTip = "Payload Source: " + src;
            }
        }

        // =========================================================================
        // --- LAUNCH & INJECTION ENGINE ---
        // =========================================================================
        private void ExecuteManualDeleteAppCache()
        {
            RunDeleteSteamAppCacheTool(true);
        }

        private bool RunDeleteSteamAppCacheTool(bool isManual = false)
        {
            bool isAr = (settings.lang == "ar");

            if (IsSteamRunning())
            {
                StopSteamProcess();
                Thread.Sleep(500);
            }

            // Target DeleteSteamAppCache.exe in steamDir first, fallback to StealthMode
            string steamDelExe = !string.IsNullOrEmpty(steamDir) ? Path.Combine(steamDir, "DeleteSteamAppCache.exe") : null;
            string localDelExe = Path.Combine(appDir, "StealthMode", "DeleteSteamAppCache.exe");
            if (!File.Exists(localDelExe))
                localDelExe = Path.Combine(appDir, "DeleteSteamAppCache.exe");

            if (!File.Exists(localDelExe))
            {
                try
                {
                    string srcDesc;
                    byte[] bytes = GetPayloadBytes("DeleteSteamAppCache.exe", out srcDesc);
                    if (bytes != null && bytes.Length > 0)
                    {
                        localDelExe = Path.Combine(appDir, "DeleteSteamAppCache.exe");
                        File.WriteAllBytes(localDelExe, bytes);
                    }
                }
                catch { }
            }

            // Ensure DeleteSteamAppCache.exe exists in steamDir
            if (!string.IsNullOrEmpty(steamDelExe) && !File.Exists(steamDelExe) && File.Exists(localDelExe))
            {
                try { File.Copy(localDelExe, steamDelExe, true); } catch { }
            }

            string targetExe = (steamDelExe != null && File.Exists(steamDelExe)) ? steamDelExe : localDelExe;

            // DeleteSteamAppCache.exe specifically calls DeleteFileW on <steamDir>\appcache\packageinfo.vdf.
            // If packageinfo.vdf is missing (e.g. already deleted), DeleteFileW fails (ERROR_FILE_NOT_FOUND)
            // and the tool displays "Failed to delete Steam app cache!".
            // We ensure packageinfo.vdf exists so DeleteSteamAppCache.exe always deletes it cleanly and reports success.
            if (!string.IsNullOrEmpty(steamDir))
            {
                try
                {
                    string cacheDir = Path.Combine(steamDir, "appcache");
                    if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
                    string pkgInfo = Path.Combine(cacheDir, "packageinfo.vdf");
                    if (!File.Exists(pkgInfo))
                    {
                        File.WriteAllBytes(pkgInfo, new byte[] { 0 });
                    }
                }
                catch { }
            }

            bool launched = false;
            if (File.Exists(targetExe))
            {
                try
                {
                    var psi = new ProcessStartInfo(targetExe) {
                        WorkingDirectory = Path.GetDirectoryName(targetExe),
                        UseShellExecute = true
                    };
                    var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        launched = true;
                        if (!isManual)
                        {
                            // In 1-click automatic mode, auto-dismiss the DeleteSteamAppCache dialog if it pops up
                            for (int w = 0; w < 15; w++)
                            {
                                Thread.Sleep(100);
                                IntPtr hwnd = FindWindow(null, "DeleteSteamAppCache");
                                if (hwnd != IntPtr.Zero)
                                {
                                    SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                                    break;
                                }
                                if (proc.HasExited) break;
                            }
                            if (!proc.HasExited) proc.WaitForExit(1500);
                        }
                        else
                        {
                            // In manual mode, wait for the user to view and dismiss the tool's dialog
                            proc.WaitForExit();
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (isManual)
                    {
                        MessageBox.Show(
                            isAr ? ("تعذر تشغيل DeleteSteamAppCache.exe: " + ex.Message) : ("Failed to run DeleteSteamAppCache.exe: " + ex.Message),
                            isAr ? "خطأ" : "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            if (isManual && launched)
            {
                MessageBox.Show(
                    isAr ? "✅ تم تشغيل أداة DeleteSteamAppCache.exe الأصلية ومسح الكاش بنجاح!" :
                           "✅ DeleteSteamAppCache.exe executed and Steam cache cleared successfully!",
                    isAr ? "اكتمل مسح الكاش" : "AppCache Cleared",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return launched;
        }

        private bool CheckIfSteamFamiliesEnrolled()
        {
            if (string.IsNullOrEmpty(steamDir)) return false;
            try
            {
                string userDir = Path.Combine(steamDir, "userdata");
                if (Directory.Exists(userDir))
                {
                    foreach (var cfg in Directory.GetFiles(userDir, "localconfig.vdf", SearchOption.AllDirectories))
                    {
                        string content = File.ReadAllText(cfg);
                        if (content.IndexOf("FamilyGroup", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private bool InjectFilesOnly(bool showSuccessDialog = true)
        {
            bool isAr = (settings.lang == "ar");

            if (string.IsNullOrEmpty(steamDir) || !Directory.Exists(steamDir))
            {
                MessageBox.Show(
                    isAr ? "لم يتم العثور على مجلد Steam! يرجى التأكد من مسار Steam في الإعدادات." : "Steam directory was not found! Please check your path in Settings.",
                    isAr ? "خطأ في المسار" : "Path Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // 1. قفل ستيم إن كان يعمل
            if (IsSteamRunning())
            {
                StopSteamProcess();
                Thread.Sleep(600);
            }

            // 2. تجهيز مجلد AppList وملف AppList.ini
            SyncAppListIni();
            string stealthAppList = Path.Combine(appDir, "StealthMode", "AppList");
            string sourceAppList = Directory.Exists(stealthAppList) ? stealthAppList : Path.Combine(appDir, "AppList");
            string steamAppList = Path.Combine(steamDir, "AppList");
            try
            {
                if (!Directory.Exists(steamAppList)) Directory.CreateDirectory(steamAppList);
                foreach (var f in Directory.GetFiles(steamAppList, "*.txt")) { try { File.Delete(f); } catch { } }

                string srcIni = Path.Combine(sourceAppList, "AppList.ini");
                if (File.Exists(srcIni))
                {
                    File.Copy(srcIni, Path.Combine(steamAppList, "AppList.ini"), true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    isAr ? ("فشل نسخ مجلد AppList إلى Steam: " + ex.Message) : ("Failed copying AppList to Steam: " + ex.Message),
                    isAr ? "خطأ في الإعداد" : "Setup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // 4. نسخ user32SF.dll أولاً ثم إعادة تسميته لـ user32.dll
            bool isFamilies = (settings.mode != "standard");
            string targetDllName = isFamilies ? "user32SF.dll" : "user32.dll";
            string srcDesc;
            byte[] dllBytes = GetPayloadBytes(targetDllName, out srcDesc);

            if (dllBytes == null || dllBytes.Length == 0)
            {
                targetDllName = (targetDllName == "user32SF.dll") ? "user32.dll" : "user32SF.dll";
                dllBytes = GetPayloadBytes(targetDllName, out srcDesc);
            }

            if (dllBytes == null || dllBytes.Length == 0)
            {
                MessageBox.Show(
                    isAr ? string.Format("تعذر استخراج ملف الحقن {0}!", targetDllName) : string.Format("Failed to extract stealth payload {0}!", targetDllName),
                    isAr ? "خطأ في الملفات" : "Payload Missing",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            string destDll = Path.Combine(steamDir, "user32.dll");
            string tempSfDll = Path.Combine(steamDir, "user32SF.dll");
            try
            {
                if (File.Exists(destDll)) File.Delete(destDll);
                if (File.Exists(tempSfDll)) File.Delete(tempSfDll);

                if (isFamilies)
                {
                    File.WriteAllBytes(tempSfDll, dllBytes);
                    Thread.Sleep(150);
                    File.Copy(tempSfDll, destDll, true);
                    try { File.Delete(tempSfDll); } catch { }
                }
                else
                {
                    File.WriteAllBytes(destDll, dllBytes);
                }
            }
            catch
            {
                int killed = KillZombieSteamProcesses();
                bool healed = false;
                if (killed > 0)
                {
                    try
                    {
                        if (File.Exists(destDll)) File.Delete(destDll);
                        if (File.Exists(tempSfDll)) File.Delete(tempSfDll);
                        if (isFamilies)
                        {
                            File.WriteAllBytes(tempSfDll, dllBytes);
                            Thread.Sleep(150);
                            File.Copy(tempSfDll, destDll, true);
                            try { File.Delete(tempSfDll); } catch { }
                        }
                        else
                        {
                            File.WriteAllBytes(destDll, dllBytes);
                        }
                        healed = true;
                    }
                    catch { }
                }

                if (!healed)
                {
                    ShowToast(
                        isAr ? ("تعذر نسخ user32.dll بسبب قفل الملفات. تم فتح طبيب ستيم للفحص.") :
                               ("Failed placing user32.dll (file locked). Opening Steam Doctor..."),
                        ToastType.Error);
                    OpenSteamDoctorModal();
                    return false;
                }
            }

            isInjected = true;
            UpdateStatusUI();

            if (showSuccessDialog)
            {
                ShowToast(
                    isAr ? "✅ تم تجهيز وحقن الملفات بنجاح في مجلد Steam!" :
                           "✅ All files injected successfully into Steam folder!",
                    ToastType.Success);
            }

            return true;
        }

        private void LaunchSteamOnly()
        {
            ExecuteSmartLaunch();
        }

        private void ExecuteSmartLaunch()
        {
            bool isAr = (settings.lang == "ar");

            if (string.IsNullOrEmpty(steamDir) || !Directory.Exists(steamDir))
            {
                ShowToast(
                    isAr ? "لم يتم العثور على مجلد Steam! يرجى التأكد من مسار Steam في الإعدادات." : "Steam directory was not found! Please check your path in Settings.",
                    ToastType.Error);
                OpenSteamDoctorModal();
                return;
            }

            string steamExe = Path.Combine(steamDir, "steam.exe");
            if (!File.Exists(steamExe))
            {
                ShowToast(
                    isAr ? ("تعذر العثور على steam.exe في: " + steamDir) : ("Could not find steam.exe at: " + steamDir),
                    ToastType.Error);
                OpenSteamDoctorModal();
                return;
            }

            // 1. إغلاق ستيم إن كان قيد التشغيل لضمان بيئة نظيفة تماماً
            if (IsSteamRunning())
            {
                StopSteamProcess();
                Thread.Sleep(800);
            }

            // 2. مسح كاش ستيم تلقائياً عبر DeleteSteamAppCache إن كان الخيار مفعلاً
            if (settings.clearAppCache)
            {
                RunDeleteSteamAppCacheTool(false);
                Thread.Sleep(300);
            }

            // 3. تجهيز وحقن ملفات التخفي (AppList.ini بدون BOM وملف user32SF كـ user32.dll)
            if (!InjectFilesOnly(false)) return;

            // 4. تشغيل ستيم مباشرة بدون معطيات تشويش
            string args = !string.IsNullOrEmpty(settings.steamArgs) ? settings.steamArgs.Trim() : "";
            try
            {
                var psi = new ProcessStartInfo(steamExe) {
                    WorkingDirectory = steamDir,
                    UseShellExecute = true
                };
                if (!string.IsNullOrEmpty(args))
                {
                    psi.Arguments = args;
                }
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                ShowToast(
                    isAr ? ("فشل تشغيل Steam: " + ex.Message) : ("Failed to launch Steam: " + ex.Message),
                    ToastType.Error);
                OpenSteamDoctorModal();
                return;
            }

            isInjected = true;
            UpdateStatusUI();

            monitorThread = new Thread(() => {
                for (int i = 0; i < 30; i++)
                {
                    if (IsSteamRunning()) break;
                    Thread.Sleep(500);
                }

                while (true)
                {
                    if (!isInjected) break;
                    if (!IsSteamRunning())
                    {
                        isInjected = false;
                        Dispatcher.Invoke((Action)(() => UpdateStatusUI()));
                        break;
                    }
                    Thread.Sleep(2000);
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

        private static readonly string[] INJECTION_ARTIFACTS = new string[] {
            "user32.dll", "user32SF.dll", "AppList", "DLLInjector.exe", "DLLInjector.ini",
            "GreenLumaSettings_2026.exe", "GreenLuma_2026_x64.dll", "GreenLuma_2026_x86.dll",
            "GreenLuma_2026.log", "GreenLuma2026_Files", "NoQuestion.bin", "DeleteSteamAppCache.exe"
        };

        private int DeleteInjectedFiles()
        {
            if (string.IsNullOrEmpty(steamDir)) return 0;

            int deletedCount = 0;
            for (int i = 0; i < 6; i++)
            {
                bool anyFailed = false;
                foreach (var item in INJECTION_ARTIFACTS)
                {
                    string target = Path.Combine(steamDir, item);
                    try
                    {
                        if (File.Exists(target)) { File.Delete(target); deletedCount++; }
                        else if (Directory.Exists(target)) { Directory.Delete(target, true); deletedCount++; }
                    }
                    catch
                    {
                        anyFailed = true;
                    }
                }
                if (!anyFailed) break;
                Thread.Sleep(500);
            }
            return deletedCount;
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
            int purged = DeleteInjectedFiles();

            isInjected = false;
            UpdateStatusUI();
            MessageBox.Show(
                isAr ? string.Format("تم تنظيف مجلد Steam بنجاح! تم حذف {0} عناصر.", purged) : string.Format("Steam directory cleaned successfully! Purged {0} items.", purged),
                isAr ? "اكتمل التنظيف" : "Clean Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }


        // =========================================================================
        // --- 🚀 STEP-BY-STEP FIRST TIME QUICK GUIDE (INTERACTIVE DIALOG) ---
        // =========================================================================
        private void OpenQuickGuideDialog()
        {
            bool isAr = (settings.lang == "ar");

            var mainDock = new DockPanel() { Margin = new Thickness(4) };

            // Bottom Got-It Button
            var btnClose = new Button() {
                Content = isAr ? "✅ فهمت كل شيء، لنبدأ اللعب!" : "✅ Got it! Let's Start Gaming",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                Padding = new Thickness(24, 10, 24, 10),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 0)
            };
            Action doDismiss = () => {
                MarkGuideAsShownPermanently();
                CloseInAppModal();
            };

            btnClose.Click += (s, e) => doDismiss();
            DockPanel.SetDock(btnClose, Dock.Bottom);
            mainDock.Children.Add(btnClose);

            var scroll = new ScrollViewer() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var sp = new StackPanel();

            // Header Title
            var titleHeader = new StackPanel() { Margin = new Thickness(0, 0, 0, 20) };
            titleHeader.Children.Add(new TextBlock() {
                Text = isAr ? "🚀 دليل التشغيل السريع الشامل (خطوة بخطوة)" : "🚀 Step-by-Step Quick Start Guide",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            titleHeader.Children.Add(new TextBlock() {
                Text = isAr ? "تعلم كيفية تفعيل وتشغيل ألعابك وإضافاتك المفضلة وحقن ستيم في 4 خطوات بسيطة فقط:" : 
                              "Learn how to unlock and play your favorite Steam games & DLCs in 4 simple steps:",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            });
            sp.Children.Add(titleHeader);

            // Step Card Helper
            Action<string, string, string, string> AddStepCard = (stepNum, icon, title, desc) => {
                var card = new Border() {
                    Background = new SolidColorBrush(Color.FromArgb(80, 15, 23, 42)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12, 16, 14),
                    Margin = new Thickness(0, 0, 0, 12)
                };
                var stepStack = new StackPanel();
                
                var topRow = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                topRow.Children.Add(new TextBlock() {
                    Text = stepNum + " " + icon + " ",
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129))
                });
                topRow.Children.Add(new TextBlock() {
                    Text = title,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                stepStack.Children.Add(topRow);

                stepStack.Children.Add(new TextBlock() {
                    Text = desc,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 18
                });

                card.Child = stepStack;
                sp.Children.Add(card);
            };

            AddStepCard(
                isAr ? "الخطوة الأولى" : "Step 1",
                "🔍",
                isAr ? "البحث وإضافة الألعاب والـ DLCs" : "Search & Add Your Games & DLCs",
                isAr ? "اكتب اسم أي لعبة أو إضافة في شريط البحث بالأعلى، وستظهر لك النتائج فورا مع الأغلفة وحالة المشاركة العائلية (Family Share). اضغط على اللعبة لإضافتها لقائمتك. الألعاب الكاملة تحتوي على زر '+ إضافة الـ DLCs' لجلب كل إضافات اللعبة الرسمية بنقرة واحدة." :
                       "Type any game or DLC title into the search bar above to see live Steam store suggestions with cover art and Family Sharing status. Click to add it to your library. Base games include a '+ Add DLCs' button to pull all official DLCs with one click.");

            AddStepCard(
                isAr ? "الخطوة الثانية" : "Step 2",
                "🛡️",
                isAr ? "محرك التخفي الشامل التلقائي (Stealth Engine)" : "Universal Stealth Engine",
                isAr ? "يعتمد البرنامج تلقائياً على أحدث محرك تخفي لستيم (user32SF.dll)، وهو متوافق تماماً مع الحسابات العادية وحسابات عائلات ستيم وجميع الألعاب. يقوم البرنامج بنسخ وإعادة تسمية الملفات بصورة صحيحة ونظيفة بدون أي تدخل يدوي منك." :
                       "The Manager automatically utilizes the latest universal stealth engine (user32SF.dll), fully compatible with standard Steam accounts, Steam Families, and all modern titles. The Manager handles all file copying and proper renaming automatically.");

            AddStepCard(
                isAr ? "الخطوة الثالثة" : "Step 3",
                "🚀",
                isAr ? "التشغيل الذكي بضغطة زر واحدة (1-Click Launch)" : "1-Click Smart Launch",
                isAr ? "اضغط على الزر الأخضر الرئيسي '🚀 تشغيل ستيم مع GreenLuma'. سيقوم البرنامج تلقائياً بإغلاق ستيم، مسح الكاش القديم عبر DeleteSteamAppCache، توليد ملف AppList.ini سليم ونقي بدون BOM، وحقن الملفات وتشغيل ستيم فوراً. عند تسجيل الدخول ستجد ألعابك مفتوحة وجاهزة للتحميل واللعب!" :
                       "Click the primary green button '🚀 Launch Steam (GreenLuma)'. The app automatically closes any active Steam, wipes stale appcache via DeleteSteamAppCache, compiles a clean BOM-free AppList.ini with slot 90 priority, injects the payload, and starts Steam cleanly.");

            AddStepCard(
                isAr ? "الخطوة الرابعة" : "Step 4",
                "🧹",
                isAr ? "لعب آمن وتنظيف شامل عند الإغلاق" : "Safe Gaming & 1-Click Cleanup",
                isAr ? "العب ألعابك كالمعتاد! وعندما تنتهي، اضغط على زر '🛑 إيقاف وحذف الملفات' أو أغلق ستيم. يقوم البرنامج بحذف جميع ملفات الحقن فوراً من مجلد ستيم ليبقى مجلد حسابك نظيفاً وسليماً 100%." :
                       "Enjoy your games! When you're done, click '🛑 Stop & Clean Files' or exit Steam. The app automatically purges all injected stealth artifacts from your Steam directory, keeping your installation 100% pristine.");

            AddStepCard(
                isAr ? "تحديثات CS.RIN.RU" : "CS.RIN.RU Updates",
                "🔄",
                isAr ? "كيفية تطبيق أي تحديث جديد من منتدى CS.RIN.RU؟" : "How to Apply New CS.RIN.RU Updates?",
                isAr ? "عند صدور أي تحديث جديد لـ GreenLuma من المطور Steam006 على منتدى CS.RIN.RU:\n1. فك ضغط الملف المنزّل من المنتدى وستجد بداخله مجلد 'StealthMode'.\n2. انسخ مجلد 'StealthMode' وضعه بجانب ملف GreenLumaManager.exe مباشرة (أو انسخ ملفات user32SF.dll و DeleteSteamAppCache.exe بجانب البرنامج).\n3. سيقوم البرنامج فوراً بالتعرف على التحديث الجديد وإعطائه الأولوية تلقائياً (ستلاحظ في شريط الحالة بالأسفل ظهور StealthMode بدلاً من Core)!" :
                       "When a new GreenLuma release is posted by Steam006 on CS.RIN.RU:\n1. Extract the downloaded archive and locate the 'StealthMode' folder.\n2. Place the 'StealthMode' folder directly beside GreenLumaManager.exe (or place user32SF.dll & DeleteSteamAppCache.exe beside the EXE).\n3. The Manager immediately detects the new files and prioritizes them automatically (the telemetry badge on the right will show StealthMode instead of Core)!");

            scroll.Content = sp;
            mainDock.Children.Add(scroll);
            ShowInAppModal(mainDock, isAr ? "📖 دليل التشغيل السريع - GreenLuma 2026" : "GreenLuma 2026 - Quick Start Guide", () => {
                MarkGuideAsShownPermanently();
            }, 680);
        }

        private bool IsFirstTimeUser()
        {
            if (settings != null && settings.firstTimeGuideShown) return false;
            if (gamesList != null && gamesList.Count > 0) return false;

            string gamesFile = Path.Combine(appDir, "games.json");
            if (File.Exists(gamesFile))
            {
                try
                {
                    if (new FileInfo(gamesFile).Length > 10) return false;
                }
                catch { }
            }

            string settingsFile = Path.Combine(appDir, "settings.json");
            if (File.Exists(settingsFile))
            {
                try
                {
                    if (new FileInfo(settingsFile).Length > 10) return false;
                }
                catch { }
            }

            string markerFile = Path.Combine(appDir, ".guide_done");
            if (File.Exists(markerFile)) return false;

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\GreenLuma2026"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("FirstTimeGuideShown");
                        if (val != null && Convert.ToInt32(val) == 1) return false;
                    }
                }
            }
            catch { }

            return true;
        }

        private void MarkGuideAsShownPermanently()
        {
            try
            {
                if (settings != null)
                {
                    settings.firstTimeGuideShown = true;
                    SaveSettings();
                }

                string markerFile = Path.Combine(appDir, ".guide_done");
                if (!File.Exists(markerFile))
                {
                    File.WriteAllText(markerFile, DateTime.UtcNow.ToString("o"));
                }

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\GreenLuma2026"))
                {
                    if (key != null)
                    {
                        key.SetValue("FirstTimeGuideShown", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        // =========================================================================
        // --- 📥 IN-APP AUTO-UPDATER & HOT-SWAP ENGINE ---
        // =========================================================================
        private void StartInAppUpdate()
        {
            bool isAr = (settings.lang == "ar");
            btnUpdateBanner.IsEnabled = false;
            btnUpdateBanner.Content = isAr ? "⏳ جاري التنزيل..." : "⏳ Downloading...";

            ThreadPool.QueueUserWorkItem((state) => {
                try
                {
                    string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                    string currentDir = Path.GetDirectoryName(currentExe);
                    string tempExe = Path.Combine(currentDir, "GreenLumaManager.new");
                    string backupExe = Path.Combine(currentDir, "GreenLumaManager.old");

                    string downloadUrl = pendingExeDownloadUrl;
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        downloadUrl = string.Format("https://github.com/{0}/releases/download/{1}/GreenLumaManager.exe", GITHUB_REPO, pendingLatestTag);
                    }

                    if (File.Exists(tempExe)) { try { File.Delete(tempExe); } catch { } }

                    // 1. Download new executable with live percentage
                    using (var wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "GreenLumaManager-Updater");
                        var syncLock = new object();
                        bool completed = false;
                        Exception dlEx = null;

                        wc.DownloadProgressChanged += (s, e) => {
                            Dispatcher.Invoke((Action)(() => {
                                txtUpdateBanner.Text = isAr ?
                                    string.Format("⏳ جاري تنزيل التحديث ({0}%)...", e.ProgressPercentage) :
                                    string.Format("⏳ Downloading update ({0}%)...", e.ProgressPercentage);
                            }));
                        };

                        wc.DownloadFileCompleted += (s, e) => {
                            lock (syncLock)
                            {
                                completed = true;
                                dlEx = e.Error;
                                Monitor.Pulse(syncLock);
                            }
                        };

                        lock (syncLock)
                        {
                            wc.DownloadFileAsync(new Uri(downloadUrl), tempExe);
                            Monitor.Wait(syncLock);
                        }

                        if (!completed || dlEx != null) throw (dlEx != null ? dlEx : new Exception("Download timed out or failed."));
                    }

                    // Validate downloaded executable
                    if (!File.Exists(tempExe) || new FileInfo(tempExe).Length < 100000)
                    {
                        throw new Exception("Downloaded binary is missing or incomplete.");
                    }

                    // 2. Download accompanying payload DLLs if present in release
                    if (pendingOtherAssets != null && pendingOtherAssets.Count > 0)
                    {
                        string stealthDir = Path.Combine(currentDir, "StealthMode");
                        if (!Directory.Exists(stealthDir)) Directory.CreateDirectory(stealthDir);

                        using (var wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "GreenLumaManager-Updater");
                            foreach (var asset in pendingOtherAssets)
                            {
                                try
                                {
                                    string targetPath = Path.Combine(stealthDir, asset.Key);
                                    wc.DownloadFile(new Uri(asset.Value), targetPath);
                                }
                                catch { }
                            }
                        }
                    }

                    // 3. Hot-swap the running executable in Windows
                    backupExe = Path.Combine(currentDir, string.Format("GreenLumaManager_{0}.old", DateTime.Now.Ticks));
                    File.Move(currentExe, backupExe);

                    for (int retry = 0; retry < 5; retry++)
                    {
                        try { File.Move(tempExe, currentExe); break; }
                        catch { Thread.Sleep(200); }
                    }

                    Dispatcher.Invoke((Action)(() => {
                        txtUpdateBanner.Text = isAr ? "✅ اكتمل التحديث! جاري إعادة التشغيل..." : "✅ Update complete! Restarting...";
                    }));

                    Thread.Sleep(600);

                    // 4. Launch new updated version and terminate old process immediately
                    var psi = new ProcessStartInfo() {
                        FileName = currentExe,
                        WorkingDirectory = currentDir,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke((Action)(() => {
                        btnUpdateBanner.IsEnabled = true;
                        btnUpdateBanner.Content = isAr ? "⚡ تحديث وتثبيت فوري" : "⚡ Update & Restart Now";
                        txtUpdateBanner.Text = isAr ? "❌ تعذر التنزيل التلقائي. يمكنك التحميل يدويا." : "❌ Auto-update failed. You can download manually.";

                        string errMsg = isAr ?
                            ("تعذر إكمال التحديث التلقائي:\n" + ex.Message + "\n\nهل ترغب في فتح صفحة التحميل على GitHub؟") :
                            ("Could not complete automatic update:\n" + ex.Message + "\n\nOpen download page on GitHub instead?");


                        if (MessageBox.Show(errMsg, isAr ? "خطأ في التحديث" : "Update Error", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            try { Process.Start(pendingUpdateUrl); } catch { }
                        }
                    }));
                }
            });
        }

        // ==========================================
        // --- GITHUB RELEASE UPDATE CHECKER ---
        // ==========================================
        private void CheckAndWarnAppLocation()
        {
            try
            {
                string normAppDir = Path.GetFullPath(appDir).TrimEnd('\\', '/').ToLowerInvariant();
                string normSteamDir = !string.IsNullOrEmpty(steamDir) ? Path.GetFullPath(steamDir).TrimEnd('\\', '/').ToLowerInvariant() : "";
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string downloadsPath = !string.IsNullOrEmpty(userProfile) ? Path.Combine(userProfile, "Downloads").ToLowerInvariant() : "";
                string tempPath = Path.GetTempPath().TrimEnd('\\', '/').ToLowerInvariant();

                bool isAr = (settings.lang == "ar");

                if (normAppDir.StartsWith(tempPath))
                {
                    MessageBox.Show(
                        isAr ? "⚠️ تنبيه هام: أنت تقوم بتشغيل البرنامج من مجلد مؤقت (Temp) أو من داخل الأرشيف دون فك الضغط!\n\nيرجى إنشاء مجلد مستقل (مثال: C:\\GreenLuma أو على سطح المكتب) ونقل البرنامج ومجلد StealthMode إليه لضمان حفظ الألعاب والإعدادات بشكل دائم." :
                               "⚠️ Warning: You are running the application from a temporary archive or Temp directory!\n\nPlease extract GreenLumaManager.exe into its own dedicated folder (e.g. C:\\GreenLuma or on Desktop) to ensure settings and games are saved properly.",
                        isAr ? "تحذير مسار التشغيل" : "Placement Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else if (!string.IsNullOrEmpty(downloadsPath) && normAppDir.Equals(downloadsPath, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        isAr ? "💡 نصيحة تنظيمية: أنت تقوم بتشغيل البرنامج مباشرة من مجلد التنزيلات (Downloads).\n\nيُفضل بشدة إنشاء مجلد مخصص (مثال: C:\\GreenLuma) ونقل البرنامج ومجلد StealthMode إليه لتجنب الحذف بالخطأ من برامج تنظيف النظام." :
                               "💡 Recommendation: You are running directly from your Downloads folder.\n\nIt is strongly recommended to create a dedicated folder (e.g. C:\\GreenLuma) and place GreenLumaManager.exe and StealthMode there.",
                        isAr ? "تنظيم مجلد البرنامج" : "Recommended Setup",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (!string.IsNullOrEmpty(normSteamDir) && normAppDir.Equals(normSteamDir, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        isAr ? "⚠️ تحذير: لا تضع ملف GreenLumaManager.exe داخل مجلد Steam مباشرة!\n\nالبرنامج مصمم كأداة تحكم مستقلة تقوم بحقن وتنظيف الملفات في ستيم من الخارج. يرجى نقل البرنامج إلى مجلد خارجي مستقل." :
                               "⚠️ Warning: Do not place GreenLumaManager.exe directly inside your Steam folder!\n\nThe manager is an external controller. Please move it to its own separate directory.",
                        isAr ? "خطأ في مكان البرنامج" : "Invalid Location",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch { }
        }

        private void CheckForUpdatesAsync(bool manualUserClick)
        {
            ThreadPool.QueueUserWorkItem((s) => {
                string latestTag = "";
                string releaseUrl = "https://github.com/" + GITHUB_REPO + "/releases";
                string exeUrl = "";
                var extraAssets = new List<KeyValuePair<string, string>>();

                // 1. Check Manager GitHub Release
                try
                {
                    string apiUrl = "https://api.github.com/repos/" + GITHUB_REPO + "/releases/latest";
                    var req = (HttpWebRequest)WebRequest.Create(apiUrl);
                    req.UserAgent = "GreenLumaManager/" + CURRENT_VERSION;
                    req.Timeout = 8000;
                    using (var resp = req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        var js = new JavaScriptSerializer();
                        var dict = js.Deserialize<Dictionary<string, object>>(json);
                        if (dict != null)
                        {
                            if (dict.ContainsKey("tag_name")) latestTag = dict["tag_name"].ToString().Trim();
                            if (dict.ContainsKey("html_url")) releaseUrl = dict["html_url"].ToString();

                            if (dict.ContainsKey("assets"))
                            {
                                var rawAssets = dict["assets"] as System.Collections.IEnumerable;
                                if (rawAssets != null)
                                {
                                    foreach (var item in rawAssets)
                                    {
                                        var assetDict = item as Dictionary<string, object>;
                                        if (assetDict != null && assetDict.ContainsKey("name") && assetDict.ContainsKey("browser_download_url"))
                                        {
                                            string aName = assetDict["name"].ToString();
                                            string aUrl = assetDict["browser_download_url"].ToString();

                                            if (aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                            {
                                                exeUrl = aUrl;
                                            }
                                            else
                                            {
                                                extraAssets.Add(new KeyValuePair<string, string>(aName, aUrl));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                // 2. Check CS.RIN.RU GreenLuma Core Payload Status
                string latestPayloadVer = "2026 (v1.8.7)";
                string latestPayloadDate = "2026-08-13";
                string csTopicUrl = "https://cs.rin.ru/forum/viewtopic.php?f=10&t=140004";
                string payloadNotesAr = "إصدار GreenLuma 2026 المتوافق مع نظام عائلات ستيم وواجهات ستيم الحديثة عبر user32SF.dll.";
                string payloadNotesEn = "GreenLuma 2026 with full Steam Families & modern Steam compatibility.";

                try
                {
                    string manifestUrl = "https://raw.githubusercontent.com/" + GITHUB_REPO + "/master/payload_version.json";
                    var mReq = (HttpWebRequest)WebRequest.Create(manifestUrl);
                    mReq.UserAgent = "GreenLumaManager/" + CURRENT_VERSION;
                    mReq.Timeout = 6000;
                    using (var mResp = mReq.GetResponse())
                    using (var mReader = new StreamReader(mResp.GetResponseStream()))
                    {
                        string mJson = mReader.ReadToEnd();
                        var js = new JavaScriptSerializer();
                        var mDict = js.Deserialize<Dictionary<string, object>>(mJson);
                        if (mDict != null)
                        {
                            if (mDict.ContainsKey("latest_greenluma_version")) latestPayloadVer = mDict["latest_greenluma_version"].ToString();
                            if (mDict.ContainsKey("release_date")) latestPayloadDate = mDict["release_date"].ToString();
                            if (mDict.ContainsKey("cs_rin_topic_url")) csTopicUrl = mDict["cs_rin_topic_url"].ToString();
                            if (mDict.ContainsKey("notes_ar")) payloadNotesAr = mDict["notes_ar"].ToString();
                            if (mDict.ContainsKey("notes_en")) payloadNotesEn = mDict["notes_en"].ToString();
                        }
                    }
                }
                catch { }

                Dispatcher.Invoke((Action)(() => {
                    bool isAr = (settings.lang == "ar");

                    if (!string.IsNullOrEmpty(latestTag) && IsRemoteVersionNewer(latestTag, CURRENT_VERSION))
                    {
                        pendingUpdateUrl = releaseUrl;
                        pendingLatestTag = latestTag;
                        pendingExeDownloadUrl = !string.IsNullOrEmpty(exeUrl) ? exeUrl : 
                            string.Format("https://github.com/{0}/releases/download/{1}/GreenLumaManager.exe", GITHUB_REPO, latestTag);
                        pendingOtherAssets = extraAssets;

                        txtUpdateBanner.Text = isAr ? 
                            string.Format("🎉 يتوفر إصدار جديد ({0}) على GitHub! إصدارك الحالي: {1}", latestTag, CURRENT_VERSION) :
                            string.Format("🎉 New update available ({0}) on GitHub! Your current version: {1}", latestTag, CURRENT_VERSION);
                        btnUpdateChangelog.Content = isAr ? "ملاحظات الإصدار ↗" : "Release Notes ↗";
                        updateBannerBorder.Visibility = Visibility.Visible;
                    }

                    if (manualUserClick)
                    {
                        OpenUpdateCenterDialog(latestTag, releaseUrl, exeUrl, latestPayloadVer, latestPayloadDate, csTopicUrl, isAr ? payloadNotesAr : payloadNotesEn);
                    }
                }));
            });
        }

        private void OpenUpdateCenterDialog(string latestTag, string releaseUrl, string exeUrl, string latestPayloadVer, string latestPayloadDate, string csTopicUrl, string payloadNotes)
        {
            bool isAr = (settings.lang == "ar");
            bool hasAppUpdate = IsRemoteVersionNewer(latestTag, CURRENT_VERSION);

            string currentPayloadSrc;
            GetPayloadBytes("user32SF.dll", out currentPayloadSrc);

            var mainDock = new DockPanel() { Margin = new Thickness(16) };

            var btnClose = new Button() {
                Content = isAr ? "إغلاق" : "Close",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                Padding = new Thickness(24, 8, 24, 8),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0)
            };
            btnClose.Click += (s, e) => CloseInAppModal();
            DockPanel.SetDock(btnClose, Dock.Bottom);
            mainDock.Children.Add(btnClose);

            var scroll = new ScrollViewer() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var sp = new StackPanel();

            var titleHeader = new StackPanel() { Margin = new Thickness(0, 0, 0, 16) };
            titleHeader.Children.Add(new TextBlock() {
                Text = isAr ? "🔄 مركز التحديثات وفحص CS.RIN.RU" : "🔄 Update Center & CS.RIN.RU Checker",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            titleHeader.Children.Add(new TextBlock() {
                Text = isAr ? "فحص حالة واجهة البرنامج وملفات محرك GreenLuma على CS.RIN.RU:" :
                              "Check status of Manager GUI and CS.RIN.RU GreenLuma Core payloads:",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            });
            sp.Children.Add(titleHeader);

            // Card 1: Manager App GUI
            var cardApp = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(90, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var appStack = new StackPanel();
            var appTitleRow = new DockPanel() { Margin = new Thickness(0, 0, 0, 8) };
            var txtAppHead = new TextBlock() {
                Text = isAr ? "🖥️ واجهة البرنامج (GreenLuma Manager)" : "🖥️ Manager GUI Application",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249))
            };
            DockPanel.SetDock(txtAppHead, Dock.Left);
            appTitleRow.Children.Add(txtAppHead);

            var badgeApp = new Border() {
                Background = hasAppUpdate ? new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)) : new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                BorderBrush = hasAppUpdate ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            badgeApp.Child = new TextBlock() {
                Text = hasAppUpdate ? (isAr ? "⚠️ تحديث متوفر!" : "⚠️ Update Available!") : (isAr ? "✅ محدث بالكامل" : "✅ Up to Date"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = hasAppUpdate ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) : new SolidColorBrush(Color.FromRgb(110, 231, 183))
            };
            appTitleRow.Children.Add(badgeApp);
            appStack.Children.Add(appTitleRow);

            appStack.Children.Add(new TextBlock() {
                Text = isAr ? string.Format("• الإصدار الحالي المثبت: {0}\n• أحدث إصدار على GitHub: {1}", CURRENT_VERSION, string.IsNullOrEmpty(latestTag) ? CURRENT_VERSION : latestTag) :
                              string.Format("• Current Installed Version: {0}\n• Latest on GitHub: {1}", CURRENT_VERSION, string.IsNullOrEmpty(latestTag) ? CURRENT_VERSION : latestTag),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                LineHeight = 18,
                Margin = new Thickness(0, 0, 0, 10)
            });

            if (hasAppUpdate)
            {
                var btnDoAppUpdate = new Button() {
                    Content = isAr ? "⚡ تنزيل وتثبيت تحديث البرنامج تلقائياً" : "⚡ Auto-Download & Install Update",
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                    Foreground = new SolidColorBrush(Color.FromRgb(5, 15, 10)),
                    Padding = new Thickness(14, 6, 14, 6),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                btnDoAppUpdate.Click += (s, e) => {
                    CloseInAppModal();
                    StartInAppUpdate();
                };
                appStack.Children.Add(btnDoAppUpdate);
            }
            cardApp.Child = appStack;
            sp.Children.Add(cardApp);

            // Card 2: CS.RIN.RU GreenLuma Core Payloads
            var cardCs = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(90, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var csStack = new StackPanel();
            var csTitleRow = new DockPanel() { Margin = new Thickness(0, 0, 0, 8) };
            var txtCsHead = new TextBlock() {
                Text = isAr ? "🛡️ محرك GreenLuma التخفي (CS.RIN.RU Core)" : "🛡️ GreenLuma Stealth Core (CS.RIN.RU)",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249))
            };
            DockPanel.SetDock(txtCsHead, Dock.Left);
            csTitleRow.Children.Add(txtCsHead);

            var badgeCs = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(40, 6, 182, 212)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(6, 182, 212)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            badgeCs.Child = new TextBlock() {
                Text = "Steam006 Release",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(103, 232, 249))
            };
            csTitleRow.Children.Add(badgeCs);
            csStack.Children.Add(csTitleRow);

            csStack.Children.Add(new TextBlock() {
                Text = isAr ? string.Format("• أحدث إصدار معتمد على CS.RIN.RU: {0} (تاريخ: {1})\n• الملفات النشطة على جهازك حالياً: {2}\n• تفاصيل: {3}", latestPayloadVer, latestPayloadDate, currentPayloadSrc, payloadNotes) :
                              string.Format("• Latest Verified Release on CS.RIN.RU: {0} (Date: {1})\n• Active Local Engine: {2}\n• Notes: {3}", latestPayloadVer, latestPayloadDate, currentPayloadSrc, payloadNotes),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                LineHeight = 18,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var csButtonsRow = new StackPanel() { Orientation = Orientation.Horizontal };
            var btnOpenCsThread = new Button() {
                Content = isAr ? "🌐 فتح موضوع CS.RIN.RU في المتصفح ↗" : "🌐 Open CS.RIN.RU Thread in Browser ↗",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromArgb(40, 6, 182, 212)),
                Foreground = new SolidColorBrush(Color.FromRgb(103, 232, 249)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(6, 182, 212)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnOpenCsThread.Click += (s, e) => {
                try { Process.Start(new ProcessStartInfo(csTopicUrl) { UseShellExecute = true }); } catch { }
            };
            csButtonsRow.Children.Add(btnOpenCsThread);

            var btnOpenStealthDir = new Button() {
                Content = isAr ? "📂 فتح مجلد البرنامج لوضع الملفات" : "📂 Open App Folder to Drop Files",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand
            };
            btnOpenStealthDir.Click += (s, e) => {
                try { Process.Start("explorer.exe", appDir); } catch { }
            };
            csButtonsRow.Children.Add(btnOpenStealthDir);
            csStack.Children.Add(csButtonsRow);

            cardCs.Child = csStack;
            sp.Children.Add(cardCs);

            // Card 3: Dedicated Folder Setup Notice
            var cardFolder = new Border() {
                Background = new SolidColorBrush(Color.FromArgb(60, 245, 158, 11)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 245, 158, 11)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12)
            };
            var folderStack = new StackPanel();
            folderStack.Children.Add(new TextBlock() {
                Text = isAr ? "📁 القاعدة الذهبية لتنظيم مجلد البرنامج:" : "📁 Mandatory Folder Setup Rule:",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            folderStack.Children.Add(new TextBlock() {
                Text = isAr ? 
                    "• يجب وضع البرنامج في مجلد مستقل مخصص (مثال: C:\\GreenLuma أو على سطح المكتب).\n• لا تشغل البرنامج من داخل مجلد التنزيلات (Downloads) أو من داخل الأرشيف المؤقت (Temp).\n• لا تضع البرنامج داخل مجلد Steam مباشرة؛ فالبرنامج أداة تحكم مستقلة.\n• ضع مجلد StealthMode بجانب ملف GreenLumaManager.exe وسيتعرف عليه البرنامج تلقائياً." :
                    "• Always place the app in its own dedicated directory (e.g. C:\\GreenLuma or on Desktop).\n• Never run directly from Downloads or from an unextracted Temp archive.\n• Never place the executable directly inside your Steam folder.\n• Place the StealthMode folder next to GreenLumaManager.exe for seamless automatic loading.",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(254, 243, 199)),
                LineHeight = 17
            });
            cardFolder.Child = folderStack;
            sp.Children.Add(cardFolder);

            scroll.Content = sp;
            mainDock.Children.Add(scroll);
            ShowInAppModal(mainDock, isAr ? "🔄 مركز التحديثات وفحص CS.RIN.RU" : "🔄 Update Center & CS.RIN.RU Checker", null, 680);
        }

        private void DetectSteamPath()
        {
            if (!string.IsNullOrEmpty(settings.customSteamPath) && Directory.Exists(settings.customSteamPath))
            {
                steamDir = settings.customSteamPath;
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
                    return;
                }
            }
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

            // In Stealth Mode, Steam is injected if user32.dll or user32SF.dll is inside Steam directory
            string user32 = Path.Combine(steamDir, "user32.dll");
            string user32sf = Path.Combine(steamDir, "user32SF.dll");
            if (File.Exists(user32) || File.Exists(user32sf)) return true;

            // In Normal Mode, check if DLLInjector process is currently running
            try
            {
                if (Process.GetProcessesByName("DLLInjector").Length > 0) return true;
            }
            catch { }

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
                try { p.Kill(); p.WaitForExit(1500); } catch { }
            }
            foreach (var p in Process.GetProcessesByName("steamwebhelper"))
            {
                try { p.Kill(); p.WaitForExit(1500); } catch { }
            }
            try
            {
                var psi = new ProcessStartInfo("taskkill", "/F /IM steam.exe /IM steamwebhelper.exe /T") {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var tk = Process.Start(psi);
                if (tk != null) tk.WaitForExit(2500);
            }
            catch { }
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

            if (string.IsNullOrEmpty(settings.mode) || settings.mode == "standard") settings.mode = "families";
            if (settings.steamArgs == "-inhibitbootstrap") settings.steamArgs = "";
            if (string.IsNullOrEmpty(settings.lang)) settings.lang = "en";

            UpdateModeButtonStyles();
            UpdateClearCacheButton();
        }

        private void SaveSettings()
        {
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

            // Auto-heal and link parent-child DLC relationships for existing library
            bool modified = false;
            foreach (var g in gamesList)
            {
                if (g.CheckIsDLC() && (g.parentAppId == 0 || string.IsNullOrEmpty(g.parentName)))
                {
                    // 1. Try matching with an existing base game in library
                    var parent = gamesList.Find(p => !p.CheckIsDLC() && g.name.StartsWith(p.name, StringComparison.OrdinalIgnoreCase));
                    if (parent != null)
                    {
                        g.parentAppId = parent.appId;
                        g.parentName = parent.name;
                        modified = true;
                    }
                    else
                    {
                        // 2. Try splitting on " - " or ": "
                        int idx = g.name.IndexOf(" - ");
                        if (idx < 0) idx = g.name.IndexOf(": ");
                        if (idx > 0)
                        {
                            string pName = g.name.Substring(0, idx).Trim();
                            g.parentName = pName;
                            modified = true;
                        }
                    }
                }
            }
            if (modified)
            {
                try
                {
                    var js = new JavaScriptSerializer();
                    File.WriteAllText(file, js.Serialize(gamesList), Encoding.UTF8);
                }
                catch { }
            }

            UpdateTabButtonStyles();
            RenderGamesUI();
            SyncAppListIni();
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
            var games = new List<int>();
            var dlcs = new List<int>();
            foreach (var g in gamesList)
            {
                if (g.enabled)
                {
                    if (g.CheckIsDLC()) dlcs.Add(g.appId);
                    else games.Add(g.appId);
                }
            }

            var enabledIds = new List<int>();
            enabledIds.AddRange(games);
            enabledIds.AddRange(dlcs);

            var sb = new StringBuilder();
            sb.AppendLine("[AppList]");
            sb.AppendLine("# Format:");
            sb.AppendLine("# Old AppID = New AppID to unlock");
            sb.AppendLine("# Remove the # before the old AppID");
            sb.AppendLine();

            for (int i = 0; i < DEFAULT_SLOTS.Length; i++)
            {
                if (i < enabledIds.Count)
                {
                    sb.AppendLine(string.Format("{0} = {1}", DEFAULT_SLOTS[i], enabledIds[i]));
                }
                else
                {
                    sb.AppendLine(string.Format("#{0} = ", DEFAULT_SLOTS[i]));
                }
            }

            if (enabledIds.Count > DEFAULT_SLOTS.Length)
            {
                for (int i = DEFAULT_SLOTS.Length; i < enabledIds.Count; i++)
                {
                    sb.AppendLine(string.Format("{0} = {0}", enabledIds[i]));
                }
            }

            string content = sb.ToString();
            var utf8NoBom = new UTF8Encoding(false);

            string localAppListDir = Path.Combine(appDir, "AppList");
            string stealthAppListDir = Path.Combine(appDir, "StealthMode", "AppList");
            try
            {
                if (!Directory.Exists(localAppListDir)) Directory.CreateDirectory(localAppListDir);
                foreach (var f in Directory.GetFiles(localAppListDir, "*.txt")) { try { File.Delete(f); } catch { } }
                File.WriteAllText(Path.Combine(localAppListDir, "AppList.ini"), content, utf8NoBom);

                if (Directory.Exists(stealthAppListDir))
                {
                    File.WriteAllText(Path.Combine(stealthAppListDir, "AppList.ini"), content, utf8NoBom);
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(steamDir))
            {
                string steamAppListDir = Path.Combine(steamDir, "AppList");
                try
                {
                    if (!Directory.Exists(steamAppListDir)) Directory.CreateDirectory(steamAppListDir);
                    foreach (var f in Directory.GetFiles(steamAppListDir, "*.txt")) { try { File.Delete(f); } catch { } }
                    File.WriteAllText(Path.Combine(steamAppListDir, "AppList.ini"), content, utf8NoBom);
                }
                catch { }
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
                    (isAr ? "ستيم نشط (محقون)" : "Steam Running (Injected)") : 
                    (isAr ? "الملفات جاهزة للتشغيل" : "Files Injected (Ready)");
                btnStop.Visibility = Visibility.Visible;
            }
            else
            {
                borderStatusDot.Background = running ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));
                borderStatusPulseRing.Background = running ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));
                txtStatus.Text = running ? 
                    (isAr ? "ستيم يعمل (أصلي)" : "Steam Running (Original)") : 
                    (isAr ? "ستيم غير نشط" : "Steam Inactive");
                btnStop.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            }

            RefreshPayloadSourceUI();

            // Active Steam Account Detection
            DetectActiveSteamAccount();
            if (accountPillBorder != null)
            {
                if (!string.IsNullOrEmpty(activeSteamPersona))
                {
                    txtAccountName.Text = activeSteamPersona;
                    accountPillBorder.Visibility = Visibility.Visible;
                    accountPillBorder.ToolTip = string.Format("Active Steam User: {0} ({1})", activeSteamPersona, activeSteamAccount);
                }
                else
                {
                    accountPillBorder.Visibility = Visibility.Collapsed;
                }
            }

            // Sync with Compact Tray Mode
            if (btnMiniStop != null) btnMiniStop.Visibility = btnStop.Visibility;
            if (lblMiniStatus != null) lblMiniStatus.Text = txtStatus.Text;
            if (btnMiniLaunch != null && btnLaunch != null) btnMiniLaunch.IsEnabled = btnLaunch.IsEnabled;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Keep Steam running with GreenLuma files even if manager UI is closed
        }
    }

    public static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        [STAThread]
        public static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Global\\GreenLumaStealthManager_SingleInstance_Mutex_v1", out createdNew))
            {
                if (!createdNew)
                {
                    // Bring existing instance to foreground
                    try
                    {
                        Process current = Process.GetCurrentProcess();
                        foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                        {
                            if (process.Id != current.Id && process.MainWindowHandle != IntPtr.Zero)
                            {
                                ShowWindow(process.MainWindowHandle, SW_RESTORE);
                                SetForegroundWindow(process.MainWindowHandle);
                                break;
                            }
                        }
                    }
                    catch { }
                    return;
                }

                var app = new Application();
                var window = new MainWindow();
                app.Run(window);
            }
        }
    }
}
