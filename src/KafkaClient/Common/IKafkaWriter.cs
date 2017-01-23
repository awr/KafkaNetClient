using System;
using System.Collections.Generic;
using System.IO;

namespace KafkaClient.Common
{
    public interface IKafkaWriter : IDisposable
    {
        IKafkaWriter Write(bool value);
        IKafkaWriter Write(short value);
        IKafkaWriter Write(int value);
        IKafkaWriter Write(long value);

        IKafkaWriter Write(byte value);
        IKafkaWriter Write(ArraySegment<byte> values, bool includeLength = true);

        IKafkaWriter Write(string value);
        IKafkaWriter Write(IEnumerable<string> values, bool includeLength = false);

        byte[] ToBytes();
        byte[] ToBytesNoLength();
        ArraySegment<byte> ToSegment();
        ArraySegment<byte> ToSegmentNoLength();

        IDisposable MarkForLength();
        IDisposable MarkForCrc();

        Stream Stream { get; }
    }
}