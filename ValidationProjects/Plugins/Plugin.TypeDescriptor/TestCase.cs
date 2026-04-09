using System.ComponentModel;

namespace Plugin.TypeDescriptor;

public class LocalType { }

public class LocalTypeDescriptionProvider : TypeDescriptionProvider { }

public class TestCase
{
    public void Execute()
    {
        System.ComponentModel.TypeDescriptor.AddProvider(new LocalTypeDescriptionProvider(), typeof(LocalType));
    }
}
