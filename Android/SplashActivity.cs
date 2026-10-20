using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.AppCompat.App;

namespace aura.Android;

[Activity(
    Name = "aura.Android.SplashActivity",
    Theme = "@style/MyTheme.Splash",
    MainLauncher = true,
    NoHistory = true,
    Exported = true)]
public class SplashActivity : AppCompatActivity
{
    protected override void OnResume()
    {
        base.OnResume();
        StartActivity(new Intent(this, typeof(MainActivity)));
        Finish();
    }
}
