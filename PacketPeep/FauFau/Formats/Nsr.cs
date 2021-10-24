using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Aero.Gen;
using Aero.Gen.Attributes;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace PacketPeep.FauFau.Formats
{
    public class Nsr
    {
        public Header     HeaderData;
        public KeyFrame[] KeyFrames;

        public void Read(string filePath)
        {
            var        data         = File.ReadAllBytes(filePath).AsSpan();
            var        magic        = BinaryPrimitives.ReadUInt32LittleEndian(data[..4]);
            var        isCompressed = magic == 559903;
            Span<byte> payloadData;
            int        headerSize = 0;

            HeaderData = new Header();

            if (isCompressed) {
                var decompressedData = UnGzipUnknownTargetSize(data);
                headerSize  = HeaderData.Unpack(decompressedData);
                payloadData = decompressedData[headerSize..];
            }
            else {
                headerSize  = HeaderData.Unpack(data);
                payloadData = data[headerSize..];
            }

            KeyFrames = new KeyFrame[HeaderData.Index.IndexCount];
            for (int i = 0; i < KeyFrames.Length; i++) KeyFrames[i] = new();

            // Cut it into keyframe slices by the index offsets
            for (int i = 0; i < HeaderData.Index.IndexCount; i++) {
                var idxOffset    = HeaderData.Index.IndexOffsets[i] - headerSize;
                var hasNextIndex = i                                                   + 1 < HeaderData.Index.IndexCount;
                var endOffset    = hasNextIndex ? HeaderData.Index.IndexOffsets[i + 1] - headerSize : payloadData.Length;
                var keyFrameData = payloadData.Slice(idxOffset, endOffset - idxOffset);
                ReadKeyframe(keyFrameData, i);
            }
        }

        private static Span<byte> UnGzipUnknownTargetSize(Span<byte> source, CompressionLevel level = CompressionLevel.Default)
        {
            using (MemoryStream payload = new MemoryStream(source.ToArray()))
            using (MemoryStream inflated = new MemoryStream())
            using (GZipStream ds = new GZipStream(payload, CompressionMode.Decompress, level)) {
                ds.CopyTo(inflated);
                return inflated.ToArray().AsSpan();
            }
        }

        private void ReadKeyframe(Span<byte> data, int idx)
        {
            int offset = 0;
            do {
                var slice = data.Slice(offset);
                var frame = new Frame();
                var read  = frame.Unpack(slice);
                offset += read;

                KeyFrames[idx].Frames.Add(frame);
            } while (offset < data.Length);
        }
    }

    [Aero]
    public partial class Header
    {
        public NsrDescription Description;
        public IndexSection   Index;
        public MetaSection    Meta;
    }

    [AeroBlock]
    public struct NsrDescription
    {
        [AeroString(4)] public string Magic;

        public int Version;
        public int HeaderLength;
        public int MetaLength;
        public int DescLength;
        public int DataOffset;
        public int Unk1;

        public int   ProtocolVersion;
        public ulong Timestamp;

        public uint Unk2;
        public uint Unk3;
    }

    [AeroBlock]
    public struct IndexSection
    {
        [AeroString(4)] public string Magic;

        public int  Version;
        public long Unk;
        public int  IndexCount;
        public int  IndexOffset;

        [AeroArray(nameof(IndexCount))] public int[] IndexOffsets;
    }

    [AeroBlock]
    public struct MetaSection
    {
        public              int     Version;
        public              int     ZoneId;
        [AeroString] public string  Description;
        [AeroString] public string  LocalDateString;
        public              Vector3 Position;
        public              Vector4 Rotation;
        public              ulong   CharacterGUID;
        [AeroString] public string  CharacterName;

        [AeroArray(18)] public byte[] Unk2;
        [AeroString]    public string FirefallVersionString;
        public                 ulong  TimeStamp;

        public int Month;
        public int Day;
        public int RealYear;
        public int FictionalYear;
        float      FictionalTime;

        [AeroString] public string FictionalDateString;

        [AeroArray(31)] public byte[] Unk3;
    }

    [Aero]
    public partial class Frame
    {
        public uint   TimeStamp;
        public ushort Length;
        public byte   Unk1;
        public byte   Unk2;

        [AeroArray(nameof(Length))] public byte[] Data;

        public byte  ControllerId => Data[0];
        public ulong EntityId     => BinaryPrimitives.ReadUInt64LittleEndian(Data.AsSpan()[..8]) >> 8;

        public byte MsgIdMaybe => Data[8];
        public int  WeirdInt   => Data.Length > 12 ? BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan().Slice(8, 4)) : -1;
    }

    public class KeyFrame
    {
        public List<Frame> Frames = new();
    }
}