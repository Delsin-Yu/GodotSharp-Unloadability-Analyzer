namespace Plugin.ThreadStatic;

public class TestCase
{
    [ThreadStatic] private static object? _field;

    public void Execute()
    {
        _field = new object();
        GC.KeepAlive(_field);
    }
}
