namespace Mudpie.Console.Data
{
    using System;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Cryptography;
    using System.Text;

    using JetBrains.Annotations;

    public class Player : ObjectBase
    {
        [NotNull]
        public string Username { get; set; }

        [CanBeNull]
        public string PasswordHash { get; set; }

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

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}
