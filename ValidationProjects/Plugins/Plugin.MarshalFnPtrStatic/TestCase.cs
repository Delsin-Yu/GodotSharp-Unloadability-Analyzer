using System.Runtime.InteropServices;

namespace Plugin.MarshalFnPtrStatic;

public class TestCase
{
    private static void StaticMethod() { }

    public void Execute()
    {
        Action del = StaticMethod;
        Marshal.GetFunctionPointerForDelegate(del);
    }
}
