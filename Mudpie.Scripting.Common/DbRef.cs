// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DbRef.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A DbRef is a unique reference number (and type) that can be used to locate an object within the data store
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Mudpie.Scripting.Common
{
    using System;

    using JetBrains.Annotations;

    using Newtonsoft.Json;

    /// <summary>
    /// A DbRef is a unique reference number (and type) that can be used to locate an object within the data store
    /// </summary>
    // ReSharper disable once StyleCop.SA1650
    [JsonConverter(typeof(DbRefJsonConverter))]
    // ReSharper disable once StyleCop.SA1650
    public struct DbRef
    {
        /// <summary>
        /// A value indicating this instance is unset
        /// </summary>
        public static readonly DbRef Nothing = 0;

        /// <summary>
        /// A value indicating this instance is ambiguous
        /// </summary>
        public static readonly DbRef Ambiguous = -1;

        /// <summary>
        /// A value indicating no match could be found
        /// </summary>
        public static readonly DbRef FailedMatch = -2;

        /// <summary>
        /// The internal numeric value of the database reference
        /// </summary>
        private readonly int referenceNumber;

        /// <summary>
        /// Initializes a new instance of the <see cref="DbRef"/> struct.
        /// </summary>
        /// <param name="referenceNumber">
        /// The reference number.
        /// </param>
        private DbRef(int referenceNumber)
        {
            this.referenceNumber = referenceNumber;
        }

        /// <summary>
        /// Implicit conversion from database reference to string
        /// </summary>
        /// <param name="reference">The reference to convert into a string</param>
        [NotNull, Pure]
        public static implicit operator string(DbRef reference)
        {
            return "#" + reference.referenceNumber.ToString(@"##000000");
        }

        /// <summary>
        /// Adds two database references together.  If the databases references are not the same,
        /// they become <see cref="Ambiguous"/>.  If either is <see cref="Ambiguous"/>, the result
        /// is still <see cref="Ambiguous"/>.  If one is <see cref="Nothing"/> and the other is not,
        /// then the other is returned.  If one is <see cref="FailedMatch"/> and the other is not,
        /// then the other is returned.
        /// </summary>
        /// <param name="a">The first database reference to add</param>
        /// <param name="b">The second database reference to add</param>
        /// <returns>A value indicating the 'sum' of the two database references</returns>
        /// <exception cref="InvalidOperationException">Thrown if the logic of the method cannot determine if the references can add (software error).</exception>
        public static DbRef operator +(DbRef a, DbRef b)
        {
            if (a.Equals(Ambiguous) || b.Equals(Ambiguous))
            {
                return Ambiguous;
            }

            if (a.Equals(FailedMatch) && b.Equals(FailedMatch))
            {
                return FailedMatch;
            }

            if (a.Equals(Nothing))
            {
                return b;
            }

            if (b.Equals(Nothing))
            {
                return a;
            }

            if (!a.Equals(FailedMatch) && !b.Equals(FailedMatch))
            {
                return Ambiguous;
            }

            if (a.Equals(FailedMatch))
            {
                return b;
            }

            if (b.Equals(FailedMatch))
            {
                return a;
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Implicit conversion from a string to a database reference
        /// </summary>
        /// <param name="referenceString">The string to convert into a database reference</param>
        public static implicit operator DbRef(string referenceString)
        {
            if (string.IsNullOrWhiteSpace(referenceString))
            {
                return Nothing;
            }

            if (!referenceString.StartsWith("#"))
            {
                throw new ArgumentException($"Unable to parse string {referenceString} as a DBREF; does not start with a #", nameof(referenceString));
            }

            int refNumber;
            if (!int.TryParse(referenceString.Substring(1), out refNumber))
            {
                throw new ArgumentException($"Unable to parse string {referenceString} as a DBREF; portion after # is not an integer", nameof(referenceString));
            }

            return new DbRef(refNumber);
        }

        /// <summary>
        /// Implicit conversion from database reference to an integer
        /// </summary>
        /// <param name="reference">The reference to convert into an integer</param>
        public static implicit operator int(DbRef reference)
        {
            return reference.referenceNumber;
        }

        /// <summary>
        /// Implicit conversion from an integer to a database reference
        /// </summary>
        /// <param name="referenceNumber">The integer to convert into a database reference</param>
        public static implicit operator DbRef(int referenceNumber)
        {
            return new DbRef(referenceNumber);
        }

        /// <summary>
        /// Attempts to parse a string as a <see cref="DbRef"/>
        /// </summary>
        /// <param name="referenceString">The string to attempt to parse</param>
        /// <param name="reference">If parsed, the output result</param>
        /// <returns>A <see cref="bool"/> indicating whether the <param name="referenceString"></param> can be parsed as a <see cref="DbRef"/></returns>
        [Pure]
        public static bool TryParse([CanBeNull] string referenceString, out DbRef reference)
        {
            reference = Nothing;

            if (referenceString == null || !referenceString.StartsWith("#"))
            {
                return false;
            }

            int refNumber;
            if (!int.TryParse(referenceString.Substring(1), out refNumber))
            {
                return false;
            }

            reference = referenceString;
            return true;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return this.Equals(obj as DbRef?);
        }

        /// <summary>
        /// Tests the equality of a database reference to this instance
        /// </summary>
        /// <param name="obj">The reference to test against this instance</param>
        /// <returns>
        /// True if the instances are the same number; otherwise, false.
        /// </returns>
        [PublicAPI, Pure]
        public bool Equals(DbRef? obj)
        {
            return obj?.referenceNumber == this.referenceNumber;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.referenceNumber.GetHashCode();
        }
    }
}
