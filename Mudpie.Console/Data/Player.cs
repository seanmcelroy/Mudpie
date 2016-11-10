﻿namespace Mudpie.Console.Data
{
    using System;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Cryptography;
    using System.Text;

    using JetBrains.Annotations;

    public class Player : ObjectBase
    {
        /// <summary>
        /// Gets or sets the identity of the user for authentication
        /// </summary>
        [NotNull]
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the hash of the player's password
        /// </summary>
        [CanBeNull]
        public string PasswordHash { get; set; }

        /// <summary>
        /// Gets or sets the salt used to hash the player's password
        /// </summary>
        [CanBeNull]
        public string PasswordSalt { get; set; }

        public DateTime? LastLogin { get; set; }

        public void SetPassword(SecureString password)
        {
            var saltBytes = new byte[64];
            var rng = RandomNumberGenerator.Create();
            rng.GetNonZeroBytes(saltBytes);
            var salt = Convert.ToBase64String(saltBytes);
            var bstr = Marshal.SecureStringToBSTR(password);
            try
            {
                this.PasswordHash = Convert.ToBase64String(new SHA512CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(string.Concat(salt, Marshal.PtrToStringBSTR(bstr)))));
                this.PasswordSalt = salt;
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
            }
        }

        /// <summary>Determines whether the specified object is equal to the current object.</summary>
        /// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
        /// <param name="obj">The object to compare with the current object. </param>
        /// <filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to Player return false.
            var p = obj as Player;
            if ((object)p == null)
                return false;

            // Return true if the fields match:
            return this.Id == p.Id;
        }

        /// <summary>Serves as the default hash function. </summary>
        /// <returns>A hash code for the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}