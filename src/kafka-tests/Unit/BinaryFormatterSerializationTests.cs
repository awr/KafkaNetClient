﻿using KafkaNet.Model;
using KafkaNet.Protocol;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;

namespace kafka_tests.Unit
{
    [Category("Unit")]
    [TestFixture]
    public class BinaryFormatterSerializationTests
    {
        [Test]
        public void ShouldSerializeCachedMetadataException()
        {
            var expected = new CachedMetadataException("blblb") { Topic = "foobar"};
            var actual = SerializeDeserialize(expected);

            Assert.AreEqual(expected.Topic, actual.Topic);
        }

        [Test]
        public void ShouldSerializeBufferUnderRunException()
        {
            var expected = new BufferUnderRunException(44, 44, 43);
            var actual = SerializeDeserialize(expected);

            Assert.AreEqual(expected.MessageHeaderSize, actual.MessageHeaderSize);
            Assert.AreEqual(expected.RequiredBufferSize, actual.RequiredBufferSize);
        }

        [Test]
        public void ShouldSerializeOffsetOutOfRangeException()
        {
            var expected = new FetchRequestException(
                new FetchRequest {
                    Fetches = new List<Fetch> {
                        new Fetch(){MaxBytes = 1,Topic = "aa",Offset = 2,PartitionId = 3}
                    }
                }, ErrorResponseCode.OffsetOutOfRange);
            var actual = SerializeDeserialize(expected);

            Assert.AreEqual(expected.Request.Fetches[0].MaxBytes, actual.Request.Fetches[0].MaxBytes);
            Assert.AreEqual(expected.Request.Fetches[0].Offset, actual.Request.Fetches[0].Offset);
            Assert.AreEqual(expected.Request.Fetches[0].PartitionId, actual.Request.Fetches[0].PartitionId);
            Assert.AreEqual(expected.Request.Fetches[0].Topic, actual.Request.Fetches[0].Topic);
        }

        [Test]
        public void ShouldSerializeOffsetOutOfRangeExceptionNull()
        {
            var expected = new FetchRequestException("a");
            var actual = SerializeDeserialize(expected);

            Assert.AreEqual(expected.Request, actual.Request);
        }

        [Test]
        public void ShouldSerializeOffsetKafkaEndpointInnerObjectAreNull()
        {
            var expected = new KafkaRequestException("a");
            var actual = SerializeDeserialize(expected);

            Assert.AreEqual(expected.Endpoint.ServeUri, actual.Endpoint.ServeUri);
            Assert.AreEqual(expected.Endpoint.Endpoint, actual.Endpoint.Endpoint);
        }

        [Test]
        public void ShouldSerializeOffsetKafkaEndpoint()
        {
            var expected = new KafkaRequestException("a") {
                Endpoint = new KafkaEndpoint(new Uri("http://S1.com"), 
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8888))
            };
            var actual = SerializeDeserialize(expected);

            Assert.AreEqual(expected.Endpoint.ServeUri, actual.Endpoint.ServeUri);
            Assert.AreEqual(expected.Endpoint.Endpoint, actual.Endpoint.Endpoint);
        }

        [Test]
        public void ShouldSerializeOffsetKafkaEndpointNull()
        {
            var expected = new KafkaRequestException("a", null);
            var actual = SerializeDeserialize(expected);

            Assert.AreEqual(expected.Endpoint, actual.Endpoint);
        }

        [Test]
        public void ShouldSerializeKafkaApplicationException()
        {
            var expected = new KafkaRequestException(ApiKeyRequestType.Fetch, ErrorResponseCode.OffsetOutOfRange, "3");
            var actual = SerializeDeserialize(expected);

            Assert.AreEqual(expected.ErrorCode, actual.ErrorCode);
        }

        private static T SerializeDeserialize<T> (T expected)
        {
            var formatter = new BinaryFormatter();
            MemoryStream memoryStream = new MemoryStream();
            formatter.Serialize(memoryStream, expected);
            memoryStream.Seek(0, 0);

            var actual = (T)formatter.Deserialize(memoryStream);
            return actual;
        }
    }
}