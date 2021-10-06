using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Aero.Gen.Attributes;
using McMaster.NETCore.Plugins;

namespace PacketPeep.Systems
{
    public static class PacketParser
    {
        private static PluginLoader Loader;
        private static MethodInfo   GetAeroMessageHandlerMI;

        public static void Init()
        {
            // Warn if we don't have a path for the aero messages dll
            if (string.IsNullOrEmpty(Config.Inst.AeroMessageDllLocation)) {
                PacketPeepTool.Log.AddLogWarn(LogCategories.PacketParser, "No Path set for the Aero Messages Dll!, Packet parsing won't work unless this is set in the settings!");
                PacketPeepTool.Log.AddLogInfo(LogCategories.PacketParser, "Please get the packetPeepData repo and compile the messages dll for the game version you want and then set that dll in the settings :>");
            }
            else if (!File.Exists(Config.Inst.AeroMessageDllLocation)) {
                PacketPeepTool.Log.AddLogWarn(LogCategories.PacketParser, $"Can't find Aero Messages dll at the location: {Config.Inst.AeroMessageDllLocation}, please make sure its still there.");
            }
            else {
                CreateLoader();
            }
        }

        private static void CreateLoader()
        {
            try {
                Loader?.Dispose();

                Loader = PluginLoader.CreateFromAssemblyFile(
                    assemblyFile: Config.Inst.AeroMessageDllLocation,
                    sharedTypes: new[] {typeof(Aero.Gen.IAero)},
                    isUnloadable: true,
                    configure: config =>
                    {
                        config.EnableHotReload   = true;
                        config.PreferSharedTypes = true;
                    });

                if (Loader != null) {
                    Loader.Reloaded         += LoaderOnReloaded;
                    GetAeroMessageHandlerMI =  Loader.LoadDefaultAssembly().GetType("AeroRouting")?.GetMethod("GetNewMessageHandler");

                    CheckAeroAssemblyVersion();
                    PacketPeepTool.Log.AddLogInfo(LogCategories.PacketParser, "Dll loaded and message router bound");
                }
                else {
                    PacketPeepTool.Log.AddLogError(LogCategories.PacketParser, "Error loading Aero Messages DLL");
                }
            }
            catch (Exception e) {
                PacketPeepTool.Log.AddLogError(LogCategories.PacketParser, $"Error loading Dll: {e}");
            }
        }

        private static void LoaderOnReloaded(object sender, PluginReloadedEventArgs eventargs)
        {
            PacketPeepTool.Log.AddLogInfo(LogCategories.PacketParser, "DLL reloaded.");
        }

        private static void CheckAeroAssemblyVersion()
        {
            if (Loader != null) {
                var aeroMsgsAeroGenRef = Loader.LoadDefaultAssembly().GetReferencedAssemblies().FirstOrDefault(x => x.Name    == "Aero.Gen");
                var hostRef            = Assembly.GetExecutingAssembly().GetReferencedAssemblies().FirstOrDefault(x => x.Name == "Aero.Gen");

                if (aeroMsgsAeroGenRef.Version != hostRef.Version) {
                    PacketPeepTool.Log.AddLogError(LogCategories.PacketParser, $"The Version ({aeroMsgsAeroGenRef.Version}) of Aero.Gen in the loaded dll doesn't match the host ({hostRef.Version}), these need to match.");
                }
            }
        }

        public static void LoadDll()
        {
            PacketPeepTool.Log.AddLogInfo(LogCategories.PacketParser, "Loaded Dll!");
        }

        public static object GetMessageFromIds(AeroMessageIdAttribute.MsgType msgType, AeroMessageIdAttribute.MsgSrc msgSrc, int messageId, int controllerId = -1)
        {
            using (Loader.EnterContextualReflection()) {
                var result = GetAeroMessageHandlerMI.Invoke(null, new object[] {msgType, msgSrc, messageId, controllerId});
                return result;
            }
        }

        public static void RefreshDllLocation()
        {
            CreateLoader();
        }

        // any drawing we need for the parser like popups and alerts
        public static void Draw()
        {
            
        }
    }
}