using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using KafkaClient.Common;

namespace KafkaClient.Protocol
{
    public class MetadataResponse : IResponse
    {
        public MetadataResponse(IEnumerable<Broker> brokers = null, IEnumerable<MetadataTopic> topics = null)
        {
            Brokers = ImmutableList<Broker>.Empty.AddNotNullRange(brokers);
            Topics = ImmutableList<MetadataTopic>.Empty.AddNotNullRange(topics);
            Errors = ImmutableList<ErrorResponseCode>.Empty.AddRange(Topics.Select(t => t.ErrorCode));
        }

        public ImmutableList<ErrorResponseCode> Errors { get; }

        public ImmutableList<Broker> Brokers { get; }
        public ImmutableList<MetadataTopic> Topics { get; }
    }
}