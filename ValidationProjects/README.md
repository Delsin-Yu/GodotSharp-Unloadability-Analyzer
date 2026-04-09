# Validation Projects

Empirical test suite that validates which code patterns actually prevent `AssemblyLoadContext` unloading on .NET 10.

## Architecture

- **PluginHost** — Console app that loads a plugin DLL into a collectible `AssemblyLoadContext`, invokes `Execute()`, unloads the ALC, then checks `WeakReference.IsAlive` after 10 GC cycles.
- **Plugins/** — One project per test case. Each contains a `TestCase` class with a parameterless `Execute()` method that exercises a single pattern.

## Running

```powershell
# Build and run all plugins
.\run-all.ps1

# Run a single plugin
dotnet run --project PluginHost -- Plugins\Plugin.GCHandle\bin\Debug\net10.0\Plugin.GCHandle.dll
```

## Results

> **Runtime**: .NET 10.0 | **Platform**: Windows x64 | **Date**: 2026-04-09

### Patterns that PREVENT unloading (FAIL = leaked)

| Plugin | Pattern | Analyzer Rule |
|--------|---------|---------------|
| Plugin.ExternalStaticEvent | `Console.CancelKeyPress += handler` | GDU0001 |
| Plugin.GCHandle | `GCHandle.Alloc(new LocalType())` without `Free()` | GDU0002 |
| Plugin.ThreadPoolRegisterWait | `ThreadPool.RegisterWaitForSingleObject(...)` | GDU0003 |
| Plugin.SystemTextJson | `JsonSerializer.Serialize(localType)` | GDU0004 |
| Plugin.NewtonsoftJson | `JsonConvert.SerializeObject(localType)` | GDU0005 |
| Plugin.TypeDescriptor | `TypeDescriptor.AddProvider(...)` | GDU0006 |
| Plugin.ThreadCreation | `new Thread(() => Sleep(∞)).Start()` | GDU0007 |
| Plugin.TimerCreation | `new Timer(callback, 0, 1000)` stored in static field | GDU0008 |
| Plugin.EncodingRegisterProvider | `Encoding.RegisterProvider(customProvider)` | GDU0009 |
| Plugin.TaskRun | `Task.Run(() => Sleep(∞))` | GDU0010 |
| Plugin.ThreadPoolQueueWork | `ThreadPool.QueueUserWorkItem(_ => Sleep(∞))` | GDU0011 |

### Patterns that DO NOT prevent unloading (PASS = unloaded OK)

| Plugin | Pattern | Notes |
|--------|---------|-------|
| Plugin.Baseline | No-op (control) | Confirms test harness works |
| Plugin.ThreadStatic | `[ThreadStatic]` field + assignment | Safe on .NET 10; TLS slot is cleaned up |
| Plugin.MarshalFnPtr | `Marshal.GetFunctionPointerForDelegate` (instance method) | Pointer is local, delegate goes out of scope |
| Plugin.MarshalFnPtrStatic | `Marshal.GetFunctionPointerForDelegate` (static method) | Same — safe when pointer is not retained by native code |
| Plugin.XmlSerializer | `new XmlSerializer(typeof(T))` + `Serialize(...)` | Fixed in .NET 10; no longer caches collectible types |
| Plugin.MethodHandleGetFnPtr | `delegate.Method.MethodHandle.GetFunctionPointer()` | Returns plain `IntPtr`, no GC root created |
| Plugin.FunctionPointer | `delegate*<void> ptr = &StaticMethod` | C# 9 function pointer, no GC root |

### Summary

- **11 / 18** patterns prevent unloading → covered by analyzer rules GDU0001–GDU0011
- **7 / 18** patterns are safe → no analyzer rule needed
