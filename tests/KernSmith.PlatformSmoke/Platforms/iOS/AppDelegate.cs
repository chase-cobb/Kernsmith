using Foundation;
using UIKit;

namespace KernSmith.PlatformSmoke;

// Shared by the iOS and Mac Catalyst targets.
[Register("AppDelegate")]
public class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        var text = new UITextView { Editable = false, Font = UIFont.FromName("Menlo", 10) };
        Window = new UIWindow(UIScreen.MainScreen.Bounds)
        {
            RootViewController = new UIViewController { View = text },
        };
        Window.MakeKeyAndVisible();

        // Results go to stdout (visible with `xcrun simctl launch --console-pty`) and the screen.
        // With KSMOKE_EXIT=1 in the environment the app exits when done, for CI.
        Task.Run(() =>
        {
            bool ok = SmokeRunner.Run(line =>
            {
                Console.WriteLine(line);
                InvokeOnMainThread(() => text.Text += line + "\n");
            });
            if (Environment.GetEnvironmentVariable("KSMOKE_EXIT") == "1")
                Environment.Exit(ok ? 0 : 1);
        });
        return true;
    }

    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
