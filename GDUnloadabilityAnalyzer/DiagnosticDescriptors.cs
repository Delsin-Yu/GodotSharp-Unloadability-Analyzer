using Microsoft.CodeAnalysis;

namespace GDUnloadabilityAnalyzer;

internal static class DiagnosticDescriptors
{
    private const string Category = "Unloadability";
    private const string HelpLinkUri = "https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability#troubleshoot-unloadability-issues";

    // ── Category A — Reference-Escaping API Calls ───────────────────────

    public static readonly DiagnosticDescriptor GDU0001_SubscriptionToExternalStaticEvent = new(
        id: "GDU0001",
        title: "Subscription to external static event",
        messageFormat: "Subscribing to external static event '{0}.{1}' creates a delegate root that prevents AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Subscribing to events on shared types like AppDomain, TaskScheduler, or Console creates delegate roots that prevent the collectible assembly from being unloaded. Unsubscribe from the event before the AssemblyLoadContext unloads to avoid leaking.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0002_GCHandleAlloc = new(
        id: "GDU0002",
        title: "GCHandle.Alloc usage",
        messageFormat: "GCHandle.Alloc creates a strong GC root that prevents AssemblyLoadContext unload; ensure the handle is freed before unloading",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Normal and Pinned GCHandles create strong GC roots. If the handle is not freed before unloading, the collectible assembly cannot be reclaimed. Suppress this warning if you are sure the handle will be freed before unloading.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0003_ThreadPoolRegisterWaitForSingleObject = new(
        id: "GDU0003",
        title: "ThreadPool.RegisterWaitForSingleObject usage",
        messageFormat: "ThreadPool.RegisterWaitForSingleObject with a callback from a collectible assembly prevents AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "RegisteredWaitHandle holds a reference to the callback delegate, keeping the collectible assembly rooted and preventing unload. Call RegisteredWaitHandle.Unregister before the AssemblyLoadContext unloads to avoid leaking.",
        helpLinkUri: HelpLinkUri);

    // ── Category B — Known Problematic Type-Caching APIs ────────────────

    public static readonly DiagnosticDescriptor GDU0004_SystemTextJsonSerialization = new(
        id: "GDU0004",
        title: "System.Text.Json serialization",
        messageFormat: "System.Text.Json serialization caches Type references internally, preventing AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The internal JsonSerializerOptions cache holds Type references from the collectible assembly, preventing it from being unloaded. It may be possible to clear the cache via reflection before unloading.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0005_NewtonsoftJsonSerialization = new(
        id: "GDU0005",
        title: "Newtonsoft.Json serialization",
        messageFormat: "Newtonsoft.Json serialization caches contract metadata internally, preventing AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The internal contract resolver in Newtonsoft.Json caches type metadata, preventing the collectible assembly from being unloaded. It may be possible to clear the cache via reflection before unloading.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0006_TypeDescriptorModification = new(
        id: "GDU0006",
        title: "TypeDescriptor modification",
        messageFormat: "TypeDescriptor.{0} registers type metadata in a global store, preventing AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "TypeDescriptor registers type metadata in global stores (e.g. via AddProvider, AddAttributes, or Refresh) that are never cleared, preventing the collectible assembly from being unloaded. This is mainly used by WinForms/WPF design-time infrastructure and is rare in Godot projects.",
        helpLinkUri: HelpLinkUri);

    // ── Category C — Thread/Timer/Task ──────────────────────────────────

    public static readonly DiagnosticDescriptor GDU0007_ThreadCreation = new(
        id: "GDU0007",
        title: "Thread creation",
        messageFormat: "Creating a Thread with a method from a collectible assembly prevents AssemblyLoadContext unload while the thread is running",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Threads running methods from a collectible assembly prevent the AssemblyLoadContext from being unloaded until the thread completes. Suppress this warning if you ensure the thread exits before unloading.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0008_TimerCreation = new(
        id: "GDU0008",
        title: "Timer creation",
        messageFormat: "Creating a Timer with a callback from a collectible assembly keeps the assembly methods referenced, preventing AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Timer callbacks hold references to methods from the collectible assembly, preventing the AssemblyLoadContext from being unloaded. Dispose the Timer before unloading to avoid leaking.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0010_TaskRun = new(
        id: "GDU0010",
        title: "Task.Run usage",
        messageFormat: "Task.Run with a callback from a collectible assembly prevents AssemblyLoadContext unload while the task is running",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Task.Run schedules a callback on the thread pool. While the task is executing, the callback delegate roots the collectible assembly, preventing unload. Suppress this warning if you ensure the task completes before unloading.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0011_ThreadPoolQueueUserWorkItem = new(
        id: "GDU0011",
        title: "ThreadPool.QueueUserWorkItem usage",
        messageFormat: "ThreadPool.QueueUserWorkItem with a callback from a collectible assembly prevents AssemblyLoadContext unload while the work item is executing",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "ThreadPool.QueueUserWorkItem schedules a callback. While executing, the callback delegate roots the collectible assembly, preventing unload. Suppress this warning if you ensure the work item completes before unloading.",
        helpLinkUri: HelpLinkUri);

    // ── Category D — Global Registration ────────────────────────────────

    public static readonly DiagnosticDescriptor GDU0009_EncodingRegisterProvider = new(
        id: "GDU0009",
        title: "Encoding.RegisterProvider usage",
        messageFormat: "Encoding.RegisterProvider registers an EncodingProvider in a global list, preventing AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Encoding.RegisterProvider adds the provider to a global list that is never cleared, preventing the collectible assembly from being unloaded. There is no way to unregister a provider; avoid this call in collectible assemblies.",
        helpLinkUri: HelpLinkUri);
}
