using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Aero.Gen;
using Aero.Gen.Attributes;
using FauCap;
using McMaster.NETCore.Plugins;

namespace PacketPeep.Systems
{
    public static class PacketParser
    {
        private static PluginLoader Loader;
        private static MethodInfo   GetAeroMessageHandlerMI;

        public static Action OnDllReload;

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
            if (Config.Inst.AeroMessageDllLocation == null || !File.Exists(Config.Inst.AeroMessageDllLocation))
                return;
            
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

                    PostDllLoad();
                }
                else {
                    PacketPeepTool.Log.AddLogError(LogCategories.PacketParser, "Error loading Aero Messages DLL");
                }
            }
            catch (Exception e) {
                PacketPeepTool.Log.AddLogError(LogCategories.PacketParser, $"Error loading Dll: {e}");
            }
        }

        private static void PostDllLoad()
        {
            GetAeroMessageHandlerMI = Loader.LoadDefaultAssembly().GetType("AeroRouting")?.GetMethod("GetNewMessageHandler");

            if (GetAeroMessageHandlerMI == null) {
                PacketPeepTool.Log.AddLogError(LogCategories.PacketParser, "GetAeroMessageHandlerMI was null from loaded dll!");
            }

            CheckAeroAssemblyVersion();
            PacketPeepTool.Log.AddLogInfo(LogCategories.PacketParser, "Dll loaded and message router bound");
        }

        private static void LoaderOnReloaded(object sender, PluginReloadedEventArgs eventargs)
        {
            PostDllLoad();
            OnDllReload?.Invoke();
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

        public static IAero GetMessageFromIds(AeroMessageIdAttribute.MsgType msgType, AeroMessageIdAttribute.MsgSrc msgSrc, int messageId, int controllerId = -1)
        {
            using (Loader.EnterContextualReflection()) {
                var result = (IAero)GetAeroMessageHandlerMI.Invoke(null, new object[] {msgType, msgSrc, messageId, controllerId});
                return result;
            }
        }

        public static void ParseMessagesForSession(PacketDbSession session)
        {
            foreach (var msg in session.Session.Messages) {
                var msgHeader = Utils.GetGssMessageHeader(msg);
                var msgSrc    = msgHeader.IsCommand ? AeroMessageIdAttribute.MsgSrc.Command : AeroMessageIdAttribute.MsgSrc.Message;
                var msgType = msgHeader.Channel switch
                {
                    Channel.Control       => AeroMessageIdAttribute.MsgType.Control,
                    Channel.Matrix        => AeroMessageIdAttribute.MsgType.Matrix,
                    Channel.ReliableGss   => AeroMessageIdAttribute.MsgType.GSS,
                    Channel.UnreliableGss => AeroMessageIdAttribute.MsgType.GSS,
                    _                     => AeroMessageIdAttribute.MsgType.Control
                };

                if (msg.Server == Server.Matrix) { // jack messages
                    session.ParsedMessages.Add(null);
                }
                else {
                    if (msg is SubMessage {EntityId: 0}) {
                        session.ParsedMessages.Add(null);
                    }
                    else {
                        var msgObj = GetMessageFromIds(msgType, msgSrc, msgHeader.MessageId, msgHeader.ControllerId);
                        //Debug.WriteLine($"MessageId: {msgHeader.MessageId}, {msgHeader.ControllerId}, msgObj: {msgObj}");

                        if (msgObj != null) {
                            try {
                                var data   = msg.Data[msgHeader.Length..];
                                var isView = msgObj is IAeroViewInterface;

                                // If its a controller skip the player id
                                if (msgObj.GetType().GetCustomAttribute<AeroAttribute>().AeroType == AeroGenTypes.Controller && msgHeader.MessageId is 4) {
                                    data = data[8..];
                                }
                                
                                if (msgObj is IAeroViewInterface aeroView && msgHeader.MessageId is 1) {
                                    var amountRead = aeroView.UnpackChanges(data);
                                }
                                else {
                                    var amountRead = msgObj.Unpack(data);
                                }
                                
                                PacketPeepTool.Log.AddLogTrace(LogCategories.PacketParser, $"Parsed packet {msgObj.GetType().Name} {msgType} {msgHeader.ControllerId}::{msgHeader.MessageId}, {Utils.GetMessageName(msg)}, IsView: {isView}");
                            }
                            catch (Exception e) {
                                PacketPeepTool.Log.AddLogError(LogCategories.PacketParser, $"Error unpacking message for {msgType} {msgHeader.ControllerId}::{msgHeader.MessageId}, Message Idx: {msg.Id} {Utils.GetMessageName(msg)} to {msgObj.GetType().Name}\n{e}");
                            }
                        }
                    
                    
                        // If it was null from not having a class yet still add it to keep the indexes lined up
                        session.ParsedMessages.Add(msgObj);
                    }
                }
            }
            
            GC.Collect();
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