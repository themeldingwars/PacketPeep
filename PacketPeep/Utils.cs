using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FauCap;
using ImGuiNET;
using PacketPeep.Systems;

namespace PacketPeep
{
    public static class Utils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetMessageName(Message msg)
        {
            var gameMsg = msg as GameMessage;

            if (msg.Server == Server.Game) {
                var id = msg is SubMessage subMsg ? (byte)(subMsg.EntityId & 0x00000000000000FF) : gameMsg.Data[0];
                switch (gameMsg.Channel) {
                    case Channel.Control:
                        return PacketPeepTool.PcktDb.GetMessageName(PacketDb.CONTROL_REF_ID, id);

                    case Channel.Matrix:
                        return PacketPeepTool.PcktDb.GetMessageName(PacketDb.MATRIX_REF_ID, id);

                    case Channel.ReliableGss:
                    case Channel.UnreliableGss:
                    {
                        var msgId    = msg is SubMessage subMsg2 ? subMsg2.Data[0] : gameMsg.Data[8];
                        var viewName = PacketPeepTool.PcktDb.GetViewName(id);
                        var msgName  = gameMsg.FromServer || id is 0 or 251 ? PacketPeepTool.PcktDb.GetMessageName(id, msgId) : PacketPeepTool.PcktDb.GetCommandName(id, msgId);
                        return $"{viewName}::{msgName}";
                    }
                }
            }
            else if (msg.Server == Server.Matrix && msg.Data.Length >= 4) {
                var name = MemoryMarshal.Read<uint>(msg.Data[..4]) switch
                {
                    1162563408 => "POKE",
                    1162364232 => "HEHE",
                    1195857224 => "HUGG",
                    1397967179 => "KISS",
                    1414677057 => "ABRT",
                    _          => string.Join("", msg.Data[..4].ToArray().Select(x => (char) x))
                };

                return name;
            }

            return "";
        }

        public static Vector4 GetMessageChannelColor(Message msg)
        {
            if (msg.Server == Server.Game) {
                var gameMsg = msg as GameMessage;
                var color = gameMsg.Channel switch
                {
                    Channel.Control       => Config.Inst.PColors[Config.Colors.Control],
                    Channel.Matrix        => Config.Inst.PColors[Config.Colors.Matrix],
                    Channel.ReliableGss   => Config.Inst.PColors[Config.Colors.RGSS],
                    Channel.UnreliableGss => Config.Inst.PColors[Config.Colors.UGSS],
                };

                return color;
            }

            return Config.Inst.PColors[Config.Colors.Jack];
        }
        
        public static void DrawTableSimpleRow(string name, string text)
        {
            ImGui.TableNextColumn();
            ImGui.Text(name);
            ImGui.TableNextColumn();
            ImGui.Text(text);
        }

        public static MessageHeader GetGssMessageHeader(Message msg)
        {
            var isGameMsg   = msg is GameMessage;
            var gameMessage = msg as GameMessage;
            var isGss       = isGameMsg && gameMessage.Channel is Channel.ReliableGss or Channel.UnreliableGss;

            var header = new MessageHeader
            {
                IsCommand = !msg.FromServer
            };

            if (isGameMsg) {
                header.Channel = gameMessage.Channel;
            }
            else if (msg is MatrixMessage) {
                header.Channel = Channel.Matrix;
            }
            else {
                header.Channel = Channel.Control;
            }

            if (msg is SubMessage subMessage) {
                header.ControllerId = (int)(subMessage.EntityId & 0x00000000000000FF);
                header.MessageId    = msg.Data[0];
                header.EntityId     = subMessage.EntityId;
                header.Length       = 1;
            }
            else if (isGss) {
                header.ControllerId = msg.Data[0];
                header.MessageId    = msg.Data[8];
                header.EntityId     = BitConverter.ToUInt64(msg.Data[..8]) >> 8;
                header.Length       = 9;
            }
            else if (isGameMsg) {
                header.MessageId = msg.Data[0];
                header.Length    = 1;
            }
            else {
                header.Length = 0;
            }

            return header;
        }

        public static ulong GetEntityId(Message msg)
        {
            if (msg is SubMessage subMessage)
            {
                return subMessage.EntityId;
            }

            if (msg is GameMessage { Channel: Channel.ReliableGss or Channel.UnreliableGss })
            {
                return BitConverter.ToUInt64(msg.Data[..8]) >> 8;
            }

            return 0;
        }
    }

    public struct MessageHeader
    {
        public int     ControllerId;
        public int     MessageId;
        public ulong   EntityId;
        public bool    IsCommand;
        public Channel Channel;
        public int     Length;

        public bool IsGss => Channel is Channel.ReliableGss or Channel.UnreliableGss;
    }
}