namespace Plugin.ExternalStaticEvent;

public class TestCase
{
    public void Execute()
    {
        Console.CancelKeyPress += OnCancel;
    }

    private static void OnCancel(object? sender, ConsoleCancelEventArgs e) { }
}
