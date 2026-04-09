using System.Runtime.InteropServices;

namespace Plugin.MarshalFnPtr;

public class TestCase
{
    private void InstanceMethod() { }

    public void Execute()
    {
        Action del = InstanceMethod;
        Marshal.GetFunctionPointerForDelegate(del);
    }
}
