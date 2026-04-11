using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Plugin.EncodingCycleA;

public class CustomProviderA : EncodingProvider
{
    public override Encoding? GetEncoding(int codepage) => null;
    public override Encoding? GetEncoding(string name) => null;
}

public class TestCase
{
    public void Execute()
    {
        Encoding.RegisterProvider(new CustomProviderA());
        RemoveCollectibleProviders();
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
