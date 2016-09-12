﻿using System.Collections.Immutable;

namespace KafkaClient.Protocol
{
    public interface IKafkaResponse
    {
        /// <summary>
        /// Any errors from the server
        /// </summary>
        ImmutableList<ErrorResponseCode> Errors { get; }
    }
}