using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using ImTool;

namespace PacketPeep.Widgets
{
    public class MainTab : Tab
    {
        public override string         Name { get; } = "Packet Peep";
        public          bool           ShowOpenCaptureDialog = false;
        public          PacketPeepTool Tool;
        public static   uint           DockId = 1;

        public PacketExplorer PacketExp = new PacketExplorer(PacketPeepTool.PcktDb);

        public List<MessageInspector> MsgInspectors = new();

        public MainTab()
        {
            // Hook up selecting messages to open an inspector
            PacketExp.OnMessageSelected += (idx, selected) =>
            {
                if (selected)
                    CloseMessageInspector(idx);
                else
                    OpenMessageInspector(idx);
            };
        }

        public override void SubmitContent()
        {
            //ImGui.ShowDemoWindow();

            if (ShowOpenCaptureDialog) DrawCaptureDialog();
            FileBrowser.Draw();

            var size = ImGui.GetWindowSize();
            size.Y -= 5;
            ImGui.DockSpace(DockId, size, ImGuiDockNodeFlags.PassthruCentralNode);

            PacketExp.Draw();
            PacketPeepTool.Log.DrawWindow();

            // Prob need some way to keep drawing these if this tab is changed incase these are moved out of the main window
            DrawMessageInspectors();
        }

        public void OpenMessageInspector(int idx)
        {
            if (MsgInspectors.Any(x => x.SessionName == PacketExp.ActiveFilter.SessionName && x.MessageIdx == idx)) return; // Already open

            var msg          = PacketExp.GetMsg(idx);
            var msgInspector = new MessageInspector(PacketExp.ActiveFilter.SessionName, idx, msg);
            msgInspector.OnClose = CloseMessageInspector;
            MsgInspectors.Add(msgInspector);
        }

        public void CloseMessageInspector(int idx) => MsgInspectors.RemoveAll(x => x.SessionName == PacketExp.ActiveFilter.SessionName && x.MessageIdx == idx);

        private void DrawMessageInspectors()
        {
            var toCloseList = new List<MessageInspector>();

            foreach (var msgInspector in MsgInspectors) {
                if (!msgInspector.Draw()) {
                    toCloseList.Add(msgInspector);
                }
            }

            foreach (var msgInspector in toCloseList) {
                MsgInspectors.RemoveAll(x => x.SessionName == msgInspector.SessionName && x.MessageIdx == msgInspector.MessageIdx);
                if (PacketExp.SelectedIdxs.TryGetValue(msgInspector.SessionName, out var selectedIdxs)) {
                    selectedIdxs.Remove(msgInspector.MessageIdx);
                }
            }
        }

        public override void SubmitSettings(bool active)
        {
            //ImGui.Text($"Submitted from DemoTab.SubmitSettings({active})");

            Setting_GameVersion();
            Setting_PacketListDisplay();
            Setting_MessageInspector();
            Setting_Colors();
        }

        private static void Setting_GameVersion()
        {
            ImGui.Text("Game Version:");
            ImGui.SameLine();
            if (ImGui.BeginCombo("###From", PacketPeepTool.PcktDb.SelectedBuildVersion)) {
                foreach (var buildName in PacketPeepTool.PcktDb.BuildVerStrings) {
                    ImGui.Selectable("###GameBuildVersion", buildName == PacketPeepTool.PcktDb.SelectedBuildVersion);
                    ImGui.SameLine();
                    if (ImGui.IsItemClicked()) {
                        PacketPeepTool.PcktDb.SelectedBuildVersion = buildName;
                    }

                    ImGui.SameLine();

                    ImGui.Text(buildName);
                }

                ImGui.EndCombo();
            }
        }

        private static void Setting_PacketListDisplay()
        {
            if (ImGui.CollapsingHeader("Packet List Display")) {
                ImGui.Indent();
                {
                    ImGui.Checkbox("Show Packet Idx", ref Config.Inst.PacketList.ShowPacketIdx);
                    ImGui.Checkbox("Show Packet Seq Num", ref Config.Inst.PacketList.ShowPacketSeqNum);
                    ImGui.Checkbox("Show Packet Ids", ref Config.Inst.PacketList.ShowPacketIds);
                }
                ImGui.Unindent();
            }
        }

        private static void Setting_MessageInspector()
        {
            if (ImGui.CollapsingHeader("Message Inspector")) {
                ImGui.Indent();
                {
                    ImGui.Checkbox("Show Parsed Values In Side", ref Config.Inst.ShowParsedValuesInSide);
                    ImGui.Checkbox("Show Parsed Values In Tooltip", ref Config.Inst.ShowParsedValuesInToolTip);
                }
                ImGui.Unindent();
            }
        }

        private static void Setting_Colors()
        {
            if (ImGui.CollapsingHeader("Colors")) {
                foreach (var kvp in Config.Inst.PColors) {
                    var color = kvp.Value;
                    ImGui.ColorEdit4(kvp.Key.ToString(), ref color);
                    Config.Inst.PColors[kvp.Key] = color;
                }
            }
        }

        private void DrawCaptureDialog()
        {
            FileBrowser.OpenFiles((files) =>
            {
                // Save the dir path for convenience for other runs
                var dirPath = Path.GetDirectoryName(files.FirstOrDefault() ?? "");
                if (dirPath != Config.Inst.LastCaptureDir) {
                    Config.Inst.LastCaptureDir = dirPath;
                    Config.Inst.Save();
                }

                foreach (var file in files) {
                    PacketPeepTool.PcktDb.LoadCapture(file);
                }
            }, Config.Inst.LastCaptureDir);

            ShowOpenCaptureDialog = false;
        }

        public void OpenCaptureDiaglog() => ShowOpenCaptureDialog = true;
    }
}