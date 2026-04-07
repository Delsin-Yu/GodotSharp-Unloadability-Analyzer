using Microsoft.CodeAnalysis;

namespace GDUnloadabilityAnalyzer;

internal static class DiagnosticDescriptors
{
    private const string Category = "Unloadability";
    private const string HelpLinkUri = "https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability#troubleshoot-unloadability-issues";

    // ── Category A — Declaration-Level Hazards ──────────────────────────

    public static readonly DiagnosticDescriptor GDU0001_ThreadStaticField = new(
        id: "GDU0001",
        title: "ThreadStatic field",
        messageFormat: "[ThreadStatic] field '{0}' is not supported on collectible assemblies and will prevent unloading",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Fields marked with [ThreadStatic] are stored in thread-local storage which is not compatible with collectible assemblies.",
        helpLinkUri: HelpLinkUri);

    // ── Category B — Reference-Escaping API Calls ───────────────────────

    public static readonly DiagnosticDescriptor GDU0002_SubscriptionToExternalStaticEvent = new(
        id: "GDU0002",
        title: "Subscription to external static event",
        messageFormat: "Subscribing to external static event '{0}.{1}' creates a delegate root that prevents AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Subscribing to events on shared types like AppDomain, TaskScheduler, or Console creates delegate roots that prevent the collectible assembly from being unloaded.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0003_GCHandleAlloc = new(
        id: "GDU0003",
        title: "GCHandle.Alloc usage",
        messageFormat: "GCHandle.Alloc creates a strong GC root that prevents AssemblyLoadContext unload; ensure the handle is freed before unloading",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Normal and Pinned GCHandles create strong GC roots. If the handle is not freed before unloading, the collectible assembly cannot be reclaimed.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0004_MarshalGetFunctionPointerForDelegate = new(
        id: "GDU0004",
        title: "Marshal.GetFunctionPointerForDelegate usage",
        messageFormat: "Marshal.GetFunctionPointerForDelegate creates an unmanaged function pointer that prevents the delegate and its assembly from being collected",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Creating an unmanaged function pointer from a delegate prevents the delegate from being garbage collected, blocking AssemblyLoadContext unload.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0005_ThreadPoolRegisterWaitForSingleObject = new(
        id: "GDU0005",
        title: "ThreadPool.RegisterWaitForSingleObject usage",
        messageFormat: "ThreadPool.RegisterWaitForSingleObject with a callback from a collectible assembly prevents AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "RegisteredWaitHandle holds a reference to the callback delegate, keeping the collectible assembly rooted and preventing unload.",
        helpLinkUri: HelpLinkUri);

    // ── Category C — Known Problematic Type-Caching APIs ────────────────

    public static readonly DiagnosticDescriptor GDU0006_SystemTextJsonSerialization = new(
        id: "GDU0006",
        title: "System.Text.Json serialization",
        messageFormat: "System.Text.Json serialization caches Type references internally, preventing AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The internal JsonSerializerOptions cache holds Type references from the collectible assembly, preventing it from being unloaded.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0007_NewtonsoftJsonSerialization = new(
        id: "GDU0007",
        title: "Newtonsoft.Json serialization",
        messageFormat: "Newtonsoft.Json serialization caches contract metadata internally, preventing AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The internal contract resolver in Newtonsoft.Json caches type metadata, preventing the collectible assembly from being unloaded.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0008_XmlSerializerConstruction = new(
        id: "GDU0008",
        title: "XmlSerializer construction",
        messageFormat: "XmlSerializer construction generates and caches dynamic serialization assemblies by Type, preventing AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "XmlSerializer generates and caches dynamic serialization assemblies keyed by Type, keeping the collectible assembly rooted.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0009_TypeDescriptorModification = new(
        id: "GDU0009",
        title: "TypeDescriptor modification",
        messageFormat: "TypeDescriptor.{0} registers type metadata in a global store, preventing AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "TypeDescriptor registers type metadata in global stores that are never cleared, preventing the collectible assembly from being unloaded.",
        helpLinkUri: HelpLinkUri);

    // ── Category D — Thread/Timer ───────────────────────────────────────

    public static readonly DiagnosticDescriptor GDU0010_ThreadCreation = new(
        id: "GDU0010",
        title: "Thread creation",
        messageFormat: "Creating a Thread with a method from a collectible assembly prevents AssemblyLoadContext unload while the thread is running",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Threads running methods from a collectible assembly prevent the AssemblyLoadContext from being unloaded until the thread completes.",
        helpLinkUri: HelpLinkUri);

    public static readonly DiagnosticDescriptor GDU0011_TimerCreation = new(
        id: "GDU0011",
        title: "Timer creation",
        messageFormat: "Creating a Timer with a callback from a collectible assembly keeps the assembly methods referenced, preventing AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Timer callbacks hold references to methods from the collectible assembly, preventing the AssemblyLoadContext from being unloaded.",
        helpLinkUri: HelpLinkUri);

    // ── Category E — Global Registration ────────────────────────────────

    public static readonly DiagnosticDescriptor GDU0012_EncodingRegisterProvider = new(
        id: "GDU0012",
        title: "Encoding.RegisterProvider usage",
        messageFormat: "Encoding.RegisterProvider registers an EncodingProvider in a global list, preventing AssemblyLoadContext unload",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Encoding.RegisterProvider adds the provider to a global list that is never cleared, preventing the collectible assembly from being unloaded.",
        helpLinkUri: HelpLinkUri);
}
