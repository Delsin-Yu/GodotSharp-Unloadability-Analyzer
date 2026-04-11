using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Plugin.EncodingCycleB;

public class CustomProviderB : EncodingProvider
{
    public override Encoding? GetEncoding(int codepage) => null;
    public override Encoding? GetEncoding(string name) => null;
}

public class TestCase
{
    public void Execute()
    {
        Encoding.RegisterProvider(new CustomProviderB());
        RemoveCollectibleProviders();

        // Post-cleanup verification: built-in encodings should still work
        var utf8 = Encoding.GetEncoding("utf-8");
        var test = "Hello, 世界!";
        if (utf8.GetString(utf8.GetBytes(test)) != test)
            throw new InvalidOperationException("UTF-8 broken after CycleA cleanup");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RemoveCollectibleProviders()
    {
        var callingAssembly = Assembly.GetCallingAssembly();
        var field = typeof(EncodingProvider).GetField("s_providers", BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new MissingFieldException(nameof(EncodingProvider), "s_providers");

        var providers = (EncodingProvider[]?)field.GetValue(null);
        if (providers is null) return;

        var cleaned = providers.Where(p => !ReferenceEquals(p.GetType().Assembly, callingAssembly)).ToArray();
        field.SetValue(null, cleaned.Length == 0 ? null : cleaned);
    }
}
