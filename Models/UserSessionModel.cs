namespace MITANZ360Pro.Web.Models;

public class UserSessionModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string Campus { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public string ProfileImage { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = [];
    public bool IsAuthenticated { get; set; }
}