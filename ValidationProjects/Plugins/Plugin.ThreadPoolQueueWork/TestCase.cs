namespace Plugin.ThreadPoolQueueWork;

public class TestCase
{
    public void Execute()
    {
        ThreadPool.QueueUserWorkItem(_ => Thread.Sleep(Timeout.Infinite));
    }
}
