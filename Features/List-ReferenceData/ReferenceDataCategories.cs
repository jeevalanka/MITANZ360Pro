namespace MITANZ360Pro.Web.Modules.ReferenceData;

/// <summary>Canonical category names used by app forms.</summary>
public static class ReferenceDataCategories
{
    public const string Gender = "Gender";
    public const string Nationality = "Nationality";
    public const string Country = "Country";
    public const string PreferredContactMethod = "PreferredContactMethod";
    public const string PreferredCountry = "PreferredCountry";
    public const string PreferredQualification = "PreferredQualification";
    public const string PreferredCourse = "PreferredCourse";
    public const string PreferredIntake = "PreferredIntake";
    public const string EnglishTest = "EnglishTest";
    public const string HighestQualification = "HighestQualification";

    public static IReadOnlyList<string> All { get; } =
    [
        Gender,
        Nationality,
        Country,
        PreferredContactMethod,
        PreferredCountry,
        PreferredQualification,
        PreferredCourse,
        PreferredIntake,
        EnglishTest,
        HighestQualification
    ];
}
