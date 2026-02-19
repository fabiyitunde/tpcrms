// Run with: dotnet script tools/UpdatePasswords.csx
// Or execute the SQL directly after getting the hash

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
Console.WriteLine($"Password hash for 'Password1$$$': {passwordHash}");
Console.WriteLine();
Console.WriteLine("SQL to update all users:");
Console.WriteLine($"UPDATE Users SET PasswordHash = '{passwordHash}' WHERE UserName IN ('admin', 'loanofficer', 'loanofficer2', 'branchapprover', 'creditofficer', 'horeviewer', 'committee1', 'committee2', 'committee3', 'finalapprover', 'operations', 'riskmanager', 'auditor');");
