# Diagnostic Rules Reference

Each rule is empirically validated — see [`ValidationProjects/`](ValidationProjects/) for the test harness and results.

Only types annotated with `[Tool]` (or nested within `[Tool]` types) are analyzed, since only those types execute inside the Godot editor where `AssemblyLoadContext` unloading applies.

### Registering cleanup code in Godot

For rules that require cleanup before unloading, register a handler on the `AssemblyLoadContext.Unloading` event:

```csharp
using Godot;
using System;
using System.Runtime.Loader;
using System.Reflection;

[Tool]
public partial class MyToolScript : Node
{
    public override void _Ready()
    {
        // ... code that creates an unloadability issue ...

        // Register cleanup to run before the ALC unloads
        AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly())!.Unloading += alc =>
        {
            // Perform cleanup here (unsubscribe events, free handles, dispose timers, etc.)
        };
    }
}
```

---

## GDU0001 — Subscription to external static event

**Severity**: Warning  
**Category**: Reference-Escaping

### Problem

Subscribing to a static event on a type from the root `AssemblyLoadContext` (framework/BCL/Godot assemblies) creates a delegate root stored in the external assembly's static field. Since the root ALC is never unloaded, the delegate — and thus the plugin assembly — is kept alive indefinitely.

```csharp
// BAD: Console lives in the root ALC; its CancelKeyPress invocation list
// now holds a reference to OnCancel's target, rooting the plugin assembly.
Console.CancelKeyPress += OnCancel;
```

### Fix

Unsubscribe from the event before the `AssemblyLoadContext` unloads:

```csharp
Console.CancelKeyPress -= OnCancel;
```

In Godot, register cleanup via the `AssemblyLoadContext.Unloading` callback (see [pattern above](#registering-cleanup-code-in-godot)).

### Scope

Only events from root-ALC assemblies are flagged (`System.*`, `Microsoft.*`, `Godot*`). Events from project references or NuGet packages loaded in the same collectible ALC are safe and are not reported.

---

## GDU0002 — GCHandle.Alloc usage

**Severity**: Warning  
**Category**: Reference-Escaping

### Problem

`GCHandle.Alloc` creates a GC root. If the allocated object's type belongs to the collectible assembly and the handle is not freed, the GC root prevents the `AssemblyLoadContext` from being collected.

```csharp
// BAD: GCHandle roots a plugin-defined type; without Free(), the ALC leaks.
var handle = GCHandle.Alloc(new MyPluginType());
```

### Fix

Always pair `GCHandle.Alloc` with `GCHandle.Free` before unloading:

```csharp
var handle = GCHandle.Alloc(new MyPluginType());
try
{
    // Use the handle...
}
finally
{
    handle.Free();
}
```

Suppress this warning if you are certain the handle will be freed before the ALC unloads.

---

## GDU0003 — ThreadPool.RegisterWaitForSingleObject

**Severity**: Warning  
**Category**: Reference-Escaping

### Problem

`ThreadPool.RegisterWaitForSingleObject` stores a reference to the callback delegate in the `RegisteredWaitHandle`. As long as the wait is registered, the delegate — and the plugin assembly defining it — cannot be collected.

```csharp
// BAD: The callback delegate roots the plugin assembly.
var rwh = ThreadPool.RegisterWaitForSingleObject(
    waitHandle, (state, timedOut) => { /* ... */ }, null, -1, true);
```

### Fix

Unregister the wait handle before the ALC unloads:

```csharp
rwh.Unregister(waitHandle);
```

---

## GDU0004 — System.Text.Json serialization

**Severity**: Warning  
**Category**: Type-Caching

### Problem

`System.Text.Json.JsonSerializer` methods cache `Type` metadata in the internal `JsonSerializerOptions` cache. Since `System.Text.Json` is loaded in the root ALC, these cached Type references keep the collectible assembly rooted.

```csharp
// BAD: Caches typeof(MyData) in the root ALC's serializer cache.
var json = JsonSerializer.Serialize(new MyData());
var obj = JsonSerializer.Deserialize<MyData>(json);
```

### Fix

It may be possible to clear the internal cache via reflection before unloading. Alternatively, avoid serializing plugin-defined types directly; use intermediate DTOs defined in a shared (non-collectible) assembly.

---

## GDU0005 — Newtonsoft.Json serialization

**Severity**: Warning  
**Category**: Type-Caching

### Problem

`Newtonsoft.Json` caches contract metadata (via `DefaultContractResolver`) keyed by Type. If Newtonsoft.Json is loaded in the root ALC (e.g., by the host), these cached references keep the plugin assembly rooted.

```csharp
// BAD: Caches typeof(MyData) in the contract resolver.
var json = JsonConvert.SerializeObject(new MyData());
```

### Fix

It may be possible to clear the internal cache via reflection before unloading. Alternatively, use a dedicated `IContractResolver` per collectible assembly and discard it before unloading.

> **Note**: If Newtonsoft.Json is loaded in the *same* collectible ALC as the plugin (not in the root ALC), its caches should be collectible together. The severity of this issue depends on the assembly loading configuration.

---

## GDU0006 — TypeDescriptor modification

**Severity**: Warning  
**Category**: Type-Caching

### Problem

`TypeDescriptor.AddProvider`, `AddAttributes`, `AddProviderTransparent`, and `Refresh` register type metadata in global static dictionaries that are never cleared. These references root the collectible assembly.

```csharp
// BAD: Registers metadata for MyType in a global store.
TypeDescriptor.AddProvider(new MyProvider(), typeof(MyType));
TypeDescriptor.Refresh(typeof(MyType));
```

### Fix

There is no way to clear the global TypeDescriptor stores. Avoid calling these methods with types from collectible assemblies.

`TypeDescriptor` is primarily used by WinForms/WPF design-time infrastructure and is rare in Godot projects. If you encounter this warning, check whether a third-party library is calling TypeDescriptor internally.

---

## GDU0007 — Thread creation

**Severity**: Warning  
**Category**: Thread/Timer/Task

### Problem

Creating and starting a `Thread` with a method from a collectible assembly prevents the `AssemblyLoadContext` from being unloaded while the thread is running. The thread's stack references the plugin method, rooting the assembly.

```csharp
// BAD: Thread runs indefinitely, preventing ALC unload.
var thread = new Thread(() => { while (true) { /* ... */ } });
thread.Start();
```

### Fix

Ensure the thread exits before the ALC unloads. Use a `CancellationToken` or a shutdown signal:

```csharp
var cts = new CancellationTokenSource();
var thread = new Thread(() =>
{
    while (!cts.Token.IsCancellationRequested) { /* ... */ }
});
thread.Start();

// Before unloading:
cts.Cancel();
thread.Join();
```

Suppress this warning if you are certain the thread will complete before unloading.

---

## GDU0008 — Timer creation

**Severity**: Warning  
**Category**: Thread/Timer/Task

### Problem

`System.Threading.Timer` and `System.Timers.Timer` hold references to their callback delegates. While active, the timer roots the plugin assembly.

```csharp
// BAD: Timer callback roots the plugin assembly indefinitely.
var timer = new Timer(_ => DoWork(), null, 0, 1000);
```

### Fix

Dispose the timer before the ALC unloads:

```csharp
timer.Dispose();
```

In Godot, dispose timers via the `AssemblyLoadContext.Unloading` callback (see [pattern above](#registering-cleanup-code-in-godot)).

---

## GDU0009 — Encoding.RegisterProvider

**Severity**: Warning  
**Category**: Global Registration

### Problem

`Encoding.RegisterProvider` adds an `EncodingProvider` to a global list that is never cleared. If the provider type is from a collectible assembly, the reference prevents the ALC from being collected.

```csharp
// BAD: Provider is added to a global list forever.
Encoding.RegisterProvider(new MyEncodingProvider());
```

### Fix

There is no way to unregister an encoding provider once registered. Avoid calling `Encoding.RegisterProvider` from collectible assemblies entirely.

---

## GDU0010 — Task.Run usage

**Severity**: Warning  
**Category**: Thread/Timer/Task

### Problem

`Task.Run` schedules a callback on the thread pool. While the task is executing, the callback delegate roots the collectible assembly, preventing unload.

```csharp
// BAD: Long-running task roots the plugin assembly.
Task.Run(() => { while (true) { /* ... */ } });
```

### Fix

Ensure the task completes (or is cancelled) before the ALC unloads:

```csharp
var cts = new CancellationTokenSource();
var task = Task.Run(() =>
{
    while (!cts.Token.IsCancellationRequested) { /* ... */ }
}, cts.Token);

// Before unloading:
cts.Cancel();
await task;
```

Suppress this warning if you are certain the task will complete before unloading.

---

## GDU0011 — ThreadPool.QueueUserWorkItem

**Severity**: Warning  
**Category**: Thread/Timer/Task

### Problem

`ThreadPool.QueueUserWorkItem` schedules a callback. While the work item is executing, the callback delegate roots the collectible assembly, preventing unload.

```csharp
// BAD: Long-running work item roots the plugin assembly.
ThreadPool.QueueUserWorkItem(_ => { while (true) { /* ... */ } });
```

### Fix

Ensure the work item completes before the ALC unloads. For long-running operations, prefer `Task.Run` with a `CancellationToken` so you can await completion:

```csharp
var cts = new CancellationTokenSource();
var task = Task.Run(() =>
{
    while (!cts.Token.IsCancellationRequested) { /* ... */ }
}, cts.Token);

// Before unloading:
cts.Cancel();
await task;
```

Suppress this warning if you are certain the work item will complete before unloading.
