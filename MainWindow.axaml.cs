using Avalonia.Controls;

namespace aura;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Closing += (s, e) =>
        {
            if (!aura.Services.TrayService.Instance.IsExiting)
            {
                e.Cancel = true;
                Hide();
            }
        };
    }
}