namespace MITANZ360Pro.Web.Modules.ReferenceData;

/// <summary>Built-in seed catalogue for Import Defaults (Category + Code unique).</summary>
public static class ReferenceDataDefaults
{
    public static IReadOnlyList<(string Category, string Code, string Title)> GetSeedRows()
    {
        var rows = new List<(string, string, string)>();

        void Add(string category, string code, string title)
            => rows.Add((category, code, title));

        // Nationality
        Add("Nationality", "LK", "Sri Lankan");
        Add("Nationality", "NZ", "New Zealander");
        Add("Nationality", "AU", "Australian");
        Add("Nationality", "IN", "Indian");
        Add("Nationality", "CA", "Canadian");
        Add("Nationality", "GB", "British");
        Add("Nationality", "US", "American");
        Add("Nationality", "SG", "Singaporean");
        Add("Nationality", "MY", "Malaysian");
        Add("Nationality", "JP", "Japanese");

        // Country
        Add("Country", "LK", "Sri Lanka");
        Add("Country", "NZ", "New Zealand");
        Add("Country", "AU", "Australia");
        Add("Country", "IN", "India");
        Add("Country", "CA", "Canada");
        Add("Country", "GB", "United Kingdom");
        Add("Country", "US", "United States");
        Add("Country", "SG", "Singapore");
        Add("Country", "MY", "Malaysia");
        Add("Country", "JP", "Japan");

        // PreferredCountry
        Add("PreferredCountry", "NZ", "New Zealand");
        Add("PreferredCountry", "AU", "Australia");
        Add("PreferredCountry", "CA", "Canada");
        Add("PreferredCountry", "UK", "United Kingdom");
        Add("PreferredCountry", "IE", "Ireland");
        Add("PreferredCountry", "US", "United States");
        Add("PreferredCountry", "SG", "Singapore");

        // PreferredQualification
        Add("PreferredQualification", "CERT", "Certificate");
        Add("PreferredQualification", "DIP", "Diploma");
        Add("PreferredQualification", "ADIP", "Advanced Diploma");
        Add("PreferredQualification", "UGDIP", "Graduate Diploma");
        Add("PreferredQualification", "PGDIP", "Postgraduate Diploma");
        Add("PreferredQualification", "BACH", "Bachelor's Degree");
        Add("PreferredQualification", "MAST", "Master's Degree");
        Add("PreferredQualification", "MBA", "Master of Business Administration");
        Add("PreferredQualification", "PHD", "Doctor of Philosophy");

        // PreferredCourse
        Add("PreferredCourse", "IT", "Information Technology");
        Add("PreferredCourse", "CS", "Computer Science");
        Add("PreferredCourse", "CYBER", "Cyber Security");
        Add("PreferredCourse", "CLOUD", "Cloud Computing");
        Add("PreferredCourse", "AI", "Artificial Intelligence");
        Add("PreferredCourse", "DS", "Data Science");
        Add("PreferredCourse", "BUS", "Business Management");
        Add("PreferredCourse", "ACC", "Accounting");
        Add("PreferredCourse", "ENG", "Engineering");
        Add("PreferredCourse", "NURS", "Nursing");
        Add("PreferredCourse", "HEALTH", "Health Sciences");
        Add("PreferredCourse", "EDU", "Education");
        Add("PreferredCourse", "HOSP", "Hospitality Management");
        Add("PreferredCourse", "COOK", "Commercial Cookery");
        Add("PreferredCourse", "EARLY", "Early Childhood Education");

        // PreferredIntake
        Add("PreferredIntake", "JAN", "January");
        Add("PreferredIntake", "FEB", "February");
        Add("PreferredIntake", "MAR", "March");
        Add("PreferredIntake", "APR", "April");
        Add("PreferredIntake", "MAY", "May");
        Add("PreferredIntake", "JUN", "June");
        Add("PreferredIntake", "JUL", "July");
        Add("PreferredIntake", "AUG", "August");
        Add("PreferredIntake", "SEP", "September");
        Add("PreferredIntake", "OCT", "October");
        Add("PreferredIntake", "NOV", "November");
        Add("PreferredIntake", "DEC", "December");

        // HighestQualification
        Add("HighestQualification", "NONE", "No Formal Qualification");
        Add("HighestQualification", "PRIMARY", "Primary School");
        Add("HighestQualification", "OLEVEL", "O/L or GCSE");
        Add("HighestQualification", "ALEVEL", "A/L or High School");
        Add("HighestQualification", "CERT", "Certificate");
        Add("HighestQualification", "DIP", "Diploma");
        Add("HighestQualification", "ADIP", "Advanced Diploma");
        Add("HighestQualification", "HND", "Higher National Diploma");
        Add("HighestQualification", "BACH", "Bachelor's Degree");
        Add("HighestQualification", "PGDIP", "Postgraduate Diploma");
        Add("HighestQualification", "MAST", "Master's Degree");
        Add("HighestQualification", "PHD", "Doctorate");

        return rows;
    }
}
