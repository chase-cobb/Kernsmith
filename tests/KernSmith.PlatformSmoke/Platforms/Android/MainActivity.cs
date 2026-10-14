using Android.App;
using Android.OS;
using Android.Widget;

namespace KernSmith.PlatformSmoke;

[Activity(Label = "KernSmith Smoke", MainLauncher = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var text = new TextView(this) { TextSize = 10 };
        var scroll = new ScrollView(this);
        scroll.AddView(text);
        SetContentView(scroll);

        // Run off the UI thread; results go to logcat (tag KSMOKE) and the screen.
        Task.Run(() =>
        {
            SmokeRunner.Run(line =>
            {
                Android.Util.Log.Info(SmokeRunner.Prefix, line);
                RunOnUiThread(() => text.Append(line + "\n"));
            });
        });
    }
}
