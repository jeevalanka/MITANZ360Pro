using Microsoft.AspNetCore.Identity;

namespace MITANZ360Pro.Web.Data;

public static class DbInitializer
{
    private const string SysAdminRole = "SysAdmin";
    private const string SysAdminEmail = "supper@mitanz360.edu";
    private const string SysAdminPassword = "P@ssw0rd"; // change after first login

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // ✅ 1. ENSURE ROLE EXISTS
        if (!await roleManager.RoleExistsAsync(SysAdminRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(SysAdminRole));
            if (!roleResult.Succeeded)
            {
                throw new Exception($"Failed to create role {SysAdminRole}");
            }
        }

        // ✅ 2. ENSURE USER EXISTS
        var user = await userManager.FindByEmailAsync(SysAdminEmail);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = SysAdminEmail,
                Email = SysAdminEmail,
                EmailConfirmed = true
            };

            var userResult = await userManager.CreateAsync(user, SysAdminPassword);
            if (!userResult.Succeeded)
            {
                throw new Exception($"Failed to create SysAdmin user");
            }
        }

        // ✅ 3. ENSURE ROLE ASSIGNMENT
        if (!await userManager.IsInRoleAsync(user, SysAdminRole))
        {
            var roleAssignResult = await userManager.AddToRoleAsync(user, SysAdminRole);
            if (!roleAssignResult.Succeeded)
            {
                throw new Exception($"Failed to assign SysAdmin role");
            }
        }
    }
}