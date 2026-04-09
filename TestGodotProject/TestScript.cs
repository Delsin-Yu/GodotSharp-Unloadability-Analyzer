using Godot;
using System;
using System.Collections.Generic;

// Negative test: No [Tool] attribute, so NO diagnostics should fire.
// This validates the analyzer only targets [Tool] types.
public partial class TestScript : Node
{
    // === Safe patterns (NO warnings expected) ===
    private static int _safeCounter;
    private const string Constant = "safe";
    private static string _safeString = "also safe";
    private List<int> _instanceField = new();
    [ThreadStatic] private static object? _tls;                // ThreadStatic - validated safe on .NET 10

    // === GDU0001: Subscription to external static event ===
    private void SubscribeExternal()
    {
        System.Console.CancelKeyPress += OnCancelKeyPress;
    }
    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) { }

    // === GDU0002: GCHandle.Alloc ===
    private void AllocHandle()
    {
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(this);
        handle.Free();
    }

    // === GDU0003: ThreadPool.RegisterWaitForSingleObject ===
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

    // === GDU0004: System.Text.Json ===
    private void UseSystemTextJson()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { Foo = 1 });
        var obj = System.Text.Json.JsonSerializer.Deserialize<object>(json);
    }

    // === GDU0005: Newtonsoft.Json (requires Newtonsoft.Json package) ===
    // private void UseNewtonsoftJson()
    // {
    //     var json = Newtonsoft.Json.JsonConvert.SerializeObject(new { Foo = 1 });
    // }

    // === GDU0006: TypeDescriptor ===
    private void UseTypeDescriptor()
    {
        System.ComponentModel.TypeDescriptor.Refresh(typeof(TestScript));
    }

    // === GDU0007: Thread creation ===
    private void CreateThread()
    {
        var thread = new System.Threading.Thread(() => { });
    }

    // === GDU0008: Timer creation ===
    private void CreateTimer()
    {
        var timer1 = new System.Threading.Timer(_ => { });
        var timer2 = new System.Timers.Timer();
    }

    // === GDU0009: Encoding.RegisterProvider ===
    private void RegisterEncoding()
    {
        System.Text.Encoding.RegisterProvider(null!);
    }

    // === GDU0010: Task.Run ===
    private void UseTaskRun()
    {
        System.Threading.Tasks.Task.Run(() => { });
    }

    // === GDU0011: ThreadPool.QueueUserWorkItem ===
    private void UseThreadPoolQueue()
    {
        System.Threading.ThreadPool.QueueUserWorkItem(_ => { });
    }
}
