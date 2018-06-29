using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using KafkaClient.Common;
using KafkaClient.Connections;

namespace KafkaClient.Tests
{
    public static class TestConfig
    {
        public static string TopicName([CallerMemberName] string name = null)
        {
            return $"{Environment.MachineName}-Topic-{name}";
        }

        public static string GroupId([CallerMemberName] string name = null)
        {
            return $"{Environment.MachineName}-Group-{name}-{Guid.NewGuid():N}";
        }

        // turned down to reduce log noise -- turn up Level if necessary
        public static readonly ILog Log = new ConsoleLog();

        public static TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        public static Endpoint ServerEndpoint()
        {
            return new Endpoint(new IPEndPoint(IPAddress.Loopback, ServerPort()), KafkaLocalConnectionString);
        }

        public static int ServerPort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint) socket.LocalEndPoint).Port;
            }
        }

        public const string KafkaLocalConnectionString = "tcp://localhost";
        public const string Kafka9ConnectionString = "tcp://kafka9:9092";
        public const string Kafka10ConnectionString = "tcp://kafka10:9093";
        public const string Kafka11ConnectionString = "tcp://kafka11:9094";

        public static Uri Kafka9IntegrationUri { get; } = new Uri(Kafka9ConnectionString);
        public static Uri Kafka10IntegrationUri { get; } = new Uri(Kafka10ConnectionString);
        public static Uri Kafka11IntegrationUri { get; } = new Uri(Kafka11ConnectionString);

        public static KafkaOptions Options { get; } = new KafkaOptions(
            Kafka10IntegrationUri,
            new ConnectionConfiguration(ConnectionConfiguration.Defaults.ConnectionRetry(TimeSpan.FromSeconds(10)), requestTimeout: TimeSpan.FromSeconds(10)),
            new RouterConfiguration(Retry.AtMost(2)),
            producerConfiguration: new ProducerConfiguration(stopTimeout: TimeSpan.FromSeconds(1)),
            consumerConfiguration: new ConsumerConfiguration(TimeSpan.FromMilliseconds(50), maxPartitionFetchBytes: 4096 * 8, heartbeatTimeout: TimeSpan.FromSeconds(6)),
            log: Log);

        public static KafkaOptions IntegrationOptions { get; } = new KafkaOptions(
            Kafka10IntegrationUri,
            new ConnectionConfiguration(ConnectionConfiguration.Defaults.ConnectionRetry(TimeSpan.FromSeconds(10)), requestTimeout: TimeSpan.FromSeconds(10)),
            producerConfiguration: new ProducerConfiguration(stopTimeout: TimeSpan.FromSeconds(1)),
            consumerConfiguration: new ConsumerConfiguration(heartbeatTimeout: TimeSpan.FromSeconds(6)),
            log: Log);
    }
}