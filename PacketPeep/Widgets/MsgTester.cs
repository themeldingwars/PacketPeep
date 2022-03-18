using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Aero.Gen;
using Aero.Gen.Attributes;
using FauCap;
using ImGuiNET;
using ImTool;
using PacketPeep.Systems;

namespace PacketPeep.Widgets
{
    public class MsgTester
    {
        public Message Msg;
        public IAero   MsgObj;

        private const int PROFILE_ITERATIONS = 1000;

        private HexView hexDiff;
        private string  title;
        private byte[]  msgData;
        private int     getPackedSize    = 0;
        private int     actualPackedSize = 0;
        private bool    doesRepackMatch  = false;

        private TimeSpan profilePackMinTime;
        private TimeSpan profilePackMaxTime;
        private TimeSpan profilePackAvgTime;

        private TimeSpan profileUnpackMinTime;
        private TimeSpan profileUnpackMaxTime;
        private TimeSpan profileUnpackAvgTime;

        public MsgTester(Message msg, IAero msgObj)
        {
            Msg    = msg;
            MsgObj = msgObj;

            hexDiff = new HexView();
            CalcData();
        }

        public void CalcData()
        {
            var headerData = Utils.GetGssMessageHeader(Msg);

            var tempBuffer = new byte[5000];
            getPackedSize    = MsgObj.GetPackedSize();
            actualPackedSize = MsgObj.Pack(tempBuffer.AsSpan());
            msgData          = tempBuffer.AsSpan()[..actualPackedSize].ToArray();

            doesRepackMatch = msgData.SequenceEqual(Msg.Data[headerData.Length..].ToArray());

            ProfilePackTime();
            ProfileUnPackTime();

            var diffBytes = new List<HexView.HighlightSection>();
            var msgBytes  = Msg.Data[headerData.Length..];
            var maxBytes  = Math.Max(msgBytes.Length, msgData.Length);

            for (int i = 0; i < maxBytes; i++) {
                var highlight = new HexView.HighlightSection
                {
                    HoverName  = $"{i}",
                    Offset     = i,
                    Length     = 1,
                    Color      = ImGui.ColorConvertU32ToFloat4(0xFF5057FF),
                    IsSelected = false
                };

                if (i < msgBytes.Length && i < msgData.Length) {
                    var byte1 = msgBytes[i];
                    var byte2 = msgData[i];

                    if (byte1 != byte2) {
                        diffBytes.Add(highlight);
                    }
                }
                else {
                    diffBytes.Add(highlight);
                }
            }

            hexDiff.SetData(msgData, diffBytes.ToArray());
        }

        private void ProfilePackTime()
        {
            var tempBuffer = new byte[5000].AsSpan();
            var timings    = new TimeSpan[PROFILE_ITERATIONS];
            var sw         = new Stopwatch();
            for (int i = 0; i < PROFILE_ITERATIONS; i++) {
                sw.Reset();
                sw.Start();
                var packedAmount = MsgObj.Pack(tempBuffer);
                sw.Stop();
                timings[i] = sw.Elapsed;
            }

            profilePackMinTime = timings.Min();
            profilePackAvgTime = TimeSpan.FromTicks(timings.Sum(x => x.Ticks) / PROFILE_ITERATIONS);
            profilePackMaxTime = timings.Max();
        }

        private void ProfileUnPackTime()
        {
            var headerData = Utils.GetGssMessageHeader(Msg);
            var timings    = new TimeSpan[PROFILE_ITERATIONS];
            var sw         = new Stopwatch();
            for (int i = 0; i < PROFILE_ITERATIONS; i++) {
                var msgSrc = headerData.Channel switch
                {
                    Channel.Control       => AeroMessageIdAttribute.MsgType.Control,
                    Channel.Matrix        => AeroMessageIdAttribute.MsgType.Matrix,
                    Channel.ReliableGss   => AeroMessageIdAttribute.MsgType.GSS,
                    Channel.UnreliableGss => AeroMessageIdAttribute.MsgType.GSS
                };

                var msgObj = PacketParser.GetMessageFromIds(msgSrc, headerData.IsCommand ? AeroMessageIdAttribute.MsgSrc.Command : AeroMessageIdAttribute.MsgSrc.Message, headerData.MessageId, headerData.ControllerId);

                sw.Reset();
                sw.Start();
                var unpackedAmount = msgObj.Unpack(Msg.Data[headerData.Length..]);
                sw.Stop();
                timings[i] = sw.Elapsed;
            }

            profileUnpackMinTime = timings.Min();
            profileUnpackAvgTime = TimeSpan.FromTicks(timings.Sum(x => x.Ticks) / PROFILE_ITERATIONS);
            profileUnpackMaxTime = timings.Max();
        }

        public void Draw()
        {
            ImGui.SetNextWindowSize(new Vector2(500, 0), ImGuiCond.Appearing);
            if (ImGui.Begin($"Message Tester {Utils.GetMessageName(Msg)}", ImGuiWindowFlags.NoSavedSettings)) {
                if (ImGui.Button("Retest!", new Vector2(-1, 0))) {
                    CalcData();
                }

                DrawResultsTable();

                if (!doesRepackMatch) {
                    hexDiff.ShowSideParsedValues = true;
                    hexDiff.ShowParsedValuesInTT = true;

                    ImGui.PushID("Hex Diff View");
                    ImGui.BeginChild("Hex Diff View 2", new Vector2(-1, 0), true, ImGuiWindowFlags.NoSavedSettings);
                    hexDiff.Draw();
                    ImGui.EndChild();
                    ImGui.PopID();
                }
            }

            ImGui.End();
        }

        private void DrawResultsTable()
        {
            if (ImGui.BeginTable("Results", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg)) {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 0.6f);
                ImGui.TableSetupColumn("Result", ImGuiTableColumnFlags.None, 0.2f);
                ImGui.TableSetupColumn("###Icon", ImGuiTableColumnFlags.None, 0.2f);
                ImGui.TableHeadersRow();

                DrawResultTableRow("msgData.Length", $"{msgData.Length}");
                DrawResultTableRow("GetPackedSize", $"{getPackedSize}", msgData.Length       == getPackedSize);
                DrawResultTableRow("ActualPackedSize", $"{actualPackedSize}", msgData.Length == actualPackedSize);

                DrawResultTableRow("Does Repack Match", $"{doesRepackMatch}", doesRepackMatch);
                
                DrawEmptyTableRow();

                DrawResultTableRow("Unpack Timing Min", $"{profilePackMinTime.TotalMilliseconds} ms");
                DrawResultTableRow("Unpack Timing Avg", $"{profilePackAvgTime.TotalMilliseconds} ms");
                DrawResultTableRow("Unpack Timing Max", $"{profilePackMaxTime.TotalMilliseconds} ms");
                
                DrawEmptyTableRow();

                DrawResultTableRow("Pack Timing Min", $"{profileUnpackMinTime.TotalMilliseconds} ms");
                DrawResultTableRow("Pack Timing Avg", $"{profileUnpackAvgTime.TotalMilliseconds} ms");
                DrawResultTableRow("Pack Timing Max", $"{profileUnpackMaxTime.TotalMilliseconds} ms");

                ImGui.EndTable();
            }
        }

        private void DrawResultTableRow(string name, string result, bool? pass = null)
        {
            ImGui.TableNextColumn();
            ImGui.Text(name);

            ImGui.TableNextColumn();
            ImGui.Text(result);

            ImGui.TableNextColumn();
            if (pass != null) {
                FontManager.PushFont("FAS");
                ImGui.PushStyleColor(ImGuiCol.Text, pass.Value ? 0xff98ff98 : 0xFF5057FF);
                {
                    ImGui.Text(pass.Value ? "" : "");
                }
                ImGui.PopStyleColor();
                FontManager.PopFont();
            }
        }

        private void DrawEmptyTableRow() => DrawResultTableRow("", "");
    }
}