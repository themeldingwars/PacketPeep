using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Schema;
using FauCap;
using ImGuiNET;
using ImTool;
using PacketPeep.Systems;
using Vortice.Direct3D11;

namespace PacketPeep.Widgets
{
    public class PacketExplorer
    {
        public  PacketFilter                       ActiveFilter          = new();
        private string                             channelsPreviewStr    = "";
        private string                             fromPreviewStr        = "";
        private Dictionary<string, ControllerData> controllerList        = new();
        private MsgFilterData                      pendingMsgFilterData  = MsgFilterData.Create();
        private string                             pendingControllerName = "";
        private string                             pendingMessageName    = "";
        private PacketDb                           pcktDb                = null;

        public PacketDbSession GetActiveSession() => pcktDb.Sessions[ActiveFilter.SessionName];
        public Message         GetMsg(int idx)    => GetActiveSession().Session.Messages[idx];

        private int                           plCtxSelectedItem   = -1;
        private bool                          shouldApplyFilter   = false;
        private int                           hoveredIdx          = -1;
        public  Dictionary<string, List<int>> SelectedIdxs        = new();
        private List<int>                     selectedIdsFiltered = new(); // A cached version of the sesion name selectedIds
        private AckMarkerData                 ackMarker;
        private float                         scrollTo = -1;

        // Events
        public Action<int, bool> OnMessageSelected; // When a message is selected / deselects, msg id and if selected or not
        public Action<int>       OnMessageRightClick;
        public Action<int>       OnMessageHovered;
        public Action<int>       OnMessageContextMenuDraw;

        // Static Ids
        private const string PL_ITEM_CTX_POPUP_ID = "###PacketListItemContextPupop";

        public PacketExplorer(PacketDb pcktDb)
        {
            this.pcktDb = pcktDb;
            BuildMessageNameLists();
        }

        public void Draw()
        {
            //ImGui.SetNextWindowDockID(MainTab.DockId, ImGuiCond.FirstUseEver);
            if (!ImGui.Begin("Packet DB")) return;

            if (!SelectedIdxs.TryGetValue(ActiveFilter.SessionName, out selectedIdsFiltered)) selectedIdsFiltered = new List<int>();

            DrawFilters();

            DrawPacketList();

            ImGui.End();

            // Apply filters from context menus etc
            if (shouldApplyFilter) {
                pcktDb.ApplyFilter(ActiveFilter);
                shouldApplyFilter = false;
            }
        }

        private void BuildMessageNameLists()
        {
            var matrixMsgs = pcktDb.GetSiftMatrixProtocol;
            var gssMsgs    = pcktDb.GetSiftGssProtocol;

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
            ImGui.SetNextItemWidth(-32);
            hasFiltersChanged = DrawFiltersMessages(hasFiltersChanged);

            ImGui.SameLine();
            DrawMessageFinder();

            // If we don't have a session set yet pick the first
            if (ActiveFilter.SessionName == "" && pcktDb.Sessions.Count > 0) {
                ActiveFilter.SessionName = pcktDb.Sessions.Keys.First();
                hasFiltersChanged        = true;
            }

            hasFiltersChanged = DrawFiltersSessions(hasFiltersChanged);

            // Apply the filter if it changed
            if (hasFiltersChanged) {
                pcktDb.ApplyFilter(ActiveFilter);
            }
        }

        private int messageFinderMsgIdx = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawMessageFinder()
        {
            const string FIND_POPUP_NAME = "Find Message";

            FontManager.PushFont("FAS");
            if (ImGui.Button("")) {
                ImGui.OpenPopup(FIND_POPUP_NAME);
                messageFinderMsgIdx = 0;
            }

            FontManager.PopFont();

            if (ImGui.BeginPopup(FIND_POPUP_NAME)) {
                if (!ImGui.IsAnyItemFocused() && !ImGui.IsAnyItemActive() && !ImGui.IsMouseClicked(0)) ImGui.SetKeyboardFocusHere();
                ImGui.InputInt("###Message Idx", ref messageFinderMsgIdx, 0, 0);

                ImGui.SameLine();
                if (ImGui.Button("Go") || ImGui.IsKeyPressedMap(ImGuiKey.Tab)) {
                    ImGui.CloseCurrentPopup();

                    // Find the offset for the message
                    var offsetIdx = 0;
                    if (pcktDb.FilteredIndices.Contains(messageFinderMsgIdx)) {
                        foreach (var idx in pcktDb.FilteredIndices) {
                            var message = pcktDb.Sessions[ActiveFilter.SessionName].Session.Messages[idx];
                            if (message.Id == messageFinderMsgIdx) {
                                break;
                            }

                            offsetIdx++;
                        }

                        scrollTo = offsetIdx * 17;
                    }
                }

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Scroll to the message in the list. [Tab]");

                ImGui.SameLine();
                if (ImGui.Button("Open") || ImGui.IsKeyPressedMap(ImGuiKey.Enter)) {
                    ImGui.CloseCurrentPopup();
                    ToggleMessageSelect(messageFinderMsgIdx);
                    OnMessageSelected?.Invoke(messageFinderMsgIdx, false);
                }

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open the message in an inspector. [Enter]");

                ImGui.EndPopup();
            }
        }

        private bool DrawFiltersSessions(bool hasFiltersChanged)
        {
            ImGui.Text("Sessions: ");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-32);
            if (ImGui.BeginCombo("###Sessions", ActiveFilter.SessionName)) {
                foreach (var sessionName in pcktDb.Sessions.Keys) {
                    ImGui.Selectable("###SessionSelected", sessionName == ActiveFilter.SessionName);
                    ImGui.SameLine();
                    if (ImGui.IsItemClicked()) {
                        ActiveFilter.SessionName = sessionName;
                        hasFiltersChanged        = true;
                    }

                    ImGui.SameLine();
                    ImGui.Text(sessionName);

                    if (ImGui.IsItemHovered() && pcktDb.Sessions.TryGetValue(sessionName, out PacketDbSession session)) {
                        DrawFiltersSessionTooltip(session);
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();
            const string REMOVE_SESSION_POPUP_NAME = "Remove session?";
            DrawRemoveSessionButton(REMOVE_SESSION_POPUP_NAME);

            // Are you sure popup for removing a session
            if (ImGui.BeginPopupModal(REMOVE_SESSION_POPUP_NAME)) {
                ImGui.Text($"Are you sure you want to remove the session {ActiveFilter.SessionName}?");

                if (ImGui.Button("Yes") && ActiveFilter.SessionName != "") {
                    pcktDb.RemoveSession(ActiveFilter.SessionName);
                    ActiveFilter.SessionName = pcktDb.Sessions.Keys.FirstOrDefault() ?? "";
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DrawRemoveSessionButton(string removeSessionPopupName)
        {
            FontManager.PushFont("FAS");
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.ColorConvertU32ToFloat4(0xFF5057FF));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.ColorConvertU32ToFloat4(0xFFa5a1FF));
            if (ImGui.Button("")) {
                ImGui.OpenPopup(removeSessionPopupName);
            }

            ImGui.PopStyleColor(2);
            FontManager.PopFont();
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
                    foreach (var (viewId, viewData) in pcktDb.ControllerList) {
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

                var midDisplayName = pendingMessageName != "" ? pendingMessageName : "MessageId";
                ImGui.SameLine();
                ImGui.SetNextItemWidth(300);
                if (ImGui.BeginCombo("###MessageId", midDisplayName, ImGuiComboFlags.HeightLarge)) {
                    if (pcktDb.ControllerList.TryGetValue(pendingMsgFilterData.ViewId, out var cData)) {
                        // Presets
                        SimpleDropDownSelectable("All", -1, () =>
                        {
                            pendingMsgFilterData.MsgId = -1;
                            pendingMsgFilterData.CmdId = -1;
                            pendingMessageName         = "All";
                        });

                        if (pendingMsgFilterData.ViewId >= 0) {
                            SimpleDropDownSelectable("All Messages", -2, () =>
                            {
                                pendingMsgFilterData.MsgId = -1;
                                pendingMessageName         = "All Messages";
                            });
                            
                            SimpleDropDownSelectable("All Commands", -3, () =>
                            {
                                pendingMsgFilterData.CmdId = -1;
                                pendingMessageName         = "All Commands";
                            });
                        }

                        // Messages
                        if (cData.Messages != null && ImGui.CollapsingHeader("Messages", ImGuiTreeNodeFlags.DefaultOpen)) {
                            foreach (var (msgId, msgName) in cData.Messages) {
                                SimpleDropDownSelectable(msgName, msgId, () =>
                                {
                                    pendingMsgFilterData.MsgId = msgId;
                                    pendingMessageName         = $"MSG: {msgName}";
                                });
                            }
                        }

                        // Commands
                        if (cData.Commands != null && ImGui.CollapsingHeader("Commands", ImGuiTreeNodeFlags.DefaultOpen)) {
                            foreach (var (cmdId, cmdName) in cData.Commands) {
                                SimpleDropDownSelectable(cmdName, cmdId, () =>
                                {
                                    pendingMsgFilterData.CmdId = cmdId;
                                    pendingMessageName         = $"CMD: {cmdName}";
                                });
                            }
                        }
                    }

                    ImGui.EndCombo();
                }

                ImGui.SameLine();
                if (ImGui.Button("Add") && pendingMsgFilterData.ViewId != int.MinValue) {
                    ActiveFilter.MsgFilters.Add(pendingMsgFilterData);
                    hasFiltersChanged = true;

                    pendingControllerName = "";
                    pendingMessageName    = "";
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
                    foreach (var msgf in ActiveFilter.MsgFilters) {
                        var view = pcktDb.GetControllerData(msgf.ViewId);

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
                        ActiveFilter.MsgFilters.RemoveAt(index);
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
                hasFiltersChanged |= ImGui.Checkbox("Client", ref ActiveFilter.FromClient);
                hasFiltersChanged |= ImGui.Checkbox("Server", ref ActiveFilter.FromServer);

                ImGui.EndCombo();
            }

            if (hasFiltersChanged || fromPreviewStr == "") SetFromPreviewStr();
            return hasFiltersChanged;
        }

        private bool DrawFilterChannels(bool hasFiltersChanged)
        {
            ImGui.SetNextItemWidth(300);
            if (ImGui.BeginCombo("###Channels", channelsPreviewStr)) {
                hasFiltersChanged |= ImGui.Checkbox("Jack", ref ActiveFilter.ChanJack);
                hasFiltersChanged |= ImGui.Checkbox("Control", ref ActiveFilter.ChanControl);
                hasFiltersChanged |= ImGui.Checkbox("Matrix", ref ActiveFilter.ChanMatrix);
                hasFiltersChanged |= ImGui.Checkbox("UGSS", ref ActiveFilter.ChanUgss);
                hasFiltersChanged |= ImGui.Checkbox("RGSS", ref ActiveFilter.ChanRgss);

                ImGui.EndCombo();
            }

            if (hasFiltersChanged || channelsPreviewStr == "") SetChanPreviewStr();
            return hasFiltersChanged;
        }

        private unsafe void DrawPacketList()
        {
            if (ImGui.BeginChild("###PacketListArea", new Vector2(-1, -40))) {
                var numColumns = Config.Inst.PacketList.GetNumColumsNeeded() + 3; // 3 fixed ones
                if (ImGui.BeginTable("Packet List", numColumns, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.ContextMenuInBody)) {
                    DrawPacketListHeader(numColumns);

                    if (ActiveFilter.SessionName != "" && pcktDb.Sessions[ActiveFilter.SessionName].Session.Messages.Count > 0) {
                        ImGuiListClipper    clipperData;
                        ImGuiListClipperPtr clipper = new ImGuiListClipperPtr(&clipperData);
                        clipper.Begin(pcktDb.FilteredIndices.Count);

                        while (clipper.Step()) {
                            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                                if (i < pcktDb.Sessions[ActiveFilter.SessionName].Session.Messages.Count) {
                                    DrawPacketListItem(i);
                                }
                            }
                        }

                        clipper.End();
                    }

                    if (scrollTo != -1f) {
                        ImGui.SetScrollY(scrollTo);
                        scrollTo = -1f;
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
            var idx        = pcktDb.FilteredIndices[i];
            var msg        = pcktDb.Sessions[ActiveFilter.SessionName].Session.Messages[idx];
            var gameMsg    = msg as GameMessage;
            var isGameMsg  = gameMsg != null;
            var isGss      = gameMsg is {Channel: Channel.ReliableGss or Channel.UnreliableGss};
            var isSelected = selectedIdsFiltered.Contains(idx);
            var isAnAck    = gameMsg is {Channel: Channel.Control} && (gameMsg.Data[0] == 0x2 || gameMsg.Data[0] == 0x3);

            // Packet idx
            if (Config.Inst.PacketList.ShowPacketIdx) {
                ImGui.TableNextColumn();
                ImGui.Text($"{idx:N0}");
            }

            // Source
            ImGui.TableNextColumn();
            if (msg.FromServer) ImGui.TextColored(Config.Inst.PColors[Config.Colors.Server], " S ");
            else ImGui.TextColored(Config.Inst.PColors[Config.Colors.Client], " C ");

            // Channel
            ImGui.TableNextColumn();
            if (msg.Server == Server.Game) {
                switch (gameMsg.Channel) {
                    case Channel.Control:
                        ImGui.TextColored(Config.Inst.PColors[Config.Colors.Control], " C ");
                        break;
                    case Channel.Matrix:
                        ImGui.TextColored(Config.Inst.PColors[Config.Colors.Matrix], " M ");
                        break;
                    case Channel.ReliableGss:
                        ImGui.TextColored(Config.Inst.PColors[Config.Colors.RGSS], " R ");
                        break;
                    case Channel.UnreliableGss:
                        ImGui.TextColored(Config.Inst.PColors[Config.Colors.UGSS], " U ");
                        break;
                }
            }
            else
                ImGui.TextColored(Config.Inst.PColors[Config.Colors.Jack], " J ");

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
                    if (gameMsg is SubMessage subMsg) {
                        ImGui.Text($"{(byte) (subMsg.EntityId & 0x00000000000000FF)}::{gameMsg.Data[0]}");
                    }
                    else {
                        ImGui.Text(isGss ? $"{gameMsg.Data[0]}::{gameMsg.Data[8]}" : $"{gameMsg.Data[0]}");
                    }
            }

            // Name
            ImGui.TableNextColumn();
            DrawPacketListName(msg, gameMsg);

            // Ack marker
            if (gameMsg is {IsSequenced: true} and not SubMessage && !ackMarker.DontShow && BitConverter.ToUInt16(new[] {gameMsg.Raw[3], gameMsg.Raw[2]}) == ackMarker.SeqNum && ackMarker.AckPacketIdx >= idx) {
                if ((ackMarker.IsGss && gameMsg.Channel == Channel.ReliableGss || !ackMarker.IsGss && gameMsg.Channel == Channel.Matrix) && ackMarker.FromServer != msg.FromServer) {
                    ImGui.SameLine();
                    FontManager.PushFont("FAS");
                    ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(0xff98ff98), "");
                    FontManager.PopFont();
                }
            }

            // Events and Actions
            ImGui.SameLine();

            // Select
            if (ImGui.Selectable("###", isSelected, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.DontClosePopups)) {
                ToggleMessageSelect(idx);
                OnMessageSelected?.Invoke(idx, isSelected);
            }

            // Right / middle click context menu
            if (ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Right)) {
                OnMessageRightClick?.Invoke(idx);
                ImGui.OpenPopup(PL_ITEM_CTX_POPUP_ID);
                plCtxSelectedItem = idx;
            }

            // Hovered
            if (ImGui.IsItemHovered()) {
                hoveredIdx = idx;
                OnMessageHovered?.Invoke(idx);
                if (isAnAck) {
                    ackMarker.IsGss        = gameMsg.Data[0] == 0x3;
                    ackMarker.FromServer   = msg.FromServer;
                    ackMarker.DontShow     = false;
                    ackMarker.SeqNum       = BitConverter.ToUInt16(new[] {gameMsg.Data[4], gameMsg.Data[3]});
                    ackMarker.AckPacketIdx = idx;
                }
                else {
                    ackMarker.DontShow = true;
                }
            }

            // Context Menu
            if (plCtxSelectedItem == idx && ImGui.BeginPopupContextItem(PL_ITEM_CTX_POPUP_ID)) {
                DrawPacketListItemContextMenu(i);
                ImGui.EndPopup();
            }
        }

        private void UpdateHoveredForAcks(int idx)
        {
        }

        private void DrawPacketListItemContextMenu(int i)
        {
            var idx     = pcktDb.FilteredIndices[i];
            var msg     = pcktDb.Sessions[ActiveFilter.SessionName].Session.Messages[idx];
            var gameMsg = msg as GameMessage;
            var isGss   = gameMsg is {Channel: Channel.ReliableGss or Channel.UnreliableGss};

            ImGui.Text($"Idx: {i}");
            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();
            ImGui.TextColored(Config.Inst.PColors[gameMsg.FromServer ? Config.Colors.Server : Config.Colors.Client], gameMsg.FromServer ? "Server" : "Client");
            ImGui.SameLine();
            ImGui.Spacing();
            ImGui.SameLine();

            var chanColor = gameMsg.Channel switch
            {
                Channel.Control       => Config.Inst.PColors[Config.Colors.Control],
                Channel.Matrix        => Config.Inst.PColors[Config.Colors.Matrix],
                Channel.UnreliableGss => Config.Inst.PColors[Config.Colors.UGSS],
                Channel.ReliableGss   => Config.Inst.PColors[Config.Colors.RGSS],
            };
            ImGui.TextColored(chanColor, gameMsg.Channel.ToString());

            ImGui.Text($"Size: {gameMsg.Data.Length:N0}");
            ImGui.NewLine();

            if (ImGui.MenuItem("Add filter for this type")) {
                ActiveFilter.AddFilter(gameMsg.Channel, gameMsg.FromServer, gameMsg.Data[0], isGss ? gameMsg.Data[8] : gameMsg.Data[0]);
                shouldApplyFilter = true;
            }

            OnMessageContextMenuDraw?.Invoke(i);
        }

        private static void DrawPacketListName(Message msg, GameMessage gameMsg)
        {
            try {
                if (msg.Server == Server.Game) {
                    var id = msg is SubMessage subMsg ? (byte) (subMsg.EntityId & 0x00000000000000FF) : gameMsg.Data[0];
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
                            var msgId    = msg is SubMessage subMsg2 ? subMsg2.Data[0] : gameMsg.Data[8];
                            var viewName = PacketPeepTool.PcktDb.GetViewName(id);
                            var msgName  = gameMsg.FromServer || id is 0 or 251 ? PacketPeepTool.PcktDb.GetMessageName(id, msgId) : PacketPeepTool.PcktDb.GetCommandName(id, msgId);

                            var isSubMessage = msg is SubMessage;
                            ImGui.Text($"{(isSubMessage ? "\t|-- " : "")}{viewName}::{msgName}");
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
            if (!ActiveFilter.ChanJack && !ActiveFilter.ChanMatrix && !ActiveFilter.ChanUgss && !ActiveFilter.ChanRgss) {
                channelsPreviewStr += "none";
            }
            else {
                if (ActiveFilter.ChanJack) channelsPreviewStr   += "Jack, ";
                if (ActiveFilter.ChanJack) channelsPreviewStr   += "Ctrl, ";
                if (ActiveFilter.ChanMatrix) channelsPreviewStr += "Matrix, ";
                if (ActiveFilter.ChanUgss) channelsPreviewStr   += "UGSS, ";
                if (ActiveFilter.ChanRgss) channelsPreviewStr   += "RGSS, ";

                channelsPreviewStr = channelsPreviewStr.TrimEnd(',', ' ');
            }
        }

        private void SetFromPreviewStr()
        {
            fromPreviewStr = "From: ";
            if (ActiveFilter.FromClient && ActiveFilter.FromServer) {
                fromPreviewStr += "Both";
            }
            else if (ActiveFilter.FromClient) {
                fromPreviewStr += "Client";
            }
            else if (ActiveFilter.FromServer) {
                fromPreviewStr += "Server";
            }
            else {
                fromPreviewStr += "none";
            }
        }

        private void ToggleMessageSelect(int idx)
        {
            if (!SelectedIdxs.ContainsKey(ActiveFilter.SessionName)) SelectedIdxs.Add(ActiveFilter.SessionName, new List<int>(20));

            if (SelectedIdxs.TryGetValue(ActiveFilter.SessionName, out var selectedIdxs)) {
                if (selectedIdxs.Contains(idx)) {
                    selectedIdxs.Remove(idx);
                }
                else {
                    selectedIdxs.Add(idx);
                }
            }
        }

        private struct ControllerData
        {
            public int                      Id;
            public Dictionary<string, byte> Messages;
            public Dictionary<string, byte> Commands;
        }

        private struct AckMarkerData
        {
            public ushort SeqNum;
            public bool   IsGss;
            public bool   FromServer;
            public bool   DontShow;
            public int    AckPacketIdx;
        }
    }
}