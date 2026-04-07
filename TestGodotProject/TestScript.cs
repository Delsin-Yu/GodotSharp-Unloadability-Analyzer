using Godot;
using System;
using System.Collections.Generic;

// Negative test: No [Tool] attribute, so NO diagnostics should fire.
// This validates the analyzer only targets [Tool] types.
public partial class TestScript : Node
{
    // === GDU0001: ThreadStatic ===
    [ThreadStatic] private static object? _tls;

    // === Safe patterns (NO warnings expected) ===
    private static int _safeCounter;
    private const string Constant = "safe";
    private static string _safeString = "also safe";
    private List<int> _instanceField = new();

    // === GDU0002: Subscription to external static event ===
    private void SubscribeExternal()
    {
        System.Console.CancelKeyPress += OnCancelKeyPress;
    }
    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) { }

    // === GDU0003: GCHandle.Alloc ===
    private void AllocHandle()
    {
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(this);
        handle.Free();
    }

    // === GDU0004: Marshal.GetFunctionPointerForDelegate ===
    private void GetFnPtr()
    {
        Action a = () => { };
        var ptr = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(a);
    }

    // === GDU0005: ThreadPool.RegisterWaitForSingleObject ===
    private void RegisterWait()
    {
        var waitHandle = new System.Threading.ManualResetEvent(false);
        System.Threading.ThreadPool.RegisterWaitForSingleObject(
            waitHandle,
            (state, timedOut) => { },
            null,
            -1,
            true);
    }

    // === GDU0006: System.Text.Json ===
    private void UseSystemTextJson()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { Foo = 1 });
        var obj = System.Text.Json.JsonSerializer.Deserialize<object>(json);
    }

    // === GDU0007: Newtonsoft.Json (requires Newtonsoft.Json package) ===
    // private void UseNewtonsoftJson()
    // {
    //     var json = Newtonsoft.Json.JsonConvert.SerializeObject(new { Foo = 1 });
    // }

    // === GDU0008: XmlSerializer ===
    private void UseXmlSerializer()
    {
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(TestScript));
    }

    // === GDU0009: TypeDescriptor ===
    private void UseTypeDescriptor()
    {
        System.ComponentModel.TypeDescriptor.Refresh(typeof(TestScript));
    }

    // === GDU0010: Thread creation ===
    private void CreateThread()
    {
        var thread = new System.Threading.Thread(() => { });
    }

    // === GDU0011: Timer creation ===
    private void CreateTimer()
    {
        var timer1 = new System.Threading.Timer(_ => { });
        var timer2 = new System.Timers.Timer();
    }

    // === GDU0012: Encoding.RegisterProvider ===
    private void RegisterEncoding()
    {
        System.Text.Encoding.RegisterProvider(null!);
    }
}
