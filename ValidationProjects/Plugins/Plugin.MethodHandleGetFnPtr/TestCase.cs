namespace Plugin.MethodHandleGetFnPtr;

public class TestCase
{
    private static void StaticMethod() { }

    public void Execute()
    {
        Action del = StaticMethod;
        var ptr = del.Method.MethodHandle.GetFunctionPointer();
        GC.KeepAlive(ptr);
    }
}
