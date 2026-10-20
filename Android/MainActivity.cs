using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using aura.Services;

namespace aura.Android;

[Activity(
    Name = "aura.Android.MainActivity",
    Label = "lumen aura",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    RoundIcon = "@drawable/icon",
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode,
    WindowSoftInputMode = global::Android.Views.SoftInput.AdjustResize)]
public class MainActivity : AvaloniaMainActivity
{
    private const int RequestVpnPermission = 0x1A2B;
    public static System.Threading.Tasks.TaskCompletionSource<bool>? VpnPermissionTcs { get; set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // 1. Initialize Android Application List Provider for Split-Tunneling
        ProcessService.Instance.CustomProvider = new AndroidAppListProvider(this);

        // 2. Initialize Android Native VpnService Adapter
        VpnEngineService.Instance.PlatformAdapter = new AndroidVpnPlatformAdapter(this);

        LogService.Instance.Log("android", "INIT", "lumen aura Android platform initialized with native VpnService & PackageManager.");
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == RequestVpnPermission)
        {
            if (resultCode == Result.Ok)
            {
                LogService.Instance.Log("android", "AUTH", "Android VPN system permission GRANTED by user.");
                VpnPermissionTcs?.TrySetResult(true);
            }
            else
            {
                LogService.Instance.Log("android", "WARN", "Android VPN system permission was DENIED by user.");
                VpnPermissionTcs?.TrySetResult(false);
            }
        }
    }
}
