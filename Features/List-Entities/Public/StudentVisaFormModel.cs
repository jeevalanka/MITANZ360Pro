using System.ComponentModel.DataAnnotations;

namespace MITANZ360Pro.Web.Modules.Entities;

public sealed class StudentVisaFormModel
{
    [Required(ErrorMessage = "First Name is required.")]
    [StringLength(100, ErrorMessage = "First Name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Last Name is required.")]
    [StringLength(100, ErrorMessage = "Last Name cannot exceed 100 characters.")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Gender is required.")]
    public string Gender { get; set; } = "";

    [Required(ErrorMessage = "Date of Birth is required.")]
    public DateTime? DateOfBirth { get; set; }

    [Required(ErrorMessage = "Nationality is required.")]
    [StringLength(100, ErrorMessage = "Nationality cannot exceed 100 characters.")]
    public string Nationality { get; set; } = "";

    [Required(ErrorMessage = "Passport Number is required.")]
    [StringLength(50, MinimumLength = 5, ErrorMessage = "Passport Number must be 5–50 characters.")]
    [RegularExpression(@"^[A-Za-z0-9]{5,20}$", ErrorMessage = "Passport Number must be 5–20 letters or digits.")]
    public string PassportNumber { get; set; } = "";

    [Required(ErrorMessage = "Mobile Number is required.")]
    [StringLength(30, ErrorMessage = "Mobile Number cannot exceed 30 characters.")]
    [RegularExpression(@"^[+]?\d[\d\s\-()]{6,29}$", ErrorMessage = "Enter a valid mobile number.")]
    public string MobileNumber { get; set; } = "";

    [StringLength(30, ErrorMessage = "WhatsApp Number cannot exceed 30 characters.")]
    [RegularExpression(@"^$|^[+]?\d[\d\s\-()]{6,29}$", ErrorMessage = "Enter a valid WhatsApp number.")]
    public string WhatsAppNumber { get; set; } = "";

    [Required(ErrorMessage = "Email Address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(200, ErrorMessage = "Email Address cannot exceed 200 characters.")]
    public string EmailAddress { get; set; } = "";

    [Required(ErrorMessage = "Current Address is required.")]
    [StringLength(500, ErrorMessage = "Current Address cannot exceed 500 characters.")]
    public string CurrentAddress { get; set; } = "";

    [Required(ErrorMessage = "Country is required.")]
    [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters.")]
    public string Country { get; set; } = "";

    [Required(ErrorMessage = "City is required.")]
    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string City { get; set; } = "";

    [Required(ErrorMessage = "Preferred Contact Method is required.")]
    public string PreferredContactMethod { get; set; } = "";

    [Required(ErrorMessage = "Preferred Country is required.")]
    [StringLength(100, ErrorMessage = "Preferred Country cannot exceed 100 characters.")]
    public string PreferredCountry { get; set; } = "";

    [Required(ErrorMessage = "Preferred Qualification is required.")]
    [StringLength(200, ErrorMessage = "Preferred Qualification cannot exceed 200 characters.")]
    public string PreferredQualification { get; set; } = "";

    [Required(ErrorMessage = "Preferred Course is required.")]
    [StringLength(200, ErrorMessage = "Preferred Course cannot exceed 200 characters.")]
    public string PreferredCourse { get; set; } = "";

    [Required(ErrorMessage = "Preferred Intake is required.")]
    [StringLength(100, ErrorMessage = "Preferred Intake cannot exceed 100 characters.")]
    public string PreferredIntake { get; set; } = "";

    public string EnglishTest { get; set; } = "";

    [StringLength(10, ErrorMessage = "IELTS Score cannot exceed 10 characters.")]
    [RegularExpression(@"^$|^\d+(\.\d{1,2})?$", ErrorMessage = "Enter a valid IELTS score (e.g. 6.5).")]
    public string IELTSScore { get; set; } = "";

    [StringLength(200, ErrorMessage = "Highest Qualification cannot exceed 200 characters.")]
    public string HighestQualification { get; set; } = "";

    [StringLength(200, ErrorMessage = "Current Occupation cannot exceed 200 characters.")]
    public string CurrentOccupation { get; set; } = "";

    /// <summary>Key field — set from ?r= or existing record; readonly in UI.</summary>
    [StringLength(20)]
    public string ReferralCode { get; set; } = "";

    public string ReferralPartner { get; set; } = "";

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must confirm the information is accurate.")]
    public bool ConfirmAccurate { get; set; }

    public bool AgreeUpdates { get; set; }

    /// <summary>Honeypot anti-bot field — must stay empty.</summary>
    public string Website { get; set; } = "";

    public Dictionary<string, object?> ToMetadata()
    {
        var meta = new Dictionary<string, object?>
        {
            [StudentVisaMetadataKeys.FirstName] = FirstName.Trim(),
            [StudentVisaMetadataKeys.LastName] = LastName.Trim(),
            [StudentVisaMetadataKeys.Gender] = Gender.Trim(),
            [StudentVisaMetadataKeys.DOB] = DateOfBirth?.ToString("yyyy-MM-dd"),
            [StudentVisaMetadataKeys.Nationality] = Nationality.Trim(),
            [StudentVisaMetadataKeys.Passport] = PassportNumber.Trim().ToUpperInvariant(),
            [StudentVisaMetadataKeys.Phone] = MobileNumber.Trim(),
            [StudentVisaMetadataKeys.WhatsAppNumber] = string.IsNullOrWhiteSpace(WhatsAppNumber) ? null : WhatsAppNumber.Trim(),
            [StudentVisaMetadataKeys.Email] = EmailAddress.Trim().ToLowerInvariant(),
            [StudentVisaMetadataKeys.CurrentAddress] = CurrentAddress.Trim(),
            [StudentVisaMetadataKeys.Country] = Country.Trim(),
            [StudentVisaMetadataKeys.City] = City.Trim(),
            [StudentVisaMetadataKeys.PreferredContactMethod] = PreferredContactMethod.Trim(),
            [StudentVisaMetadataKeys.PreferredCountry] = PreferredCountry.Trim(),
            [StudentVisaMetadataKeys.PreferredQualification] = PreferredQualification.Trim(),
            [StudentVisaMetadataKeys.PreferredCourse] = PreferredCourse.Trim(),
            [StudentVisaMetadataKeys.PreferredIntake] = PreferredIntake.Trim(),
            [StudentVisaMetadataKeys.EnglishTest] = string.IsNullOrWhiteSpace(EnglishTest) ? null : EnglishTest.Trim(),
            [StudentVisaMetadataKeys.IELTSScore] = string.IsNullOrWhiteSpace(IELTSScore) ? null : IELTSScore.Trim(),
            [StudentVisaMetadataKeys.HighestQualification] = string.IsNullOrWhiteSpace(HighestQualification) ? null : HighestQualification.Trim(),
            [StudentVisaMetadataKeys.CurrentOccupation] = string.IsNullOrWhiteSpace(CurrentOccupation) ? null : CurrentOccupation.Trim(),
            [StudentVisaMetadataKeys.AgreeUpdates] = AgreeUpdates
        };

        var referral = ReferralCodeHelper.Normalize(ReferralCode);
        if (referral != null)
        {
            meta[StudentVisaMetadataKeys.ReferralCode] = referral;
        }

        if (!string.IsNullOrWhiteSpace(ReferralPartner))
        {
            meta[StudentVisaMetadataKeys.ReferralPartner] = ReferralPartner.Trim();
        }

        // Remove null entries so template validation skips optional empties cleanly
        return meta
            .Where(kv => kv.Value != null && !(kv.Value is string s && string.IsNullOrWhiteSpace(s)))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public void LoadFromEntity(Entity entity)
    {
        var m = entity.Metadata;
        FirstName = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.FirstName);
        LastName = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.LastName);
        Gender = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.Gender);
        Nationality = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.Nationality);
        PassportNumber = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.Passport);
        MobileNumber = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.Phone);
        WhatsAppNumber = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.WhatsAppNumber);
        EmailAddress = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.Email);
        CurrentAddress = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.CurrentAddress);
        Country = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.Country);
        City = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.City);
        PreferredContactMethod = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.PreferredContactMethod);
        PreferredCountry = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.PreferredCountry);
        PreferredQualification = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.PreferredQualification);
        PreferredCourse = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.PreferredCourse);
        PreferredIntake = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.PreferredIntake);
        EnglishTest = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.EnglishTest);
        IELTSScore = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.IELTSScore);
        HighestQualification = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.HighestQualification);
        CurrentOccupation = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.CurrentOccupation);
        ReferralCode = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.ReferralCode);
        ReferralPartner = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.ReferralPartner);
        AgreeUpdates = StudentVisaMetadataHelper.GetBool(m, StudentVisaMetadataKeys.AgreeUpdates);

        var dob = StudentVisaMetadataHelper.GetString(m, StudentVisaMetadataKeys.DOB);
        if (DateTime.TryParse(dob, out var parsed))
        {
            DateOfBirth = parsed.Date;
        }
    }
}
