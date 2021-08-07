using System.IO;
using System.Linq;
using ImGuiNET;
using ImTool;

namespace PacketPeep.Widgets
{
    public class MainTab : Tab
    {
        public override string         Name { get; } = "Packet Peep";
        public          bool           ShowOpenCaptureDialog = false;
        public          PacketPeepTool Tool;

        public PacketExplorer PacketExp = new PacketExplorer();

        public override void SubmitContent()
        {
            //ImGui.ShowDemoWindow();
            
            if (ShowOpenCaptureDialog) DrawCaptureDialog();
            FileBrowser.Draw();
            
            PacketExp.Draw();
            
            PacketPeepTool.Log.DrawWindow();
        }

        public override void SubmitSettings(bool active)
        {
            //ImGui.Text($"Submitted from DemoTab.SubmitSettings({active})");

            Setting_GameVersion();
            Setting_PacketListDisplay();
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
                ImGui.Checkbox("Show Packet Idx", ref Config.Inst.PacketList.ShowPacketIdx);
                ImGui.Checkbox("Show Packet Seq Num", ref Config.Inst.PacketList.ShowPacketSeqNum);
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