using System.Text.Json.Serialization;

namespace MITANZ360Pro.Web.Models;

public class SystemSettings
{
    public ApplicationSettings Application { get; set; } = new();

    public BrandingSettings Branding { get; set; } = new();

    public FeatureSettings Features { get; set; } = new();

    public AISettings AI { get; set; } = new();

    public ClientSettings Client { get; set; } = new();

    public EmailSettings EmailSettings { get; set; } = new();
}
public class ApplicationSettings
{
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
    public string SupportPhone { get; set; } = string.Empty;
}
public class BrandingSettings
{
    public string LogoUrl { get; set; } = string.Empty;
    public string LogoSmallUrl { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = string.Empty;
    public string SecondaryColor { get; set; } = string.Empty;
    public string AccentColor { get; set; } = string.Empty;
}
public class FeatureSettings
{
    public bool EnableAI { get; set; }
    public bool EnableAudit { get; set; }
    public bool EnableCourseManagement { get; set; }
    public bool EnableStudentPortal { get; set; }
    public bool EnableTeacherPortal { get; set; }
    public bool EnableSharePointIntegration { get; set; }
}
public class AISettings
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
    public bool EnableEducationalAnalysis { get; set; }
}
public class ClientSettings
{
    public string ClientName { get; set; } = string.Empty;
    public string ClientCode { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
}

public sealed class EmailSettings
{
    /// <summary>
    /// Email provider identifier.
    /// Example: "MicrosoftGraph"
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown in outgoing emails.
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// Mailbox used by Microsoft Graph sendMail.
    /// Example: admin@mit-ga.com
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// Primary support / admin recipient for contact emails.
    /// </summary>
    public string AdminEmail { get; set; } = string.Empty;
}
