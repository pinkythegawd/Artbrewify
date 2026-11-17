using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Artbrewify.Views;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        if (File.Exists(logoPath))
            LogoImage.Source = new BitmapImage(new Uri(logoPath));
    }
}