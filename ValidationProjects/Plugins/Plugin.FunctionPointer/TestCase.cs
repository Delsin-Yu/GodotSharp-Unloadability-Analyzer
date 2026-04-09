namespace Plugin.FunctionPointer;

public class TestCase
{
    private static void StaticMethod() { }

    public unsafe void Execute()
    {
        delegate*<void> ptr = &StaticMethod;
        ptr();
    }
}
