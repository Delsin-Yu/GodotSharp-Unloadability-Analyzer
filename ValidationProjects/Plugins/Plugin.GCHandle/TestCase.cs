using System.Runtime.InteropServices;

namespace Plugin.GCHandle;

public struct LocalData { public int Value; }

public class TestCase
{
    public void Execute()
    {
        // Pinned handle creates a strong root in the runtime handle table,
        // preventing GC from collecting the LocalData[] (whose type belongs
        // to this collectible ALC). No need to store the handle — the pin
        // persists until Free() is called.
        System.Runtime.InteropServices.GCHandle.Alloc(
            new LocalData[] { new() { Value = 42 } }, GCHandleType.Pinned);
    }
}
