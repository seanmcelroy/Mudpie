// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DbRefJsonConverter.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A custom JSON converter to serialize <see cref="DbRef"/> instances
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Scripting.Common
{
    using System;
    using System.Diagnostics;

    using JetBrains.Annotations;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// A custom JSON converter to serialize <see cref="DbRef"/> instances
    /// </summary>
    public class DbRefJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, [NotNull] JsonSerializer serializer)
        {
            var dbref = (DbRef?)value ?? DbRef.Nothing;
            serializer.Serialize(writer, dbref.ToString());
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            Debug.Assert(token != null, "token != null");
            var dbref = (DbRef)token.ToString();
            return dbref;
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return typeof(DbRef).IsAssignableFrom(objectType);
        }
    }
}
