﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KafkaClient.Common;
using KafkaClient.Protocol;
using KafkaClient.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace KafkaClient.Tests
{
    [TestFixture]
    [Category("Integration")]
    public class ProducerConsumerTests
    {
        [Test]
        [TestCase(10, 1000)]
        [TestCase(100, 1000)]
        [TestCase(1000, 1000)]
        public async Task SendAsyncShouldHandleHighVolumeOfMessages(int amount, int maxAsync)
        {
            using (var router = new BrokerRouter(new KafkaOptions(IntegrationConfig.IntegrationUri)))
            using (var producer = new Producer(router, new ProducerConfiguration(maxAsync, amount / 2)))
            {
                var tasks = new Task<ProduceResponse.Topic>[amount];

                for (var i = 0; i < amount; i++) {
                    tasks[i] = producer.SendMessageAsync(new Message(Guid.NewGuid().ToString()), IntegrationConfig.TopicName(), CancellationToken.None);
                }
                var results = await Task.WhenAll(tasks.ToArray());

                //Because of how responses are batched up and sent to servers, we will usually get multiple responses per requested message batch
                //So this assertion will never pass
                //Assert.That(results.Count, Is.EqualTo(amount));

                Assert.That(results.Any(x => x.ErrorCode != ErrorResponseCode.None), Is.False,
                    "Should not have received any results as failures.");
            }
        }

        [Test]
        public async Task ProducerAckLevel()
        {
            using (var router = new BrokerRouter(IntegrationConfig.IntegrationUri, log: IntegrationConfig.NoDebugLog ))
            using (var producer = new Producer(router))
            {
                var responseAckLevel0 = await producer.SendMessageAsync(new Message("Ack Level 0"), IntegrationConfig.TopicName(), 0, new SendMessageConfiguration(acks: 0), CancellationToken.None);
                Assert.AreEqual(responseAckLevel0.Offset, -1);
                var responseAckLevel1 = await producer.SendMessageAsync(new Message("Ack Level 1"), IntegrationConfig.TopicName(), 0, new SendMessageConfiguration(acks: 1), CancellationToken.None);
                Assert.That(responseAckLevel1.Offset, Is.GreaterThan(-1));
            }
        }

        [Test]
        public async Task ProducerAckLevel1ResponseOffsetShouldBeEqualToLastOffset()
        {
            using (var router = new BrokerRouter(IntegrationConfig.IntegrationUri, log: IntegrationConfig.NoDebugLog ))
            using (var producer = new Producer(router))
            {
                var responseAckLevel1 = await producer.SendMessageAsync(new Message("Ack Level 1"), IntegrationConfig.TopicName(), 0, new SendMessageConfiguration(acks: 1), CancellationToken.None);
                var offsetResponse = await producer.BrokerRouter.GetTopicOffsetsAsync(IntegrationConfig.TopicName(), CancellationToken.None);
                var maxOffset = offsetResponse.First(x => x.PartitionId == 0);
                Assert.AreEqual(responseAckLevel1.Offset, maxOffset.Offset - 1);
            }
        }

        [Test]
        public async Task ProducerLastResposeOffsetAckLevel1ShouldBeEqualsToLastOffset()
        {
            using (var router = new BrokerRouter(IntegrationConfig.IntegrationUri, log: IntegrationConfig.NoDebugLog ))
            using (var producer = new Producer(router))
            {
                var responseAckLevel1 = await producer.SendMessagesAsync(new [] { new Message("Ack Level 1"), new Message("Ack Level 1") }, IntegrationConfig.TopicName(), 0, new SendMessageConfiguration(acks: 1), CancellationToken.None);
                var offsetResponse = await producer.BrokerRouter.GetTopicOffsetsAsync(IntegrationConfig.TopicName(), CancellationToken.None);
                var maxOffset = offsetResponse.First(x => x.PartitionId == 0);

                Assert.AreEqual(responseAckLevel1.Last().Offset, maxOffset.Offset - 1);
            }
        }

        [Test]
        public async Task ConsumeByOffsetShouldGetSameMessageProducedAtSameOffset()
        {
            long offsetResponse;
            var messge = Guid.NewGuid();

            using (var router = new BrokerRouter(IntegrationConfig.IntegrationUri, log: IntegrationConfig.NoDebugLog)) {
                using (var producer = new Producer(router)) {
                    var responseAckLevel1 = await producer.SendMessageAsync(new Message(messge.ToString()), IntegrationConfig.TopicName(), 0, new SendMessageConfiguration(acks: 1), CancellationToken.None);
                    offsetResponse = responseAckLevel1.Offset;
                }
            }

            using (var router = new BrokerRouter(IntegrationConfig.IntegrationUri, log: IntegrationConfig.NoDebugLog ))
            using (var consumer = new OldConsumer(new ConsumerOptions(IntegrationConfig.TopicName(), router) { MaxWaitTimeForMinimumBytes = TimeSpan.Zero }, new OffsetPosition[] { new OffsetPosition(0, offsetResponse) }))
            {
                var result = consumer.Consume().Take(1).ToList().FirstOrDefault();
                Assert.AreEqual(messge.ToString(), result.Value.ToUtf8String());
            }
        }

        [Test]
        public void ConsumerShouldConsumeInSameOrderAsProduced()
        {
            var expected = new List<string> { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19" };
            var testId = Guid.NewGuid().ToString();

            using (var router = new BrokerRouter(IntegrationConfig.IntegrationUri, log: IntegrationConfig.NoDebugLog ))
            using (var producer = new Producer(router))
            {
                var offsets = producer.BrokerRouter.GetTopicOffsetsAsync(IntegrationConfig.TopicName(), CancellationToken.None).Result;

                using (var consumer = new OldConsumer(new ConsumerOptions(IntegrationConfig.TopicName(), router) { MaxWaitTimeForMinimumBytes = TimeSpan.Zero },
                    offsets.Select(x => new OffsetPosition(x.PartitionId, x.Offset)).ToArray()))
                {
                    for (var i = 0; i < 20; i++)
                    {
                        producer.SendMessageAsync(new Message(i.ToString(), testId), IntegrationConfig.TopicName(), CancellationToken.None).Wait();
                    }

                    var results = consumer.Consume().Take(20).ToList();

                    //ensure the produced messages arrived
                    IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create($"Message order:  {string.Join(", ", results.Select(x => x.Value.ToUtf8String()).ToList())}"));

                    Assert.That(results.Count, Is.EqualTo(20));
                    Assert.That(results.Select(x => x.Value.ToUtf8String()).ToList(), Is.EqualTo(expected), "Expected the message list in the correct order.");
                    Assert.That(results.Any(x => x.Key.ToUtf8String() != testId), Is.False);
                }
            }
        }

        /// <summary>
        /// order Should remain in the same ack leve and partition
        /// </summary>
        /// <returns></returns>
        public async Task ConsumerShouldConsumeInSameOrderAsAsyncProduced()
        {
            var partition = 0;
            var numberOfMessage = 200;
            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> create BrokerRouter"));
            var router = new BrokerRouter(new KafkaOptions(IntegrationConfig.IntegrationUri));
            var causesRaceConditionOldVersion = 2;
            var producer = new Producer(router, new ProducerConfiguration(causesRaceConditionOldVersion, batchMaxDelay: TimeSpan.Zero)); // this is slow on purpose
            //this is not slow  var producer = new Producer(router);
            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> create producer"));
            var offsets = await producer.BrokerRouter.GetTopicOffsetsAsync(IntegrationConfig.TopicName(), CancellationToken.None);
            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> request Offset"));
            var sendList = new List<Task>(numberOfMessage);
            for (var i = 0; i < numberOfMessage; i++)
            {
                var sendTask = producer.SendMessageAsync(new Message(i.ToString()), IntegrationConfig.TopicName(), partition, new SendMessageConfiguration(1, null, MessageCodec.CodecNone), CancellationToken.None);
                sendList.Add(sendTask);
            }

            await Task.WhenAll(sendList.ToArray());
            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> done send"));

            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> create Consumer"));
            var consumerOptions = new ConsumerOptions(IntegrationConfig.TopicName(), router);
            consumerOptions.PartitionWhitelist = new List<int> { partition };

            var consumer = new OldConsumer(consumerOptions, offsets.Select(x => new OffsetPosition(x.PartitionId, x.Offset)).ToArray());

            var expected = 0;
            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> start Consume"));
            await Task.Run((() =>
            {
                var results = consumer.Consume().Take(numberOfMessage).ToList();
                Assert.IsTrue(results.Count() == numberOfMessage, "not Consume all ,messages");
                IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> done Consume"));

                foreach (var message in results)
                {
                    Assert.That(message.Value.ToUtf8String(), Is.EqualTo(expected.ToString()),
                        "Expected the message list in the correct order.");
                    expected++;
                }
            }));
            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> start producer Dispose"));
            producer.Dispose();
            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> start consumer Dispose"));
            consumer.Dispose();
            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> start router Dispose"));
            router.Dispose();
        }

        [Test]
        [TestCase(1, 70)]
        [TestCase(1000, 70)]
        [TestCase(30000, 550)]
        [TestCase(50000, 850)]
        [TestCase(200000, 8050)]
        public async Task ConsumerShouldConsumeInSameOrderAsAsyncProduced_dataLoad(int numberOfMessage, int timeoutInMs)
        {
            var topicName = IntegrationConfig.TopicName() + numberOfMessage;
            var partition = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create($">> create BrokerRouter ,time Milliseconds:{stopwatch.ElapsedMilliseconds}"));
            var router = new BrokerRouter(IntegrationConfig.IntegrationUri, log: IntegrationConfig.NoDebugLog);
            stopwatch.Restart();
            var producer = new Producer(router, new ProducerConfiguration(batchSize: numberOfMessage / 10, batchMaxDelay: TimeSpan.FromMilliseconds(25)));
            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create($">> create producer ,time Milliseconds:{stopwatch.ElapsedMilliseconds}"));
            stopwatch.Restart();
            var offsets = await producer.BrokerRouter.GetTopicOffsetsAsync(topicName, CancellationToken.None);
            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create($">> request Offset,time Milliseconds:{stopwatch.ElapsedMilliseconds}"));
            stopwatch.Restart();
            var sendList = new List<Task>(numberOfMessage);
            for (var i = 0; i < numberOfMessage; i++)
            {
                var sendTask = producer.SendMessageAsync(new Message(i.ToString()), topicName, partition, new SendMessageConfiguration(acks: 1, codec: MessageCodec.CodecNone), CancellationToken.None);
                sendList.Add(sendTask);
            }
            var maxTimeToRun = TimeSpan.FromMilliseconds(timeoutInMs);
            var doneSend = Task.WhenAll(sendList.ToArray());
            await Task.WhenAny(doneSend, Task.Delay(maxTimeToRun));
            if (!doneSend.IsCompleted) {
                var completed = sendList.Count(t => t.IsCompleted);
                Assert.Fail($"Only finished sending {completed} of {numberOfMessage} in {timeoutInMs} ms.");
            }

            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create($">> done send ,time Milliseconds:{stopwatch.ElapsedMilliseconds}"));
            stopwatch.Restart();

            var consumerOptions = new ConsumerOptions(topicName, router);
            consumerOptions.PartitionWhitelist = new List<int> { partition };
            consumerOptions.MaxWaitTimeForMinimumBytes = TimeSpan.Zero;
            var consumer = new OldConsumer(consumerOptions, offsets.Select(x => new OffsetPosition(x.PartitionId, x.Offset)).ToArray());

            var expected = 0;
            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create($">> start Consume ,time Milliseconds:{stopwatch.ElapsedMilliseconds}"));

            IEnumerable<Message> messages = null;
            var doneConsume = Task.Run((() =>
             {
                 stopwatch.Restart();
                 messages = consumer.Consume().Take(numberOfMessage).ToArray();
                 IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create($">> done Consume ,time Milliseconds:{stopwatch.ElapsedMilliseconds}"));
                 stopwatch.Restart();
             }));

            await Task.WhenAny(doneConsume, Task.Delay(maxTimeToRun));

            Assert.IsTrue(doneConsume.IsCompleted, "not done to Consume in time");
            Assert.IsTrue(messages.Count() == numberOfMessage, "not Consume all ,messages");

            foreach (var message in messages)
            {
                Assert.That(message.Value.ToUtf8String(), Is.EqualTo(expected.ToString()),
                    "Expected the message list in the correct order.");
                expected++;
            }
            stopwatch.Restart();

            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create($">> start producer Dispose ,time Milliseconds:{stopwatch.ElapsedMilliseconds}"));
            producer.Dispose();

            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create($">> start consumer Dispose ,time Milliseconds:{stopwatch.ElapsedMilliseconds}"));
            consumer.Dispose();

            stopwatch.Restart();

            IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create($">> start router Dispose,time Milliseconds:{stopwatch.ElapsedMilliseconds}"));
            router.Dispose();
        }

        [Test]
        public void ConsumerShouldBeAbleToSeekBackToEarlierOffset()
        {
            var expected = new List<string> { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19" };
            var testId = Guid.NewGuid().ToString();

            using (var router = new BrokerRouter(new KafkaOptions(IntegrationConfig.IntegrationUri)))
            using (var producer = new Producer(router))
            {
                var offsets = producer.BrokerRouter.GetTopicOffsetsAsync(IntegrationConfig.TopicName(), CancellationToken.None).Result
                    .Select(x => new OffsetPosition(x.PartitionId, x.Offset)).ToArray();

                using (var consumer = new OldConsumer(new ConsumerOptions(IntegrationConfig.TopicName(), router) { MaxWaitTimeForMinimumBytes = TimeSpan.Zero }, offsets))
                {
                    for (var i = 0; i < 20; i++)
                    {
                        producer.SendMessageAsync(new Message(i.ToString(), testId), IntegrationConfig.TopicName(), CancellationToken.None).Wait();
                    }

                    var sentMessages = consumer.Consume().Take(20).ToList();

                    //ensure the produced messages arrived
                    IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> Message order:  {0}", string.Join(", ", sentMessages.Select(x => x.Value.ToUtf8String()).ToList())));

                    Assert.That(sentMessages.Count, Is.EqualTo(20));
                    Assert.That(sentMessages.Select(x => x.Value.ToUtf8String()).ToList(), Is.EqualTo(expected));
                    Assert.That(sentMessages.Any(x => x.Key.ToUtf8String() != testId), Is.False);

                    //seek back to initial offset
                    consumer.SetOffsetPosition(offsets);

                    var resetPositionMessages = consumer.Consume().Take(20).ToList();

                    //ensure all produced messages arrive again
                    IntegrationConfig.NoDebugLog.Info(() => LogEvent.Create(">> Message order:  {0}", string.Join(", ", resetPositionMessages.Select(x => x.Value).ToList())));

                    Assert.That(resetPositionMessages.Count, Is.EqualTo(20));
                    Assert.That(resetPositionMessages.Select(x => x.Value.ToUtf8String()).ToList(), Is.EqualTo(expected));
                    Assert.That(resetPositionMessages.Any(x => x.Key.ToUtf8String() != testId), Is.False);
                }
            }
        }

        [Test]
        public void ConsumerShouldBeAbleToGetCurrentOffsetInformation()
        {
            using (var router = new BrokerRouter(new KafkaOptions(IntegrationConfig.IntegrationUri)))
            using (var producer = new Producer(router))
            {
                var startOffsets = producer.BrokerRouter.GetTopicOffsetsAsync(IntegrationConfig.TopicName(), CancellationToken.None).Result
                    .Select(x => new OffsetPosition(x.PartitionId, x.Offset)).ToArray();

                using (var consumer = new OldConsumer(new ConsumerOptions(IntegrationConfig.TopicName(), router) { MaxWaitTimeForMinimumBytes = TimeSpan.Zero }, startOffsets))
                {
                    for (var i = 0; i < 20; i++)
                    {
                        producer.SendMessageAsync(new Message(i.ToString(), "1"), IntegrationConfig.TopicName(), CancellationToken.None).Wait();
                    }

                    var results = consumer.Consume().Take(20).ToList();

                    //ensure the produced messages arrived
                    for (var i = 0; i < 20; i++)
                    {
                        Assert.That(results[i].Value.ToUtf8String(), Is.EqualTo(i.ToString()));
                    }

                    //the current offsets should be 20 positions higher than start
                    var currentOffsets = consumer.GetOffsetPosition();
                    Assert.That(currentOffsets.Sum(x => x.Offset) - startOffsets.Sum(x => x.Offset), Is.EqualTo(20));
                }
            }
        }

        [Test]
        public async Task ProducerShouldUsePartitionIdInsteadOfMessageKeyToChoosePartition()
        {
            var partitionSelector = Substitute.For<IPartitionSelector>();
            partitionSelector.Select(null, null)
                             .ReturnsForAnyArgs(_ => _.Arg<MetadataResponse.Topic>().Partitions.Single(p => p.PartitionId == 1));

            var router = new BrokerRouter(new KafkaOptions(IntegrationConfig.IntegrationUri, partitionSelector: partitionSelector));
            var producer = new Producer(router);

            var offsets = await producer.BrokerRouter.GetTopicOffsetsAsync(IntegrationConfig.TopicName(), CancellationToken.None);
            var partitionId = 0;

            //message should send to PartitionId and not use the key to Select Broker Route !!
            for (var i = 0; i < 20; i++)
            {
                await producer.SendMessageAsync(new Message(i.ToString(), "key"), IntegrationConfig.TopicName(), partitionId, new SendMessageConfiguration(acks: 1, codec: MessageCodec.CodecNone), CancellationToken.None);
            }

            //consume form partitionId to verify that date is send to currect partion !!.
            var consumer = new OldConsumer(new ConsumerOptions(IntegrationConfig.TopicName(), router) { PartitionWhitelist = { partitionId } }, offsets.Select(x => new OffsetPosition(x.PartitionId, x.Offset)).ToArray());

            for (var i = 0; i < 20; i++)
            {
                Message result = null;// = consumer.Consume().Take(1).First();
                await Task.Run(() => result = consumer.Consume().Take(1).First());
                Assert.That(result.Value.ToUtf8String(), Is.EqualTo(i.ToString()));
            }

            consumer.Dispose();
            producer.Dispose();
        }

        [Test]
        public void ConsumerShouldNotLoseMessageWhenBlocked()
        {
            var testId = Guid.NewGuid().ToString();

            using (var router = new BrokerRouter(new KafkaOptions(IntegrationConfig.IntegrationUri)))
            using (var producer = new Producer(router))
            {
                var offsets = producer.BrokerRouter.GetTopicOffsetsAsync(IntegrationConfig.TopicName(), CancellationToken.None).Result;

                //create consumer with buffer size of 1 (should block upstream)
                using (var consumer = new OldConsumer(new ConsumerOptions(IntegrationConfig.TopicName(), router) { ConsumerBufferSize = 1, MaxWaitTimeForMinimumBytes = TimeSpan.Zero },
                      offsets.Select(x => new OffsetPosition(x.PartitionId, x.Offset)).ToArray()))
                {
                    for (var i = 0; i < 20; i++)
                    {
                        producer.SendMessageAsync(new Message(i.ToString(), testId), IntegrationConfig.TopicName(), CancellationToken.None).Wait();
                    }

                    for (var i = 0; i < 20; i++)
                    {
                        var result = consumer.Consume().Take(1).First();
                        Assert.That(result.Key.ToUtf8String(), Is.EqualTo(testId));
                        Assert.That(result.Value.ToUtf8String(), Is.EqualTo(i.ToString()));
                    }
                }
            }
        }

        [Test]
        public async Task ConsumerShouldMoveToNextAvailableOffsetWhenQueryingForNextMessage()
        {
            const int expectedCount = 1000;
            var options = new KafkaOptions(IntegrationConfig.IntegrationUri);

            using (var producerRouter = new BrokerRouter(options))
            using (var producer = new Producer(producerRouter))
            {
                //get current offset and reset consumer to top of log
                var offsets = await producer.BrokerRouter.GetTopicOffsetsAsync(IntegrationConfig.TopicName(), CancellationToken.None).ConfigureAwait(false);

                using (var consumerRouter = new BrokerRouter(options))
                using (var consumer = new OldConsumer(new ConsumerOptions(IntegrationConfig.TopicName(), consumerRouter) { MaxWaitTimeForMinimumBytes = TimeSpan.Zero },
                     offsets.Select(x => new OffsetPosition(x.PartitionId, x.Offset)).ToArray()))
                {
                    Console.WriteLine("Sending {0} test messages", expectedCount);
                    var response = await producer.SendMessagesAsync(Enumerable.Range(0, expectedCount).Select(x => new Message(x.ToString())), IntegrationConfig.TopicName(), CancellationToken.None);

                    Assert.That(response.Any(x => x.ErrorCode != (int)ErrorResponseCode.None), Is.False, "Error occured sending test messages to server.");

                    var stream = consumer.Consume();

                    Console.WriteLine("Reading message back out from consumer.");
                    var data = stream.Take(expectedCount).ToList();

                    var consumerOffset = consumer.GetOffsetPosition().OrderBy(x => x.PartitionId).ToList();

                    var serverOffset = await producer.BrokerRouter.GetTopicOffsetsAsync(IntegrationConfig.TopicName(), CancellationToken.None).ConfigureAwait(false);
                    var positionOffset = serverOffset.Select(x => new OffsetPosition(x.PartitionId, x.Offset))
                        .OrderBy(x => x.PartitionId)
                        .ToList();

                    Assert.That(consumerOffset, Is.EqualTo(positionOffset), "The consumerOffset position should match the server offset position.");
                    Assert.That(data.Count, Is.EqualTo(expectedCount), "We should have received 2000 messages from the server.");
                }
            }
        }
    }
}