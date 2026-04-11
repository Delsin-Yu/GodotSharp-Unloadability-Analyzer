# Validation Results

> **Date**: 2026-04-12  
> **Runtime**: .NET 10.0 (net10.0 TFM)  
> **Platform**: Windows x64  
> **Test Harness**: PluginHost with collectible AssemblyLoadContext  
> **Suite Size**: 34 plugins

## Results Table

| # | Plugin | Rule | Pattern | Expected | Actual | Match |
|---|--------|------|---------|----------|--------|-------|
| 1 | Plugin.Baseline | — | No-op baseline | PASS | PASS | ✅ |
| 2 | Plugin.ThreadStatic | GDU0001 | `[ThreadStatic]` field assignment | FAIL | **PASS** | ❌ |
| 3 | Plugin.ExternalStaticEvent | GDU0002 | Subscribe to `Console.CancelKeyPress` | FAIL | FAIL | ✅ |
| 4 | Plugin.GCHandle | GDU0003 | `GCHandle.Alloc(new LocalData[], GCHandleType.Pinned)` stored in static field without Free | FAIL | FAIL | ✅ |
| 4a | Plugin.GCHandleCleanup | GDU0003 | `GCHandle.Alloc(new LocalData[], GCHandleType.Pinned)` + `handle.Free()` | PASS | PASS | ✅ |
| 5 | Plugin.MarshalFnPtr | GDU0004 | `Marshal.GetFunctionPointerForDelegate` (instance method) | FAIL | **PASS** | ❌ |
| 6 | Plugin.ThreadPoolRegisterWait | GDU0005 | `ThreadPool.RegisterWaitForSingleObject` | FAIL | FAIL | ✅ |
| 7 | Plugin.SystemTextJson | GDU0006 | `JsonSerializer.Serialize(localType)` | FAIL | FAIL | ✅ |
| 8 | Plugin.NewtonsoftJson | GDU0007 | `JsonConvert.SerializeObject(localType)` | FAIL | FAIL | ✅ |
| 9 | Plugin.NewtonsoftJsonBruteForceClear | GDU0007 | `JsonConvert.SerializeObject(localType)` + broader ComponentModel cache clearing | PASS | PASS | ✅ |
| 10 | Plugin.NewtonsoftJsonPinPointClear | GDU0007 | `JsonConvert.SerializeObject(localType)` + public refresh + 3 runtime-internal ComponentModel roots | PASS | PASS | ✅ |
| 11 | Plugin.XmlSerializer | GDU0008 | `new XmlSerializer(typeof(LocalType))` | FAIL | **PASS** | ❌ |
| 12 | Plugin.TypeDescriptor | GDU0009 | `TypeDescriptor.AddProvider(...)` | FAIL | FAIL | ✅ |
| 12a | Plugin.TypeDescriptorCleanup | GDU0009 | `TypeDescriptor.AddProvider(...)` + `RemoveProvider` + cache cleanup | PASS | PASS | ✅ |
| 13 | Plugin.ThreadCreation | GDU0010 | `new Thread(Sleep(∞)).Start()` | FAIL | FAIL | ✅ |
| 14 | Plugin.TimerCreation | GDU0011 | `new Timer(callback, 0, 1000)` stored in static field | FAIL | FAIL | ✅ |
| 15 | Plugin.EncodingRegisterProvider | GDU0012 | `Encoding.RegisterProvider(customProvider)` | FAIL | FAIL | ✅ |
| 15a | Plugin.EncodingRegisterProviderCleanup | GDU0012 | `Encoding.RegisterProvider(...)` + remove from `EncodingProvider.s_providers` via reflection | PASS | PASS | ✅ |
| 16 | Plugin.TaskRun | — | `Task.Run(() => Sleep(∞))` | FAIL | FAIL | ✅ |
| 17 | Plugin.ThreadPoolQueueWork | — | `ThreadPool.QueueUserWorkItem(_ => Sleep(∞))` | FAIL | FAIL | ✅ |
| 18 | Plugin.MarshalFnPtrStatic | GDU0004 | `Marshal.GetFunctionPointerForDelegate` (static method) | FAIL | **PASS** | ❌ |
| 19 | Plugin.MethodHandleGetFnPtr | — | `delegate.Method.MethodHandle.GetFunctionPointer()` | ? | PASS | ℹ️ |
| 20 | Plugin.FunctionPointer | — | `delegate*<void> ptr = &StaticMethod` | ? | PASS | ℹ️ |

**Legend**: ✅ = claim matches reality, ❌ = claim does NOT match reality, ⚠️ = inconclusive (test limitation), ℹ️ = exploratory (no existing claim)

### Score: 10/14 rules validated (71%), 4 false positives, 0 inconclusive

---

## Cache-Clearing Workarounds

| Pattern | Plugin | Result |
|---------|--------|--------|
| `JsonSerializerOptionsUpdateHandler.ClearCache` via reflection | Plugin.SystemTextJsonClearCache | ✅ PASS — Calling `ClearCache(null)` after serialization successfully clears the internal type cache, allowing the ALC to unload. |
| Public refresh, then `TypeDescriptor._defaultProviderInitialized`, `ReflectTypeDescriptionProvider._typeData`, and `ReflectTypeDescriptionProvider.s_attributeCache` | Plugin.NewtonsoftJsonPinPointClear | ✅ PASS — This is the narrowest passing cleanup validated on .NET 10. The boundary depends on runtime-internal `System.ComponentModel` state and is not a public compatibility guarantee. |
| `ReflectTypeDescriptionProvider` roots plus `TypeDescriptor.s_providerTable` and `TypeDescriptor.s_defaultProviderInitialized` | Plugin.NewtonsoftJsonBruteForceClear | ✅ PASS — Historical broader workaround retained as a baseline for comparison with the pinpoint net10 result. |

| Remove collectible provider from `EncodingProvider.s_providers` via reflection | Plugin.EncodingRegisterProviderCleanup | ✅ PASS — Reflect into the private `s_providers` array on `EncodingProvider`, filter out providers whose type belongs to the collectible assembly, and replace the array. Atomic for readers since `Encoding.GetEncoding` snapshots the array reference. |
| Public `RemoveProvider` + `Refresh` + prune `_defaultProviderInitialized`, `_providerTable`, `_typeData` | Plugin.TypeDescriptorCleanup | ✅ PASS — `RemoveProvider` removes the explicitly-added provider node; the remaining cache cleanup prunes secondary roots. `s_attributeCache` may be absent on some .NET 10 builds. |

---

## Extended Validation Tests

These plugins validate that the reflection-based cleanup workarounds do not break API functionality for non-collectible (BCL) types.

### Post-Cleanup Verification

All selective cleanup plugins (PinPoint, STJ ClearCache, TypeDescriptor, Encoding) now include post-cleanup verification within their `Execute()` method. After cleanup, they verify that BCL-type serialization, TypeDescriptor queries, and encoding operations still function correctly.

**Key finding**: `Plugin.NewtonsoftJsonBruteForceClear` PASSES its unload check but produces an EXCEPTION during post-cleanup BCL serialization — confirming that brute-force cache clearing destroys shared BCL metadata. This validates the pinpoint approach as the correct strategy.

### Complex Type Graph Tests

| Plugin | Pattern | Result |
|--------|---------|--------|
| Plugin.NewtonsoftJsonPinPointClearComplex | Polymorphic types + custom JsonConverter + cleanup + BCL verification | PASS |
| Plugin.SystemTextJsonClearCacheComplex | Custom JsonConverter\<T> + cleanup + BCL verification | PASS |

### Multi-Cycle Load/Unload Tests

These plugins test sequential load/unload cycles to verify cleanup doesn't corrupt state for subsequent plugins.

| Plugin Pair | Pattern | Result |
|-------------|---------|--------|
| NewtonsoftJsonPinPointClearCycleA → CycleB | Serialize LocalTypeA, cleanup → Serialize LocalTypeB, cleanup + BCL verification | Both PASS |
| TypeDescriptorCycleA → CycleB | AddProvider for LocalTypeA, cleanup → AddProvider for LocalTypeB, cleanup + BCL verification | Both PASS |
| EncodingCycleA → EncodingCycleB | RegisterProvider for ProviderA, cleanup → RegisterProvider for ProviderB, cleanup + encoding verification | Both PASS |

### Concurrent Stress Tests

| Plugin | Pattern | Result |
|--------|---------|--------|
| Plugin.NewtonsoftJsonPinPointClearConcurrent | 4 background threads serializing BCL types + main thread cleanup | PASS (no thread exceptions) |
| Plugin.SystemTextJsonClearCacheConcurrent | 4 background threads serializing BCL types + main thread ClearCache | PASS (no thread exceptions) |

---

## Why Only TypeDescriptor Roots Matter

Newtonsoft.Json's own internal caches (`DefaultContractResolver._contractCache`, `JsonTypeReflector` caches, `CachedAttributeGetter`, `ConvertUtils.CastConverters`, etc.) all use `ThreadSafeStore<TKey, TValue>` backed by `ConcurrentDictionary` with **strong** key references. However, when Newtonsoft.Json is loaded into the collectible ALC via `CopyLocalLockFileAssemblies=true`, all of these caches live **inside** the collectible ALC's heap. They are collected along with the ALC and do not constitute external roots.

The only cross-ALC cache pollution occurs when Newtonsoft.Json's contract resolution path calls into the shared framework:
- `DefaultContractResolver.CreateContract` → `JsonTypeReflector.CanTypeDescriptorConvertString` → `TypeDescriptor.GetConverter(pluginType)`
- `ConvertUtils.TryConvertInternal` → `TypeDescriptor.GetConverter(type)` (deserialization path)

Both paths populate `System.ComponentModel.TypeDescriptor` state in the root (non-collectible) ALC. The three runtime-internal roots cleared by `Plugin.NewtonsoftJsonPinPointClear` are precisely the caches populated through this cross-ALC boundary.

**Caveat**: If Newtonsoft.Json is loaded into the **default/shared ALC** (e.g., by a host that pre-loads it), then `DefaultContractResolver._contractCache` becomes an additional root that would also need clearing.
