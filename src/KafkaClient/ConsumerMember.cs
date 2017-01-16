using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KafkaClient.Assignment;
using KafkaClient.Common;
using KafkaClient.Protocol;
using Nito.AsyncEx;

namespace KafkaClient
{
    public class ConsumerMember : IConsumerMember
    {
        private readonly IConsumer _consumer;
        private IRouter Router => _consumer.Router;
        private IConsumerConfiguration Configuration => _consumer.Configuration;

        public ConsumerMember(IConsumer consumer, JoinGroupRequest request, JoinGroupResponse response, ILog log = null)
        {
            _consumer = consumer;
            Log = log ?? consumer.Router?.Log ?? TraceLog.Log;

            GroupId = request.GroupId;
            MemberId = response.MemberId;
            ProtocolType = request.ProtocolType;

            OnJoinGroup(response);

            // This thread will heartbeat on the appropriate frequency
            _heartbeatDelay = TimeSpan.FromMilliseconds(request.SessionTimeout.TotalMilliseconds / 2);
            _heartbeatTimeout = request.SessionTimeout;
            _heartbeatTask = Task.Factory.StartNew(DedicatedHeartbeatTask, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private readonly CancellationTokenSource _disposeToken = new CancellationTokenSource();
        private int _disposeCount;
        private int _activeHeartbeatCount;
        private readonly Task _heartbeatTask;
        private readonly TimeSpan _heartbeatDelay;
        private readonly TimeSpan _heartbeatTimeout;

        private readonly AsyncLock _lock = new AsyncLock();
        private readonly AsyncManualResetEvent _isAssigned = new AsyncManualResetEvent(false);
        private ImmutableDictionary<string, IMemberMetadata> _memberMetadata = ImmutableDictionary<string, IMemberMetadata>.Empty;
        private IImmutableDictionary<TopicPartition, IMessageBatch> _batches = ImmutableDictionary<TopicPartition, IMessageBatch>.Empty;
        private IMemberAssignment _assignment;

        public ILog Log { get; }
        public bool IsLeader { get; private set; }
        public int GenerationId { get; private set; }
        private string _groupProtocol;

        public string GroupId { get; }
        public string MemberId { get; }

        /// <summary>
        /// State machine for Member state
        /// See https://cwiki.apache.org/confluence/display/KAFKA/Kafka+Client-side+Assignment+Proposal for basis
        /// </summary>
        /// <remarks>
        ///                            +===========+
        ///                            [           ]
        ///     +----------------------+  Assign   ]
        ///     |                      [           ]
        ///     |                      +=====+=====+
        ///     |                            ^
        ///     | SyncGroupRequest           | JoinGroupResponse
        ///     | (only leader assigns)      | ErrorResponseCode.None
        ///     |                            |
        ///     |                      +-----+-----+
        ///     |                      |           |
        ///     |                  +--->  Joining  |
        ///     |                  |   |           |
        ///     |                  |   +-----+-----+
        ///     |                  |         |
        ///     |        JoinGroup |         | JoinGroupResponse
        ///     |        Request   |         | ErrorResponseCode.GroupCoordinatorNotAvailable
        ///     |                  |         | ErrorResponseCode.GroupLoadInProgress
        ///     |                  |         v                 
        ///     |                  |   +-----+-----+
        ///     |                  +---+           |
        ///     |                      |  Rejoin   |
        ///     |  +------------------->           |
        ///     |  | SyncGroupResponse +-----+-----+
        ///     v  | RebalanceInProgress     ^
        ///  +--+--+-----+                   | HeartbeatResponse
        ///  |           |                   | ErrorResponseCode.RebalanceInProgress
        ///  |  Syncing  |                   |
        ///  |           |             +-----+--------+
        ///  +-----+-----+             |              |
        ///        |               +---> Heartbeating |
        ///        |               |   |              |
        ///        |               |   +-----+--------+
        ///        |               |         |
        ///        |     Heartbeat |         | HeartbeatResponse
        ///        |     Request   |         | ErrorResponseCode.None
        ///        |               |         v
        ///        |               |   +-----+-----+
        ///        |               +---+           |
        ///        |                   |  Stable   |
        ///        +------------------->           |
        ///        SyncGroupResponse   +-----------+ 
        ///        ErrorResponseCode.None                   
        /// </remarks>
        private enum MemberState
        {
            Assign,
            //Syncing,
            Stable,
            //Heartbeating,
            Rejoin
            //Joining
        }

        /// <summary>
        /// See https://cwiki.apache.org/confluence/display/KAFKA/Kafka+Client-side+Assignment+Proposal for details
        /// </summary>
        private async Task DedicatedHeartbeatTask()
        {
            try {
                // only allow one heartbeat to execute, dump out all other requests
                if (Interlocked.Increment(ref _activeHeartbeatCount) != 1) return;

                var state = MemberState.Assign;
                var response = ErrorResponseCode.None;
                var lastHeartbeat = DateTimeOffset.UtcNow;
                while (!(_disposeToken.IsCancellationRequested || IsOverdue(lastHeartbeat) || IsUnrecoverable(response))) {
                    try {
                        var currentState = state;
                        Log.Info(() => LogEvent.Create($"Local state {currentState} for member {MemberId}"));
                        switch (currentState) {
                            case MemberState.Assign:
                                response = await SyncGroupAsync(_disposeToken.Token).ConfigureAwait(false);
                                if (response.IsSuccess()) {
                                    state = MemberState.Stable;
                                } else if (response == ErrorResponseCode.RebalanceInProgress) {
                                    state = MemberState.Rejoin;
                                }
                                break;

                            case MemberState.Stable:
                                await Task.Delay(_heartbeatDelay, _disposeToken.Token).ConfigureAwait(false);
                                response = await SendHeartbeatAsync(_disposeToken.Token).ConfigureAwait(false);
                                if (response == ErrorResponseCode.RebalanceInProgress) {
                                    state = MemberState.Rejoin;
                                }
                                break;

                            case MemberState.Rejoin:
                                response = await JoinGroupAsync(_disposeToken.Token).ConfigureAwait(false);
                                if (response.IsSuccess()) {
                                    state = MemberState.Assign;
                                } else if (response == ErrorResponseCode.GroupCoordinatorNotAvailable || response == ErrorResponseCode.GroupLoadInProgress) {
                                    state = MemberState.Rejoin;
                                }
                                break;

                            default:
                                Log.Warn(() => LogEvent.Create($"Unexpected local state {currentState} for member {MemberId}"));
                                state = MemberState.Assign;
                                await Task.Delay(1, _disposeToken.Token).ConfigureAwait(false);
                                continue; // while
                        }
                        if (response.IsSuccess()) {
                            lastHeartbeat = DateTimeOffset.UtcNow;
                        }
                    } catch (OperationCanceledException) { // cancellation token fired while attempting to get tasks: normal behavior
                    } catch (Exception ex) {
                        Log.Warn(() => LogEvent.Create(ex));
                    }
                }
                await DisposeAsync(CancellationToken.None);
            } catch (OperationCanceledException) { // cancellation token fired while attempting to get tasks: normal behavior
            } catch (Exception ex) {
                Log.Warn(() => LogEvent.Create(ex));
            } finally {
                Interlocked.Decrement(ref _activeHeartbeatCount);
                Log.Info(() => LogEvent.Create($"Stopped heartbeat for {GroupId}/{MemberId}"));
            }
        }

        private bool IsOverdue(DateTimeOffset lastHeartbeat)
        {
            return _heartbeatTimeout < DateTimeOffset.UtcNow - lastHeartbeat;
        }

        private bool IsUnrecoverable(ErrorResponseCode? response)
        {
            return response == ErrorResponseCode.IllegalGeneration 
                || response == ErrorResponseCode.UnknownMemberId;
        }

        private async Task<ErrorResponseCode> JoinGroupAsync(CancellationToken cancellationToken)
        {
            if (_disposeCount > 0) throw new ObjectDisposedException($"Member {MemberId} is no longer valid");

            IEnumerable<IMemberMetadata> memberMetadata = null;
            using (_lock.Lock()) {
                if (IsLeader) {
                    memberMetadata = _memberMetadata.Values;
                }
            }

            // on success, this will call OnRejoin before returning
            try {
                await _consumer.JoinGroupAsync(GroupId, ProtocolType, memberMetadata, cancellationToken, this);
                return ErrorResponseCode.None;
            } catch (RequestException ex) when (ex.ApiKey == ApiKeyRequestType.JoinGroup) {
                return ex.ErrorCode;
            }
        }

        public void OnJoinGroup(JoinGroupResponse response)
        {
            if (response.MemberId != MemberId) throw new ArgumentOutOfRangeException(nameof(response), $"Member is not valid ({MemberId} != {response.MemberId})");
            if (_disposeCount > 0) throw new ObjectDisposedException($"Member {MemberId} is no longer valid");

            using (_lock.Lock()) {
                IsLeader = response.LeaderId == MemberId;
                GenerationId = response.GenerationId;
                _groupProtocol = response.GroupProtocol;
                _memberMetadata = response.Members.ToImmutableDictionary(member => member.MemberId, member => member.Metadata);
            }
        }

        public async Task<ErrorResponseCode> SendHeartbeatAsync(CancellationToken cancellationToken)
        {
            var request = new HeartbeatRequest(GroupId, GenerationId, MemberId);
            var response = await Router.SendAsync(request, GroupId, cancellationToken, retryPolicy: Configuration.GroupCoordinationRetry).ConfigureAwait(false);
            return response.ErrorCode;
        }

        public async Task<ErrorResponseCode> SyncGroupAsync(CancellationToken cancellationToken)
        {
            using (await _lock.LockAsync(cancellationToken)) {
                if (_disposeCount > 0) throw new ObjectDisposedException($"Member {MemberId} is no longer valid");
                await Task.WhenAll(_batches.Values.Select(b => b.CommitMarkedAsync(cancellationToken)));
                IEnumerable<SyncGroupRequest.GroupAssignment> groupAssignments = null;
                if (IsLeader) {
                    var encoder = _consumer.Encoders[ProtocolType];
                    var assigner = encoder.GetAssignor(_groupProtocol);
                    var assignments = await assigner.AssignMembersAsync(Router, GroupId, GenerationId, _memberMetadata, cancellationToken).ConfigureAwait(false);
                    groupAssignments = assignments.Select(pair => new SyncGroupRequest.GroupAssignment(pair.Key, pair.Value));
                }

                var request = new SyncGroupRequest(GroupId, GenerationId, MemberId, groupAssignments);
                var response = await Router.SyncGroupAsync(request, new RequestContext(protocolType: ProtocolType), Configuration.GroupCoordinationRetry, cancellationToken).ConfigureAwait(false);
                if (!response.ErrorCode.IsSuccess()) return response.ErrorCode;

                _assignment = response.MemberAssignment;
                var validPartitions = _assignment.PartitionAssignments.ToImmutableHashSet();
                var invalidPartitions = _batches.Where(pair => !validPartitions.Contains(pair.Key)).ToList();
                foreach (var invalidPartition in invalidPartitions) {
                    invalidPartition.Value.Dispose();
                }
                _batches = _batches.RemoveRange(invalidPartitions.Select(pair => pair.Key));
                _isAssigned.Set();
                return ErrorResponseCode.None;
            }
        }

        /// <summary>
        /// Leave the consumer group and stop heartbeats.
        /// </summary>
        public async Task DisposeAsync(CancellationToken cancellationToken)
        {
            // skip multiple calls to dispose
            if (Interlocked.Increment(ref _disposeCount) != 1) return;

            _disposeToken.Cancel();
            using (await _lock.LockAsync(cancellationToken)) {
                await Task.WhenAll(_batches.Values.Select(b => b.CommitMarkedAsync(cancellationToken)));
                foreach (var batch in _batches.Values) {
                    batch.Dispose();
                }
                _batches = ImmutableDictionary<TopicPartition, IMessageBatch>.Empty;
                _assignment = null;

                if (cancellationToken == CancellationToken.None) {
                    await Task.WhenAny(_heartbeatTask, Task.Delay(TimeSpan.FromSeconds(1), cancellationToken));
                    var request = new LeaveGroupRequest(GroupId, MemberId, false);
                    await Router.SendAsync(request, GroupId, cancellationToken, retryPolicy: Configuration.GroupCoordinationRetry).ConfigureAwait(false);
                } else {
                    await _heartbeatTask.WaitAsync(cancellationToken);
                    var request = new LeaveGroupRequest(GroupId, MemberId);
                    var response = await Router.SendAsync(request, GroupId, cancellationToken, retryPolicy: Configuration.GroupCoordinationRetry).ConfigureAwait(false);
                    if (!response.ErrorCode.IsSuccess()) {
                        throw request.ExtractExceptions(response);
                    }
                }
            }
        }

        public void Dispose()
        {
            AsyncContext.Run(() => DisposeAsync(CancellationToken.None));
        }

        public string ProtocolType { get; }

        public async Task<IMessageBatch> FetchBatchAsync(CancellationToken cancellationToken, int? batchSize = null)
        {
            await _isAssigned.WaitAsync(cancellationToken);
            using (await _lock.LockAsync(cancellationToken)) {
                if (_disposeCount > 0) throw new ObjectDisposedException($"Member {MemberId} is no longer valid");
                if (_assignment == null) return MessageBatch.Empty;

                foreach (var partition in _assignment.PartitionAssignments) {
                    if (!_batches.ContainsKey(partition)) {
                        var batch = await _consumer.FetchBatchAsync(GroupId, MemberId, GenerationId, partition.TopicName, partition.PartitionId, cancellationToken, batchSize);
                        _batches = _batches.Add(partition, batch);
                        return batch;
                    }
                }
            }
            return MessageBatch.Empty;
        }
    }
}