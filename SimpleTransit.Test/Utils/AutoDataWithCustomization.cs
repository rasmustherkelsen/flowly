using AutoFixture;
using AutoFixture.Xunit2;

namespace SimpleTransit.Test.Utils;

public class AutoDataWithCustomizationAttribute(params Type[] types) : AutoDataAttribute(() =>
{
    if (types.Length == 0) throw new ArgumentException("At least one type must be provided", nameof(types));

    var fixture = new Fixture();
    foreach (var type in types)
    {
        fixture.Customize((ICustomization)Activator.CreateInstance(type)!);
    }

    return fixture;
});