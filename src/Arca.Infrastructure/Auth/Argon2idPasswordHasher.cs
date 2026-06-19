using System.Security.Cryptography;
using System.Text;
using Arca.Application.Security;
using Konscious.Security.Cryptography;

namespace Arca.Infrastructure.Auth;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int MemorySize = 19 * 1024;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt, MemorySize, Iterations, Parallelism);

        return $"argon2id$m={MemorySize},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || parts[0] != "argon2id")
        {
            return false;
        }

        var parameters = ParseParameters(parts[1]);
        var salt = Convert.FromBase64String(parts[2]);
        var expectedHash = Convert.FromBase64String(parts[3]);
        var actualHash = ComputeHash(password, salt, parameters.Memory, parameters.Iterations, parameters.Parallelism);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memory, int iterations, int parallelism)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            MemorySize = memory,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };

        return argon2.GetBytes(HashSize);
    }

    private static (int Memory, int Iterations, int Parallelism) ParseParameters(string value)
    {
        var parameters = value.Split(',')
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2)
            .ToDictionary(part => part[0], part => int.Parse(part[1]), StringComparer.Ordinal);

        return (parameters["m"], parameters["t"], parameters["p"]);
    }
}
