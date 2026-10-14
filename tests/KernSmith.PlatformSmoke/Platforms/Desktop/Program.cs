namespace KernSmith.PlatformSmoke;

public static class Program
{
    public static int Main() => SmokeRunner.Run(Console.WriteLine) ? 0 : 1;
}
