using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Server.Utilities
{
    public static class Password
    {
        public const int DigestLength = 384 / 8;
        
        public static bool EvaluatePassword(string password, byte[] extantDigest)
        {
            var extantSalt = new byte[128 / 8];
            var extantHash = new byte[256 / 8];

            Array.Copy(extantDigest, extantSalt, extantSalt.Length);
            Array.Copy(extantDigest, extantSalt.Length, extantHash, 0, extantHash.Length);

            var hash = KeyDerivation.Pbkdf2(password, extantSalt, KeyDerivationPrf.HMACSHA1, 10000, 256 / 8);

            var match = true;

            System.Diagnostics.Debug.Assert(hash.Length == extantHash.Length);

            for (var i = 0; i < extantHash.Length; i++)
            {
                match &= extantHash[i] == hash[i];
            }

            return match;
        }

        public static byte[] GetPasswordDigest(string password)
        {
            // generate a 128-bit salt using a secure PRNG
            var salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // derive a 256-bit subkey (use HMACSHA1 with 10,000 iterations)
            var hashed = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA1, 10000, 256 / 8);

            var digest = new byte[384 / 8];

            Array.Copy(salt, digest, salt.Length);
            Array.Copy(hashed, 0, digest, salt.Length, hashed.Length);

            return digest;
        }
    }
}
