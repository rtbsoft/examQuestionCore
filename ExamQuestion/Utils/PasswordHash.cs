using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Cryptography.KeyDerivation;

//derived from https://github.com/aspnet/Identity/blob/master/src/Core/PasswordHasher.cs
//the original is more flexible in that it supports other random number generators and 
//multiple formats and algorithms. This one is hard coded to one format.

namespace ExamQuestion.Utils
{
    public static class PasswordHash
    {
        public static string HashPassword(string password) =>
            password == null
                ? throw new ArgumentNullException(nameof(password))
                : Convert.ToBase64String(createHash(password));

        public static bool VerifyHashedPassword(string hashedPassword, string providedPassword)
        {
            var success = false;

            if (hashedPassword == null)
                throw new ArgumentNullException(nameof(hashedPassword));

            if (providedPassword == null)
                throw new ArgumentNullException(nameof(providedPassword));

            var decodedHashedPassword = Convert.FromBase64String(hashedPassword);

            if (decodedHashedPassword.Length > 0)
                success = verifyHash(decodedHashedPassword, providedPassword);

            return success;
        }

        private static byte[] createHash(string password)
        {
            var outputBytes = new byte[HeaderSize + SaltSize + HashSize];
            var salt = new byte[SaltSize];

            RandomNumberGenerator.Create().GetBytes(salt, offset: 0, SaltSize);
            var subKey = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, IterationCount, HashSize);

            writeNetworkByteOrder(outputBytes, AlgorithmOffset, (uint)KeyDerivationPrf.HMACSHA256);
            writeNetworkByteOrder(outputBytes, IterationSizeOffset, IterationCount);
            writeNetworkByteOrder(outputBytes, SaltSizeOffset, SaltSize);

            Buffer.BlockCopy(salt, srcOffset: 0, outputBytes, HeaderSize, SaltSize);
            Buffer.BlockCopy(subKey, srcOffset: 0, outputBytes, HeaderSize + SaltSize, HashSize);

            return outputBytes;
        }

        private static bool verifyHash(byte[] hashedPassword, string password)
        {
            var success = false;

            try
            {
                // Read header information
                var prf = (KeyDerivationPrf)readNetworkByteOrder(hashedPassword, AlgorithmOffset);
                var iterationCount = (int)readNetworkByteOrder(hashedPassword, IterationSizeOffset);
                var saltLength = (int)readNetworkByteOrder(hashedPassword, SaltSizeOffset);

                if (iterationCount == IterationCount && saltLength == SaltSize &&
                    hashedPassword.Length == HeaderSize + SaltSize + HashSize)
                {
                    var salt = new byte[SaltSize];
                    Buffer.BlockCopy(hashedPassword, HeaderSize, salt, dstOffset: 0, SaltSize);

                    var expectedSubKey = new byte[HashSize];
                    Buffer.BlockCopy(hashedPassword, HeaderSize + SaltSize, expectedSubKey, dstOffset: 0, HashSize);

                    // Hash the incoming password and verify it
                    var actualSubKey = KeyDerivation.Pbkdf2(password, salt, prf, iterationCount, HashSize);
                    success = byteArraysEqual(actualSubKey, expectedSubKey);
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }

            return success;
        }

        private static uint readNetworkByteOrder(IReadOnlyList<byte> buffer, int offset) =>
            ((uint)buffer[offset + 0] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) |
            buffer[offset + 3];

        private static void writeNetworkByteOrder(IList<byte> buffer, int offset, uint value)
        {
            buffer[offset + 0] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)(value >> 0);
        }

        // Compares two byte arrays for equality. The method is specifically written so that the loop is not optimized.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static bool byteArraysEqual(IReadOnlyList<byte> a, IReadOnlyList<byte> b)
        {
            var isEqual = true;

            if (a != null || b != null)
            {
                if (a != null && b != null && a.Count == b.Count)
                    for (var i = 0; i < a.Count; i++)
                        isEqual &= a[i] == b[i];
                else
                    isEqual = false;
            }

            return isEqual;
        }
        /* ======================
         * HASHED PASSWORD FORMAT
         * ======================
         * 
         * PBKDF2 with HMAC-SHA256, 128-bit salt, 256-bit sub key, 10000 iterations.
         * Format: { prf (UInt32), iteration count (UInt32), salt length (UInt32), salt, sub key }
         * (All UInt32s are stored big-endian.)
         */

        #region constants

        private const int IterationCount = 10000;
        private const int SaltSize = 128 / 8;
        private const int HashSize = 256 / 8;
        private const int AlgorithmOffset = 0;
        private const int IterationSizeOffset = 4;
        private const int SaltSizeOffset = 8;
        private const int HeaderSize = 12;

        #endregion
    }
}