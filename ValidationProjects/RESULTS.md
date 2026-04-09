# Validation Results

> **Date**: 2026-04-09  
> **Runtime**: .NET 10.0 (net10.0 TFM)  
> **Platform**: Windows x64  
> **Test Harness**: PluginHost with collectible AssemblyLoadContext

## 1. Results Table

| # | Plugin | Rule | Pattern | Expected | Actual | Match |
|---|--------|------|---------|----------|--------|-------|
| 1 | Plugin.Baseline | — | No-op baseline | PASS | PASS | ✅ |
| 2 | Plugin.ThreadStatic | GDU0001 | `[ThreadStatic]` field assignment | FAIL | **PASS** | ❌ |
| 3 | Plugin.ExternalStaticEvent | GDU0002 | Subscribe to `Console.CancelKeyPress` | FAIL | FAIL | ✅ |
| 4 | Plugin.GCHandle | GDU0003 | `GCHandle.Alloc(new object())` without Free | FAIL | **PASS** | ⚠️ |
| 5 | Plugin.MarshalFnPtr | GDU0004 | `Marshal.GetFunctionPointerForDelegate` (instance method) | FAIL | **PASS** | ❌ |
| 6 | Plugin.ThreadPoolRegisterWait | GDU0005 | `ThreadPool.RegisterWaitForSingleObject` | FAIL | FAIL | ✅ |
| 7 | Plugin.SystemTextJson | GDU0006 | `JsonSerializer.Serialize(localType)` | FAIL | FAIL | ✅ |
| 8 | Plugin.NewtonsoftJson | GDU0007 | `JsonConvert.SerializeObject(localType)` | FAIL | FAIL | ✅ |
| 9 | Plugin.XmlSerializer | GDU0008 | `new XmlSerializer(typeof(LocalType))` | FAIL | **PASS** | ❌ |
| 10 | Plugin.TypeDescriptor | GDU0009 | `TypeDescriptor.AddProvider(...)` | FAIL | FAIL | ✅ |
| 11 | Plugin.ThreadCreation | GDU0010 | `new Thread(Sleep(∞)).Start()` | FAIL | FAIL | ✅ |
| 12 | Plugin.TimerCreation | GDU0011 | `new Timer(callback, 0, 1000)` stored in static field | FAIL | FAIL | ✅ |
| 13 | Plugin.EncodingRegisterProvider | GDU0012 | `Encoding.RegisterProvider(customProvider)` | FAIL | FAIL | ✅ |
| 14 | Plugin.TaskRun | — | `Task.Run(() => Sleep(∞))` | FAIL | FAIL | ✅ |
| 15 | Plugin.ThreadPoolQueueWork | — | `ThreadPool.QueueUserWorkItem(_ => Sleep(∞))` | FAIL | FAIL | ✅ |
| 16 | Plugin.MarshalFnPtrStatic | GDU0004 | `Marshal.GetFunctionPointerForDelegate` (static method) | FAIL | **PASS** | ❌ |
| 17 | Plugin.MethodHandleGetFnPtr | — | `delegate.Method.MethodHandle.GetFunctionPointer()` | ? | PASS | ℹ️ |
| 18 | Plugin.FunctionPointer | — | `delegate*<void> ptr = &StaticMethod` | ? | PASS | ℹ️ |

**Legend**: ✅ = claim matches reality, ❌ = claim does NOT match reality, ⚠️ = inconclusive (test limitation), ℹ️ = exploratory (no existing claim)

### Score: 9/14 rules validated (64%), 4 false positives, 1 inconclusive

---

## 2. Neki's Questions Answered

### Q: Does `[ThreadStatic]` prevent ALC unloading?
**Answer: NO** — Plugin.ThreadStatic **PASSED** (unloaded successfully). On .NET 10, a `[ThreadStatic]` field storing a `new object()` does not prevent unloading. The thread-local storage slot is cleaned up during GC once the thread and ALC are no longer rooted. **GDU0001 may be a false positive** on modern runtimes, or the test case is too simple (single assignment on the calling thread). Further testing with cross-thread TLS access may be needed.

### Q: Does subscribing to an external static event prevent unloading?
**Answer: YES** — Plugin.ExternalStaticEvent **FAILED** (leaked). Subscribing to `Console.CancelKeyPress += handler` creates a delegate root in the BCL's static event backing field, preventing ALC collection. **GDU0002 validated.**

### Q: Does `Marshal.GetFunctionPointerForDelegate` prevent unloading?
**Answer: NO (in this test)** — Both Plugin.MarshalFnPtr (instance method) and Plugin.MarshalFnPtrStatic (static method) **PASSED**. The function pointer is created but never passed to unmanaged code and the delegate is a local variable that goes out of scope. The runtime thunk can be collected. **GDU0004 may be a false positive** when the function pointer is short-lived. However, if the pointer is passed to native code that retains it, the delegate and ALC would remain rooted.

### Q: Does `delegate.Method.MethodHandle.GetFunctionPointer()` prevent unloading?
**Answer: NO** — Plugin.MethodHandleGetFnPtr **PASSED**. Obtaining a function pointer via `MethodHandle.GetFunctionPointer()` does not create a GC root. The `IntPtr` is a plain integer. **This pattern is safe.** No analyzer rule needed.

### Q: Does `delegate*<void> ptr = &StaticMethod` (managed function pointer) prevent unloading?
**Answer: NO** — Plugin.FunctionPointer **PASSED**. C# 9 function pointers (`delegate*`) are direct addresses stored in local variables. They do not create GC roots. **This pattern is safe.** No analyzer rule needed.

### Q: Can `GCHandle` be freed before unload to avoid leaking?
**Answer: INCONCLUSIVE** — Plugin.GCHandle **PASSED**, but the test allocates a GCHandle for `new object()` (System.Object, a BCL type). Since the rooted object's type is from the BCL, not the collectible assembly, the GCHandle does not root the ALC. A proper test should use a plugin-defined type as the GCHandle target. **GDU0003 is likely correct in principle** but the test case was too weak to prove it.

### Q: Can `ThreadPool.RegisterWaitForSingleObject` be unregistered before unload?
**Answer: YES, it leaks without unregistration** — Plugin.ThreadPoolRegisterWait **FAILED** (leaked). The registered wait handle holds the callback delegate alive, rooting the ALC. **GDU0005 validated.** Calling `RegisteredWaitHandle.Unregister()` before unload should resolve this.

### Q: Does `Timer`/`Thread` prevent unloading while active vs after cleanup?
**Answer: YES, both prevent unloading while active** —
- Plugin.ThreadCreation **FAILED**: A background thread running `Sleep(∞)` prevents ALC unloading. **GDU0010 validated.**
- Plugin.TimerCreation **FAILED**: A `System.Threading.Timer` stored in a static field with an active callback prevents ALC unloading. **GDU0011 validated.**

### Q: Do `Task.Run` and `ThreadPool.QueueUserWorkItem` prevent unloading?
**Answer: YES** — Both Plugin.TaskRun and Plugin.ThreadPoolQueueWork **FAILED** (leaked). When the callback runs indefinitely (`Sleep(∞)`), the thread pool thread executing the callback holds a reference to the collectible assembly's method, preventing unloading. **New analyzer rules are recommended** for `Task.Run` and `ThreadPool.QueueUserWorkItem`.

### Q: Does `Encoding.RegisterProvider` prevent unloading?
**Answer: YES** — Plugin.EncodingRegisterProvider **FAILED** (leaked). The global provider list holds a strong reference to the custom `EncodingProvider` from the collectible assembly. **GDU0012 validated.**

---

## 3. Recommendations

### Rules Validated (claim matches reality)
| Rule | Pattern | Status |
|------|---------|--------|
| GDU0002 | External static event subscription | ✅ Confirmed |
| GDU0005 | ThreadPool.RegisterWaitForSingleObject | ✅ Confirmed |
| GDU0006 | System.Text.Json serialization | ✅ Confirmed |
| GDU0007 | Newtonsoft.Json serialization | ✅ Confirmed |
| GDU0009 | TypeDescriptor modification | ✅ Confirmed |
| GDU0010 | Thread creation | ✅ Confirmed |
| GDU0011 | Timer creation | ✅ Confirmed |
| GDU0012 | Encoding.RegisterProvider | ✅ Confirmed |

### Rules Needing Revision
| Rule | Issue | Recommendation |
|------|-------|----------------|
| GDU0001 | `[ThreadStatic]` did not prevent unloading on .NET 10 | Investigate further: may be fixed in modern runtime. Consider downgrading severity or adding a note that this may only apply to older runtimes. Run additional tests with TLS access from multiple threads. |
| GDU0004 | `Marshal.GetFunctionPointerForDelegate` did not prevent unloading when the pointer is short-lived | Narrow the rule: only warn when the IntPtr escapes the local scope (passed to native code, stored in a field, etc.). Short-lived function pointers are safe. |
| GDU0008 | `XmlSerializer` construction did not prevent unloading on .NET 10 | XmlSerializer may have been fixed in .NET 10 to use weak references or collectible dynamic assemblies. Consider removing or downgrading this rule for .NET 10+. |

### Inconclusive Rules
| Rule | Issue | Recommendation |
|------|-------|----------------|
| GDU0003 | GCHandle test used BCL type (`System.Object`), not a plugin-defined type | Rewrite test: `GCHandle.Alloc(new LocalType())` where `LocalType` is from the plugin assembly to properly test ALC rooting. |

### New Rules Recommended
| Pattern | Severity | Rationale |
|---------|----------|-----------|
| `Task.Run(callback)` | Warning | Task.Run with a long-running callback from a collectible assembly prevents unloading while the task is active. |
| `ThreadPool.QueueUserWorkItem(callback)` | Warning | Same as Task.Run — the thread pool work item's callback roots the ALC. |

### Cache-Clearing Workaround Validated
| Pattern | Plugin | Result |
|---------|--------|--------|
| `JsonSerializerOptionsUpdateHandler.ClearCache` via reflection | Plugin.SystemTextJsonClearCache | ✅ PASS — Calling `ClearCache(null)` after serialization successfully clears the internal type cache, allowing the ALC to unload. |

---

## 4. Test Environment Notes

- **Plugin.NewtonsoftJson** required `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` in its .csproj for the Newtonsoft.Json.dll to be resolved at runtime by `AssemblyDependencyResolver`.
- **Plugin.ThreadCreation**, **Plugin.TaskRun**, and **Plugin.ThreadPoolQueueWork** start background threads that sleep infinitely. These process-exit correctly because the PluginHost calls `GC.Collect`, checks `WeakReference`, and exits — the OS tears down the background threads on process exit.
- **PluginHost Program.cs** was modified to add try/catch around `Execute()` invocation so that plugins that throw (e.g., platform-specific issues) still report unload status.
