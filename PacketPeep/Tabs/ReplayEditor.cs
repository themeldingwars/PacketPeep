using System;
using System.IO;
using System.Numerics;
using ImGuiNET;
using ImTool;
using PacketPeep.FauFau.Formats;
using PacketPeep.Systems;
using PacketPeep.Widgets;

namespace PacketPeep.Tabs
{
    public class ReplayEditorTab : Tab
    {
        public override string         Name => "Replay Editor";
        public          PacketPeepTool Tool;
        private         bool           showOpenReplayDialog = false;

        private Nsr ReplayFile;

        private AeroInspector ReplayInspectorHeaderTemp;
        
        protected override unsafe void SubmitContent()
        {
            PacketPeepTool.ActiveTab = TabIds.ReplayEd;
            
            /*var wClass = new ImGuiWindowClass();
            wClass.DockNodeFlagsOverrideSet = ImGuiDockNodeFlags.NoWindowMenuButton | ImGuiDockNodeFlags.NoCloseButton | ImGuiDockNodeFlags.NoDockingOverMe;
            ImGuiNative.igSetNextWindowClass(&wClass);
            ImGui.Begin("Workspace");
            
            ImGui.End();*/
            
            DrawReplayerHeaderInspector();
            DrawDebug();

            if (showOpenReplayDialog) DrawReplayDialog();
            FileBrowser.Draw();
            
            PacketPeepTool.Log.DrawWindow();
        }
        
        protected override void CreateDockSpace(Vector2 size)
        {
            ImGui.DockBuilderSplitNode(DockSpaceID, ImGuiDir.Left, 0.2f, out var leftId, out var rightId);
            ImGui.DockBuilderSplitNode(rightId, ImGuiDir.Down, 0.2f, out var rightBottomId, out var rightTopId);

            ImGui.DockBuilderDockWindow("Replay Header", leftId);
            ImGui.DockBuilderDockWindow("Logs", rightBottomId);
            //ImGui.DockBuilderDockWindow("Workspace", rightTopId);
            ImGui.DockBuilderDockWindow("Replay Debug", rightTopId);
        }
        
        public void OpenReplay(string path)
        {
            ReplayFile = new();
            ReplayFile.Read(path);

            ReplayInspectorHeaderTemp = new(ReplayFile.HeaderData, null);
        }

        private void DrawReplayerHeaderInspector()
        {
            if (ImGui.Begin("Replay Header")) {
                if (ReplayFile is {HeaderData: { }}) {
                    ReplayInspectorHeaderTemp.Draw();   
                }
            }
            
            ImGui.End();
        }
        
        private void DrawReplayerFramesInspector()
        {
            if (ImGui.Begin("Replay Inspector Frames")) {
                
            }
            
            ImGui.End();
        }
        
        private void DrawDebug()
        {
            if (ImGui.Begin("Replay Debug")) {
                if (ReplayFile != null) {
                    ImGui.Text($"Num Keyframes: {ReplayFile.KeyFrames.Length}");

                    for (var idx = 0; idx < ReplayFile.KeyFrames.Length; idx++) {
                        var keyframe = ReplayFile.KeyFrames[idx];
                        if (ImGui.CollapsingHeader($"Keyframe: {idx}, Num frames: {keyframe.Frames.Count}")) {
                            ImGui.Indent();

                            for (int i = 0; i < keyframe.Frames.Count; i++) {
                                var frame = keyframe.Frames[i];
                                ImGui.Text($"Frame idx: {i}");
                                ImGui.Text($"Time: {frame.TimeStamp} ({TimeSpan.FromMilliseconds(frame.TimeStamp)})");
                                ImGui.Text($"Length: {frame.Length}");
                                ImGui.Text($"Unk1: {frame.Unk1}");
                                ImGui.Text($"Unk2: {frame.Unk2}");

                                var controllerData = PacketPeepTool.PcktDb.ControllerList[frame.ControllerId];
                                var hasMsgName     = controllerData.Messages.TryGetValue(frame.MsgIdMaybe, out var msgName);
                                ImGui.Text($"ControllerId: {frame.ControllerId} ({controllerData.Name})");
                                ImGui.Text($"EntityId: {frame.EntityId}");
                                ImGui.Text($"MsgIdMaybe: {frame.MsgIdMaybe} ({(hasMsgName ? msgName : frame.MsgIdMaybe)})");
                                ImGui.Text($"WeirdInt: {frame.WeirdInt}");
                                
                                ImGui.Separator();
                                ImGui.NewLine();
                            }

                            ImGui.Unindent();
                        }
                    }
                }
            }
            
            ImGui.End();
        }
        
        private void DrawReplayDialog()
        {
            FileBrowser.OpenFile(file =>
            {
                // Save the dir path for convenience for other runs
                var dirPath = Path.GetDirectoryName(file ?? "");
                if (dirPath != Config.Inst.LastReplayDir) {
                    Config.Inst.LastReplayDir = dirPath;
                    Config.Inst.Save();
                }

                PacketPeepTool.Log.AddLogInfo(LogCategories.ReplayEditor, $"Loading replay: {file}");
                OpenReplay(file);
            }, Config.Inst.LastReplayDir, "*.nsr");

            showOpenReplayDialog = false;
        }

        public void OpenReplayDiaglog() => showOpenReplayDialog = true;

    }
}