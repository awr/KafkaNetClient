using System;
using System.Collections.Generic;
using System.Linq;
using KafkaClient.Common;

namespace KafkaClient.Protocol.Types
{
    public class ConsumerEncoder : ProtocolTypeEncoder<ConsumerProtocolMetadata, ConsumerMemberAssignment>
    {
        /// <inheritdoc />
        public ConsumerEncoder() : base("consumer")
        {
        }

        /// <inheritdoc />
        public override IMemberMetadata DecodeMetadata(byte[] bytes)
        {
            using (var stream = new BigEndianBinaryReader(bytes)) {
                var version = stream.ReadInt16();
                var topicNames = new string[stream.ReadInt32()];
                for (var t = 0; t < topicNames.Length; t++) {
                    topicNames[t] = stream.ReadString();
                }
                var userData = stream.ReadBytes();
                return new ConsumerProtocolMetadata(version, topicNames, userData);
            }
        }

        /// <inheritdoc />
        public override IMemberAssignment DecodeAssignment(byte[] bytes)
        {
            using (var stream = new BigEndianBinaryReader(bytes)) {
                var version = stream.ReadInt16();

                var topics = new List<Topic>();
                var topicCount = stream.ReadInt32();
                for (var t = 0; t < topicCount; t++) {
                    var topicName = stream.ReadString();

                    var partitionCount = stream.ReadInt32();
                    for (var p = 0; p < partitionCount; p++) {
                        var partitionId = stream.ReadInt32();
                        topics.Add(new Topic(topicName, partitionId));
                    }
                }
                return new ConsumerMemberAssignment(version, topics);
            }
        }

        /// <inheritdoc />
        protected override byte[] EncodeMetadata(ConsumerProtocolMetadata metadata)
        {
            using (var packer = new MessagePacker()) {
                packer.Pack(metadata.Version)
                      .Pack(metadata.Subscriptions.Count);
                foreach (var subscription in metadata.Subscriptions) {
                    packer.Pack(subscription);
                }
                packer.Pack(metadata.UserData);

                return packer.ToBytes();
            }
        }

        /// <inheritdoc />
        protected override byte[] EncodeAssignment(ConsumerMemberAssignment assignment)
        {
            using (var packer = new MessagePacker()) {
                var topicGroups = assignment.PartitionAssignments.GroupBy(x => x.TopicName).ToList();

                packer.Pack(assignment.Version)
                      .Pack(topicGroups.Count);

                foreach (var topicGroup in topicGroups) {
                    var partitions = topicGroup.ToList();
                    packer.Pack(topicGroup.Key)
                          .Pack(partitions.Count);

                    foreach (var partition in partitions) {
                        packer.Pack(partition.PartitionId);
                    }
                }

                return packer.ToBytes();
            }
        }
    }
}