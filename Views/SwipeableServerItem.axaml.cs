using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using aura.Models;
using aura.Services;
using aura.ViewModels;

namespace aura.Views;

public partial class SwipeableServerItem : UserControl
{
    private Point _startPoint;
    private bool _isPointerDown;
    private bool _isDragging;
    private bool _hasMoved;
    private bool _isAnimating;

    public SwipeableServerItem()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnCardPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, OnCardPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnCardPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerCaptureLostEvent, OnCardPointerCaptureLost, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLocalizedTexts();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateLocalizedTexts();
    }

    private void UpdateLocalizedTexts()
    {
        var loc = LocalizationService.Instance;
        if (MenuPing != null) MenuPing.Header = loc["CheckPing"];
        if (MenuCopy != null) MenuCopy.Header = loc["CopyJson"];
        if (MenuDelete != null) MenuDelete.Header = loc["DeleteServer"];
        if (LeftActionText != null) LeftActionText.Text = loc["CheckPing"];
        if (RightActionText != null) RightActionText.Text = loc["DeleteServer"];
    }

    private SubscriptionsViewModel? GetSubscriptionsViewModel()
    {
        var parent = this.Parent;
        while (parent != null)
        {
            if (parent is SubscriptionsView subView && subView.DataContext is SubscriptionsViewModel vm)
            {
                return vm;
            }
            if (parent is Control c && c.DataContext is SubscriptionsViewModel cVm)
            {
                return cVm;
            }
            if (parent is Control mainCtrl && mainCtrl.DataContext is MainViewModel mainVm)
            {
                return mainVm.SubscriptionsVM;
            }
            parent = parent.Parent;
        }
        return null;
    }

    private ServerNode? GetServerNode() => DataContext as ServerNode;

    private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isAnimating) return;

        var prop = e.GetCurrentPoint(this).Properties;
        if (prop.IsRightButtonPressed)
        {
            // Allow default right-click context menu on desktop
            return;
        }

        _startPoint = e.GetPosition(this);
        _isPointerDown = true;
        _isDragging = false;
        _hasMoved = false;
    }

    private void OnCardPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerDown || _isAnimating) return;

        var currentPoint = e.GetPosition(this);
        double dx = currentPoint.X - _startPoint.X;
        double dy = currentPoint.Y - _startPoint.Y;

        if (!_isDragging)
        {
            // Threshold to initiate horizontal swipe (must be predominantly horizontal)
            if (Math.Abs(dx) > 12 && Math.Abs(dx) > Math.Abs(dy) * 1.4)
            {
                _isDragging = true;
                _hasMoved = true;
                e.Pointer.Capture(this);
            }
        }

        if (_isDragging)
        {
            CardBorder.RenderTransform = new TranslateTransform(dx, 0);

            double progress = Math.Min(1.0, Math.Abs(dx) / Math.Max(120.0, Bounds.Width * 0.45));

            if (dx > 0)
            {
                // Swiping Left to Right -> Ping Action
                LeftActionBg.Opacity = progress;
                RightActionBg.Opacity = 0.0;
            }
            else
            {
                // Swiping Right to Left -> Delete Action
                LeftActionBg.Opacity = 0.0;
                RightActionBg.Opacity = progress;
            }

            e.Handled = true;
        }
    }

    private void OnCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPointerDown || _isAnimating) return;
        _isPointerDown = false;

        if (_isDragging)
        {
            e.Pointer.Capture(null);
            _isDragging = false;

            double currentDx = CardBorder.RenderTransform is TranslateTransform tt ? tt.X : 0;
            double threshold = Math.Max(130.0, Bounds.Width * 0.48);

            if (currentDx > threshold)
            {
                // Swiped past threshold to the right: Trigger Ping of this server only!
                _isAnimating = true;
                AnimateValue(currentDx, 0, 180, val =>
                {
                    CardBorder.RenderTransform = new TranslateTransform(val, 0);
                    LeftActionBg.Opacity = Math.Max(0.0, val / threshold);
                }, () =>
                {
                    LeftActionBg.Opacity = 0.0;
                    _isAnimating = false;
                    var node = GetServerNode();
                    var vm = GetSubscriptionsViewModel();
                    if (node != null && vm != null)
                    {
                        vm.PingSingleServerCommand.Execute(node);
                    }
                });
            }
            else if (currentDx < -threshold)
            {
                // Swiped past threshold to the left: Smooth animated removal without sudden jumps!
                _isAnimating = true;
                double targetX = -Bounds.Width - 30;

                // Phase 1: Slide card smoothly completely offscreen to the left (150ms)
                AnimateValue(currentDx, targetX, 150, val =>
                {
                    CardBorder.RenderTransform = new TranslateTransform(val, 0);
                    CardBorder.Opacity = Math.Max(0.0, 1.0 - Math.Abs(val) / Math.Abs(targetX));
                }, () =>
                {
                    CardBorder.Opacity = 0.0;
                    double initialHeight = Bounds.Height > 0 ? Bounds.Height : 54;

                    // Phase 2: Collapse height of the container smoothly to 0 (200ms)
                    AnimateValue(initialHeight, 0, 200, h =>
                    {
                        RootContainer.Height = Math.Max(0, h);
                    }, () =>
                    {
                        var node = GetServerNode();
                        var vm = GetSubscriptionsViewModel();
                        if (node != null && vm != null)
                        {
                            vm.DeleteServerCommand.Execute(node);
                        }
                    });
                });
            }
            else
            {
                // Did not pull to the end: Smoothly snap back to center
                _isAnimating = true;
                AnimateValue(currentDx, 0, 180, val =>
                {
                    CardBorder.RenderTransform = new TranslateTransform(val, 0);
                    LeftActionBg.Opacity = 0.0;
                    RightActionBg.Opacity = 0.0;
                }, () =>
                {
                    LeftActionBg.Opacity = 0.0;
                    RightActionBg.Opacity = 0.0;
                    _isAnimating = false;
                });
            }

            e.Handled = true;
        }
        else if (!_hasMoved)
        {
            // Simple tap / click: Select the server
            var node = GetServerNode();
            var vm = GetSubscriptionsViewModel();
            if (node != null && vm != null)
            {
                vm.SelectServerNodeCommand.Execute(node);
            }
        }
    }

    private void OnCardPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isDragging && !_isAnimating)
        {
            _isDragging = false;
            _isPointerDown = false;
            double currentDx = CardBorder.RenderTransform is TranslateTransform tt ? tt.X : 0;
            AnimateValue(currentDx, 0, 150, val =>
            {
                CardBorder.RenderTransform = new TranslateTransform(val, 0);
                LeftActionBg.Opacity = 0;
                RightActionBg.Opacity = 0;
            }, () =>
            {
                LeftActionBg.Opacity = 0;
                RightActionBg.Opacity = 0;
            });
        }
    }

    private void AnimateValue(double from, double to, int durationMs, Action<double> onProgress, Action? onComplete = null)
    {
        var sw = Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };

        timer.Tick += (s, e) =>
        {
            double elapsed = sw.ElapsedMilliseconds;
            double t = Math.Min(1.0, elapsed / durationMs);
            // Cubic Ease Out: 1 - (1 - t)^3
            double eased = 1.0 - Math.Pow(1.0 - t, 3);
            double val = from + (to - from) * eased;

            onProgress(val);

            if (t >= 1.0)
            {
                timer.Stop();
                onProgress(to);
                onComplete?.Invoke();
            }
        };

        timer.Start();
    }

    public void OnContextMenuPingClicked(object? sender, RoutedEventArgs e)
    {
        var node = GetServerNode();
        var vm = GetSubscriptionsViewModel();
        if (node != null && vm != null)
        {
            vm.PingSingleServerCommand.Execute(node);
        }
    }

    public void OnContextMenuCopyClicked(object? sender, RoutedEventArgs e)
    {
        var node = GetServerNode();
        var vm = GetSubscriptionsViewModel();
        if (node != null && vm != null)
        {
            vm.CopyServerJsonCommand.Execute(node);
        }
    }

    public void OnContextMenuDeleteClicked(object? sender, RoutedEventArgs e)
    {
        var node = GetServerNode();
        var vm = GetSubscriptionsViewModel();
        if (node != null && vm != null)
        {
            vm.DeleteServerCommand.Execute(node);
        }
    }
}
