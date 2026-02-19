#r "nuget: Microsoft.AspNetCore.Cryptography.KeyDerivation, 9.0.0"

using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

const int SaltSize = 16;
const int HashSize = 32;
const int Iterations = 100000;

string HashPassword(string password)
{
    var salt = new byte[SaltSize];
    using (var rng = RandomNumberGenerator.Create())
    {
        rng.GetBytes(salt);
    }

    var hash = KeyDerivation.Pbkdf2(
        password: password,
        salt: salt,
        prf: KeyDerivationPrf.HMACSHA256,
        iterationCount: Iterations,
        numBytesRequested: HashSize
    );

    var hashBytes = new byte[SaltSize + HashSize];
    Array.Copy(salt, 0, hashBytes, 0, SaltSize);
    Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

    return Convert.ToBase64String(hashBytes);
}

var passwordHash = HashPassword("Password1$$$");
Console.WriteLine($"Password hash for 'Password1$$$':");
Console.WriteLine(passwordHash);
Console.WriteLine();
Console.WriteLine("SQL to update all users:");
Console.WriteLine($"UPDATE Users SET PasswordHash = '{passwordHash}';");
