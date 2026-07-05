using Microsoft.EntityFrameworkCore;
using ShelfScout.Api.Domain;

namespace ShelfScout.Api;

/// <summary>
/// Resolves the immutable Authentik uid to a User, lazy-provisioning a User + Identity on
/// first sight (ADR 0012). Matched on uid, never on email — an email change never
/// duplicates or locks out a User.
/// </summary>
public class IdentityResolver(ShelfScoutDbContext db)
{
    public async Task<User> ResolveAsync(
        string uid,
        string? email,
        string? displayName,
        CancellationToken ct = default)
    {
        var identity = await db.Identities
            .Include(i => i.User)
            .SingleOrDefaultAsync(i => i.Provider == Identity.AuthentikProvider && i.Subject == uid, ct);

        if (identity is not null)
        {
            identity.User.Email = email;
            identity.User.DisplayName = displayName;
            await db.SaveChangesAsync(ct);
            return identity.User;
        }

        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Users.Add(user);
        db.Identities.Add(new Identity
        {
            Provider = Identity.AuthentikProvider,
            Subject = uid,
            User = user,
        });

        await db.SaveChangesAsync(ct);
        return user;
    }
}
