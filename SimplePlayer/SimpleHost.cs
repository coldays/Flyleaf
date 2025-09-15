using FlyleafLib;
using FlyleafLib.Controls;
using FlyleafLib.MediaPlayer;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using static FlyleafLib.Utils;
using static FlyleafLib.Utils.NativeMethods;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace SimplePlayer;

public class SimpleHost : ContentControl, IHostPlayer
{
    #region Properties / Variables
    public Window Owner { get; private set; }
    public nint SurfaceHandle { get; private set; }
    public nint OverlayHandle { get; private set; }
    public nint OwnerHandle { get; private set; }

    public int UniqueId { get; private set; }

    public double DpiX { get; private set; } = 1;
    public double DpiY { get; private set; } = 1;


    public event EventHandler SurfaceCreated;
    public event EventHandler OverlayCreated;

    private static bool isDesginMode;
    private static int idGenerator = 1;
    private static nint NONE_STYLE = (nint)(WindowStyles.WS_MINIMIZEBOX | WindowStyles.WS_CLIPSIBLINGS | WindowStyles.WS_CLIPCHILDREN | WindowStyles.WS_VISIBLE); // WS_MINIMIZEBOX required for swapchain
    private static Rect rectRandom = new(1, 2, 3, 4);

    private int _panPrevX, _panPrevY;
    private bool _isMouseBindingsSubscribedSurface;
    private bool _isMouseBindingsSubscribedOverlay;

    private Point _zeroPoint = new(0, 0);
    private Point _mouseLeftDownPoint = new(0, 0);
    private Point _mouseMoveLastPoint = new(0, 0);

    private Rect _zeroRect = new(0, 0, 0, 0);
    private Rect _rectInit;
    private Rect _rectInitLast = rectRandom;
    private Rect _rectIntersect;
    private Rect _rectIntersectLast = rectRandom;
    private Window _overlay;
    public Window _surface;
    private bool _presentationSourceChanging = false;

    protected readonly LogHandler Log;
    #endregion

    #region Dependency Properties
    public void BringToFront() => SetWindowPos(SurfaceHandle, nint.Zero, 0, 0, 0, 0, (uint)(SetWindowPosFlags.SWP_SHOWWINDOW | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOMOVE));
    public bool BringToFrontOnClick
    {
        get { return (bool)GetValue(BringToFrontOnClickProperty); }
        set { SetValue(BringToFrontOnClickProperty, value); }
    }
    public static readonly DependencyProperty BringToFrontOnClickProperty =
    DependencyProperty.Register(nameof(BringToFrontOnClick), typeof(bool), typeof(SimpleHost), new PropertyMetadata(true));

    public int ActivityTimeout
    {
        get => (int)GetValue(ActivityTimeoutProperty);
        set => SetValue(ActivityTimeoutProperty, value);
    }
    public static readonly DependencyProperty ActivityTimeoutProperty =
        DependencyProperty.Register(nameof(ActivityTimeout), typeof(int), typeof(SimpleHost), new PropertyMetadata(0, new PropertyChangedCallback(OnActivityTimeoutChanged)));

    public bool IsPanMoving
    {
        get { return (bool)GetValue(IsPanMovingProperty); }
        private set { SetValue(IsPanMovingProperty, value); }
    }
    public static readonly DependencyProperty IsPanMovingProperty =
        DependencyProperty.Register(nameof(IsPanMoving), typeof(bool), typeof(SimpleHost), new PropertyMetadata(false));

    public Player Player
    {
        get => (Player)GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }
    public static readonly DependencyProperty PlayerProperty =
        DependencyProperty.Register(nameof(Player), typeof(Player), typeof(SimpleHost), new PropertyMetadata(null, OnPlayerChanged));

    public ControlTemplate OverlayTemplate
    {
        get => (ControlTemplate)GetValue(OverlayTemplateProperty);
        set => SetValue(OverlayTemplateProperty, value);
    }
    public static readonly DependencyProperty OverlayTemplateProperty =
        DependencyProperty.Register(nameof(OverlayTemplate), typeof(ControlTemplate), typeof(SimpleHost), new PropertyMetadata(null, new PropertyChangedCallback(OnOverlayTemplateChanged)));

    #endregion

    #region Events
    private static void OnPlayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (isDesginMode)
            return;

        SimpleHost host = d as SimpleHost;
        if (!host.IsLoaded)
            return;

        host.SetPlayer((Player)e.OldValue);
    }
    private static void OnActivityTimeoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SimpleHost host = d as SimpleHost;
        if (!host.IsLoaded)
            return;

        if (host.Player == null)
            return;

        host.Player.Activity.Timeout = host.ActivityTimeout;
    }
    private static void OnOverlayTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (isDesginMode)
            return;

        SimpleHost host = d as SimpleHost;
        if (!host.IsLoaded)
            return;

        host._overlay.Template = host.OverlayTemplate;
        host.CreateOverlay();
    }

    private static object OnContentChanging(DependencyObject d, object baseValue)
    {
        if (isDesginMode)
            return baseValue;

        SimpleHost host = d as SimpleHost;
        if (!host.IsLoaded)
            return host._overlay;

        if (baseValue != null && host._overlay == null)
            host._overlay = new Window() { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true };

        if (host._overlay != null)
            host._overlay.Content = baseValue;

        return host._overlay;
    }

    private void SimpleHost_Unloaded(object sender, RoutedEventArgs e)
    {
        if (Player is not null)
        {
            Player?.renderer.DisposeSwapChain();
            Player.Host = this;
        }

        if (_overlay != null)
        {
            if (_isMouseBindingsSubscribedOverlay)
                SetMouseOverlay();

            _overlay.IsVisibleChanged -= OverlayStandAlone_IsVisibleChanged;
            _overlay.KeyUp -= Overlay_KeyUp;
            _overlay.KeyDown -= Overlay_KeyDown;
            _overlay.Closed -= Overlay_Closed;
            _overlay.Drop -= Overlay_Drop;
            _overlay.DragEnter -= Overlay_DragEnter;

            _overlay.Owner = null;
            SetParent(OverlayHandle, nint.Zero);
            _overlay.Width = _overlay.Height = 1;
            //_overlay.Show();
            _overlay.Close();
        }

        if (_surface != null)
        {
            if (_isMouseBindingsSubscribedSurface)
                SetMouseSurface();

            _surface.Closed -= Surface_Closed;
            _surface.KeyDown -= Surface_KeyDown;
            _surface.KeyUp -= Surface_KeyUp;
            _surface.Drop -= Surface_Drop;
            _surface.DragEnter -= Surface_DragEnter;
            _surface.SizeChanged -= SetRectOverlay;

            _surface.Owner = null;
            SetParent(SurfaceHandle, nint.Zero);
            _surface.Width = _surface.Height = 1;
            //_surface.Show();
            _surface.Close();
        }

        if (Owner != null)
        {
            Owner.SizeChanged -= Owner_SizeChanged;
        }

        _surface = null;
        _overlay = null;
        Owner = null;

        SurfaceHandle = nint.Zero;
        OverlayHandle = nint.Zero;
        OwnerHandle = nint.Zero;
    }

    private void OnSourceChanged(object sender, SourceChangedEventArgs e)
    {
        if (IsLoaded && e.NewSource is null)
        {
            // View was disconnected from view
            _presentationSourceChanging = true;
        }
    }

    private void Host_Loaded(object sender, RoutedEventArgs e)
    {
        Window owner = Window.GetWindow(this);
        if (owner == null)
            return;

        _presentationSourceChanging = false;

        var ownerHandle = new WindowInteropHelper(owner).EnsureHandle();

        Owner = owner;
        OwnerHandle = ownerHandle;

        // Create surface first
        CreateSurface();

        _surface.Title = Owner.Title;
        _surface.Icon = Owner.Icon;

        CreateOverlay();
        _overlay.DataContext = DataContext;

        Owner.SizeChanged += Owner_SizeChanged;

        Attach();
        _surface.Show();
        _overlay.Show();
    }

#if NET5_0_OR_GREATER
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        DpiX = newDpi.DpiScaleX;
        DpiY = newDpi.DpiScaleY;
    }
#endif

    // WindowChrome Issue #410: It will not properly move child windows when resized from top or left
    private void Owner_SizeChanged(object sender, SizeChangedEventArgs e)
        => _rectInitLast = Rect.Empty;

    private void Host_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        _overlay.DataContext = DataContext;
    }

    private void Host_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_presentationSourceChanging)
            return;
        if (!IsLoaded)
            return;
        if (IsVisible)
        {
            _surface.Show();
            _overlay.Show();

            // It happens (eg. with MetroWindow) that overlay left will not be equal to surface left so we reset it by detach/attach the overlay to surface (https://github.com/SuRGeoNix/Flyleaf/issues/370)
            RECT surfRect = new();
            RECT overRect = new();
            GetWindowRect(SurfaceHandle, ref surfRect);
            GetWindowRect(OverlayHandle, ref overRect);

            if (surfRect.Left != overRect.Left)
            {
                // Detach Overlay
                SetParent(OverlayHandle, nint.Zero);
                SetWindowLong(OverlayHandle, (int)WindowLongFlags.GWL_STYLE, NONE_STYLE);
                _overlay.Owner = null;

                SetWindowPos(OverlayHandle, nint.Zero, 0, 0, (int)_surface.ActualWidth, (int)_surface.ActualHeight,
                    (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));

                // Attache Overlay
                SetWindowLong(OverlayHandle, (int)WindowLongFlags.GWL_STYLE, NONE_STYLE | (nint)(WindowStyles.WS_CHILD | WindowStyles.WS_MAXIMIZE));
                _overlay.Owner = _surface;
                SetParent(OverlayHandle, SurfaceHandle);

                // Required to restore overlay
                Rect tt1 = new(0, 0, 0, 0);
                SetRect(ref tt1);
            }
        }
        else
        {
            _surface.Hide();
            _overlay?.Hide();
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        RecalcRect();
    }
    private void RecalcRect()
    {
        // Finds Rect Intersect with TPGFlyleafHost's parents and Clips Surface/Overlay (eg. within ScrollViewer)
        // TBR: Option not to clip rect or stop at first/second parent?
        // For performance should focus only on ScrollViewer if any and Owner Window (other sources that clip our host?)

        if (!IsVisible)
            return;

        if (Owner is null)
        {
            return;
        }

        try
        {
            _rectInit = _rectIntersect = new(TransformToAncestor(Owner).Transform(_zeroPoint), RenderSize);

            FrameworkElement parent = this;
            while ((parent = VisualTreeHelper.GetParent(parent) as FrameworkElement) != null)
                _rectIntersect.Intersect(new Rect(parent.TransformToAncestor(Owner).Transform(_zeroPoint), parent.RenderSize));

            if (_rectInit != _rectInitLast)
            {
                SetRect(ref _rectInit);
                _rectInitLast = _rectInit;
            }

            if (_rectIntersect == Rect.Empty)
            {
                if (_rectIntersect == _rectIntersectLast)
                    return;

                _rectIntersectLast = _rectIntersect;
                SetVisibleRect(ref _zeroRect);
            }
            else
            {
                _rectIntersect.X -= _rectInit.X;
                _rectIntersect.Y -= _rectInit.Y;

                if (_rectIntersect == _rectIntersectLast)
                    return;

                _rectIntersectLast = _rectIntersect;

                SetVisibleRect(ref _rectIntersect);
            }
        }
        catch (Exception ex)
        {
            // It has been noticed with NavigationService (The visual tree changes, visual root IsVisible is false but TPGFlyleafHost is still visible)
            if (Logger.CanDebug)
                Log.Debug($"Host_LayoutUpdated: {ex.Message}");

            // TBR: (Currently handle on each time Visible=true) It's possible that the owner/parent has been changed (for some reason Host_Loaded will not be called) *probably when the Owner stays the same but the actual Handle changes
            //if (ex.Message == "The specified Visual is not an ancestor of this Visual.")
            //Host_Loaded(null, null);
        }
    }
    #endregion

    #region Events Surface / Overlay

    private void Surface_KeyDown(object sender, KeyEventArgs e) { RaiseEvent(e); }
    private void Overlay_KeyDown(object sender, KeyEventArgs e) { RaiseEvent(e); }

    private void Surface_KeyUp(object sender, KeyEventArgs e) { RaiseEvent(e); }
    private void Overlay_KeyUp(object sender, KeyEventArgs e) { RaiseEvent(e); }

    private void Surface_Drop(object sender, DragEventArgs e)
    {
        _surface.ReleaseMouseCapture();

        if (Player == null)
            return;

        // Raise event in host
        RaiseEvent(e);

        _surface.Activate();
    }
    private void Overlay_Drop(object sender, DragEventArgs e)
    {
        _overlay.ReleaseMouseCapture();

        if (Player == null)
            return;

        // Raise event in host
        RaiseEvent(e);

        _overlay.Activate();
    }
    private void Surface_DragEnter(object sender, DragEventArgs e) { if (Player != null) e.Effects = DragDropEffects.All; }
    private void Overlay_DragEnter(object sender, DragEventArgs e) { if (Player != null) e.Effects = DragDropEffects.All; }

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SO_MouseLeftButtonDown(e, _surface);
    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SO_MouseLeftButtonDown(e, _overlay);
    private void SO_MouseLeftButtonDown(MouseButtonEventArgs e, Window window)
    {
        if (BringToFrontOnClick) // Activate and Z-order top
            BringToFront();

        window.Focus();
        Player?.Activity.RefreshFullActive();

        if (Player is null)
            return;

        // Do not allow panning when not zoomed in
        if (Player.renderer.Zoom == 1.0)
            return;

        _mouseLeftDownPoint = e.GetPosition(window);

        // PanMove
        _panPrevX = Player.PanXOffset;
        _panPrevY = Player.PanYOffset;
        IsPanMoving = true;

        window.CaptureMouse();
    }

    private void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Surface_ReleaseCapture();
    private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Overlay_ReleaseCapture();
    private void Surface_LostMouseCapture(object sender, MouseEventArgs e) => Surface_ReleaseCapture();
    private void Overlay_LostMouseCapture(object sender, MouseEventArgs e) => Overlay_ReleaseCapture();
    private void Surface_ReleaseCapture()
    {
        if (!IsPanMoving)
            return;

        _surface.ReleaseMouseCapture();

        if (IsPanMoving)
            IsPanMoving = false;
        else
            return;
    }
    private void Overlay_ReleaseCapture()
    {
        if (!IsPanMoving)
            return;

        _overlay.ReleaseMouseCapture();

        if (IsPanMoving)
            IsPanMoving = false;
    }

    private void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        var cur = e.GetPosition(_surface);

        if (Player != null && cur != _mouseMoveLastPoint)
        {
            Player.Activity.RefreshFullActive();
            _mouseMoveLastPoint = cur;
        }

        SO_MouseLeftDownAndMove(cur);
    }
    private void Overlay_MouseMove(object sender, MouseEventArgs e)
    {
        var cur = e.GetPosition(_overlay);

        if (Player != null && cur != _mouseMoveLastPoint)
        {
            Player.Activity.RefreshFullActive();
            _mouseMoveLastPoint = cur;
        }

        SO_MouseLeftDownAndMove(cur);
    }
    private void SO_MouseLeftDownAndMove(Point cur)
    {
        // Player's Pan Move (Ctrl + Drag Move)
        if (IsPanMoving)
        {
            Player.PanXOffset = _panPrevX + (int)(cur.X - _mouseLeftDownPoint.X);
            Player.PanYOffset = _panPrevY + (int)(cur.Y - _mouseLeftDownPoint.Y);
            // Only change viewport once
            //Player.renderer.SetPanXY(
            //    _panPrevX + (int)(cur.X - _mouseLeftDownPoint.X),
            //    _panPrevY + (int)(cur.Y - _mouseLeftDownPoint.Y)
            //);

            return;
        }
    }

    private void Surface_MouseLeave(object sender, MouseEventArgs e) { _surface.Cursor = Cursors.Arrow; }
    private void Overlay_MouseLeave(object sender, MouseEventArgs e) { _overlay.Cursor = Cursors.Arrow; }

    private void Surface_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Player == null || e.Delta == 0)
            return;

        var cur = e.GetPosition(_surface);
        Point curDpi = new(cur.X * DpiX, cur.Y * DpiY);
        if (e.Delta > 0)
            Player.ZoomIn(curDpi);
        else
            Player.ZoomOut(curDpi);
    }
    private void Overlay_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Player == null || e.Delta == 0)
            return;

        var cur = e.GetPosition(_overlay);
        Point curDpi = new(cur.X * DpiX, cur.Y * DpiY);
        if (e.Delta > 0)
            Player.ZoomIn(curDpi);
        else
            Player.ZoomOut(curDpi);
    }

    private void Surface_Closed(object sender, EventArgs e)
    {
        // TODO: Logg here if this was unexpected...
    }
    private void Overlay_Closed(object sender, EventArgs e)
    {
        // TODO: Logg here if this was unexpected...
    }
    private void OverlayStandAlone_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Surface should be visible first (this happens only on initialization of standalone)

        if (_surface.IsVisible)
            return;

        if (_overlay.IsVisible)
        {
            _surface.Show();
            ShowWindow(OverlayHandle, (int)ShowWindowCommands.SW_SHOWMINIMIZED);
            ShowWindow(OverlayHandle, (int)ShowWindowCommands.SW_SHOWMAXIMIZED);
        }
    }
    #endregion

    #region Constructors
    static SimpleHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SimpleHost), new FrameworkPropertyMetadata(typeof(SimpleHost)));
        ContentProperty.OverrideMetadata(typeof(SimpleHost), new FrameworkPropertyMetadata(null, new CoerceValueCallback(OnContentChanging)));
    }
    public SimpleHost()
    {
        UniqueId = idGenerator++;
        isDesginMode = DesignerProperties.GetIsInDesignMode(this);
        if (isDesginMode)
            return;

        Log = new LogHandler(("[#" + UniqueId + "]").PadRight(8, ' ') + $" [TPGFlyleafHost NP] ");
        Loaded += Host_Loaded;
        Unloaded += SimpleHost_Unloaded;
        DataContextChanged += Host_DataContextChanged;
        IsVisibleChanged += Host_IsVisibleChanged;

        // This event will fire when we swap the host between windows
        PresentationSource.AddSourceChangedHandler(this, OnSourceChanged);
    }

    #endregion

    #region Methods

    public virtual void SetPlayer(Player oldPlayer)
    {
        // De-assign old Player's Handle/TPGFlyleafHost
        if (oldPlayer != null)
        {
            Log.Debug($"De-assign Player #{oldPlayer.PlayerId}");

            oldPlayer.VideoDecoder.DestroySwapChain();
            oldPlayer.Host = null;
        }

        if (Player == null)
            return;

        Log.Prefix = ("[#" + UniqueId + "]").PadRight(8, ' ') + $" [TPGFlyleafHost #{Player.PlayerId}] ";

        // De-assign new Player's Handle/TPGFlyleafHost
        Player.Host?.Player_Disposed();

        if (Player == null) // We might just de-assign our Player
            return;

        // Assign new Player's (Handle/TPGFlyleafHost)
        Log.Debug($"Assign Player #{Player.PlayerId}");

        Player.Host = this;
        Player.Activity.Timeout = ActivityTimeout;

        if (_surface != null)
        {
            _surface.Background = new SolidColorBrush(Player.Config.Video.BackgroundColor);

            Player.VideoDecoder.CreateSwapChain(SurfaceHandle);
        }
    }
    private void CreateSurface()
    {
        if (_surface != null)
            return;

        // Required for some reason (WindowStyle.None will not be updated with our style)
        _surface = new();
        _surface.Name = $"Surface_{UniqueId}";
        _surface.Width = _surface.Height = 1; // Will be set on loaded
        _surface.WindowStyle = WindowStyle.None;
        _surface.ResizeMode = ResizeMode.NoResize;
        _surface.ShowInTaskbar = false;

        _surface.Background = Player != null ? new SolidColorBrush(Player.Config.Video.BackgroundColor) : Brushes.Black;

        // When using ItemsControl with ObservableCollection<Player> to fill DataTemplates with TPGFlyleafHost EnsureHandle will call Host_loaded
        Loaded -= Host_Loaded;
        SurfaceHandle = new WindowInteropHelper(_surface).EnsureHandle();
        Loaded += Host_Loaded;

        unchecked
        {
            // aka: Changed WS_CHILD to WS_POPUP because it made the main window flicker when style was not set to None and transparent (we cannot do this becuase it overlaps the taskbar)
            SetWindowLong(SurfaceHandle, (int)WindowLongFlags.GWL_STYLE, NONE_STYLE | (nint)WindowStyles.WS_POPUP);
        }
        SetWindowLong(SurfaceHandle, (int)WindowLongFlags.GWL_EXSTYLE, (nint)WindowStylesEx.WS_EX_LAYERED);

        Player?.VideoDecoder.CreateSwapChain(SurfaceHandle);

        _surface.Closed += Surface_Closed;
        _surface.KeyDown += Surface_KeyDown;
        _surface.KeyUp += Surface_KeyUp;
        _surface.Drop += Surface_Drop;
        _surface.DragEnter += Surface_DragEnter;
        _surface.SizeChanged += SetRectOverlay;

        SetMouseSurface();

        _surface.AllowDrop = true;

        SurfaceCreated?.Invoke(this, new());
    }
    private void CreateOverlay()
    {
        if (_overlay != null)
            return;

        _overlay = new Window() { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true };

        Loaded -= Host_Loaded;
        OverlayHandle = new WindowInteropHelper(_overlay).EnsureHandle();
        Loaded += Host_Loaded;

        _overlay.Resources = Resources;
        _overlay.DataContext = DataContext;

        SetWindowPos(OverlayHandle, nint.Zero, 0, 0, (int)_surface.ActualWidth, (int)_surface.ActualHeight,
                (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));

        _overlay.Name = $"Overlay_{UniqueId}";
        _overlay.Background = Brushes.Transparent;
        _overlay.ShowInTaskbar = false;
        _overlay.Owner = _surface;
        SetParent(OverlayHandle, SurfaceHandle);
        SetWindowLong(OverlayHandle, (int)WindowLongFlags.GWL_STYLE, NONE_STYLE | (nint)(WindowStyles.WS_CHILD | WindowStyles.WS_MAXIMIZE)); // TBR: WS_MAXIMIZE required? (possible better for DWM on fullscreen?)

        _overlay.KeyUp += Overlay_KeyUp;
        _overlay.KeyDown += Overlay_KeyDown;
        _overlay.Closed += Overlay_Closed;
        _overlay.Drop += Overlay_Drop;
        _overlay.DragEnter += Overlay_DragEnter;

        SetMouseOverlay();

        // Owner will close the overlay
        _overlay.KeyDown += (o, e) => { if (e.Key == Key.System && e.SystemKey == Key.F4) _surface?.Focus(); };

        _overlay.AllowDrop = true;

        _overlay.Template = OverlayTemplate;

        if (_surface.IsVisible)
            _overlay.Show();
        else if (!_overlay.IsVisible)
        {
            _overlay.Show();
            _overlay.Hide();
        }

        OverlayCreated?.Invoke(this, new());
    }
    private void SetMouseSurface()
    {
        if (_surface == null)
            return;

        if (!_isMouseBindingsSubscribedSurface)
        {
            _surface.LostMouseCapture += Surface_LostMouseCapture;
            _surface.MouseLeftButtonDown += Surface_MouseLeftButtonDown;
            _surface.MouseLeftButtonUp += Surface_MouseLeftButtonUp;
            _surface.MouseWheel += Surface_MouseWheel;
            _surface.MouseMove += Surface_MouseMove;
            _surface.MouseLeave += Surface_MouseLeave;
            _isMouseBindingsSubscribedSurface = true;
        }
        else if (_isMouseBindingsSubscribedSurface)
        {
            _surface.LostMouseCapture -= Surface_LostMouseCapture;
            _surface.MouseLeftButtonDown -= Surface_MouseLeftButtonDown;
            _surface.MouseLeftButtonUp -= Surface_MouseLeftButtonUp;
            _surface.MouseWheel -= Surface_MouseWheel;
            _surface.MouseMove -= Surface_MouseMove;
            _surface.MouseLeave -= Surface_MouseLeave;
            _isMouseBindingsSubscribedSurface = false;
        }
    }
    private void SetMouseOverlay()
    {
        if (_overlay == null)
            return;

        if (!_isMouseBindingsSubscribedOverlay)
        {
            _overlay.LostMouseCapture += Overlay_LostMouseCapture;
            _overlay.MouseLeftButtonDown += Overlay_MouseLeftButtonDown;
            _overlay.MouseLeftButtonUp += Overlay_MouseLeftButtonUp;
            _overlay.MouseWheel += Overlay_MouseWheel;
            _overlay.MouseMove += Overlay_MouseMove;
            _overlay.MouseLeave += Overlay_MouseLeave;
            _isMouseBindingsSubscribedOverlay = true;
        }
        else if (_isMouseBindingsSubscribedOverlay)
        {
            _overlay.LostMouseCapture -= Overlay_LostMouseCapture;
            _overlay.MouseLeftButtonDown -= Overlay_MouseLeftButtonDown;
            _overlay.MouseLeftButtonUp -= Overlay_MouseLeftButtonUp;
            _overlay.MouseWheel -= Overlay_MouseWheel;
            _overlay.MouseMove -= Overlay_MouseMove;
            _overlay.MouseLeave -= Overlay_MouseLeave;
            _isMouseBindingsSubscribedOverlay = false;
        }
    }

    private void Attach()
    {
        Window wasFocus = _overlay != null && _overlay.IsKeyboardFocusWithin ? _overlay : _surface;

        _surface.Topmost = false;
        _surface.MinWidth = MinWidth;
        _surface.MinHeight = MinHeight;
        _surface.MaxWidth = MaxWidth;
        _surface.MaxHeight = MaxHeight;

        unchecked
        {
            // aka: Changed WS_CHILD to WS_POPUP because it made the main window flicker when style was not set to None and transparent (we cannot do this becuase it overlaps the taskbar)
            SetWindowLong(SurfaceHandle, (int)WindowLongFlags.GWL_STYLE, NONE_STYLE | (nint)WindowStyles.WS_POPUP);
        }
        _surface.Owner = Owner;
        SetParent(SurfaceHandle, OwnerHandle);

        _rectInitLast = _rectIntersectLast = rectRandom;
        RecalcRect();
        Owner.Activate();
        wasFocus.Focus();
    }

    public void SetRect(ref Rect rect)
    {
        SetWindowPos(SurfaceHandle, nint.Zero, (int)Math.Round(rect.X * DpiX), (int)Math.Round(rect.Y * DpiY), (int)Math.Round(rect.Width * DpiX), (int)Math.Round(rect.Height * DpiY),
            (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));
    }

    private void SetRectOverlay(object sender, SizeChangedEventArgs e)
    {
        if (_overlay != null)
            SetWindowPos(OverlayHandle, nint.Zero, 0, 0, (int)Math.Round(_surface.ActualWidth * DpiX), (int)Math.Round(_surface.ActualHeight * DpiY),
                (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOACTIVATE));
    }

    public void ResetVisibleRect()
    {
        SetWindowRgn(SurfaceHandle, nint.Zero, true);
        if (_overlay != null)
            SetWindowRgn(OverlayHandle, nint.Zero, true);
    }
    public void SetVisibleRect(ref Rect rect)
    {
        SetWindowRgn(SurfaceHandle, CreateRectRgn((int)Math.Round(rect.X * DpiX), (int)Math.Round(rect.Y * DpiY), (int)Math.Round(rect.Right * DpiX), (int)Math.Round(rect.Bottom * DpiY)), true);
        if (_overlay != null)
            SetWindowRgn(OverlayHandle, CreateRectRgn((int)Math.Round(rect.X * DpiX), (int)Math.Round(rect.Y * DpiY), (int)Math.Round(rect.Right * DpiX), (int)Math.Round(rect.Bottom * DpiY)), true);
    }

    public Point? GetVideoFramePoint()
    {
        if (_surface is null || Player is null)
            return null;

        Point mousePos = Mouse.GetPosition(_surface);
        var viewport = Player.renderer.GetViewport;

        return new Point((mousePos.X - viewport.X) / viewport.Width, (mousePos.Y - viewport.Y) / viewport.Height);
    }

    public bool Player_CanHideCursor() => _surface != null && _surface.IsActive || _overlay != null && _overlay.IsActive;
    public bool Player_GetFullScreen() => false;
    public void Player_SetFullScreen(bool value)
    {
    }
    public void Player_Disposed() => UIInvokeIfRequired(() => Player = null);
    #endregion
}
