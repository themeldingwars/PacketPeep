using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetCoreServer;
using PacketPeep.FauFau.Formats;
using PacketPeep.Systems.Tinker;

namespace PacketPeep.Systems
{
    public class ReplayServer : UdpServer
    {
        public Nsr                               ReplayFile;
        public Dictionary<EndPoint, ClientState> ConnectedClients = new();

        public ReplayServer(ushort port = 44500) : base(IPAddress.Any, port)
        {
            PacketPeepTool.Log.AddLogInfo(LogCategories.ReplayEditor, $"ReplayServer started :>");
        }

        protected override void OnStarted()
        {
            ReceiveAsync();
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
        {
            PacketPeepTool.Log.AddLogInfo(LogCategories.ReplayEditor, $"{buffer}");

            // From the client
            if (buffer[offset] == 0x02) {
                if (ConnectedClients.TryGetValue(endpoint, out var clientState)) {
                }
                else {
                    var clState = new ClientState();
                    ConnectedClients.Add(endpoint, clState);
                }
            }
        }

        protected override void OnSent(EndPoint endpoint, long sent)
        {
            // Continue receive datagrams
            ReceiveAsync();
        }

        private void SendMeta(EndPoint ep, uint timestamp = 1)
        {
            var meta = new JustMeta();
            meta.Meta = ReplayFile.HeaderData.Meta;
            var metaSize = meta.GetPackedSize();
            var buffer   = new byte[5000];

            var unkHeader = new byte[]
            {
                0x00, 0x00, 0x00, 0x00, 0x00, 0x5F, 0x4C, 0x00, 0x00, 0x21, 0x0E, 0x59, 0xA1, 0xAF, 0x46, 0x05,
                0x00, 0x88, 0x13, 0x00, 0x00, 0x6F, 0x6E, 0x2D, 0x49, 0x4C
            };
            BinaryPrimitives.WriteUInt32LittleEndian(unkHeader.AsSpan().Slice(9, 4), timestamp);

            unkHeader.CopyTo(buffer.AsSpan().Slice(0, unkHeader.Length));

            buffer[27] = 0x01;
            var size = meta.Pack(buffer.AsSpan().Slice(27));

            Send(ep, buffer.AsSpan().Slice(0, size + 4).ToArray());
        }

        // Send the meta to all clients
        public void SendMeta(MetaSection meta, uint timestamp = 1)
        {
            foreach (var clientKvp in ConnectedClients) {
                var buffer = new byte[5000];

                var unkHeader = new byte[]
                {
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x5F, 0x4C, 0x00, 0x00, 0x21, 0x0E, 0x59, 0xA1, 0xAF, 0x46, 0x05,
                    0x00, 0x88, 0x13, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x58, 0x01 //0x6F, 0x6E, 0x2D, 0x49, 0x4C
                };
                BinaryPrimitives.WriteUInt32LittleEndian(unkHeader.AsSpan().Slice(9, 4), timestamp);
                
                // 1470710718148231
                // 1470710728148231
                
                unkHeader.CopyTo(buffer.AsSpan()[..unkHeader.Length]);
                var metaPacker = new JustMeta();
                metaPacker.Meta = meta;
                var size = metaPacker.Pack(buffer.AsSpan()[(unkHeader.Length)..]);

                var metaBuff = new byte[]
                {
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x5F, 0x4C, 0x00, 0x00, 0x04, 0xFF, 0xDD, 0x9C, 0x70, 0x4D, 0x05,
                    0x00, 0x88, 0x13, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x58, 0x01, 0x04, 0x00, 0x00, 0x00, 0xEB,
                    0x03, 0x00, 0x00, 0x28, 0x6E, 0x6F, 0x20, 0x72, 0x65, 0x63, 0x6F, 0x72, 0x64, 0x69, 0x6E, 0x67,
                    0x20, 0x64, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E, 0x29, 0x00, 0x57, 0x65,
                    0x64, 0x20, 0x4F, 0x63, 0x74, 0x20, 0x32, 0x37, 0x20, 0x32, 0x33, 0x3A, 0x30, 0x34, 0x3A, 0x35,
                    0x38, 0x20, 0x32, 0x30, 0x32, 0x31, 0x00, 0x4F, 0x65, 0x78, 0x43, 0xA3, 0xF3, 0xE3, 0x42, 0x5D,
                    0x12, 0xF2, 0x42, 0xC3, 0x59, 0xF8, 0x3D, 0x53, 0x73, 0x1A, 0xBE, 0x06, 0xBE, 0x43, 0x3F, 0x87,
                    0x5F, 0x1D, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x28, 0x75, 0x6E, 0x64, 0x65,
                    0x66, 0x69, 0x6E, 0x65, 0x64, 0x20, 0x63, 0x68, 0x61, 0x72, 0x61, 0x63, 0x74, 0x65, 0x72, 0x29,
                    0x00, 0x0D, 0x03, 0xFB, 0x00, 0x14, 0x74, 0x1B, 0x5B, 0x58, 0x46, 0x00, 0xFA, 0x00, 0xB8, 0xDA,
                    0xC6, 0x58, 0x46, 0x46, 0x69, 0x72, 0x65, 0x66, 0x61, 0x6C, 0x6C, 0x20, 0x28, 0x76, 0x31, 0x2E,
                    0x35, 0x2E, 0x31, 0x39, 0x36, 0x32, 0x29, 0x00, 0x46, 0x4A, 0x91, 0x9C, 0x70, 0x4D, 0x05, 0x00,
                    0x04, 0x00, 0x00, 0x00, 0x12, 0x00, 0x00, 0x00, 0xE1, 0x07, 0x00, 0x00, 0xC0, 0x08, 0x00, 0x00,
                    0x06, 0x45, 0x0C, 0x3F, 0x54, 0x75, 0x65, 0x73, 0x64, 0x61, 0x79, 0x2C, 0x20, 0x41, 0x70, 0x72,
                    0x69, 0x6C, 0x20, 0x31, 0x38, 0x2E, 0x35, 0x34, 0x38, 0x20, 0x5A, 0x75, 0x6C, 0x75, 0x20, 0x32,
                    0x32, 0x34, 0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x45, 0xB6, 0xA2, 0xB4, 0xD4, 0x3F, 0x03, 0x00, 0xB4, 0x76,
                    0x9B, 0x30, 0x67, 0xE7, 0x3F, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xD0, 0x3F, 0x00, 0x00,
                    0x00, 0x00, 0x02
                };

                //Send(clientKvp.Key, metaBuff);
                Send(clientKvp.Key, buffer.AsSpan().Slice(0, unkHeader.Length + size + 200).ToArray());

                //var update = new byte[] {0xC, 0x13, 0x2, 0xC8, 0xCE, 0xB1, 0x54, 0x1F, 0x6E, 0x7A, 0x92, 0x38, 0x72, 0x74, 0xE, 0xD5, 0x63, 0x32, 0xA};
                //Send(clientKvp.Key, update);
            }
        }

        public void SendUpdate(Span<byte> data, uint timestamp = 1)
        {
            var totalLen = 9 + data.Length;
            var buffer   = new byte[totalLen];
            buffer[0] = 0x01;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan().Slice(1, 4), timestamp);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan().Slice(5, 2), (ushort) (data.Length));
            buffer[7] = 3;
            buffer[8] = 0;

            data.CopyTo(buffer.AsSpan()[9..]);

            foreach (var clientKvp in ConnectedClients) {
                Send(clientKvp.Key, buffer);
            }
        }

        public void SendKeyframe(Keyframe keyframe, ulong entityId, uint timestamp = 1)
        {
            var buffer = new byte[5000].AsSpan();

            BinaryPrimitives.WriteUInt64LittleEndian(buffer[..8], entityId);
            buffer[0] = keyframe.Id;
            buffer[8] = 3;

            var writtenLen = keyframe.Frame.Pack(buffer[13..]);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(9, 4), writtenLen);
            var sendBuffer = buffer[..(writtenLen + 13)];

            var currentTime = (uint)(DateTime.Now.Ticks / 100000);
            SendUpdate(sendBuffer, timestamp);
        }
        
        public void SendMessage(byte controllerId, ulong entityId, byte msgId, Span<byte> msgBuffer, uint timestamp = 1)
        {
            var buffer = new byte[13 + msgBuffer.Length].AsSpan();

            BinaryPrimitives.WriteUInt64LittleEndian(buffer[..8], entityId);
            buffer[0] = controllerId;
            buffer[8] = msgId;
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(9, 4), msgBuffer.Length);
            
            msgBuffer.CopyTo(buffer[13..]);
            
            SendUpdate(buffer, timestamp);
        }

        public void SendControllerRemove(byte controllerId, ulong entityId, uint timestamp = 1) => SendMessage(controllerId, entityId, 5, Span<byte>.Empty);
        
        /*public void SendControllerRemove(byte controllerId, ulong entityId, uint timestamp = 1)
        {
            var buffer = new byte[5000].AsSpan();

            BinaryPrimitives.WriteUInt64LittleEndian(buffer[..8], entityId);
            buffer[0] = controllerId;
            buffer[8] = 5;

            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(9, 4), 0);

            var currentTime = (uint)(DateTime.Now.Ticks / 100000);
            SendUpdate(buffer, timestamp);
        }*/
    }

    public struct ClientState
    {
    }
}