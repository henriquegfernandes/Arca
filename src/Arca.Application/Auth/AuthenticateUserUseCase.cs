using Arca.Application.Abstractions.Auth;
using Arca.Application.Common;
using Arca.Application.Security;

namespace Arca.Application.Auth;

public sealed class AuthenticateUserUseCase(
    IUserAuthenticationRepository userRepository,
    IPasswordHasher passwordHasher)
{
    public async Task<Result<AuthenticatedUser>> ExecuteAsync(
        AuthenticateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = command.Email.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(command.Password))
        {
            await userRepository.RecordLoginAttemptAsync(
                email,
                false,
                "Missing credentials",
                command.IpAddress,
                command.UserAgent,
                cancellationToken);

            return Result<AuthenticatedUser>.Failure("Invalid email or password.");
        }

        var user = await userRepository.FindByNormalizedEmailAsync(NormalizeEmail(email), cancellationToken);
        if (user is null || !user.IsActive || !passwordHasher.VerifyPassword(command.Password, user.PasswordHash))
        {
            await userRepository.RecordLoginAttemptAsync(
                email,
                false,
                "Invalid credentials",
                command.IpAddress,
                command.UserAgent,
                cancellationToken);

            return Result<AuthenticatedUser>.Failure("Invalid email or password.");
        }

        await userRepository.UpdateLastLoginAsync(user.Id, cancellationToken);
        await userRepository.RecordLoginAttemptAsync(
            user.Email,
            true,
            null,
            command.IpAddress,
            command.UserAgent,
            cancellationToken);

        var isSuperAdmin = user.Roles.Any(role =>
            string.Equals(role.Name, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            && string.Equals(role.Scope, "System", StringComparison.OrdinalIgnoreCase));

        return Result<AuthenticatedUser>.Success(new AuthenticatedUser(
            user.Id,
            user.FullName,
            user.Email,
            isSuperAdmin,
            user.Roles.Select(role => role.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
