using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Aero.Gen;
using FauCap;
using ImGuiNET;
using ImTool;
using ImTool.SDL;
using Newtonsoft.Json;
using Octokit;
using PacketPeep.Systems;
using Veldrid.Sdl2;

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

        private uint DockSpaceID;

        // Cached data
        private string title;
        private bool   isOpen   = true;
        private ulong  entityId = 0;
        private string entityIdStr;
        private string sizeStr;
        private string idStr;
        private float  dividerHeight = 0;

        private HexView       hexView;
        private AeroInspector Inspector;
        private string        JsonView = null;

        public MessageInspector(string sessionName, int messageIdx, Message msg, IAero msgObj)
        {
            SessionName = sessionName;
            MessageIdx  = messageIdx;
            Msg         = msg;
            MsgObj      = msgObj;

            hexView = new HexView();

            CreateCachedData();

            DockSpaceID = ImGui.GetID($"dockspace_{title}");
        }

        public void CreateCachedData()
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

            Inspector = new(MsgObj, x => PacketPeepTool.Log.AddLogError(LogCategories.PacketParser, x));

            var highlights = new List<HexView.HighlightSection>(Inspector.Entries.Count);

            void AddHighlightEntry(AeroInspectorEntry entry)
            {
                if (entry.Size > 0 && !entry.IsArray) {
                    var highlight = new HexView.HighlightSection
                    {
                        Offset     = entry.Offset,
                        Length     = entry.Size,
                        Color      = Config.Inst.MessageEntryColors[$"Color {entry.ColorIdx}"],
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

            if (JsonView != null) {
                JsonView = JsonConvert.SerializeObject(MsgObj, Formatting.Indented);
            }
        }

        public unsafe bool Draw()
        {
            var popUpSize = new Vector2(580, 500);
            ImGui.SetNextWindowSize(popUpSize, ImGuiCond.Appearing);
            ImGui.SetNextWindowPos((ImGui.GetWindowSize() / 2) - (popUpSize / 2), ImGuiCond.Appearing);
            if (!ImGui.Begin(title, ref isOpen, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.MenuBar)) return isOpen;

            DrawHeader();

            var windowSize                        = ImGui.GetWindowSize() - new Vector2(0, 140);
            if (dividerHeight == 0) dividerHeight = (windowSize.Y / 2);

            ImGui.BeginChild("Hex View", new Vector2(-1, dividerHeight));
            DrawHexView();
            ImGui.EndChild();

            ImGui.InvisibleButton("Divider", new Vector2(-1, 15f));
            if (ImGui.IsItemActive()) {
                dividerHeight += ImGui.GetIO().MouseDelta.Y;
            }

            ImGui.BeginChild("Inspector", new Vector2(-1, windowSize.Y - dividerHeight));
            // SHow the json version instead
            if (JsonView != null) {
                DrawJsonView();
            }
            else {
                DrawInspector();
            }

            ImGui.EndChild();

            ImGui.End();

            return isOpen;
        }

        private void DrawHeader()
        {
            var isGameMsg   = Msg is GameMessage;
            var gameMessage = Msg as GameMessage;
            var isGss       = isGameMsg && gameMessage.Channel is Channel.ReliableGss or Channel.UnreliableGss;

            DrawMenuBar();
            
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

        private void DrawMenuBar()
        {
            if (ImGui.BeginMenuBar()) {
                ImGui.Text(Utils.GetMessageName(Msg));

                ImGui.SameLine(ImGui.GetWindowWidth() - 210);
                if (ImGui.BeginMenu("Copy")) {
                    if (ImGui.MenuItem("Copy to C# hex string")) {
                        var dataCopyStr = string.Join(", ", hexView.Bytes.Select(x => $"0x{x:X}"));
                        var copyStr     = $"new byte[] {{ {dataCopyStr} }}";
                        Sdl2Native.SDL_SetClipboardText(copyStr);
                    }

                    if (ImGui.MenuItem("Copy to js hex string")) {
                        var dataCopyStr = string.Join(", ", hexView.Bytes.Select(x => $"0x{x:X}"));
                        var copyStr     = $"new Uint8Array([{dataCopyStr}]);";
                        Sdl2Native.SDL_SetClipboardText(copyStr);
                    }

                    if (MsgObj != null && ImGui.MenuItem("Copy to json")) {
                        Sdl2Native.SDL_SetClipboardText(JsonConvert.SerializeObject(MsgObj, Formatting.Indented));
                    }

                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Filters")) {
                    if (ImGui.MenuItem("Add filter for this type")) {
                        var headerData = Utils.GetGssMessageHeader(Msg);
                        
                        // todo: fix the filtered indexes, shouldn't be on the db
                        PacketPeepTool.Main.PacketExp.ActiveFilter.AddFilter(headerData.Channel, !headerData.IsCommand, headerData.ControllerId, headerData.MessageId);
                        PacketPeepTool.PcktDb.ApplyFilter(PacketPeepTool.Main.PacketExp.ActiveFilter);
                    }
                    
                    ImGui.EndMenu();
                }

                if (ImGui.Button("Toggle Json", new Vector2(100, 0))) {
                    JsonView = JsonView == null ? JsonConvert.SerializeObject(MsgObj, Formatting.Indented) : null;
                }

                ImGui.EndMenuBar();
            }
        }

        private void DrawHexView()
        {
            hexView.ShowSideParsedValues = Config.Inst.ShowParsedValuesInSide;
            hexView.ShowParsedValuesInTT = Config.Inst.ShowParsedValuesInToolTip;
            hexView.Draw();
        }

        private void DrawInspector()
        {
            if (MsgObj == null) {
                ImGui.Text("No class to use to parse this.");
            }
            else {
                Inspector.Draw();
            }
        }

        private void DrawJsonView()
        {
            ThemeManager.PushFont(Font.FAS);
            if (ImGui.Button("")) {
                Sdl2Native.SDL_SetClipboardText(JsonView);
            }
            ThemeManager.PopFont();
                
            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.Text("Copy the json for this message to the clipboard");
                ImGui.EndTooltip();
            }

            ImGui.TextWrapped(JsonView);
        }
    }
}