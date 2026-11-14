using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;

namespace FlexiCore.ActivationApp;

[Activity(
    Label = "FlexiCore Activation Engine",
    Theme = "@style/Theme.AppCompat.Light.NoActionBar",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.SmallestScreenSize)]
public sealed class MainActivity : AvaloniaMainActivity
{
    public static MainActivity? Current { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Current = this;
        base.OnCreate(savedInstanceState);
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(Current, this)) Current = null;
        base.OnDestroy();
    }
}

[Application(Icon = "@mipmap/ic_launcher", RoundIcon = "@mipmap/ic_launcher_round")]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    protected MainApplication(nint javaReference, Android.Runtime.JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }
}
