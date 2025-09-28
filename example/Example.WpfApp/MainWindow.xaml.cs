using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Configuration.Writable;

namespace Example.WpfApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IWritableOptions<SampleSetting> _option;

    // for designer support only
    public MainWindow()
    {
        InitializeComponent();
        _option = null!;
    }

    //
    public MainWindow(IWritableOptions<SampleSetting> option)
        : this()
    {
        _option = option;
        DataContext = option.CurrentValue;
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        var setting = (SampleSetting)DataContext!;
        setting = setting with { LastUpdatedAt = DateTime.Now };
        await _option.SaveAsync(setting);
        MessageBox.Show("Saved!");

        // refresh UI
        DataContext = _option.CurrentValue;
    }
}
