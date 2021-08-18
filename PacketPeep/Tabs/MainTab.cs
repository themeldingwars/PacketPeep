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

        public MainTab()
        {
            // Temp testing for now
            PacketExp.OnMessageSelected += (i, b) => { PacketPeepTool.Log.AddLogTrace(LogCategories.General, $"Select item: {i}"); };
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
        }

        public override void SubmitSettings(bool active)
        {
            //ImGui.Text($"Submitted from DemoTab.SubmitSettings({active})");

            Setting_GameVersion();
            Setting_PacketListDisplay();
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