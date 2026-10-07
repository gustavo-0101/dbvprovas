using Microsoft.Extensions.Compliance.Classification;

namespace Dbvprovas.Api.Modules.Privacy;

// RN-PRV-001, PC-03, D-116
public static class DataTaxonomy
{
    public static string Name => "dbvprovas";
    public static DataClassification PersonalData => new(Name, nameof(PersonalData));
}

public sealed class PersonalDataAttribute() : DataClassificationAttribute(DataTaxonomy.PersonalData);
