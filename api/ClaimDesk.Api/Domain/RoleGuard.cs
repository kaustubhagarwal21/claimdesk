namespace ClaimDesk.Api.Domain;

public static class RoleGuard
{
    public static void Require(Role actual, params Role[] allowed)
    {
        if (!allowed.Contains(actual))
        {
            throw new ForbiddenException(
                $"The {actual} role may not perform this action. Allowed: {string.Join(", ", allowed)}.");
        }
    }
}
