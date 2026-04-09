using BCrypt.Net;
using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;

namespace DAL.Seeds;

public static class UserSeed
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        var landlordEmail = "seed.landlord@example.com";
        var tenantEmail = "seed.tenant@example.com";
        var adminEmail = "seed.admin@example.com";
        var staffEmail = "seed.staff@example.com";
        

        var landlordUser = await context.Users.SingleOrDefaultAsync(u => u.Email == landlordEmail, cancellationToken);
        if (landlordUser is null)
        {
            landlordUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = landlordEmail,
                FullName = "Seed Landlord",
                IdentityVerified = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Seed@123"),
                Phone = "0000000000",
                Role = "landlord",
                CreatedAt = Common.Utils.VietnamTime.Now
            };
            context.Users.Add(landlordUser);
        }

        var tenantUser = await context.Users.SingleOrDefaultAsync(u => u.Email == tenantEmail, cancellationToken);
        if (tenantUser is null)
        {
            tenantUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = tenantEmail,
                FullName = "Seed Tenant",
                IdentityVerified = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Seed@123"),
                Phone = "0000000001",
                Role = "tenant",
                CreatedAt = Common.Utils.VietnamTime.Now
            };
            context.Users.Add(tenantUser);
        }

        if (!await context.Users.AnyAsync(u => u.Email == adminEmail, cancellationToken))
        {
            context.Users.Add(new User
            {
                UserId = Guid.NewGuid(),
                Email = adminEmail,
                FullName = "Seed Admin",
                IdentityVerified = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Seed@123"),
                Phone = "0000000002",
                Role = "admin",
                CreatedAt = Common.Utils.VietnamTime.Now
            });
        }

        if (!await context.Users.AnyAsync(u => u.Email == staffEmail, cancellationToken))
        {
            context.Users.Add(new User
            {
                UserId = Guid.NewGuid(),
                Email = staffEmail,
                FullName = "Seed Staff",
                IdentityVerified = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Seed@123"),
                Phone = "0000000003",
                Role = "staff",
                CreatedAt = Common.Utils.VietnamTime.Now
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        tenantUser = await context.Users.SingleAsync(u => u.Email == tenantEmail, cancellationToken);

        if (!await context.Tenants.AnyAsync(t => t.TenantId == tenantUser.UserId, cancellationToken))
        {
            context.Tenants.Add(new Tenant
            {
                TenantId = tenantUser.UserId,
                PassportId = "P0000000",
                Nationality = "VN",
                IdentityVerificationStatus = "not_started",
                LastVerifiedAt = null
            });

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
