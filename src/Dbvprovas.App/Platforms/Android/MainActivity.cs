using Android.App;
using Android.Content.PM;

namespace Dbvprovas.App;

// Nome fixo da activity, para a fumaça do Appium abrir o app (CA-TEN-005).
[Activity(
    Name = "io.github.gustavo0101.dbvprovas.MainActivity",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
