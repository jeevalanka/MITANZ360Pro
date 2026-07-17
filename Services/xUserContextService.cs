namespace MITANZ360Pro.Web.Services
{
    public interface IUserContextService
    {
        string Role { get; }
    }

    public class UserContextService : IUserContextService
    {
        public string Role => "Student"; // Replace later with real auth
    }

}
