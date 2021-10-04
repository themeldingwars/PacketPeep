using System.Collections.Generic;
using System.Numerics;
using ImTool;
using Newtonsoft.Json;
using ImTool.JsonConverters;

namespace PacketPeep
{
    public class Config : Configuration
    {
        public static Config Inst;

        public string LastCaptureDir   = null;
        public string GameBuildVersion = "production 1962.0";

        public PacketListDisplay PacketList = new();

        [JsonProperty(ItemConverterType = typeof(ImTool.JsonConverters.ColorConverter))]
        public Dictionary<Colors, Vector4> PColors { get; set; } = new();
        
        // Message Inspector settings
        public bool ShowParsedValuesInSide    = false;
        public bool ShowParsedValuesInToolTip = true;

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
    }
}