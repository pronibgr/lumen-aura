using Avalonia.Controls;
using aura.ViewModels;

namespace aura.Views;

public partial class MainAdaptiveView : UserControl
{
    public MainAdaptiveView()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (DataContext is MainViewModel vm)
        {
            vm.UpdateAdaptiveWidth(e.NewSize.Width);
        }
    }
}
