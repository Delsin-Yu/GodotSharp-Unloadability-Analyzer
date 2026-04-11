using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Plugin.EncodingRegisterProviderCleanup;

public class CustomProvider : EncodingProvider
{
    public override Encoding? GetEncoding(int codepage) => null;
    public override Encoding? GetEncoding(string name) => null;
}

public class TestCase
{
    public void Execute()
    {
        Encoding.RegisterProvider(new CustomProvider());
        RemoveCollectibleProviders();

        // Post-cleanup verification: built-in encodings should still work
        var utf8 = System.Text.Encoding.GetEncoding("utf-8");
        if (utf8 == null)
            throw new InvalidOperationException("Post-cleanup Encoding.GetEncoding(utf-8) returned null");

        var utf8ByCodepage = System.Text.Encoding.GetEncoding(65001);
        if (utf8ByCodepage == null)
            throw new InvalidOperationException("Post-cleanup Encoding.GetEncoding(65001) returned null");

        var testString = "Hello, 世界! 🌍";
        var encoded = utf8.GetBytes(testString);
        var decoded = utf8.GetString(encoded);
        if (decoded != testString)
            throw new InvalidOperationException($"Post-cleanup UTF-8 encode/decode failed: got '{decoded}'");
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
