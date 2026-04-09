namespace Plugin.ThreadPoolRegisterWait;

public class TestCase
{
    public void Execute()
    {
        var mre = new ManualResetEvent(false);
        ThreadPool.RegisterWaitForSingleObject(
            mre,
            (state, timedOut) => { },
            null,
            -1,
            true);
    }
}
