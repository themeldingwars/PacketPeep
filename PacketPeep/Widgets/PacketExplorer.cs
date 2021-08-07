using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using FauCap;
using ImGuiNET;
using PacketPeep.Systems;
using Vortice.Direct3D11;

namespace PacketPeep.Widgets
{
    public class PacketExplorer
    {
        private PacketFilter                       activeFilter          = new();
        private string                             channelsPreviewStr    = "";
        private string                             fromPreviewStr        = "";
        private Dictionary<string, ControllerData> controllerList        = new();
        private MsgFilterData                      pendingMsgFilterData  = MsgFilterData.Create();
        private string                             pendingControllerName = "";

        public PacketExplorer()
        {
            BuildMessageNameLists();
        }

        public void Draw()
        {
            if (!ImGui.Begin("Packet DB")) return;
            DrawFilters();
            DrawPacketList();
            ImGui.End();
        }

        private void BuildMessageNameLists()
        {
            var matrixMsgs = PacketPeepTool.PcktDb.GetSiftMatrixProtocol;
            var gssMsgs    = PacketPeepTool.PcktDb.GetSiftGssProtocol;

            controllerList.Add("Matrix", new ControllerData
            {
                Id       = -1,
                Messages = matrixMsgs.Messages
            });

            controllerList.Add("Firefall", new ControllerData
            {
                Id       = 0,
                Messages = gssMsgs.Messages
            });

            // Duplication for simplicity
            foreach (var (nsName, ns) in gssMsgs.Children) {
                if (ns.Views != null) {
                    foreach (var (viewName, viewId) in ns.Views) {
                        var name = $"{nsName}::{viewName}";
                        controllerList.Add(name, new ControllerData
                        {
                            Id       = viewId,
                            Messages = ns.Messages,
                            Commands = ns.Commands
                        });
                    }
                }
            }
        }

        private void DrawFilters()
        {
            var hasFiltersChanged = DrawFilterChannels(false);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            hasFiltersChanged = DrawFiltersSource(hasFiltersChanged);

            // Message / Controllers
            ImGui.SetNextItemWidth(-1);
            hasFiltersChanged = DrawFiltersMessages(hasFiltersChanged);

            // If we don't have a session set yet pick the first
            if (activeFilter.SessionName == "" && PacketPeepTool.PcktDb.Sessions.Count > 0) {
                activeFilter.SessionName = PacketPeepTool.PcktDb.Sessions.Keys.First();
                hasFiltersChanged        = true;
            }

            hasFiltersChanged = DrawFiltersSessions(hasFiltersChanged);

            // Apply the filter if it changed
            if (hasFiltersChanged) {
                PacketPeepTool.PcktDb.ApplyFilter(activeFilter);
            }
        }

        private bool DrawFiltersSessions(bool hasFiltersChanged)
        {
            ImGui.Text("Sessions: ");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-27);
            if (ImGui.BeginCombo("###Sessions", activeFilter.SessionName)) {
                foreach (var sessionName in PacketPeepTool.PcktDb.Sessions.Keys) {
                    ImGui.Selectable("###SessionSelected", sessionName == activeFilter.SessionName);
                    ImGui.SameLine();
                    if (ImGui.IsItemClicked()) {
                        activeFilter.SessionName = sessionName;
                        hasFiltersChanged        = true;
                    }

                    ImGui.SameLine();
                    ImGui.Text(sessionName);

                    if (ImGui.IsItemHovered() && PacketPeepTool.PcktDb.Sessions.TryGetValue(sessionName, out PacketDbSession session)) {
                        DrawFiltersSessionTooltip(session);
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();
            const string REMOVE_SESSION_POPUP_NAME = "Remove session?";
            if (ImGui.Button("X")) {
                ImGui.OpenPopup(REMOVE_SESSION_POPUP_NAME);
            }

            // Are you sure popup for removing a session
            if (ImGui.BeginPopupModal(REMOVE_SESSION_POPUP_NAME)) {
                ImGui.Text($"Are you sure you want to remove the session {activeFilter.SessionName}?");

                if (ImGui.Button("Yes") && activeFilter.SessionName != "") {
                    PacketPeepTool.PcktDb.RemoveSession(activeFilter.SessionName);
                    activeFilter.SessionName = PacketPeepTool.PcktDb.Sessions.Keys.FirstOrDefault() ?? "";
                    hasFiltersChanged        = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button("Cancel")) {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
                return hasFiltersChanged;
            }

            return hasFiltersChanged;
        }

        private static void DrawFiltersSessionTooltip(PacketDbSession session)
        {
            static void DrawTableTtRow(string name, string text)
            {
                ImGui.TableNextColumn();
                ImGui.Text(name);
                ImGui.TableNextColumn();
                ImGui.Text(text);
            }

            ImGui.BeginTooltip();
            if (ImGui.BeginTable("SessionInfo", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders)) {
                DrawTableTtRow("Proto", session.Session.ProtocolVersion.ToString());
                DrawTableTtRow("Streaming Proto", session.Session.StreamingProtocol.ToString());
                DrawTableTtRow("Sequence Start", session.Session.SequenceStart.ToString());
                DrawTableTtRow("Socket ID", session.Session.SocketID.ToString());
                DrawTableTtRow("Game Server Port", session.Session.GameServerPort.ToString());
                DrawTableTtRow("Num Packets", session.Session.Packets.Count.ToString("N0"));
                DrawTableTtRow("Num Messages", session.Session.Messages.Count.ToString("N0"));

                ImGui.EndTable();
            }

            ImGui.EndTooltip();
        }

        private bool DrawFiltersMessages(bool hasFiltersChanged)
        {
            if (ImGui.BeginCombo("###Messages", "Messages", ImGuiComboFlags.HeightLarge)) {
                var cidDisplayName = pendingControllerName != "" ? pendingControllerName : "ControllerId";
                ImGui.SetNextItemWidth(300);
                if (ImGui.BeginCombo("###ControllerId", cidDisplayName, ImGuiComboFlags.HeightLarge)) {
                    foreach (var (viewId, viewData) in PacketPeepTool.PcktDb.ControllerList) {
                        SimpleDropDownSelectable(viewData.Name, viewData.Id, () =>
                        {
                            pendingControllerName       = viewData.Name;
                            pendingMsgFilterData.ViewId = viewId;
                        });

                        /*
                        if (ImGui.IsItemHovered() && view.Value.Id > 0) {
                            ImGui.BeginTooltip();
                            
                            ImGui.EndTooltip();
                        }
                        */
                    }

                    ImGui.EndCombo();
                }

                ImGui.SameLine();
                ImGui.SetNextItemWidth(300);
                if (ImGui.BeginCombo("###MessageId", "MessageId", ImGuiComboFlags.HeightLarge)) {
                    if (PacketPeepTool.PcktDb.ControllerList.TryGetValue(pendingMsgFilterData.ViewId, out var cData)) {
                        // Presets
                        SimpleDropDownSelectable("All", -1, () =>
                        {
                            pendingMsgFilterData.MsgId = -1;
                            pendingMsgFilterData.CmdId = -1;
                        });

                        if (pendingMsgFilterData.ViewId >= 0) {
                            SimpleDropDownSelectable("All Messages", -2, () => pendingMsgFilterData.MsgId = -1);
                            SimpleDropDownSelectable("All Commands", -3, () => pendingMsgFilterData.CmdId = -1);
                        }

                        // Messages
                        if (cData.Messages != null && ImGui.CollapsingHeader("Messages", ImGuiTreeNodeFlags.DefaultOpen)) {
                            foreach (var (msgId, msgName) in cData.Messages) {
                                SimpleDropDownSelectable(msgName, msgId, () => pendingMsgFilterData.MsgId = msgId);
                            }
                        }

                        // Commands
                        if (cData.Commands != null && ImGui.CollapsingHeader("Commands", ImGuiTreeNodeFlags.DefaultOpen)) {
                            foreach (var (cmdId, cmdName) in cData.Commands) {
                                SimpleDropDownSelectable(cmdName, cmdId, () => pendingMsgFilterData.CmdId = cmdId);
                            }
                        }
                    }

                    ImGui.EndCombo();
                }

                ImGui.SameLine();
                if (ImGui.Button("Add") && pendingMsgFilterData.ViewId != int.MinValue) {
                    activeFilter.MsgFilters.Add(pendingMsgFilterData);
                    hasFiltersChanged = true;

                    pendingControllerName = "";
                    pendingMsgFilterData  = MsgFilterData.Create();
                }

                // Show the set filters
                // TODO: Maybe optimise how the names are got, i figure doing the more intensive way here is fine enough as this is a drop down thats not always shown
                ImGui.NewLine();
                if (ImGui.BeginTable("Messages", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Borders)) {
                    ImGui.TableSetupScrollFreeze(3, 1);
                    ImGui.TableSetupColumn("Namespace", ImGuiTableColumnFlags.None, 1f);
                    ImGui.TableSetupColumn("Message / Command", ImGuiTableColumnFlags.None, 1f);
                    ImGui.TableSetupColumn("###Actions", ImGuiTableColumnFlags.WidthFixed, 18);
                    ImGui.TableHeadersRow();

                    var msgFiltersToRemove = new List<int>();
                    int idx2               = 0;
                    foreach (var msgf in activeFilter.MsgFilters) {
                        var view = PacketPeepTool.PcktDb.GetControllerData(msgf.ViewId);

                        ImGui.TableNextColumn();
                        ImGui.Text($"{view.Name} ({view.Id})");
                        ImGui.TableNextColumn();
                        if (msgf.MsgId == -1 && msgf.CmdId == -1) ImGui.Text("All");
                        else if (msgf.MsgId == -1) ImGui.Text("MSG: All");
                        else if (msgf.CmdId == -1) ImGui.Text("CMD: All");
                        else if (msgf.MsgId != int.MinValue) {
                            var msgName = view.Messages.First(x => x.Key == msgf.MsgId);
                            ImGui.Text($"MSG: {msgName.Key} ({msgName.Value})");
                        }
                        else if (msgf.CmdId != int.MinValue) {
                            var msgName = view.Commands.First(x => x.Key == msgf.CmdId);
                            ImGui.Text($"CMD: {msgName.Key} ({msgName.Value})");
                        }

                        ImGui.TableNextColumn();
                        if (ImGui.Button($"X###{idx2}")) {
                            msgFiltersToRemove.Add(idx2);
                            hasFiltersChanged = true;
                        }

                        idx2++;
                    }

                    foreach (var index in msgFiltersToRemove) {
                        activeFilter.MsgFilters.RemoveAt(index);
                    }

                    ImGui.EndTable();
                }

                ImGui.EndCombo();
            }

            return hasFiltersChanged;
        }

        private void SimpleDropDownSelectable(string name, int value, Action onClick)
        {
            ImGui.Selectable($"###{name}_{value}");
            ImGui.SameLine();
            if (ImGui.IsItemClicked()) onClick();

            ImGui.Text($"{value}".PadRight(4));
            ImGui.SameLine();
            ImGui.Text(name);
        }

        private bool DrawFiltersSource(bool hasFiltersChanged)
        {
            if (ImGui.BeginCombo("###From", fromPreviewStr)) {
                hasFiltersChanged |= ImGui.Checkbox("Client", ref activeFilter.FromClient);
                hasFiltersChanged |= ImGui.Checkbox("Server", ref activeFilter.FromServer);

                ImGui.EndCombo();
            }

            if (hasFiltersChanged || fromPreviewStr == "") SetFromPreviewStr();
            return hasFiltersChanged;
        }

        private bool DrawFilterChannels(bool hasFiltersChanged)
        {
            ImGui.SetNextItemWidth(300);
            if (ImGui.BeginCombo("###Channels", channelsPreviewStr)) {
                hasFiltersChanged |= ImGui.Checkbox("Jack", ref activeFilter.ChanJack);
                hasFiltersChanged |= ImGui.Checkbox("Control", ref activeFilter.ChanControl);
                hasFiltersChanged |= ImGui.Checkbox("Matrix", ref activeFilter.ChanMatrix);
                hasFiltersChanged |= ImGui.Checkbox("UGSS", ref activeFilter.ChanUgss);
                hasFiltersChanged |= ImGui.Checkbox("RGSS", ref activeFilter.ChanRgss);

                ImGui.EndCombo();
            }

            if (hasFiltersChanged || channelsPreviewStr == "") SetChanPreviewStr();
            return hasFiltersChanged;
        }

        private unsafe void DrawPacketList()
        {
            if (ImGui.BeginChild("###PacketListArea", new Vector2(-1, -40))) {
                var numColumns = Config.Inst.PacketList.GetNumColumsNeeded() + 3; // 3 fixed ones
                if (ImGui.BeginTable("Packet List", numColumns, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY)) {
                    DrawPacketListHeader(numColumns);

                    if (activeFilter.SessionName != "" && PacketPeepTool.PcktDb.Sessions[activeFilter.SessionName].Session.Messages.Count > 0) {
                        ImGuiListClipper    clipperData;
                        ImGuiListClipperPtr clipper = new ImGuiListClipperPtr(&clipperData);
                        clipper.Begin(PacketPeepTool.PcktDb.FilteredIndices.Count);

                        while (clipper.Step()) {
                            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                                DrawPacketListItem(i);
                            }
                        }

                        clipper.End();
                    }

                    ImGui.EndTable();
                }

                ImGui.EndChild();
            }
        }

        private static void DrawPacketListHeader(int numColumns)
        {
            ImGui.TableSetupScrollFreeze(numColumns, 1);
            if (Config.Inst.PacketList.ShowPacketIdx) ImGui.TableSetupColumn("Pkt Idx", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Src", ImGuiTableColumnFlags.WidthFixed, 20);
            ImGui.TableSetupColumn("Ch", ImGuiTableColumnFlags.WidthFixed, 20);
            if (Config.Inst.PacketList.ShowPacketSeqNum) ImGui.TableSetupColumn("Seq", ImGuiTableColumnFlags.WidthFixed, 45);
            if (Config.Inst.PacketList.ShowPacketIds) ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Name");
            ImGui.TableHeadersRow();
        }

        private void DrawPacketListItem(int i)
        {
            var idx       = PacketPeepTool.PcktDb.FilteredIndices[i];
            var msg       = PacketPeepTool.PcktDb.Sessions[activeFilter.SessionName].Session.Messages[idx];
            var gameMsg   = msg as GameMessage;
            var isGameMsg = gameMsg != null;
            var isGss     = gameMsg is {Channel: Channel.ReliableGss or Channel.UnreliableGss};

            // Packet idx
            if (Config.Inst.PacketList.ShowPacketIdx) {
                ImGui.TableNextColumn();
                ImGui.Text($"{idx:N0}");
            }

            // Source
            ImGui.TableNextColumn();
            ImGui.Text(msg.FromServer ? " S" : " C");

            // Channel
            ImGui.TableNextColumn();
            ImGui.Text(msg.Server == Server.Game ? GetChannelName(gameMsg.Channel) : " J ");

            if (Config.Inst.PacketList.ShowPacketSeqNum) {
                ImGui.TableNextColumn();
                if (isGameMsg && gameMsg.IsSequenced && gameMsg.Raw.Length >= 4) {
                    var seqNum = BitConverter.ToUInt16(new[] {gameMsg.Raw[3], gameMsg.Raw[2]});
                    ImGui.Text($"{seqNum:N0}");
                }
                else ImGui.Text($"....");
            }


            if (Config.Inst.PacketList.ShowPacketIds) {
                ImGui.TableNextColumn();
                if (msg.Server == Server.Game)
                    ImGui.Text(isGss ? $"{gameMsg.Data[0]}::{gameMsg.Data[8]}" : $"{gameMsg.Data[0]}");
            }

            // Name
            ImGui.TableNextColumn();
            DrawPacketListName(msg, gameMsg);
        }

        private static void DrawPacketListName(Message msg, GameMessage gameMsg)
        {
            try {
                if (msg.Server == Server.Game) {
                    var id = gameMsg.Data[0];
                    switch (gameMsg.Channel) {
                        case Channel.Control:
                            ImGui.Text(PacketPeepTool.PcktDb.GetMessageName(PacketDb.CONTROL_REF_ID, id));
                            break;

                        case Channel.Matrix:
                            ImGui.Text(PacketPeepTool.PcktDb.GetMessageName(PacketDb.MATRIX_REF_ID, id));
                            break;

                        case Channel.ReliableGss:
                        case Channel.UnreliableGss:
                        {
                            var msgId    = gameMsg.Data[8];
                            var viewName = PacketPeepTool.PcktDb.GetViewName(id);
                            var msgName  = gameMsg.FromServer || id is 0 or 251 ? PacketPeepTool.PcktDb.GetMessageName(id, msgId) : PacketPeepTool.PcktDb.GetCommandName(id, msgId);
                            ImGui.Text($"{viewName}::{msgName}");
                            break;
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

                    ImGui.Text(name);
                }
            }
            catch (Exception e) {
                Console.WriteLine(e);
                if (gameMsg.Channel == Channel.Matrix) {
                    Console.WriteLine($"{gameMsg.Data[0]}");
                }
                else {
                    Console.WriteLine($"{gameMsg.Data[0]}::{gameMsg.Data[8]}");
                }

                throw;
            }
        }

        private static string GetChannelName(Channel chan)
        {
            var name = chan switch
            {
                Channel.Control       => " C ",
                Channel.Matrix        => " M ",
                Channel.ReliableGss   => " R ",
                Channel.UnreliableGss => " U ",
                _                     => "Wat?"
            };

            return name;
        }

        private void SetChanPreviewStr()
        {
            channelsPreviewStr = "Chans: ";
            if (!activeFilter.ChanJack && !activeFilter.ChanMatrix && !activeFilter.ChanUgss && !activeFilter.ChanRgss) {
                channelsPreviewStr += "none";
            }
            else {
                if (activeFilter.ChanJack) channelsPreviewStr   += "Jack, ";
                if (activeFilter.ChanJack) channelsPreviewStr   += "Ctrl, ";
                if (activeFilter.ChanMatrix) channelsPreviewStr += "Matrix, ";
                if (activeFilter.ChanUgss) channelsPreviewStr   += "UGSS, ";
                if (activeFilter.ChanRgss) channelsPreviewStr   += "RGSS, ";

                channelsPreviewStr = channelsPreviewStr.TrimEnd(',', ' ');
            }
        }

        private void SetFromPreviewStr()
        {
            fromPreviewStr = "From: ";
            if (activeFilter.FromClient && activeFilter.FromServer) {
                fromPreviewStr += "Both";
            }
            else if (activeFilter.FromClient) {
                fromPreviewStr += "Client";
            }
            else if (activeFilter.FromServer) {
                fromPreviewStr += "Server";
            }
            else {
                fromPreviewStr += "none";
            }
        }

        private struct ControllerData
        {
            public int                      Id;
            public Dictionary<string, byte> Messages;
            public Dictionary<string, byte> Commands;
        }
    }
}