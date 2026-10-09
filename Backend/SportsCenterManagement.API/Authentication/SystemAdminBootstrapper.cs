using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.API.Authentication;

public static class SystemAdminBootstrapper
{
    public static async Task SeedAsync(SportsCenterDbContext db, IConfiguration configuration)
    {
        var usernameSetting = configuration["Bootstrap:SystemAdmin:Username"];
        var emailSetting = configuration["Bootstrap:SystemAdmin:Email"];
        var password = configuration["Bootstrap:SystemAdmin:Password"];
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var adminRole = await db.Roles.SingleAsync(role => role.Name == RoleNames.SystemAdmin);
        if (await db.Users.AnyAsync(user => user.RoleId == adminRole.Id))
        {
            await transaction.CommitAsync();
            return;
        }
        if (usernameSetting is null && emailSetting is null && password is null)
        {
            throw new InvalidOperationException(
                "No SystemAdmin account exists. Configure Bootstrap:SystemAdmin:Username, Email, and Password for first startup.");
        }
        if (string.IsNullOrWhiteSpace(usernameSetting)
            || string.IsNullOrWhiteSpace(emailSetting)
            || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Configure Bootstrap:SystemAdmin:Username, Email, and Password together to create the first SystemAdmin.");
        }

        var username = usernameSetting.Trim().ToLowerInvariant();
        var email = emailSetting.Trim().ToLowerInvariant();
        ValidatePassword(password);
        if (!Regex.IsMatch(username, @"^[a-z0-9._-]{3,100}$")
            || !new EmailAddressAttribute().IsValid(email))
        {
            throw new InvalidOperationException("The bootstrap SystemAdmin username or email is invalid.");
        }

        if (await db.Users.AnyAsync(user => user.Username == username || user.Email == email))
        {
            throw new InvalidOperationException("The bootstrap SystemAdmin username or email is already in use.");
        }

        var admin = new User
        {
            Username = username,
            Email = email,
            PasswordHash = PasswordHashing.Hash(password),
            RoleId = adminRole.Id,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        db.AuditLogs.Add(new AuditLog
        {
            Action = "system-admin.bootstrapped",
            EntityType = "User",
            EntityId = admin.Id,
            NewValues = JsonSerializer.Serialize(new { Role = RoleNames.SystemAdmin }),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 6
            || !Regex.IsMatch(password, "[A-Z]")
            || !Regex.IsMatch(password, "[a-z]")
            || !Regex.IsMatch(password, "[0-9]")
            || !Regex.IsMatch(password, "[^a-zA-Z0-9]"))
        {
            throw new InvalidOperationException(
                "The bootstrap SystemAdmin password must be at least 6 characters and contain upper/lowercase letters, a number, and a symbol.");
        }
    }
}
