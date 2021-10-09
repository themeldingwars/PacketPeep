using System;
using System.Collections.Generic;
using System.Numerics;
using Aero.Gen;
using FauCap;
using ImGuiNET;
using ImTool;
using Octokit;

namespace PacketPeep.Widgets
{
    // An inspector for network message, some basic data, a hex view and a parsed values display form the Aero struct
    public class MessageInspector
    {
        public string      SessionName;
        public int         MessageIdx;
        public Message     Msg;
        public IAero       MsgObj;
        public Action<int> OnClose;

        // Cached data
        private string title;
        private bool   isOpen   = true;
        private ulong  entityId = 0;
        private string entityIdStr;
        private string sizeStr;
        private string idStr;

        private HexView       hexView;
        private AeroInspector Inspector;

        public MessageInspector(string sessionName, int messageIdx, Message msg, IAero msgObj)
        {
            SessionName = sessionName;
            MessageIdx  = messageIdx;
            Msg         = msg;
            MsgObj      = msgObj;

            CreateCachedData();
        }

        private void CreateCachedData()
        {
            var headerData = Utils.GetGssMessageHeader(Msg);

            var isGameMsg   = Msg is GameMessage;
            var gameMessage = Msg as GameMessage;
            var isGss       = isGameMsg && gameMessage.Channel is Channel.ReliableGss or Channel.UnreliableGss;

            title   = $"{Utils.GetMessageName(Msg)} ({MessageIdx})";
            sizeStr = $@"{Msg.Data.Length - headerData.Length:N0}"; // minus the header data
            idStr   = isGss ? $"{headerData.ControllerId}::{headerData.MessageId}" : $"{headerData.MessageId}";

            if (isGss) {
                entityId    = headerData.EntityId;
                entityIdStr = $"{entityId}";
            }

            Inspector = new(MsgObj);
            hexView   = new HexView();

            var highlights = new List<HexView.HighlightSection>(Inspector.Entries.Count);
            void AddHighlightEntry(AeroInspectorEntry entry)
            {
                if (entry.Size > 0 && !entry.IsArray) {
                    var highlight = new HexView.HighlightSection
                    {
                        Offset     = entry.Offset,
                        Length     = entry.Size,
                        Color      = Config.Inst.MessageEntryColors[entry.ColorIdx],
                        HoverName  = entry.Name,
                        IsSelected = false
                    };

                    highlights.Add(highlight);
                }

                if (entry.SubEntrys.Count > 0) {
                    foreach (var subEntry in entry.SubEntrys) {
                        AddHighlightEntry(subEntry);
                    }
                }
            }
            foreach (var entry in Inspector.Entries) {
                AddHighlightEntry(entry);
            }

            hexView.SetData(Msg.Data[headerData.Length..].ToArray(), highlights.ToArray());
        }

        public bool Draw()
        {
            var popUpSize = new Vector2(520, 500);
            ImGui.SetNextWindowSize(popUpSize, ImGuiCond.Appearing);
            ImGui.SetNextWindowPos((ImGui.GetWindowSize() / 2) - (popUpSize / 2), ImGuiCond.Appearing);
            if (!ImGui.Begin(title, ref isOpen, ImGuiWindowFlags.NoSavedSettings)) return isOpen;

            DrawHeader();
            DrawHexView();
            DrawInspector();

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

        private void DrawInspector()
        {
            if (ImGui.CollapsingHeader("Inspector")) {
                if (MsgObj == null) {
                    ImGui.Text("No class to use to parse this.");
                }
                else {
                    Inspector.Draw();
                }
            }
        }
    }
}