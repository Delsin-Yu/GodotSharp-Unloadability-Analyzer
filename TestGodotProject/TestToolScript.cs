using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class TestToolScript : Node
{
    // === Safe patterns (NO warnings expected) ===
    private static int _safeCounter;                           // Value type - safe
    private const string Constant = "safe";                    // Const - safe
    private static string _safeString = "also safe";           // String excluded
    private List<int> _instanceField = new();                  // Instance field - safe
    [ThreadStatic] private static object? _tls;                // ThreadStatic - validated safe on .NET 10

    // === GDU0001: Subscription to external static event ===
    private void SubscribeExternal()
    {
        System.Console.CancelKeyPress += OnCancelKeyPress;     // Should trigger GDU0001
    }
    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) { }

    // === GDU0002: GCHandle.Alloc ===
    private void AllocHandle()
    {
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(this);  // Should trigger GDU0002
        handle.Free();
    }

    // === GDU0003: ThreadPool.RegisterWaitForSingleObject ===
    private void RegisterWait()
    {
        var waitHandle = new System.Threading.ManualResetEvent(false);
        System.Threading.ThreadPool.RegisterWaitForSingleObject(  // Should trigger GDU0003
            waitHandle,
            (state, timedOut) => { },
            null,
            -1,
            true);
    }

    // === GDU0004: System.Text.Json ===
    private void UseSystemTextJson()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { Foo = 1 });  // Should trigger GDU0004
        var obj = System.Text.Json.JsonSerializer.Deserialize<object>(json);     // Should trigger GDU0004
    }

    // === GDU0005: Newtonsoft.Json (requires Newtonsoft.Json package) ===
    // private void UseNewtonsoftJson()
    // {
    //     var json = Newtonsoft.Json.JsonConvert.SerializeObject(new { Foo = 1 });  // Would trigger GDU0005
    // }

    // === GDU0006: TypeDescriptor ===
    private void UseTypeDescriptor()
    {
        System.ComponentModel.TypeDescriptor.Refresh(typeof(TestToolScript));  // Should trigger GDU0006
    }

    // === GDU0007: Thread creation ===
    private void CreateThread()
    {
        var thread = new System.Threading.Thread(() => { });  // Should trigger GDU0007
    }

    // === GDU0008: Timer creation ===
    private void CreateTimer()
    {
        var timer1 = new System.Threading.Timer(_ => { });  // Should trigger GDU0008
        var timer2 = new System.Timers.Timer();              // Should trigger GDU0008
    }

    // === GDU0009: Encoding.RegisterProvider ===
    private void RegisterEncoding()
    {
        System.Text.Encoding.RegisterProvider(null!);  // Should trigger GDU0009
    }

    // === GDU0010: Task.Run ===
    private void UseTaskRun()
    {
        System.Threading.Tasks.Task.Run(() => { });  // Should trigger GDU0010
    }

    // === GDU0011: ThreadPool.QueueUserWorkItem ===
    private void UseThreadPoolQueue()
    {
        System.Threading.ThreadPool.QueueUserWorkItem(_ => { });  // Should trigger GDU0011
    }
}
