using System.Runtime.InteropServices;

namespace Plugin.GCHandleCleanup;

public struct LocalData { public int Value; }

public class TestCase
{
    public void Execute()
    {
        var handle = GCHandle.Alloc(
            new LocalData[] { new() { Value = 42 } }, GCHandleType.Pinned);
        // Exercise the handle
        var target = (LocalData[])handle.Target!;
        Console.WriteLine($"Pinned handle allocated, target value: {target[0].Value}");
        // Cleanup: free the handle so ALC can unload
        handle.Free();
    }
}
