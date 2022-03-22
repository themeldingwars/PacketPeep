using System;
using Aero.Gen;
using ImGuiNET;
using ImTool;

namespace PacketPeep.Widgets;

public class AeroReadLogInspector
{
    public IAero          AeroObj;
    public Action<string> LogError;
    
    private const int COLOR_BAR_WIDTH = 10;
    private const int LINE_HEIGHT     = 30;
    private const int INDENT_DIST     = 5;
    
    public AeroReadLogInspector(IAero aeroObj, Action<string> logError)
    {
        AeroObj  = aeroObj;
        LogError = logError;
        BuildData();
    }
    
    public void BuildData()
    {
        if (AeroObj == null) return;

        var readLogs = AeroObj.GetDiagReadLogs();
    }
    
    public void Draw()
    {
        FontManager.PushFont("Regular_Small");
        if (ImGui.BeginTable("Inspector Table", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable)) {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.NoSort, 0.5f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.None, 0.5f);
            ImGui.TableHeadersRow();

            var indentLevel = 0;
            foreach (var entry in AeroObj.GetDiagReadLogs()) {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, LINE_HEIGHT);
                //DrawEntry(entry, ref indentLevel);
                
                
                ImGui.TableNextColumn();
                ImGui.Text($"{entry.ParentName} {entry.Name}");

                ImGui.TableNextColumn();
                ImGui.Text($"{entry.Offset}, {entry.Length}, {entry.Type}, {entry.EntryType}");

                indentLevel = 0;
            }

            ImGui.EndTable();
        }

        FontManager.PopFont();
    }
}