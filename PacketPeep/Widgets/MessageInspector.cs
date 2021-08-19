using System;
using System.Numerics;
using FauCap;
using ImGuiNET;
using ImTool;

namespace PacketPeep.Widgets
{
    // An inspector for network message, some basic data, a hex view and a parsed values display form the Aero struct
    public class MessageInspector
    {
        public string      SessionName;
        public int         MessageIdx;
        public Message     Msg;
        public Action<int> OnClose;

        // Cached data
        private string title;
        private bool   isOpen   = true;
        private ulong  entityId = 0;
        private string entityIdStr;
        private string sizeStr;
        private string idStr;

        private HexView hexView;

        public MessageInspector(string sessionName, int messageIdx, Message msg)
        {
            SessionName = sessionName;
            MessageIdx  = messageIdx;
            Msg         = msg;

            CreateCachedData();
        }

        private void CreateCachedData()
        {
            var isGameMsg   = Msg is GameMessage;
            var gameMessage = Msg as GameMessage;
            var isGss       = isGameMsg && gameMessage.Channel is Channel.ReliableGss or Channel.UnreliableGss;

            title   = $"{Utils.GetMessageName(Msg)} ({MessageIdx})";
            sizeStr = $@"{Msg.Data.Length:N0}";
            idStr   = isGss ? $"{gameMessage.Data[0]}::{gameMessage.Data[8]}" : $"{gameMessage.Data[0]}";

            if (isGss) {
                entityId    = BitConverter.ToUInt64(Msg.Data[..8]) >> 8;
                entityIdStr = $"{entityId}";
            }

            hexView = new HexView();
            hexView.SetData(Msg.Data.ToArray(), new HexView.HighlightSection[] { });
        }

        public bool Draw()
        {
            var popUpSize = new Vector2(520, 500);
            ImGui.SetNextWindowSize(popUpSize, ImGuiCond.Appearing);
            ImGui.SetNextWindowPos((ImGui.GetWindowSize() / 2) - (popUpSize / 2), ImGuiCond.Appearing);
            if (!ImGui.Begin(title, ref isOpen, ImGuiWindowFlags.NoSavedSettings)) return isOpen;

            DrawHeader();
            DrawHexView();

            ImGui.End();

            return isOpen;
        }

        private void DrawHeader()
        {
            var isGameMsg   = Msg is GameMessage;
            var gameMessage = Msg as GameMessage;
            var isGss       = isGameMsg && gameMessage.Channel is Channel.ReliableGss or Channel.UnreliableGss;

            ImGui.Text(Utils.GetMessageName(Msg));

            if (ImGui.BeginTable("Message Inspector Header", 5, ImGuiTableFlags.Borders)) {
                ImGui.TableSetupScrollFreeze(5, 1);
                ImGui.TableSetupColumn("From", ImGuiTableColumnFlags.WidthFixed, 85f);
                ImGui.TableSetupColumn("Chan", ImGuiTableColumnFlags.WidthFixed, 100f);
                ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 50f);
                ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed, 100f);
                ImGui.TableSetupColumn("EntityId", ImGuiTableColumnFlags.WidthFixed, 200f);
                ImGui.TableHeadersRow();

                ImGui.TableNextColumn();
                if (Msg.FromServer) ImGui.TextColored(Config.Inst.PColors[Config.Colors.Server], "Server");
                else ImGui.TextColored(Config.Inst.PColors[Config.Colors.Client], "Client");

                ImGui.TableNextColumn();
                if (isGameMsg) ImGui.TextColored(Utils.GetMessageChannelColor(Msg), gameMessage.Channel.ToString());

                ImGui.TableNextColumn();
                if (isGameMsg) ImGui.Text(idStr);

                ImGui.TableNextColumn();
                ImGui.Text(sizeStr);
                
                ImGui.TableNextColumn();
                if (isGss) ImGui.Text(entityIdStr);

                ImGui.EndTable();
            }
            
            ImGui.NewLine();
        }

        private void DrawHexView()
        {
            hexView.ShowSideParsedValues = Config.Inst.ShowParsedValuesInSide;
            hexView.ShowParsedValuesInTT = Config.Inst.ShowParsedValuesInToolTip;
            hexView.Draw();
        }
    }
}