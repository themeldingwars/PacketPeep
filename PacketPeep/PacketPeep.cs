using System;
using ImGuiNET;
using ImTool;
using ImTool.SDL;
using PacketPeep.Systems;
using PacketPeep.Tabs;
using PacketPeep.Widgets;

namespace PacketPeep
{
    public class PacketPeepTool : Tool<PacketPeepTool, Config>
    {
        public static LogWindow<LogCategories> Log         = new LogWindow<LogCategories>("Logs");
        public static PacketDb                 PcktDb      = new PacketDb();
        public static MainTab                  Main        = new MainTab();
        public static ReplayEditorTab          ReplayEdTab = new ReplayEditorTab();
        public static TinkerTab                Tinker      = new();

        public static TabIds ActiveTab = TabIds.Main;

        //public PacketPeepConfig Config => config;
        
        protected override bool Initialize(string[] args)
        {
            // override this method if you need to parse cmd-line args or do checks before the tool window is created
            // only the config has been loaded at this point
            // returning false will exit the tool
            
            // example use cases: 
            //      parsing cmd-line messages
            //      mutex check to ensure only a single instance of the tool is running
            //      routing cmd-line messages to a single instance tool
            //
            
            Config.Inst      = config;
            Main.Tool        = this;
            ReplayEdTab.Tool = this;
            
            Log.ToggleLevel(LogWindow<LogCategories>.LogLevel.Trace, false);
            
            // Setup dll reloading and reparsing of packets
            PacketParser.OnDllReload += () =>
            {
                PcktDb.ReparseSessions();
                Main.RefreshMessageInspectors();
            };
            
            return true;
        }

        protected override void Load()
        {
            window.AddTab(Main);
            window.AddTab(ReplayEdTab);
            window.AddTab(Tinker);

            window.AddWindowButton(new WindowButton("Load Capture / Replay", () =>
            {
                switch (ActiveTab) {
                    case TabIds.Main:
                        Main.OpenCaptureDiaglog();
                        break;
                    
                    case TabIds.ReplayEd:
                        ReplayEdTab.OpenReplayDiaglog();
                        break;
                }
            }));
            
            PacketParser.Init();
        }

        protected override void Unload()
        {
            // time to clean up your shit
            // you can still do edits to the config,
            // changes will be saved to disk when this method returns
        }
    }

    public enum TabIds : byte
    {
        Main,
        ReplayEd,
        Tinker
    }
}