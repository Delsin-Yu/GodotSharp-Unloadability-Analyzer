namespace Plugin.ThreadCreation;

public class TestCase
{
    public void Execute()
    {
        var t = new Thread(() => Thread.Sleep(Timeout.Infinite))
        {
            IsBackground = true
        };
        t.Start();
    }
}
