using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdvocatesEventSource
{
    /// <summary>
    /// Provides a base class for converting a hierarchy of objects to or from JSON.
    /// </summary>
    /// <typeparam name="TTypeDescriptor">
    /// A type of <see cref="Enum"/> whose integer value describes the type of object in JSON.
    /// </typeparam>
    /// <typeparam name="TBase">The base type of object handled by the converter.</typeparam>
    public abstract class JsonPolymorphicConverter<TTypeDescriptor, TBase> : JsonConverter<TBase>
        where TTypeDescriptor : Enum
        where TBase : class
    {
        private const string DEFAULT_TYPE_PROPERTY_NAME = "$type";

        /// <summary>
        /// Gets the expected property name of the number in JSON whose
        /// <typeparamref name="TTypeDescriptor"/> representation is used to determine the specific
        /// type of <typeparamref name="TBase"/> instantiated.
        /// </summary>
        protected virtual string TypePropertyName => DEFAULT_TYPE_PROPERTY_NAME;

        /// <summary>
        /// Gets the expected property name of the object in JSON containing the object data payload.
        /// </summary>
        protected abstract string DataPropertyName { get; }

        /// <inheritdoc/>
        public override TBase? Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            reader.Read();
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            string? typePropertyName = reader.GetString();
            if (typePropertyName != TypePropertyName)
                throw new JsonException();

            reader.Read();
            if (reader.TokenType != JsonTokenType.Number)
                throw new JsonException();

            var typeDescriptor = (TTypeDescriptor)Enum.ToObject(typeof(TTypeDescriptor), reader.GetInt32());
            reader.Read();

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            string? dataPropertyName = reader.GetString();
            if (dataPropertyName != DataPropertyName)
                throw new JsonException();

            reader.Read();

            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            TBase? readValue = ReadFromDescriptor(ref reader, typeDescriptor);
            reader.Read();

            return readValue;
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            TTypeDescriptor typeDescriptor = DescriptorFromValue(value);

            writer.WriteStartObject();

            writer.WriteNumber(TypePropertyName, Convert.ToInt32(typeDescriptor));

            writer.WritePropertyName(DataPropertyName);
            JsonSerializer.Serialize(writer, value, value.GetType());

            writer.WriteEndObject();
        }

        /// <summary>
        /// Reads and converts the JSON to a <typeparamref name="TBase"/>-derived type described by the provided
        /// <typeparamref name="TTypeDescriptor"/> enumeration.
        /// </summary>
        /// <param name="reader">The reader, positioned at the payload data of the object to read.</param>
        /// <param name="typeDescriptor">
        /// An enumeration value that specifies the type of <typeparamref name="TBase"/> to read.
        /// </param>
        /// <returns>The converted value.</returns>
        protected abstract TBase? ReadFromDescriptor(ref Utf8JsonReader reader, TTypeDescriptor typeDescriptor);

        /// <summary>
        /// Produces a <typeparamref name="TTypeDescriptor"/> value specifying a converted value's type.
        /// </summary>
        /// <param name="value">The converted value to create a type descriptor for.</param>
        /// <returns>
        /// A <typeparamref name="TTypeDescriptor"/> value that specifies the type of <c>value</c> in JSON.
        /// </returns>
        protected abstract TTypeDescriptor DescriptorFromValue(TBase value);
    }
}
