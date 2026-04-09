namespace Plugin.TimerCreation;

public class TestCase
{
    // Store in static field so it's not GC'd before we test unloading
    private static System.Threading.Timer? _timer;

    public void Execute()
    {
        _timer = new System.Threading.Timer(_ => { }, null, 0, 1000);
    }
}
