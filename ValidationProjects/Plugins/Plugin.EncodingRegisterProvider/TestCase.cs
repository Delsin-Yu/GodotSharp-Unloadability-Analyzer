using System.Text;

namespace Plugin.EncodingRegisterProvider;

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
    }
}
