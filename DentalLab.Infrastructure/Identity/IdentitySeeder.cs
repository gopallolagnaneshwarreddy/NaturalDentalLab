using DentalLab.Application.Security;
using DentalLab.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DentalLab.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        using var scope = services.CreateScope();

        var roleManager =
            scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

        await CreateRoleAsync(
            roleManager,
            ApplicationRoles.Owner);

        await CreateRoleAsync(
            roleManager,
            ApplicationRoles.Admin);

        await CreateRoleAsync(
            roleManager,
            ApplicationRoles.Staff);

        var ownerEmail =
            configuration["BootstrapOwner:Email"]?.Trim();

        var ownerPassword =
            configuration["BootstrapOwner:Password"];

        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            throw new InvalidOperationException(
                "Bootstrap Owner email is missing. " +
                "Configure 'BootstrapOwner:Email'.");
        }

        var owner =
            await userManager.FindByEmailAsync(ownerEmail);

        if (owner is null)
        {
            if (string.IsNullOrWhiteSpace(ownerPassword))
            {
                throw new InvalidOperationException(
                    "Bootstrap Owner does not exist and the bootstrap " +
                    "password is missing. Configure " +
                    "'BootstrapOwner:Password'.");
            }

            owner = new ApplicationUser
            {
                UserName = ownerEmail,
                Email = ownerEmail,

                // The bootstrap account must be able to sign in because
                // RequireConfirmedAccount is enabled in Program.cs.
                EmailConfirmed = true,

                LockoutEnabled = true
            };

            var createResult =
                await userManager.CreateAsync(
                    owner,
                    ownerPassword);

            ThrowIfFailed(
                createResult,
                $"Could not create Bootstrap Owner '{ownerEmail}'");

            owner =
                await userManager.FindByEmailAsync(ownerEmail);

            if (owner is null)
            {
                throw new InvalidOperationException(
                    $"Bootstrap Owner '{ownerEmail}' was created, " +
                    "but could not be loaded.");
            }
        }

        await EnsureOwnerIsActiveAsync(
            userManager,
            owner);

        await EnsureOwnerRoleAsync(
            userManager,
            owner);
    }

    private static async Task CreateRoleAsync(
        RoleManager<IdentityRole> roleManager,
        string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result =
            await roleManager.CreateAsync(
                new IdentityRole(roleName));

        ThrowIfFailed(
            result,
            $"Could not create role '{roleName}'");
    }

    private static async Task EnsureOwnerRoleAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser owner)
    {
        if (await userManager.IsInRoleAsync(
                owner,
                ApplicationRoles.Owner))
        {
            return;
        }

        var result =
            await userManager.AddToRoleAsync(
                owner,
                ApplicationRoles.Owner);

        ThrowIfFailed(
            result,
            "Could not assign the Owner role");
    }

    private static async Task EnsureOwnerIsActiveAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser owner)
    {
        var requiresUpdate = false;

        if (!owner.EmailConfirmed)
        {
            owner.EmailConfirmed = true;
            requiresUpdate = true;
        }

        if (owner.LockoutEnd.HasValue)
        {
            owner.LockoutEnd = null;
            requiresUpdate = true;
        }

        if (owner.AccessFailedCount != 0)
        {
            owner.AccessFailedCount = 0;
            requiresUpdate = true;
        }

        if (!requiresUpdate)
        {
            return;
        }

        var result =
            await userManager.UpdateAsync(owner);

        ThrowIfFailed(
            result,
            "Could not activate the Bootstrap Owner");
    }

    private static void ThrowIfFailed(
        IdentityResult result,
        string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(
                error => error.Description));

        throw new InvalidOperationException(
            $"{message}: {errors}");
    }
}