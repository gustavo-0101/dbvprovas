using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Contracts;

public sealed class ContractJsonTests
{
    // Enums dos contratos viajam só como texto (D-100).
    [Fact]
    public void Contract_enums_serialize_as_text()
    {
        Assert.Equal("\"AssociateDirector\"", JsonSerializer.Serialize(RoleCode.AssociateDirector, JsonSerializerOptions.Web));
        Assert.Equal("\"Suspended\"", JsonSerializer.Serialize(MembershipStatus.Suspended, JsonSerializerOptions.Web));
        Assert.Equal(ReasonCode.TemporaryLeave, JsonSerializer.Deserialize<ReasonCode>("\"TemporaryLeave\"", JsonSerializerOptions.Web));
        foreach (var type in typeof(RoleCode).Assembly.GetExportedTypes().Where(t => t.IsEnum))
            Assert.Equal(typeof(StrictEnumConverter<>).MakeGenericType(type), type.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType);
    }

    // Número no lugar do nome é recusado: um valor fora da lista nunca vira cargo ou estado (D-100, RN-ORG-001).
    [Theory]
    [InlineData("999")]
    [InlineData("0")]
    public void Contract_enums_refuse_numbers(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RoleCode>(json, JsonSerializerOptions.Web));
}
