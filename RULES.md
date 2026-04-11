# Diagnostic Rules Reference

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

Clear the internal cache via `JsonSerializerOptionsUpdateHandler.ClearCache` before unloading:

> **Warning**: This is not an officially supported .NET API. `JsonSerializerOptionsUpdateHandler` is an internal implementation detail that may change or be removed in future .NET versions. Use at your own risk.

```csharp
using System.Reflection;
using System.Text.Json;

void ClearJsonSerializerCache()
{
    var assembly = typeof(JsonSerializerOptions).Assembly;
    var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
    var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", BindingFlags.Static | BindingFlags.Public);
    clearCacheMethod?.Invoke(null, new object[] { null! });
}
```

In Godot, call this via the `AssemblyLoadContext.Unloading` callback (see [pattern above](#registering-cleanup-code-in-godot)).

Alternatively, avoid serializing plugin-defined types directly; use intermediate DTOs defined in a shared (non-collectible) assembly.

---

## GDU0005 — Newtonsoft.Json serialization

**Severity**: Warning  
**Category**: Type-Caching

### Problem

Newtonsoft.Json's serialization path calls `TypeDescriptor.GetConverter(type)` during contract resolution (`DefaultContractResolver.CreateContract` → `JsonTypeReflector.CanTypeDescriptorConvertString`). This populates several `System.ComponentModel` caches in the root `AssemblyLoadContext` with references to the collectible plugin type, preventing unloading.

Newtonsoft.Json's own internal caches (`DefaultContractResolver._contractCache`, `JsonTypeReflector` caches, etc.) are **not** the issue — when Newtonsoft.Json is loaded into the same collectible ALC as the plugin (via `CopyLocalLockFileAssemblies`), those caches are collected along with the ALC. The leak comes exclusively from the cross-ALC `TypeDescriptor` / `ReflectTypeDescriptionProvider` state.

```csharp
// BAD: Populates TypeDescriptor caches with typeof(MyData) in the root ALC.
var json = JsonConvert.SerializeObject(new MyData());
```

### Fix

Clear the `System.ComponentModel` caches that root the collectible assembly after serialization. The validated cleanup on .NET 10 targets three runtime-internal roots after a public `TypeDescriptor.Refresh`:

> **Warning**: This cleanup uses runtime-internal `System.ComponentModel` fields (`TypeDescriptor._defaultProviderInitialized`, `ReflectTypeDescriptionProvider._typeData`, `ReflectTypeDescriptionProvider.s_attributeCache`). These are implementation details that may change across .NET versions. Validated on .NET 10 / Windows x64.

```csharp
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

public static class TypeDescriptorCacheCleaner
{
    /// <summary>
    /// Clears TypeDescriptor/ReflectTypeDescriptionProvider caches that reference types
    /// from the calling assembly, allowing a collectible AssemblyLoadContext to unload
    /// after Newtonsoft.Json (or other TypeDescriptor-triggering) serialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ClearCache()
    {
        const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var callingAssembly = Assembly.GetCallingAssembly();
        var runtime = typeof(TypeDescriptor).Assembly;
        var reflectProviderType = runtime.GetType("System.ComponentModel.ReflectTypeDescriptionProvider")!;
        var nodeType = runtime.GetType("System.ComponentModel.TypeDescriptor+TypeDescriptionNode")!;

        TypeDescriptor.Refresh(callingAssembly);
        Prune(StaticField<IDictionary>(typeof(TypeDescriptor),
            "_defaultProviderInitialized", "s_defaultProviderInitialized"));

        foreach (var entry in Snapshot(StaticField<IDictionary>(typeof(TypeDescriptor),
            "_providerTable", "s_providerTable")))
            for (var node = entry.Value; node != null && nodeType.IsInstanceOfType(node);
                 node = InstanceField(node, "Next"))
                if (InstanceField(node, "Provider") is var provider
                    && reflectProviderType.IsInstanceOfType(provider))
                    Prune((IDictionary)(InstanceField(provider, "_typeData")
                        ?? throw new InvalidOperationException("_typeData was null")));

        Prune(StaticField<IDictionary>(reflectProviderType, "s_attributeCache"));
        return;

        void Prune(IDictionary dict)
        {
            foreach (var e in Snapshot(dict))
                if (Matches(e.Key) || Matches(e.Value))
                    dict.Remove(e.Key);
        }

        bool Matches(object? value)
        {
            if (value == null) return false;
            if (ReferenceEquals(value, callingAssembly)) return true;
            switch (value)
            {
                case Type t: return ReferenceEquals(t.Assembly, callingAssembly);
                case Assembly a: return ReferenceEquals(a, callingAssembly);
                case MemberInfo m: return ReferenceEquals(m.Module.Assembly, callingAssembly);
                case WeakReference w: return Matches(w.Target);
            }
            foreach (var name in new[] { "Target", "target", "_target" })
            {
                try { if (value.GetType().GetProperty(name, instanceFlags) is { } prop
                    && prop.GetIndexParameters().Length == 0
                    && prop.GetValue(value) is var t && !ReferenceEquals(t, value)
                    && Matches(t)) return true; } catch { }
                try { if (value.GetType().GetField(name, instanceFlags)?.GetValue(value) is var t
                    && !ReferenceEquals(t, value) && Matches(t)) return true; } catch { }
            }
            return ReferenceEquals(value.GetType().Assembly, callingAssembly);
        }

        static DictionaryEntry[] Snapshot(IDictionary dict)
        { var a = new DictionaryEntry[dict.Count]; dict.CopyTo(a, 0); return a; }

        static T StaticField<T>(Type type, params string[] names)
        {
            const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            foreach (var n in names)
            { var field = type.GetField(n, f); if (field != null)
                return (T)(field.GetValue(null) ?? throw new InvalidOperationException($"{n} was null")); }
            throw new MissingFieldException(type.FullName, string.Join("/", names));
        }

        static object? InstanceField(object instance, string name)
        {
            const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            return (instance.GetType().GetField(name, f)
                ?? throw new MissingFieldException(instance.GetType().FullName, name)).GetValue(instance);
        }
    }
}
```

In Godot, call `TypeDescriptorCacheCleaner.ClearCache()` after serialization or via the `AssemblyLoadContext.Unloading` callback (see [pattern above](#registering-cleanup-code-in-godot)).

Alternatively, avoid serializing plugin-defined types with Newtonsoft.Json entirely. Use intermediate DTOs defined in a shared (non-collectible) assembly, or switch to `System.Text.Json` where the cache can be cleared more simply (see [GDU0004](#gdu0004--systemtextjson-serialization)).

> **Note**: See [Newtonsoft.Json issue #2253](https://github.com/JamesNK/Newtonsoft.Json/issues/2253) and [#2414](https://github.com/JamesNK/Newtonsoft.Json/issues/2414) for community discussion on this limitation.

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

Call `TypeDescriptor.RemoveProvider(provider, type)` to remove the explicitly-added provider, then clear the remaining `TypeDescriptor` / `ReflectTypeDescriptionProvider` caches using the same pinpoint cleanup as [GDU0005](#gdu0005--newtonsoftjson-serialization).

> **Warning**: The cache cleanup uses the same runtime-internal `System.ComponentModel` fields as the Newtonsoft.Json workaround. Validated on .NET 10 / Windows x64.

```csharp
// 1. Remove the provider (public API):
TypeDescriptor.RemoveProvider(provider, typeof(MyType));

// 2. Clear remaining caches (same utility as GDU0005):
TypeDescriptorCacheCleaner.ClearCache();
```

In Godot, register cleanup via the `AssemblyLoadContext.Unloading` callback (see [pattern above](#registering-cleanup-code-in-godot)). You must retain a reference to the provider instance so it can be passed to `RemoveProvider`.

`TypeDescriptor` is primarily used by WinForms/WPF design-time infrastructure and is rare in Godot projects.

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

Remove the registered provider from the internal `EncodingProvider.s_providers` array via reflection before unloading:

> **Warning**: `EncodingProvider.s_providers` is a private static field. This is a runtime implementation detail that may change across .NET versions. Validated on .NET 10 / Windows x64.

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

public static class EncodingProviderCleaner
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RemoveCollectibleProviders()
    {
        var callingAssembly = Assembly.GetCallingAssembly();
        var field = typeof(EncodingProvider).GetField(
            "s_providers",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(EncodingProvider), "s_providers");

        var providers = (EncodingProvider[]?)field.GetValue(null);
        if (providers is null) return;

        var cleaned = providers
            .Where(p => !ReferenceEquals(p.GetType().Assembly, callingAssembly))
            .ToArray();
        field.SetValue(null, cleaned.Length == 0 ? null : cleaned);
    }
}
```

In Godot, call `EncodingProviderCleaner.RemoveCollectibleProviders()` via the `AssemblyLoadContext.Unloading` callback (see [pattern above](#registering-cleanup-code-in-godot)).

Alternatively, avoid calling `Encoding.RegisterProvider` from collectible assemblies entirely.

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
