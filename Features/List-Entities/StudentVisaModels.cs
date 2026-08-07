using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MITANZ360Pro.Web.Modules.Entities;

/// <summary>Public student visa registration upsert context (audit + request metadata).</summary>
public sealed class StudentVisaSaveContext
{
    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? BaseUrl { get; set; }
}

public sealed class StudentVisaSaveResult
{
    public Entity Entity { get; set; } = new();

    public bool WasCreated { get; set; }

    public string VerificationToken { get; set; } = "";

    public string ConfirmationUrl { get; set; } = "";
}

public static class StudentVisaMetadataKeys
{
    public const string FirstName = "FirstName";
    public const string LastName = "LastName";
    public const string Gender = "Gender";
    public const string DOB = "DOB";
    public const string Nationality = "Nationality";
    public const string Passport = "Passport";
    public const string Phone = "Phone";
    public const string WhatsAppNumber = "WhatsAppNumber";
    public const string Email = "Email";
    public const string CurrentAddress = "CurrentAddress";
    public const string Country = "Country";
    public const string City = "City";
    public const string PreferredContactMethod = "PreferredContactMethod";
    public const string PreferredCountry = "PreferredCountry";
    public const string PreferredQualification = "PreferredQualification";
    public const string PreferredCourse = "PreferredCourse";
    public const string PreferredIntake = "PreferredIntake";
    public const string EnglishTest = "EnglishTest";
    public const string IELTSScore = "IELTSScore";
    public const string HighestQualification = "HighestQualification";
    public const string CurrentOccupation = "CurrentOccupation";
    public const string ReferralCode = "ReferralCode";
    public const string ReferralPartner = "ReferralPartner";
    public const string AgreeUpdates = "AgreeUpdates";
    public const string EmailVerified = "EmailVerified";
    public const string EmailVerifiedDate = "EmailVerifiedDate";
}

public static class ReferralCodeHelper
{
    private static readonly Regex ValidCode = new(@"^[A-Z0-9]{2,20}$", RegexOptions.Compiled);

    /// <summary>Trim, uppercase, and validate referral code. Returns null if empty/invalid.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var code = raw.Trim().ToUpperInvariant();
        return ValidCode.IsMatch(code) ? code : null;
    }

    public static bool IsValid(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true; // optional
        }

        return Normalize(raw) != null;
    }
}

public static class StudentVisaMetadataHelper
{
    public static string GetString(Dictionary<string, object?> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value == null)
        {
            return string.Empty;
        }

        return value switch
        {
            JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString() ?? string.Empty,
            JsonElement json when json.ValueKind == JsonValueKind.True => "true",
            JsonElement json when json.ValueKind == JsonValueKind.False => "false",
            JsonElement json when json.ValueKind == JsonValueKind.Number => json.GetRawText(),
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? string.Empty
        };
    }

    public static bool GetBool(Dictionary<string, object?> metadata, string key)
    {
        var text = GetString(metadata, key);
        return text.Equals("true", StringComparison.OrdinalIgnoreCase)
               || text == "1"
               || text.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public static void Set(Dictionary<string, object?> metadata, string key, object? value)
    {
        if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
        {
            metadata.Remove(key);
            return;
        }

        metadata[key] = value;
    }
}
