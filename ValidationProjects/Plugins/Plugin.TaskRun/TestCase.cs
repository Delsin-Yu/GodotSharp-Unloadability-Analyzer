namespace Plugin.TaskRun;

public class TestCase
{
    public void Execute()
    {
        Task.Run(() => Thread.Sleep(Timeout.Infinite));
    }
}
