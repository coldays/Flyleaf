﻿using FlyleafLib;
using FlyleafLib.Controls;
using FlyleafLib.Controls.WPF;
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

namespace SimplePlayer;

public class SimpleHost : ContentControl, IHostPlayer, IDisposable
{
    #region Properties / Variables
    public Window Owner { get; private set; }
    public Window Surface { get; private set; }
    public IntPtr SurfaceHandle { get; private set; }
    public IntPtr OverlayHandle { get; private set; }
    public IntPtr OwnerHandle { get; private set; }
    public int ResizingSide { get; private set; }

    public int UniqueId { get; private set; }
    public bool Disposed { get; private set; }


    public event EventHandler SurfaceCreated;
    public event EventHandler OverlayCreated;
    public event DragEventHandler OnSurfaceDrop;
    public event DragEventHandler OnOverlayDrop;

    static bool isDesginMode;
    static int  idGenerator = 1;
    static nint NONE_STYLE = (nint) (WindowStyles.WS_MINIMIZEBOX | WindowStyles.WS_CLIPSIBLINGS | WindowStyles.WS_CLIPCHILDREN | WindowStyles.WS_VISIBLE); // WS_MINIMIZEBOX required for swapchain
    static Rect rectRandom = new(1, 2, 3, 4);

    float _curResizeRatio;
    float _curResizeRatioIfEnabled;
    bool _surfaceClosed, _surfaceClosing, _overlayClosed;
    int _panPrevX, _panPrevY;
    bool _isMouseBindingsSubscribedSurface;
    bool _isMouseBindingsSubscribedOverlay;

    CornerRadius _zeroCornerRadius = new(0);
    Point _zeroPoint = new(0, 0);
    Point _mouseLeftDownPoint = new(0, 0);
    Point _mouseMoveLastPoint = new(0, 0);
    Point _ownerZeroPointPos = new();

    Rect _zeroRect = new(0, 0, 0, 0);
    Rect _rectInit;
    Rect _rectInitLast = rectRandom;
    Rect _rectIntersect;
    Rect _rectIntersectLast = rectRandom;
    RECT _beforeResizeRect = new();
    RECT _curRect = new();
    private Window _overlay;

    protected readonly LogHandler Log;
    #endregion

    #region Dependency Properties
    public void BringToFront() => SetWindowPos(SurfaceHandle, IntPtr.Zero, 0, 0, 0, 0, (UInt32)(SetWindowPosFlags.SWP_SHOWWINDOW | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOMOVE));
    public bool BringToFrontOnClick
    {
        get { return (bool)GetValue(BringToFrontOnClickProperty); }
        set { SetValue(BringToFrontOnClickProperty, value); }
    }
    public static readonly DependencyProperty BringToFrontOnClickProperty =
    DependencyProperty.Register(nameof(BringToFrontOnClick), typeof(bool), typeof(SimpleHost), new PropertyMetadata(true));

    public AvailableWindows OpenOnDrop
    {
        get => (AvailableWindows)GetValue(OpenOnDropProperty);
        set => SetValue(OpenOnDropProperty, value);
    }
    public static readonly DependencyProperty OpenOnDropProperty =
        DependencyProperty.Register(nameof(OpenOnDrop), typeof(AvailableWindows), typeof(SimpleHost), new PropertyMetadata(AvailableWindows.Surface, new PropertyChangedCallback(DropChanged)));

    public AvailableWindows PanMoveOnCtrl
    {
        get => (AvailableWindows)GetValue(PanMoveOnCtrlProperty);
        set => SetValue(PanMoveOnCtrlProperty, value);
    }
    public static readonly DependencyProperty PanMoveOnCtrlProperty =
        DependencyProperty.Register(nameof(PanMoveOnCtrl), typeof(AvailableWindows), typeof(SimpleHost), new PropertyMetadata(AvailableWindows.Surface));

    public AvailableWindows PanRotateOnShiftWheel
    {
        get => (AvailableWindows)GetValue(PanRotateOnShiftWheelProperty);
        set => SetValue(PanRotateOnShiftWheelProperty, value);
    }
    public static readonly DependencyProperty PanRotateOnShiftWheelProperty =
        DependencyProperty.Register(nameof(PanRotateOnShiftWheel), typeof(AvailableWindows), typeof(SimpleHost), new PropertyMetadata(AvailableWindows.Surface));

    public AvailableWindows PanZoomOnCtrlWheel
    {
        get => (AvailableWindows)GetValue(PanZoomOnCtrlWheelProperty);
        set => SetValue(PanZoomOnCtrlWheelProperty, value);
    }
    public static readonly DependencyProperty PanZoomOnCtrlWheelProperty =
        DependencyProperty.Register(nameof(PanZoomOnCtrlWheel), typeof(AvailableWindows), typeof(SimpleHost), new PropertyMetadata(AvailableWindows.Surface));

    public bool KeepRatioOnResize
    {
        get => (bool)GetValue(KeepRatioOnResizeProperty);
        set => SetValue(KeepRatioOnResizeProperty, value);
    }
    public static readonly DependencyProperty KeepRatioOnResizeProperty =
        DependencyProperty.Register(nameof(KeepRatioOnResize), typeof(bool), typeof(SimpleHost), new PropertyMetadata(false, new PropertyChangedCallback(OnKeepRatioOnResizeChanged)));

    public int PreferredLandscapeWidth
    {
        get { return (int)GetValue(PreferredLandscapeWidthProperty); }
        set { SetValue(PreferredLandscapeWidthProperty, value); }
    }
    public static readonly DependencyProperty PreferredLandscapeWidthProperty =
    DependencyProperty.Register(nameof(PreferredLandscapeWidth), typeof(int), typeof(SimpleHost), new PropertyMetadata(0));

    public int PreferredPortraitHeight
    {
        get { return (int)GetValue(PreferredPortraitHeightProperty); }
        set { SetValue(PreferredPortraitHeightProperty, value); }
    }
    public static readonly DependencyProperty PreferredPortraitHeightProperty =
    DependencyProperty.Register(nameof(PreferredPortraitHeight), typeof(int), typeof(SimpleHost), new PropertyMetadata(0));

    public AvailableWindows KeyBindings
    {
        get => (AvailableWindows)GetValue(KeyBindingsProperty);
        set => SetValue(KeyBindingsProperty, value);
    }
    public static readonly DependencyProperty KeyBindingsProperty =
        DependencyProperty.Register(nameof(KeyBindings), typeof(AvailableWindows), typeof(SimpleHost), new PropertyMetadata(AvailableWindows.Surface));

    public AvailableWindows MouseBindings
    {
        get => (AvailableWindows)GetValue(MouseBindingsProperty);
        set => SetValue(MouseBindingsProperty, value);
    }
    public static readonly DependencyProperty MouseBindingsProperty =
        DependencyProperty.Register(nameof(MouseBindings), typeof(AvailableWindows), typeof(SimpleHost), new PropertyMetadata(AvailableWindows.Both, new PropertyChangedCallback(OnMouseBindings)));

    public int ActivityTimeout
    {
        get => (int)GetValue(ActivityTimeoutProperty);
        set => SetValue(ActivityTimeoutProperty, value);
    }
    public static readonly DependencyProperty ActivityTimeoutProperty =
        DependencyProperty.Register(nameof(ActivityTimeout), typeof(int), typeof(SimpleHost), new PropertyMetadata(0, new PropertyChangedCallback(OnActivityTimeoutChanged)));

    public bool IsResizing
    {
        get => (bool)GetValue(IsResizingProperty);
        private set => SetValue(IsResizingProperty, value);
    }
    public static readonly DependencyProperty IsResizingProperty =
        DependencyProperty.Register(nameof(IsResizing), typeof(bool), typeof(SimpleHost), new PropertyMetadata(false));

    public bool IsPanMoving
    {
        get { return (bool)GetValue(IsPanMovingProperty); }
        private set { SetValue(IsPanMovingProperty, value); }
    }
    public static readonly DependencyProperty IsPanMovingProperty =
        DependencyProperty.Register(nameof(IsPanMoving), typeof(bool), typeof(SimpleHost), new PropertyMetadata(false));

    public bool IsDragMoving
    {
        get { return (bool)GetValue(IsDragMovingProperty); }
        set { SetValue(IsDragMovingProperty, value); }
    }
    public static readonly DependencyProperty IsDragMovingProperty =
        DependencyProperty.Register(nameof(IsDragMoving), typeof(bool), typeof(SimpleHost), new PropertyMetadata(false));

    public FrameworkElement MarginTarget
    {
        get => (FrameworkElement)GetValue(MarginTargetProperty);
        set => SetValue(MarginTargetProperty, value);
    }
    public static readonly DependencyProperty MarginTargetProperty =
        DependencyProperty.Register(nameof(MarginTarget), typeof(FrameworkElement), typeof(SimpleHost), new PropertyMetadata(null));

    public Player Player
    {
        get => (Player)GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }
    public static readonly DependencyProperty PlayerProperty =
        DependencyProperty.Register(nameof(Player), typeof(Player), typeof(SimpleHost), new PropertyMetadata(null, OnPlayerChanged));

    public Player ReplicaPlayer
    {
        get => (Player)GetValue(ReplicaPlayerProperty);
        set => SetValue(ReplicaPlayerProperty, value);
    }
    public static readonly DependencyProperty ReplicaPlayerProperty =
        DependencyProperty.Register(nameof(ReplicaPlayer), typeof(Player), typeof(SimpleHost), new PropertyMetadata(null, OnReplicaPlayerChanged));

    public ControlTemplate OverlayTemplate
    {
        get => (ControlTemplate)GetValue(OverlayTemplateProperty);
        set => SetValue(OverlayTemplateProperty, value);
    }
    public static readonly DependencyProperty OverlayTemplateProperty =
        DependencyProperty.Register(nameof(OverlayTemplate), typeof(ControlTemplate), typeof(SimpleHost), new PropertyMetadata(null, new PropertyChangedCallback(OnOverlayTemplateChanged)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(SimpleHost), new PropertyMetadata(new CornerRadius(0), new PropertyChangedCallback(OnCornerRadiusChanged)));
    #endregion

    #region Events
    private static void OnMouseBindings(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (isDesginMode)
            return;

        SimpleHost host = d as SimpleHost;
        if (host.Disposed)
            return;

        host.SetMouseSurface();
        host.SetMouseOverlay();
    }
    private static void DropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SimpleHost host = d as SimpleHost;
        if (host.Disposed)
            return;

        if (host.Surface == null)
            return;

        host.Surface.AllowDrop =
            host.OpenOnDrop == AvailableWindows.Surface || host.OpenOnDrop == AvailableWindows.Both;

        if (host._overlay == null)
            return;

        host._overlay.AllowDrop =
            host.OpenOnDrop == AvailableWindows.Overlay || host.OpenOnDrop == AvailableWindows.Both;
    }
    private void UpdateCurRatio()
    {
        if (!KeepRatioOnResize)
            return;

        if (Player != null && Player.Video.AspectRatio.Value > 0)
            _curResizeRatio = Player.Video.AspectRatio.Value;
        else if (ReplicaPlayer != null && ReplicaPlayer.Video.AspectRatio.Value > 0)
            _curResizeRatio = ReplicaPlayer.Video.AspectRatio.Value;
        else
            _curResizeRatio = (float)(16.0 / 9.0);

        _curResizeRatioIfEnabled = _curResizeRatio;

        Rect screen;

        if (Owner == null)
        {
            Height = ActualWidth / _curResizeRatio;
            return;
        }

        screen = new(_zeroPoint, Owner.RenderSize);

        double windowWidth;
        double windowHeight;

        if (_curResizeRatio >= 1)
        {
            windowHeight = PreferredLandscapeWidth / _curResizeRatio;

            if (windowHeight < Surface.MinHeight)
            {
                windowHeight = Surface.MinHeight;
                windowWidth = windowHeight * _curResizeRatio;
            }
            else if (windowHeight > Surface.MaxHeight)
            {
                windowHeight = Surface.MaxHeight;
                windowWidth = Surface.Height * _curResizeRatio;
            }
            else if (windowHeight > screen.Height)
            {
                windowHeight = screen.Height;
                windowWidth = windowHeight * _curResizeRatio;
            }
            else
                windowWidth = PreferredLandscapeWidth;
        }
        else
        {
            windowWidth = PreferredPortraitHeight * _curResizeRatio;

            if (windowWidth < Surface.MinWidth)
            {
                windowWidth = Surface.MinWidth;
                windowHeight = windowWidth / _curResizeRatio;
            }
            else if (windowWidth > Surface.MaxWidth)
            {
                windowWidth = Surface.MaxWidth;
                windowHeight = windowWidth / _curResizeRatio;
            }
            else if (windowWidth > screen.Width)
            {
                windowWidth = screen.Width;
                windowHeight = windowWidth / _curResizeRatio;
            }
            else
                windowHeight = PreferredPortraitHeight;
        }

        Height = windowHeight;
        Width = windowWidth;
    }
    private static void OnKeepRatioOnResizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (isDesginMode)
            return;

        SimpleHost host = d as SimpleHost;
        if (host.Disposed)
            return;

        if (!host.KeepRatioOnResize)
            host._curResizeRatioIfEnabled = 0;
        else
            host.UpdateCurRatio();
    }
    private static void OnPlayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (isDesginMode)
            return;

        SimpleHost host = d as SimpleHost;
        if (host.Disposed)
            return;

        host.SetPlayer((Player)e.OldValue);
    }
    private static void OnReplicaPlayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (isDesginMode)
            return;

        SimpleHost host = d as SimpleHost;
        if (host.Disposed)
            return;

        host.SetReplicaPlayer((Player)e.OldValue);
    }
    private static void OnActivityTimeoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SimpleHost host = d as SimpleHost;
        if (host.Disposed)
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
        if (host.Disposed)
            return;

        if (host._overlay == null)
        {
            host._overlay = new Window() { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true };
        }
        else
        {
            host._overlay.Template = host.OverlayTemplate;
        }
        host.SetOverlay();
    }
    private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (isDesginMode)
            return;

        SimpleHost host = d as SimpleHost;
        if (host.Disposed)
            return;

        if (host.Surface == null)
            return;

        if (host.CornerRadius == host._zeroCornerRadius)
            host.Surface.Background = Brushes.Black;
        else
        {
            host.Surface.Background = Brushes.Transparent;
            host.SetCornerRadiusBorder();
        }

        if (host?.Player == null)
            return;

        host.Player.renderer.CornerRadius = (CornerRadius)e.NewValue;

    }
    private void SetCornerRadiusBorder()
    {
        // Required to handle mouse events as the window's background will be transparent
        // This does not set the background color we do that with the renderer (which causes some issues eg. when returning from fullscreen to normalscreen)
        Surface.Content = new Border()
        {
            Background = Brushes.Black, // TBR: for alpha channel -> Background == Brushes.Transparent || Background ==null ? new SolidColorBrush(Color.FromArgb(1,0,0,0)) : Background
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            CornerRadius = CornerRadius,
        };
    }
    private static object OnContentChanging(DependencyObject d, object baseValue)
    {
        if (isDesginMode)
            return baseValue;

        SimpleHost host = d as SimpleHost;
        if (host.Disposed)
            return host._overlay;

        if (baseValue != null && host._overlay == null)
            host._overlay = new Window() { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true };

        if (host._overlay != null)
            host._overlay.Content = baseValue;

        return host._overlay;
    }

    private void Host_Loaded(object sender, RoutedEventArgs e)
    {
        Window owner = Window.GetWindow(this);
        if (owner == null)
            return;

        var ownerHandle = new WindowInteropHelper(owner).EnsureHandle();

        // Owner Changed
        if (Owner != null)
        {
            if (OwnerHandle == ownerHandle)
                return; // Check OwnerHandle changed (NOTE: Owner can be the same class/window but the handle can be different)

            Owner.SizeChanged -= Owner_SizeChanged;

            Surface.Hide();
            _overlay?.Hide();
            // TODO?
            //Detach();

            Owner = owner;
            OwnerHandle = ownerHandle;
            Surface.Title = Owner.Title;
            Surface.Icon = Owner.Icon;

            Owner.SizeChanged += Owner_SizeChanged;
            Attach();
            Host_IsVisibleChanged(null, new());

            return;
        }

        Owner = owner;
        OwnerHandle = ownerHandle;
        _overlay.DataContext = DataContext;

        SetSurface();

        Surface.Title = Owner.Title;
        Surface.Icon = Owner.Icon;

        Owner.SizeChanged += Owner_SizeChanged;
        DataContextChanged += Host_DataContextChanged;
        IsVisibleChanged += Host_IsVisibleChanged;

        Attach();
        Surface.Show();
        _overlay?.Show();
        Host_IsVisibleChanged(null, new());
    }

    // WindowChrome Issue #410: It will not properly move child windows when resized from top or left
    private void Owner_SizeChanged(object sender, SizeChangedEventArgs e)
        => _rectInitLast = Rect.Empty;

    private void Host_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // TBR
        // 1. this.DataContext: TPGFlyleafHost's DataContext will not be affected (Inheritance)
        // 2. Overlay.DataContext: Overlay's DataContext will be TPGFlyleafHost itself
        // 3. Overlay.DataContext.HostDataContext: TPGFlyleafHost's DataContext includes HostDataContext to access TPGFlyleafHost's DataContext
        // 4. In case of Stand Alone will let the user to decide
        _overlay.DataContext = DataContext;
    }

    private void Host_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            Host_Loaded(null, null);
            Surface.Show();

            if (_overlay != null)
            {
                _overlay.Show();

                // It happens (eg. with MetroWindow) that overlay left will not be equal to surface left so we reset it by detach/attach the overlay to surface (https://github.com/SuRGeoNix/Flyleaf/issues/370)
                RECT surfRect = new();
                RECT overRect = new();
                GetWindowRect(SurfaceHandle, ref surfRect);
                GetWindowRect(OverlayHandle, ref overRect);

                if (surfRect.Left != overRect.Left)
                {
                    // Detach Overlay
                    SetParent(OverlayHandle, IntPtr.Zero);
                    SetWindowLong(OverlayHandle, (int)WindowLongFlags.GWL_STYLE, NONE_STYLE);
                    _overlay.Owner = null;

                    SetWindowPos(OverlayHandle, IntPtr.Zero, 0, 0, (int)Surface.ActualWidth, (int)Surface.ActualHeight,
                        (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));

                    // Attache Overlay
                    SetWindowLong(OverlayHandle, (int)WindowLongFlags.GWL_STYLE, NONE_STYLE | (nint)(WindowStyles.WS_CHILD | WindowStyles.WS_MAXIMIZE));
                    _overlay.Owner = Surface;
                    SetParent(OverlayHandle, SurfaceHandle);

                    // Required to restore overlay
                    Rect tt1 = new(0, 0, 0, 0);
                    SetRect(ref tt1);
                }
            }

            // TBR: First time loaded in a tab control could cause UCEERR_RENDERTHREADFAILURE (can be avoided by hide/show again here)
        }
        else
        {
            Surface.Hide();
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

        if (!IsVisible || IsResizing)
            return;

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
    private void Player_Video_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (KeepRatioOnResize && e.PropertyName == nameof(Player.Video.AspectRatio) && Player.Video.AspectRatio.Value > 0)
            UpdateCurRatio();
    }
    private void ReplicaPlayer_Video_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (KeepRatioOnResize && e.PropertyName == nameof(ReplicaPlayer.Video.AspectRatio) && ReplicaPlayer.Video.AspectRatio.Value > 0)
            UpdateCurRatio();
    }
    #endregion

    #region Events Surface / Overlay
    private void Surface_KeyDown(object sender, KeyEventArgs e) { if (KeyBindings == AvailableWindows.Surface || KeyBindings == AvailableWindows.Both) e.Handled = Player.KeyDown(Player, e); }
    private void Overlay_KeyDown(object sender, KeyEventArgs e) { if (KeyBindings == AvailableWindows.Overlay || KeyBindings == AvailableWindows.Both) e.Handled = Player.KeyDown(Player, e); }

    private void Surface_KeyUp(object sender, KeyEventArgs e) { if (KeyBindings == AvailableWindows.Surface || KeyBindings == AvailableWindows.Both) e.Handled = Player.KeyUp(Player, e); }
    private void Overlay_KeyUp(object sender, KeyEventArgs e) { if (KeyBindings == AvailableWindows.Overlay || KeyBindings == AvailableWindows.Both) e.Handled = Player.KeyUp(Player, e); }

    private void Surface_Drop(object sender, DragEventArgs e)
    {
        Surface.ReleaseMouseCapture();

        if (Player == null)
            return;

        // Invoke event first and see if it gets handled
        OnSurfaceDrop?.Invoke(this, e);

        if (!e.Handled)
        {
            // Player Open File
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string filename = ((string[])e.Data.GetData(DataFormats.FileDrop, false))[0];
                Player.OpenAsync(filename);
            }

            // Player Open Text
            else if (e.Data.GetDataPresent(DataFormats.Text))
            {
                string text = e.Data.GetData(DataFormats.Text, false).ToString();
                if (text.Length > 0)
                    Player.OpenAsync(text);
            }
        }

        Surface.Activate();
    }
    private void Overlay_Drop(object sender, DragEventArgs e)
    {
        _overlay.ReleaseMouseCapture();

        if (Player == null)
            return;

        // Invoke event first and see if it gets handled
        OnOverlayDrop?.Invoke(this, e);

        if (!e.Handled)
        {
            // Player Open File
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string filename = ((string[])e.Data.GetData(DataFormats.FileDrop, false))[0];
                Player.OpenAsync(filename);
            }

            // Player Open Text
            else if (e.Data.GetDataPresent(DataFormats.Text))
            {
                string text = e.Data.GetData(DataFormats.Text, false).ToString();
                if (text.Length > 0)
                    Player.OpenAsync(text);
            }
        }

        _overlay.Activate();
    }
    private void Surface_DragEnter(object sender, DragEventArgs e) { if (Player != null) e.Effects = DragDropEffects.All; }
    private void Overlay_DragEnter(object sender, DragEventArgs e) { if (Player != null) e.Effects = DragDropEffects.All; }

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SO_MouseLeftButtonDown(e, Surface);
    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => SO_MouseLeftButtonDown(e, _overlay);
    private void SO_MouseLeftButtonDown(MouseButtonEventArgs e, Window window)
    {
        AvailableWindows availWindow;
        AttachedDragMoveOptions availDragMove;
        AttachedDragMoveOptions availDragMoveOwner;

        if (window == Surface)
        {
            availWindow = AvailableWindows.Surface;
            availDragMove = AttachedDragMoveOptions.Surface;
            availDragMoveOwner = AttachedDragMoveOptions.SurfaceOwner;
        }
        else
        {
            availWindow = AvailableWindows.Overlay;
            availDragMove = AttachedDragMoveOptions.Overlay;
            availDragMoveOwner = AttachedDragMoveOptions.OverlayOwner;
        }

        if (BringToFrontOnClick) // Activate and Z-order top
            BringToFront();

        window.Focus();
        Player?.Activity.RefreshFullActive();

        _mouseLeftDownPoint = e.GetPosition(window);

        // Resize
        if (ResizingSide != 0)
        {
            IsResizing = true;

            _ownerZeroPointPos = Owner.PointToScreen(_zeroPoint);
            GetWindowRect(SurfaceHandle, ref _beforeResizeRect);
            ResetVisibleRect();
        }
        // PanMove
        else if (Player != null &&
            (PanMoveOnCtrl == availWindow || PanMoveOnCtrl == AvailableWindows.Both) &&
            (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
        {
            _panPrevX = Player.PanXOffset;
            _panPrevY = Player.PanYOffset;
            IsPanMoving = true;
        }
        else
            return; // No Capture

        window.CaptureMouse();
    }

    private void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Surface_ReleaseCapture();
    private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Overlay_ReleaseCapture();
    private void Surface_LostMouseCapture(object sender, MouseEventArgs e) => Surface_ReleaseCapture();
    private void Overlay_LostMouseCapture(object sender, MouseEventArgs e) => Overlay_ReleaseCapture();
    private void Surface_ReleaseCapture()
    {
        if (!IsResizing && !IsPanMoving && !IsDragMoving)
            return;

        Surface.ReleaseMouseCapture();

        if (IsResizing)
        {
            ResizingSide = 0;
            Surface.Cursor = Cursors.Arrow;
            IsResizing = false;

            GetWindowRect(SurfaceHandle, ref _curRect);
            MarginTarget.Margin = new(MarginTarget.Margin.Left + (_curRect.Left - _beforeResizeRect.Left) / DpiX, MarginTarget.Margin.Top + (_curRect.Top - _beforeResizeRect.Top) / DpiY, MarginTarget.Margin.Right, MarginTarget.Margin.Bottom);
            Width = Surface.Width;
            Height = Surface.Height;
            RecalcRect();
            //Host_LayoutUpdated(null, null); // When attached to restore the clipped rect
        }
        else if (IsPanMoving)
            IsPanMoving = false;
        else if (IsDragMoving)
            IsDragMoving = false;
        else
            return;
    }
    private void Overlay_ReleaseCapture()
    {
        if (!IsResizing && !IsPanMoving && !IsDragMoving)
            return;

        _overlay.ReleaseMouseCapture();

        if (IsResizing)
        {
            ResizingSide = 0;
            _overlay.Cursor = Cursors.Arrow;
            IsResizing = false;

            GetWindowRect(SurfaceHandle, ref _curRect);
            MarginTarget.Margin = new(MarginTarget.Margin.Left + (_curRect.Left - _beforeResizeRect.Left) / DpiX, MarginTarget.Margin.Top + (_curRect.Top - _beforeResizeRect.Top) / DpiY, MarginTarget.Margin.Right, MarginTarget.Margin.Bottom);
            Width = Surface.Width;
            Height = Surface.Height;
            RecalcRect();
            //Host_LayoutUpdated(null, null); // When attached to restore the clipped rect
        }
        else if (IsPanMoving)
            IsPanMoving = false;
        else if (IsDragMoving)
            IsDragMoving = false;
    }

    private void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        var cur = e.GetPosition(Surface);

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
            //Player.PanXOffset = _panPrevX + (int)(cur.X - _mouseLeftDownPoint.X);
            //Player.PanYOffset = _panPrevY + (int)(cur.Y - _mouseLeftDownPoint.Y);
            // Only change viewport once
            Player.renderer.SetPanXY(
                _panPrevX + (int)(cur.X - _mouseLeftDownPoint.X),
                _panPrevY + (int)(cur.Y - _mouseLeftDownPoint.Y)
            );

            return;
        }

        // Resize (MouseDown + ResizeSide != 0)
        if (IsResizing)
            Resize(cur, ResizingSide, _curResizeRatioIfEnabled);

        // Drag Move Self (Attached|Detached)
        else if (IsDragMoving)
        {
            MarginTarget.Margin = new(
                    MarginTarget.Margin.Left + cur.X - _mouseLeftDownPoint.X,
                    MarginTarget.Margin.Top + cur.Y - _mouseLeftDownPoint.Y,
                    MarginTarget.Margin.Right,
                    MarginTarget.Margin.Bottom);
        }
    }

    private void Surface_MouseLeave(object sender, MouseEventArgs e) { ResizingSide = 0; Surface.Cursor = Cursors.Arrow; }
    private void Overlay_MouseLeave(object sender, MouseEventArgs e) { ResizingSide = 0; _overlay.Cursor = Cursors.Arrow; }

    private void Surface_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Player == null || e.Delta == 0)
            return;

        if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
            (PanZoomOnCtrlWheel == AvailableWindows.Surface || PanZoomOnCtrlWheel == AvailableWindows.Both))
        {
            var cur = e.GetPosition(Surface);
            Point curDpi = new(cur.X * DpiX, cur.Y * DpiY);
            if (e.Delta > 0)
                Player.ZoomIn(curDpi);
            else
                Player.ZoomOut(curDpi);
        }
        else if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) &&
            (PanRotateOnShiftWheel == AvailableWindows.Surface || PanZoomOnCtrlWheel == AvailableWindows.Both))
        {
            if (e.Delta > 0)
                Player.RotateRight();
            else
                Player.RotateLeft();
        }
    }
    private void Overlay_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Player == null || e.Delta == 0)
            return;

        if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
            (PanZoomOnCtrlWheel == AvailableWindows.Overlay || PanZoomOnCtrlWheel == AvailableWindows.Both))
        {
            var cur = e.GetPosition(_overlay);
            Point curDpi = new(cur.X * DpiX, cur.Y * DpiY);
            if (e.Delta > 0)
                Player.ZoomIn(curDpi);
            else
                Player.ZoomOut(curDpi);
        }
        else if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) &&
            (PanRotateOnShiftWheel == AvailableWindows.Overlay || PanZoomOnCtrlWheel == AvailableWindows.Both))
        {
            if (e.Delta > 0)
                Player.RotateRight();
            else
                Player.RotateLeft();
        }
    }

    private void Surface_Closed(object sender, EventArgs e)
    {
        _surfaceClosed = true;
        Dispose();
    }
    private void Surface_Closing(object sender, CancelEventArgs e) => _surfaceClosing = true;
    private void Overlay_Closed(object sender, EventArgs e)
    {
        _overlayClosed = true;
        if (!_surfaceClosing)
            Surface?.Close();
    }
    private void OverlayStandAlone_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Surface should be visible first (this happens only on initialization of standalone)

        if (Surface.IsVisible)
            return;

        if (_overlay.IsVisible)
        {
            Surface.Show();
            ShowWindow(OverlayHandle, (int)ShowWindowCommands.SW_SHOWMINIMIZED);
            ShowWindow(OverlayHandle, (int)ShowWindowCommands.SW_SHOWMAXIMIZED);
        }
    }

    public void Resize(Point p, int resizingSide, double ratio = 0.0)
    {
        double windowWidth  = Surface.ActualWidth;
        double windowHeight = Surface.ActualHeight;
        double windowLeft;
        double windowTop;

        GetWindowRect(SurfaceHandle, ref _curRect);
        windowLeft = (_curRect.Left - _ownerZeroPointPos.X) / DpiX;
        windowTop = (_curRect.Top - _ownerZeroPointPos.Y) / DpiY;

        if (resizingSide == 2 || resizingSide == 3 || resizingSide == 6)
        {
            p.X += 5;

            windowWidth = p.X > Surface.MinWidth ?
                p.X < Surface.MaxWidth ? p.X : Surface.MaxWidth :
                Surface.MinWidth;
        }
        else if (resizingSide == 1 || resizingSide == 4 || resizingSide == 5)
        {
            p.X -= 5;
            double temp = Surface.ActualWidth - p.X;
            if (temp > Surface.MinWidth && temp < Surface.MaxWidth)
            {
                windowWidth = temp;
                windowLeft = windowLeft + p.X;
            }
        }

        if (resizingSide == 2 || resizingSide == 4 || resizingSide == 8)
        {
            p.Y += 5;

            if (p.Y > Surface.MinHeight)
            {
                windowHeight = p.Y < Surface.MaxHeight ? p.Y : Surface.MaxHeight;
            }
            else
                return;
        }
        else if (resizingSide == 1 || resizingSide == 3 || resizingSide == 7)
        {
            if (ratio != 0 && resizingSide != 7)
            {
                double temp = windowWidth / ratio;
                if (temp > Surface.MinHeight && temp < Surface.MaxHeight)
                    windowTop += Surface.ActualHeight - temp;
                else
                    return;
            }
            else
            {
                p.Y -= 5;
                double temp = Surface.ActualHeight - p.Y;
                if (temp > Surface.MinHeight && temp < Surface.MaxHeight)
                {
                    windowHeight = temp;
                    windowTop += p.Y;
                }
                else
                    return;
            }
        }

        if (ratio != 0)
        {
            if (resizingSide == 7 || resizingSide == 8)
                windowWidth = windowHeight * ratio;
            else
                windowHeight = windowWidth / ratio;
        }

        if (windowWidth >= windowHeight)
            PreferredLandscapeWidth = (int)windowWidth;
        else
            PreferredPortraitHeight = (int)windowHeight;

        windowLeft *= DpiX;
        windowTop *= DpiY;
        windowWidth *= DpiX;
        windowHeight *= DpiY;

        SetWindowPos(SurfaceHandle, IntPtr.Zero,
            (int)windowLeft,
            (int)windowTop,
            (int)Math.Ceiling(windowWidth),
            (int)Math.Ceiling(windowHeight),
            (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));
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

        MarginTarget = this;
        Log = new LogHandler(("[#" + UniqueId + "]").PadRight(8, ' ') + $" [TPGFlyleafHost NP] ");
        Loaded += Host_Loaded;
    }
    #endregion

    #region Methods
    public virtual void SetReplicaPlayer(Player oldPlayer)
    {
        if (oldPlayer != null)
        {
            oldPlayer.renderer.SetChildHandle(IntPtr.Zero);
            oldPlayer.Video.PropertyChanged -= ReplicaPlayer_Video_PropertyChanged;
        }

        if (ReplicaPlayer == null)
            return;

        if (Surface != null)
            ReplicaPlayer.renderer.SetChildHandle(SurfaceHandle);

        ReplicaPlayer.Video.PropertyChanged += ReplicaPlayer_Video_PropertyChanged;
    }
    public virtual void SetPlayer(Player oldPlayer)
    {
        // De-assign old Player's Handle/TPGFlyleafHost
        if (oldPlayer != null)
        {
            Log.Debug($"De-assign Player #{oldPlayer.PlayerId}");

            oldPlayer.Video.PropertyChanged -= Player_Video_PropertyChanged;
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
        if (Player.renderer != null) // TBR: using as AudioOnly with a Control*
            Player.renderer.CornerRadius = CornerRadius;

        if (Surface != null)
        {
            if (CornerRadius == _zeroCornerRadius)
                Surface.Background = new SolidColorBrush(Player.Config.Video.BackgroundColor);
            //else // TBR: this border probably not required? only when we don't have a renderer?
            //((Border)Surface.Content).Background = new SolidColorBrush(Player.Config.Video.BackgroundColor);

            Player.VideoDecoder.CreateSwapChain(SurfaceHandle);
        }

        Player.Video.PropertyChanged += Player_Video_PropertyChanged;
        UpdateCurRatio();
    }
    public virtual void SetSurface(bool fromSetOverlay = false)
    {
        if (Surface != null)
            return;

        // Required for some reason (WindowStyle.None will not be updated with our style)
        Surface = new();
        Surface.Name = $"Surface_{UniqueId}";
        Surface.Width = Surface.Height = 1; // Will be set on loaded
        Surface.WindowStyle = WindowStyle.None;
        Surface.ResizeMode = ResizeMode.NoResize;
        Surface.ShowInTaskbar = false;

        // CornerRadius must be set initially to AllowsTransparency! 
        if (CornerRadius == _zeroCornerRadius)
            Surface.Background = Player != null ? new SolidColorBrush(Player.Config.Video.BackgroundColor) : Brushes.Black;
        else
        {
            Surface.AllowsTransparency = true;
            Surface.Background = Brushes.Transparent;
            SetCornerRadiusBorder();
        }

        // When using ItemsControl with ObservableCollection<Player> to fill DataTemplates with TPGFlyleafHost EnsureHandle will call Host_loaded
        Loaded -= Host_Loaded;
        SurfaceHandle = new WindowInteropHelper(Surface).EnsureHandle();
        Loaded += Host_Loaded;

        SetWindowLong(SurfaceHandle, (int)WindowLongFlags.GWL_STYLE, NONE_STYLE | (nint)WindowStyles.WS_CHILD);
        SetWindowLong(SurfaceHandle, (int)WindowLongFlags.GWL_EXSTYLE, (nint)WindowStylesEx.WS_EX_LAYERED);

        Player?.VideoDecoder.CreateSwapChain(SurfaceHandle);

        ReplicaPlayer?.renderer.SetChildHandle(SurfaceHandle);

        Surface.Closed += Surface_Closed;
        Surface.Closing += Surface_Closing;
        Surface.KeyDown += Surface_KeyDown;
        Surface.KeyUp += Surface_KeyUp;
        Surface.Drop += Surface_Drop;
        Surface.DragEnter += Surface_DragEnter;
        Surface.SizeChanged += SetRectOverlay;

        SetMouseSurface();

        Surface.AllowDrop =
            OpenOnDrop == AvailableWindows.Surface || OpenOnDrop == AvailableWindows.Both;

        if (IsLoaded && Owner == null && !fromSetOverlay)
            Host_Loaded(null, null);

        SurfaceCreated?.Invoke(this, new());
    }
    public virtual void SetOverlay()
    {
        if (_overlay == null)
            return;

        SetSurface(true);

        Loaded -= Host_Loaded;
        OverlayHandle = new WindowInteropHelper(_overlay).EnsureHandle();
        Loaded += Host_Loaded;

        _overlay.Resources = Resources;
        _overlay.DataContext = DataContext;

        SetWindowPos(OverlayHandle, IntPtr.Zero, 0, 0, (int)Surface.ActualWidth, (int)Surface.ActualHeight,
                (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));

        _overlay.Name = $"Overlay_{UniqueId}";
        _overlay.Background = Brushes.Transparent;
        _overlay.ShowInTaskbar = false;
        _overlay.Owner = Surface;
        SetParent(OverlayHandle, SurfaceHandle);
        SetWindowLong(OverlayHandle, (int)WindowLongFlags.GWL_STYLE, NONE_STYLE | (nint)(WindowStyles.WS_CHILD | WindowStyles.WS_MAXIMIZE)); // TBR: WS_MAXIMIZE required? (possible better for DWM on fullscreen?)

        _overlay.KeyUp += Overlay_KeyUp;
        _overlay.KeyDown += Overlay_KeyDown;
        _overlay.Closed += Overlay_Closed;
        _overlay.Drop += Overlay_Drop;
        _overlay.DragEnter += Overlay_DragEnter;

        SetMouseOverlay();

        // Owner will close the overlay
        _overlay.KeyDown += (o, e) => { if (e.Key == Key.System && e.SystemKey == Key.F4) Surface?.Focus(); };

        _overlay.AllowDrop =
            OpenOnDrop == AvailableWindows.Overlay || OpenOnDrop == AvailableWindows.Both;

        _overlay.Template = OverlayTemplate;

        if (Surface.IsVisible)
            _overlay.Show();
        else if (!_overlay.IsVisible)
        {
            _overlay.Show();
            _overlay.Hide();
        }

        if (IsLoaded && Owner == null)
            Host_Loaded(null, null);

        OverlayCreated?.Invoke(this, new());
    }
    private void SetMouseSurface()
    {
        if (Surface == null)
            return;

        if ((MouseBindings == AvailableWindows.Surface || MouseBindings == AvailableWindows.Both) && !_isMouseBindingsSubscribedSurface)
        {
            Surface.LostMouseCapture += Surface_LostMouseCapture;
            Surface.MouseLeftButtonDown += Surface_MouseLeftButtonDown;
            Surface.MouseLeftButtonUp += Surface_MouseLeftButtonUp;
            Surface.MouseWheel += Surface_MouseWheel;
            Surface.MouseMove += Surface_MouseMove;
            Surface.MouseLeave += Surface_MouseLeave;
            _isMouseBindingsSubscribedSurface = true;
        }
        else if (_isMouseBindingsSubscribedSurface)
        {
            Surface.LostMouseCapture -= Surface_LostMouseCapture;
            Surface.MouseLeftButtonDown -= Surface_MouseLeftButtonDown;
            Surface.MouseLeftButtonUp -= Surface_MouseLeftButtonUp;
            Surface.MouseWheel -= Surface_MouseWheel;
            Surface.MouseMove -= Surface_MouseMove;
            Surface.MouseLeave -= Surface_MouseLeave;
            _isMouseBindingsSubscribedSurface = false;
        }
    }
    private void SetMouseOverlay()
    {
        if (_overlay == null)
            return;

        if ((MouseBindings == AvailableWindows.Overlay || MouseBindings == AvailableWindows.Both) && !_isMouseBindingsSubscribedOverlay)
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

    public virtual void Attach()
    {
        Window wasFocus = _overlay != null && _overlay.IsKeyboardFocusWithin ? _overlay : Surface;

        Surface.Topmost = false;
        Surface.MinWidth = MinWidth;
        Surface.MinHeight = MinHeight;
        Surface.MaxWidth = MaxWidth;
        Surface.MaxHeight = MaxHeight;

        SetWindowLong(SurfaceHandle, (int)WindowLongFlags.GWL_STYLE, NONE_STYLE | (nint)WindowStyles.WS_CHILD);
        Surface.Owner = Owner;
        SetParent(SurfaceHandle, OwnerHandle);

        _rectInitLast = _rectIntersectLast = rectRandom;
        RecalcRect();
        Owner.Activate();
        wasFocus.Focus();
    }

    public void SetRect(ref Rect rect)
        => SetWindowPos(SurfaceHandle, IntPtr.Zero, (int)Math.Round(rect.X * DpiX), (int)Math.Round(rect.Y * DpiY), (int)Math.Round(rect.Width * DpiX), (int)Math.Round(rect.Height * DpiY),
            (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOACTIVATE));

    private void SetRectOverlay(object sender, SizeChangedEventArgs e)
    {
        if (_overlay != null)
            SetWindowPos(OverlayHandle, IntPtr.Zero, 0, 0, (int)Math.Round(Surface.ActualWidth * DpiX), (int)Math.Round(Surface.ActualHeight * DpiY),
                (uint)(SetWindowPosFlags.SWP_NOZORDER | SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOACTIVATE));
    }

    public void ResetVisibleRect()
    {
        SetWindowRgn(SurfaceHandle, IntPtr.Zero, true);
        if (_overlay != null)
            SetWindowRgn(OverlayHandle, IntPtr.Zero, true);
    }
    public void SetVisibleRect(ref Rect rect)
    {
        SetWindowRgn(SurfaceHandle, CreateRectRgn((int)Math.Round(rect.X * DpiX), (int)Math.Round(rect.Y * DpiY), (int)Math.Round(rect.Right * DpiX), (int)Math.Round(rect.Bottom * DpiY)), true);
        if (_overlay != null)
            SetWindowRgn(OverlayHandle, CreateRectRgn((int)Math.Round(rect.X * DpiX), (int)Math.Round(rect.Y * DpiY), (int)Math.Round(rect.Right * DpiX), (int)Math.Round(rect.Bottom * DpiY)), true);
    }

    /// <summary>
    /// Disposes the Surface and Overlay Windows and de-assigns the Player
    /// </summary>
    public void Dispose()
    {
        lock (this)
        {
            if (Disposed)
                return;

            // Disposes SwapChain Only
            Player = null;
            ReplicaPlayer = null;
            Disposed = true;

            DataContextChanged -= Host_DataContextChanged;
            IsVisibleChanged -= Host_IsVisibleChanged;
            Loaded -= Host_Loaded;

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
            }

            if (Surface != null)
            {
                if (_isMouseBindingsSubscribedSurface)
                    SetMouseSurface();

                Surface.Closed -= Surface_Closed;
                Surface.Closing -= Surface_Closing;
                Surface.KeyDown -= Surface_KeyDown;
                Surface.KeyUp -= Surface_KeyUp;
                Surface.Drop -= Surface_Drop;
                Surface.DragEnter -= Surface_DragEnter;
                Surface.SizeChanged -= SetRectOverlay;

                // If not shown yet app will not close properly
                if (!_surfaceClosed)
                {
                    Surface.Owner = null;
                    SetParent(SurfaceHandle, IntPtr.Zero);
                    Surface.Width = Surface.Height = 1;
                    Surface.Show();
                    if (!_overlayClosed)
                        _overlay?.Show();
                    Surface.Close();
                }
            }

            if (Owner != null)
            {
                Owner.SizeChanged -= Owner_SizeChanged;
            }

            Surface = null;
            _overlay = null;
            Owner = null;

            SurfaceHandle = IntPtr.Zero;
            OverlayHandle = IntPtr.Zero;
            OwnerHandle = IntPtr.Zero;

            Log.Debug("Disposed");
        }
    }

    public bool Player_CanHideCursor() => (Surface != null && Surface.IsActive) || (_overlay != null && _overlay.IsActive);
    public bool Player_GetFullScreen() => false;
    public void Player_SetFullScreen(bool value)
    {

    }
    public void Player_Disposed() => UIInvokeIfRequired(() => Player = null);
    #endregion
}
