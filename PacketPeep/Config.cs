using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using ImTool;
using Newtonsoft.Json;
using ImTool.JsonConverters;

namespace PacketPeep
{
    public class Config : Configuration
    {
        public static Config Inst;

        public string LastCaptureDir         = null;
        public string LastReplayDir         = null;
        public string GameBuildVersion       = "production 1962.0";
        public string AeroMessageDllLocation = null;

        public PacketListDisplay PacketList = new();

        [JsonProperty(ItemConverterType = typeof(ColorConverter))]
        public Dictionary<Colors, Vector4> PColors { get; set; } = new();

        [JsonProperty(ItemConverterType = typeof(ColorConverter))]
        public Dictionary<string, Vector4> MessageEntryColors { get; set; } = new();

        // Message Inspector settings
        public bool ShowParsedValuesInSide    = false;
        public bool ShowParsedValuesInToolTip = true;

        public ReplayServerSettings ReplayServer { get; set; } = new ();

        // override the default window settings
        public Config()
        {
            Title                 = "Packet Peep";
            GithubRepositoryOwner = "themeldingwars";
            GithubRepositoryName  = "PacketPeep";
            GithubReleaseName     = "PacketPeep";

            // Default colors
            PColors.Add(Colors.Server, HexColorToVec4("EC4646"));
            PColors.Add(Colors.Client, HexColorToVec4("4E89AE"));
            PColors.Add(Colors.Control, HexColorToVec4("268BDD"));
            PColors.Add(Colors.Matrix, HexColorToVec4("#A127AD"));
            PColors.Add(Colors.UGSS, HexColorToVec4("FB9300"));
            PColors.Add(Colors.RGSS, HexColorToVec4("64CCDA"));
            PColors.Add(Colors.Jack, HexColorToVec4("FF4D00"));

            // Message line entry colors
            byte[][] hexHiglightColors = new byte[][]
            {
                new byte[] {125, 62, 25},
                new byte[] {72, 60, 202},
                new byte[] {45, 98, 9},
                new byte[] {115, 32, 192},
                new byte[] {31, 81, 19},
                new byte[] {52, 26, 150},
                new byte[] {86, 88, 17},
                new byte[] {142, 26, 160},
                new byte[] {44, 77, 30},
                new byte[] {100, 26, 138},
                new byte[] {43, 99, 63},
                new byte[] {75, 61, 168},
                new byte[] {175, 48, 7},
                new byte[] {44, 27, 120},
                new byte[] {76, 69, 19},
                new byte[] {130, 54, 142},
                new byte[] {16, 50, 20},
                new byte[] {148, 25, 112},
                new byte[] {43, 52, 18},
                new byte[] {77, 39, 121},
                new byte[] {160, 30, 29},
                new byte[] {52, 72, 141},
                new byte[] {147, 49, 28},
                new byte[] {39, 31, 91},
                new byte[] {106, 72, 31},
                new byte[] {97, 31, 98},
                new byte[] {56, 80, 58},
                new byte[] {159, 28, 77},
                new byte[] {45, 84, 81},
                new byte[] {156, 35, 53},
                new byte[] {51, 81, 108},
                new byte[] {104, 24, 18},
                new byte[] {33, 43, 79},
                new byte[] {134, 54, 50},
                new byte[] {27, 48, 59},
                new byte[] {120, 25, 81},
                new byte[] {25, 41, 30},
                new byte[] {101, 67, 123},
                new byte[] {85, 82, 53},
                new byte[] {57, 22, 71},
                new byte[] {63, 45, 19},
                new byte[] {122, 57, 91},
                new byte[] {51, 38, 27},
                new byte[] {119, 32, 59},
                new byte[] {88, 70, 100},
                new byte[] {74, 28, 21},
                new byte[] {125, 68, 79},
                new byte[] {62, 31, 49},
                new byte[] {101, 66, 60},
                new byte[] {82, 20, 37}
            };

            // Ya ok this isn't greaaat but for now >,>
            var hightlightIdx = 0;
            foreach (var highlight in hexHiglightColors) {
                //var color = HexColorToVec4($"{highlight[0]:x0}{highlight[1]:x0}{highlight[2]:x0}");
                var color = new Vector4(0.003921569f * highlight[0], 0.003921569f * highlight[1], 0.003921569f * highlight[2], 1f);
                var name  = $"Color {hightlightIdx++}";
                MessageEntryColors.TryAdd(name, color);
            }
        }

        private static Vector4 HexColorToVec4(string color)
        {
            if (ColorConverter.TryConvertHexToVector4(color, out var vec4)) {
                return vec4;
            }

            return Vector4.One;
        }

        public class PacketListDisplay
        {
            public bool ShowPacketIdx    = true;
            public bool ShowPacketSeqNum = false;
            public bool ShowPacketIds    = false;

            // Get the num columns to be shown from the settings
            public int GetNumColumsNeeded()
            {
                int numColumns = 0;

                if (ShowPacketIdx) numColumns++;
                if (ShowPacketSeqNum) numColumns++;
                if (ShowPacketIds) numColumns++;

                return numColumns;
            }
        }

        public enum Colors
        {
            Server,
            Client,
            Control,
            Matrix,
            UGSS,
            RGSS,
            Jack
        }

        public class ReplayServerSettings
        {
            public ushort ListenPort = 44501;
        }
    }
}