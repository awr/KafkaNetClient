using System;
using System.Collections.Immutable;

namespace KafkaClient.Protocol
{
    /// <summary>
    /// LeaveGroup Response => *throttle_time_ms error_code 
    /// </summary>
    /// <remarks>
    /// LeaveGroup Response => *throttle_time_ms error_code 
    ///   throttle_time_ms => INT32
    ///   error_code => INT16
    /// 
    /// Version 1+: throttle_time_ms
    /// From http://kafka.apache.org/protocol.html#The_Messages_LeaveGroup
    /// </remarks>
    public class LeaveGroupResponse : ThrottledResponse, IResponse, IEquatable<LeaveGroupResponse>
    {
        public override string ToString() => $"{{{this.ThrottleToString()},error_code:{Error}}}";

        public static LeaveGroupResponse FromBytes(IRequestContext context, ArraySegment<byte> bytes)
        {
            using (var reader = new KafkaReader(bytes)) {
                var throttleTime = reader.ReadThrottleTime(context.ApiVersion >= 1);
                var errorCode = (ErrorCode)reader.ReadInt16();
                return new LeaveGroupResponse(errorCode, throttleTime);
            }
        }

        public LeaveGroupResponse(ErrorCode errorCode, TimeSpan? throttleTime = null)
            : base(throttleTime)
        {
            Error = errorCode;
            Errors = ImmutableList<ErrorCode>.Empty.Add(Error);
        }

        /// <inheritdoc />
        public IImmutableList<ErrorCode> Errors { get; }

        public ErrorCode Error { get; }

        #region Equality

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals(obj as LeaveGroupResponse);
        }

        /// <inheritdoc />
        public bool Equals(LeaveGroupResponse other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other)
                && Error == other.Error;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked {
                var hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ Error.GetHashCode();
                return hashCode;
            }
        }

        #endregion
    }
}