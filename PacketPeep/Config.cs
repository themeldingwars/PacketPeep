using ImTool;

namespace PacketPeep
{
    public class Config : Configuration
    {
        public static Config Inst;

        public string LastCaptureDir   = null;
        public string GameBuildVersion = "production 1962.0";

        public PacketListDisplay PacketList = new();

        // override the default window settings
        public Config()
        {
            Title = "Packet Peep";
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
    }
}