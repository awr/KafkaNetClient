using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using KafkaClient.Common;

namespace KafkaClient.Protocol
{
    /// <summary>
    /// DeleteRecords Request => [topics] timeout 
    /// </summary>
    /// <remarks>
    /// DeleteRecords Request => [topics] timeout 
    ///   topics => topic [partitions] 
    ///     topic => STRING
    ///     partitions => partition offset 
    ///       partition => INT32
    ///       offset => INT64
    ///   timeout => INT32
    /// 
    /// From http://kafka.apache.org/protocol.html#The_Messages_DeleteRecords
    /// </remarks>
    public class DeleteRecordsRequest : Request, IRequest<DeleteRecordsResponse>, IEquatable<DeleteRecordsRequest>
    {
        public override string ToString() => $"{{Api:{ApiKey},timeout:{Timeout},topics:[{Topics.ToStrings()}]}}";

        public override string ShortString() => Topics.Count == 1 ? $"{ApiKey} {Topics[0].TopicName}" : ApiKey.ToString();

        protected override void EncodeBody(IKafkaWriter writer, IRequestContext context)
        {
            var groupedTopics = (from t in Topics
                                   group t by t.TopicName
                                   into tpc select tpc
                                ).ToList();

            writer.Write(groupedTopics.Count);
            foreach (var topic in groupedTopics) {
                var topics = topic.ToList();
                writer.Write(topic.Key)
                        .Write(topics.Count);
                foreach (var partition in topics) {
                    writer.Write(partition.PartitionId)
                          .Write(partition.Offset);
                }
            }
            writer.WriteMilliseconds(Timeout);
        }

        public DeleteRecordsResponse ToResponse(IRequestContext context, ArraySegment<byte> bytes) => DeleteRecordsResponse.FromBytes(context, bytes);

        public DeleteRecordsRequest(IEnumerable<Topic> topics, TimeSpan? timeout = null) 
            : base(ApiKey.DeleteRecords)
        {
            Timeout = timeout.GetValueOrDefault(TimeSpan.FromSeconds(1));
            Topics = topics != null ? topics.ToImmutableList() : ImmutableList<Topic>.Empty;
        }

        /// <summary>
        /// The maximum time to await a response in ms on the server.
        /// </summary>
        public TimeSpan Timeout { get; }

        /// <summary>
        /// Collection of payloads to post to kafka
        /// </summary>
        public IImmutableList<Topic> Topics { get; }

        #region Equality 

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals(obj as DeleteRecordsRequest);
        }

        /// <inheritdoc />
        public bool Equals(DeleteRecordsRequest other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Timeout.Equals(other.Timeout) 
                && Topics.HasEqualElementsInOrder(other.Topics);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked {
                var hashCode = Timeout.GetHashCode();
                hashCode = (hashCode * 397) ^ (Topics?.Count.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        #endregion

        /// <summary>
        /// The topic and partition to remove records from.
        /// Included in <see cref="DeleteRecordsRequest"/>
        /// </summary>
        public class Topic : TopicPartition, IEquatable<Topic>
        {
            public override string ToString() => $"{{topic:{TopicName},partition_id:{PartitionId},offset:{Offset}}}";

            public Topic(string topicName, int partitionId, long offset = 0L) 
                : base(topicName, partitionId)
            {
                Offset = offset;
            }

            /// <summary>
            /// The offset before which the messages will be deleted.
            /// </summary>
            public long Offset { get; }

            #region Equality

            /// <inheritdoc />
            public override bool Equals(object obj)
            {
                return Equals(obj as Topic);
            }

            /// <inheritdoc />
            public bool Equals(Topic other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return base.Equals(other) 
                    && Offset == other.Offset;
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                unchecked {
                    int hashCode = base.GetHashCode();
                    hashCode = (hashCode*397) ^ (int) Offset;
                    return hashCode;
                }
            }

            #endregion

        }
    }
}