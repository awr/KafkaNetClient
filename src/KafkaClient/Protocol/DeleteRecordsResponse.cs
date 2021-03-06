using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using KafkaClient.Common;

namespace KafkaClient.Protocol
{
    /// <summary>
    /// DeleteRecords Response => throttle_time_ms [topics] 
    /// </summary>
    /// <remarks>
    /// DeleteRecords Response => throttle_time_ms [topics] 
    ///   throttle_time_ms => INT32
    ///   topics => topic [partitions] 
    ///     topic => STRING
    ///     partitions => partition low_watermark error_code 
    ///       partition => INT32
    ///       low_watermark => INT64
    ///       error_code => INT16
    /// 
    /// From http://kafka.apache.org/protocol.html#The_Messages_DeleteRecords
    /// </remarks>
    public class DeleteRecordsResponse : ThrottledResponse, IResponse, IEquatable<DeleteRecordsResponse>
    {
        public override string ToString() => $"{{{this.ThrottleToString()},topics:[{Topics.ToStrings()}]}}";

        public static DeleteRecordsResponse FromBytes(IRequestContext context, ArraySegment<byte> bytes)
        {
            using (var reader = new KafkaReader(bytes)) {
                var throttleTime = reader.ReadThrottleTime();

                var topicCount = reader.ReadInt32();
                reader.AssertMaxArraySize(topicCount);
                var topics = new List<Topic>();
                for (var i = 0; i < topicCount; i++) {
                    var topicName = reader.ReadString();

                    var partitionCount = reader.ReadInt32();
                    reader.AssertMaxArraySize(partitionCount);
                    for (var j = 0; j < partitionCount; j++) {
                        var partitionId = reader.ReadInt32();
                        var lowWatermark = reader.ReadInt64();
                        var errorCode = (ErrorCode) reader.ReadInt16();

                        topics.Add(new Topic(topicName, partitionId, lowWatermark, errorCode));
                    }
                }

                return new DeleteRecordsResponse(topics, throttleTime);
            }
        }

        public DeleteRecordsResponse(IEnumerable<Topic> topics = null, TimeSpan? throttleTime = null)
            : base(throttleTime)
        {
            Topics = topics.ToSafeImmutableList();
            Errors = Topics.Select(t => t.Error).ToImmutableList();
        }

        public IImmutableList<ErrorCode> Errors { get; }

        public IImmutableList<Topic> Topics { get; }

        #region Equality

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals(obj as DeleteRecordsResponse);
        }

        /// <inheritdoc />
        public bool Equals(DeleteRecordsResponse other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) 
                && Topics.HasEqualElementsInOrder(other.Topics);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked {
                var hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ (Topics?.Count.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        #endregion

        public class Topic : TopicResponse, IEquatable<Topic>
        {
            public override string ToString() => $"{{{this.PartitionToString()},low_watermark:{LowWatermark},error_code:{Error}}}";

            public Topic(string topic, int partitionId, long lowWatermark, ErrorCode errorCode)
                : base(topic, partitionId, errorCode)
            {
                LowWatermark = lowWatermark;
            }

            /// <summary>
            /// Smallest available offset of all live replicas.
            /// </summary>
            public long LowWatermark { get; }

            #region Equality

            public override bool Equals(object obj)
            {
                return Equals(obj as Topic);
            }

            public bool Equals(Topic other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return base.Equals(other) 
                    && LowWatermark == other.LowWatermark;
            }

            public override int GetHashCode()
            {
                unchecked {
                    int hashCode = base.GetHashCode();
                    hashCode = (hashCode*397) ^ LowWatermark.GetHashCode();
                    return hashCode;
                }
            }

            #endregion
        }
    }
}