using AdvocatesEventSource.Data.Model;
using System;
using System.ComponentModel;
using System.Text.Json;

namespace AdvocatesEventSource.Data
{
    /// <summary>
    /// Provides a converter of <see cref="Statistic"/> objects to or from JSON.
    /// </summary>
    public sealed class AdvocateEventsConverter : JsonPolymorphicConverter<AdvocateEventType, AdvocateEvent>
    {
        private const string _dataPropertyName = "AdvocateEvent";

        /// <inheritdoc/>
        protected override string DataPropertyName => _dataPropertyName;

        /// <inheritdoc/>
        protected override AdvocateEvent? ReadFromDescriptor(
            ref Utf8JsonReader reader, AdvocateEventType typeDescriptor)
        {
            return typeDescriptor switch
            {
                AdvocateEventType.AdvocateAdded => JsonSerializer.Deserialize<AdvocateAdded>(ref reader),
                AdvocateEventType.AdvocateModified => JsonSerializer.Deserialize<AdvocateModified>(ref reader),
                AdvocateEventType.AdvocateRemoved => JsonSerializer.Deserialize<AdvocateRemoved>(ref reader),
                _ => throw new InvalidEnumArgumentException(nameof(typeDescriptor),
                                                            (int)typeDescriptor,
                                                            typeof(AdvocateEventType))
            };
        }

        /// <inheritdoc/>
        protected override AdvocateEventType DescriptorFromValue(AdvocateEvent value)
        {
            return value switch
            {
                AdvocateAdded => AdvocateEventType.AdvocateAdded,
                AdvocateModified => AdvocateEventType.AdvocateModified,
                AdvocateRemoved => AdvocateEventType.AdvocateRemoved,
                _ => throw new ArgumentException()
            };
        }
    }
}
