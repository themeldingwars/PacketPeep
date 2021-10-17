using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Aero.Gen;
using FauCap;
using Newtonsoft.Json.Schema;
using Sift;

namespace PacketPeep.Systems
{
    public class PacketDb
    {
        public Dictionary<string, PacketDbSession> Sessions = new();

        // Sift data
        public Sift.Result           SiftData;
        public List<string>          BuildVerStrings      = new();
        public string                SelectedBuildVersion = "production 1962.0";
        public string                SiftPatchToName(Result.Patch patch) => $"{patch.Environment} {patch.Version}";
        public Result.Patch          GetPatchByName(string        name)  => SiftData.Patches.FirstOrDefault(x => SiftPatchToName(x) == name);
        public Result.MatrixProtocol GetSiftMatrixProtocol               => SiftData.MatrixProtocols[GetPatchByName(SelectedBuildVersion).MatrixProtocolVersion];
        public Result.GssProtocol    GetSiftGssProtocol                  => SiftData.GssProtocols[GetPatchByName(SelectedBuildVersion).GSSProtocolVersion];
        public List<int>             FilteredIndices = new();

        // Packet id and names
        public      Dictionary<int, ControllerData> ControllerList = new();
        public       string                          GetViewName(int id) => GetControllerData(id)?.Name ?? $"{id}";
        public const int                             CONTROL_REF_ID  = -2;
        public const int                             MATRIX_REF_ID   = -1;
        public const int                             FIREFALL_REF_ID = 0;

        public PacketDb()
        {
            LoadSiftData();
            BuildMessageNameLists();
        }

        private void LoadSiftData()
        {
            try {
                var dir = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Sift");
                SiftData = Sift.Result.Load(dir);

                BuildVerStrings = new List<string>();
                foreach (var patch in SiftData.Patches) {
                    var name = $"{patch.Environment} {patch.Version}";
                    BuildVerStrings.Add(name);
                }

                PacketPeepTool.Log.AddLogInfo(LogCategories.PacketDB, $"Loaded sift data from: {dir}");
            }
            catch (Exception e) {
                PacketPeepTool.Log.AddLogWarn(LogCategories.PacketDB, $"Couldn't load Sift data, check it exists in Data/Sift");
            }
        }

        private void BuildMessageNameLists()
        {
            var matrixMsgs = GetSiftMatrixProtocol;
            var gssMsgs    = GetSiftGssProtocol;
            ControllerList.Clear();
            
            gssMsgs.Messages.Add("RoutedMultipleMessage 1", 8);
            gssMsgs.Messages.Add("RoutedMultipleMessage 2", 9);
            var orderedMsgs = gssMsgs.Messages.OrderBy(x => x.Value).ToDictionary(x => x.Key, y => y.Value);
            gssMsgs.Messages.Clear();
            gssMsgs.Messages = orderedMsgs;

            ControllerList.Add(CONTROL_REF_ID, new ControllerData(CONTROL_REF_ID, "Control", new Dictionary<byte, string>
            {
                {0x0, "Close"},
                {0x2, "MatrixAck"},
                {0x3, "GSSAck"},
                {0x4, "TimeSyncRequest"},
                {0x5, "TimeSyncResponse"},
                {0x6, "MTUProbe"}
            }));
            ControllerList.Add(MATRIX_REF_ID, new ControllerData(MATRIX_REF_ID, "Matrix", matrixMsgs.Messages.ToDictionary(x => x.Value, y => y.Key)));
            ControllerList.Add(FIREFALL_REF_ID, new ControllerData(FIREFALL_REF_ID, "Firefall", gssMsgs.Messages.ToDictionary(x => x.Value, y => y.Key)));

            // Duplication for simplicity
            foreach (var (nsName, ns) in gssMsgs.Children) {
                if (ns.Views != null) {
                    foreach (var (viewName, viewId) in ns.Views) {
                        var name = $"{nsName}::{viewName}";
                        var controllerData = new ControllerData
                        {
                            Id       = viewId,
                            Name     = name,
                            Messages = ns.Messages?.ToDictionary(x => x.Value, y => y.Key) ?? new Dictionary<byte, string>(),
                            Commands = ns.Commands?.ToDictionary(x => x.Value, y => y.Key) ?? new Dictionary<byte, string>()
                        };
                        
                        // Inbuilt messages
                        controllerData.Messages.Add(1, "Update");
                        controllerData.Messages.Add(2, "Checksum");
                        controllerData.Messages.Add(3, "Keyframe View");
                        controllerData.Messages.Add(4, "Keyframe Controller");
                        controllerData.Messages.Add(5, "Remove Controller");
                        controllerData.Messages.Add(6, "Remove Controller");
                        var orderedMsgs2 = controllerData.Messages.OrderBy(x => x.Key).ToDictionary(x => x.Key, y => y.Value);
                        controllerData.Messages.Clear();
                        controllerData.Messages = orderedMsgs2;

                        ControllerList.Add(viewId, controllerData);
                    }
                }
            }
        }

        // Load a packet capture from either a pcap or a faucap
        public void LoadCapture(string path)
        {
            try {
                PacketPeepTool.Log.AddLogInfo(LogCategories.PacketDB, $"Loading capture: {path}");

                var extension = Path.GetExtension(path);
                var name      = Path.GetFileNameWithoutExtension(path);
                if (extension == ".pcap") {
                    var sessions = new FauCap.Converter().PcapFileToFaucap(path);
                    AddSessions(sessions, name);
                }
                else if (extension == ".faucap") {
                    var sessions = FauCap.GameSession.Read(path);
                    AddSessions(sessions, name);
                }
            }
            catch (Exception e) {
                PacketPeepTool.Log.AddLogError(LogCategories.PacketDB, $"Error loading capture {path}, error: {e.ToString()}");
            }
        }

        public void RemoveSession(string name)
        {
            if (Sessions.ContainsKey(name)) {
                Sessions.Remove(name);
                PacketPeepTool.Log.AddLogInfo(LogCategories.PacketDB, $"Removed session {name}");
                
                GC.Collect();
            }
        }

        // Filter by the settings in this filter and build the FilteredIndices list
        public void ApplyFilter(PacketFilter filter)
        {
            if (filter == null) return;

            if (Sessions.TryGetValue(filter.SessionName, out PacketDbSession session)) {
                FilteredIndices = new List<int>(1000);

                //Parallel.ForEach(session.Session.Messages, (msg) =>
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < session.Session.Messages.Count; i++) {
                    var  msg       = session.Session.Messages[i];
                    bool fromCheck = false;
                    fromCheck |= msg.FromServer  && filter.FromServer;
                    fromCheck |= !msg.FromServer && filter.FromClient;


                    // Channel filtering
                    bool channelCheck = false;
                    channelCheck |= msg.Server == Server.Matrix && filter.ChanJack; // Handshake message etc

                    bool msgIdCheck = filter.MsgFilters.Count == 0;
                    if (msg.Server == Server.Game && msg is GameMessage gameMsg) {
                        channelCheck |= gameMsg.Channel == Channel.Control       && filter.ChanControl;
                        channelCheck |= gameMsg.Channel == Channel.Matrix        && filter.ChanMatrix;
                        channelCheck |= gameMsg.Channel == Channel.UnreliableGss && filter.ChanUgss;
                        channelCheck |= gameMsg.Channel == Channel.ReliableGss   && filter.ChanRgss;

                        // Message id and controller filtering
                        foreach (var msgFilter in filter.MsgFilters) {
                            if (gameMsg.Channel == Channel.Matrix && msgFilter.ViewId == -1) {
                                var id = gameMsg.Data[0];
                                msgIdCheck |= msgFilter.MsgId == id || msgFilter.MsgId == -1;
                            }
                            else if (gameMsg.Channel == Channel.Control && msgFilter.ViewId == -2) {
                                var id = gameMsg.Data[0];
                                msgIdCheck |= msgFilter.MsgId == id || msgFilter.MsgId == -1;
                            }
                            else if (gameMsg.Channel is Channel.ReliableGss or Channel.UnreliableGss) {
                                var cid           = gameMsg.Data[0];
                                var msgId         = gameMsg.Data[8];
                                var viewIsMatched = msgFilter.ViewId == cid || (msgFilter.ViewId == 0 && cid is 0 or 251); // 0 and 251 are generic namespaces
                                if (msgFilter.MsgId != int.MinValue) {
                                    msgIdCheck |= viewIsMatched && gameMsg.FromServer && (msgFilter.MsgId == msgId || msgFilter.MsgId == -1);
                                }

                                if (msgFilter.CmdId != int.MinValue) {
                                    msgIdCheck |= viewIsMatched && !gameMsg.FromServer && (msgFilter.CmdId == msgId || msgFilter.CmdId == -1);
                                }
                            }
                        }
                    }

                    if (fromCheck && channelCheck && msgIdCheck) FilteredIndices.Add(msg.Id);
                } //);

                sw.Stop();

                PacketPeepTool.Log.AddLogTrace(LogCategories.PacketDB, $"Filtering {session.Session.Messages.Count} took {sw.Elapsed}");
            }
            else {
                PacketPeepTool.Log.AddLogWarn(LogCategories.PacketDB, $"Couldn't find session {filter.SessionName} to filter to. :<");
            }
        }

        private void AddSessions(IEnumerable<GameSession> sessions, string name)
        {
            int idx = 0;
            foreach (var session in sessions) {
                var sessionName     = $"{name} {idx++}";
                var gssProtoVer     = (ushort)IPAddress.NetworkToHostOrder((short)session.StreamingProtocol);
                var siftGssProtoVer = GetPatchByName(SelectedBuildVersion).GSSProtocolVersion;
                if (gssProtoVer != GetPatchByName(SelectedBuildVersion).GSSProtocolVersion) {
                    PacketPeepTool.Log.AddLogWarn(LogCategories.PacketDB, $"Session {sessionName} GSS Protocol version doesn't match, is {gssProtoVer}, expected {siftGssProtoVer}.");
                }
                
                if (!Sessions.ContainsKey(sessionName)) {
                    var packetSession = new PacketDbSession(session, sessionName);
                    Sessions.Add(sessionName, packetSession);
                    PacketParser.ParseMessagesForSession(packetSession);
                    PacketPeepTool.Log.AddLogInfo(LogCategories.PacketDB, $"Added session {sessionName}, {session.Datagrams.Count:N0} Datagrams, {session.Packets.Count:N0} Packets, {session.Messages.Count:N0} Messages");
                }
            }
            
            GC.Collect();
        }


        public void ReparseSessions()
        {
            foreach (var session in Sessions.Values) {
                session.ParsedMessages.Clear();
                PacketParser.ParseMessagesForSession(session);
            }
        }

        public ControllerData GetControllerData(int id)
        {
            id = id == 251 ? 0 : id; // The generic 0 Firefall viw can also be 251
            if (ControllerList.TryGetValue(id, out var cdata))
                return cdata;

            return null;
        }

        public string GetMessageName(int viewId, byte msgId)
        {
            var msgs = GetControllerData(viewId)?.Messages;
            if (msgs != null && msgs.TryGetValue(msgId, out var msgName)) return msgName;
            
            return $"{msgId}";
        }
        
        public string GetCommandName(int viewId, byte cmdId)
        {
            var cmds = GetControllerData(viewId)?.Commands;
            if (cmds != null && cmds.TryGetValue(cmdId, out var cmdName)) return cmdName;
            
            return $"{cmdId}";
        }
    }

    public class PacketDbSession
    {
        public string      Name;
        public GameSession Session;
        public List<IAero> ParsedMessages = new();

        public PacketDbSession(GameSession session, string name)
        {
            Name    = name;
            Session = session;
        }
    }

    public class PacketFilter
    {
        public bool FromServer;
        public bool FromClient;

        // Channels
        public bool ChanJack;
        public bool ChanControl;
        public bool ChanMatrix;
        public bool ChanUgss;
        public bool ChanRgss;

        public List<MsgFilterData> MsgFilters = new();
        public List<UInt64>        EntityIds  = new();

        public string SessionName = "";

        public PacketFilter()
        {
            SetDefaults();
        }

        public void SetDefaults()
        {
            FromServer = true;
            FromClient = true;

            ChanJack    = true;
            ChanControl = true;
            ChanMatrix  = true;
            ChanUgss    = true;
            ChanRgss    = true;

            MsgFilters = new List<MsgFilterData>();
            EntityIds  = new List<ulong>();

            SessionName = "";
        }

        public void AddFilter(Channel chan, bool fromServer, int viewId, int msgId, bool apply = false)
        {
            var filter = MsgFilterData.Create();
            if (chan is Channel.ReliableGss or Channel.UnreliableGss) {
                filter.ViewId = viewId;
                if (fromServer)
                    filter.MsgId = msgId;
                else
                    filter.CmdId = msgId;
            }
            else {
                filter.ViewId = chan == Channel.Control ? -2 : -1;
                filter.MsgId  = msgId;
            }

            MsgFilters.Add(filter);
        }
    }

    public struct MsgFilterData
    {
        public int ViewId;
        public int MsgId;
        public int CmdId;

        public static MsgFilterData Create()
        {
            var data = new MsgFilterData
            {
                ViewId = int.MinValue,
                MsgId  = int.MinValue,
                CmdId  = int.MinValue
            };

            return data;
        }
    }

    public class ControllerData
    {
        public int                      Id;
        public string                   Name;
        public Dictionary<byte, string> Messages;
        public Dictionary<byte, string> Commands;

        public ControllerData(int id, string name, Dictionary<byte, string> messages = null, Dictionary<byte, string> commands = null)
        {
            Id       = id;
            Name     = name;
            Messages = messages;
            Commands = commands;
        }

        public ControllerData() { }
    }
}