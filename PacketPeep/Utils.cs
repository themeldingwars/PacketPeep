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
                var id = gameMsg.Data[0];
                switch (gameMsg.Channel) {
                    case Channel.Control:
                        return PacketPeepTool.PcktDb.GetMessageName(PacketDb.CONTROL_REF_ID, id);

                    case Channel.Matrix:
                        return PacketPeepTool.PcktDb.GetMessageName(PacketDb.MATRIX_REF_ID, id);

                    case Channel.ReliableGss:
                    case Channel.UnreliableGss:
                    {
                        var msgId    = gameMsg.Data[8];
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
    }
}