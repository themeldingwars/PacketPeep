using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using FauCap;
using ImGuiNET;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using PacketPeep.Systems;
using ImTool;
using Newtonsoft.Json;

namespace PacketPeep.Widgets;

public class MagicQuery
{
    protected PacketExplorer PacketExplorer;
    protected PacketDb       PacketDb;
    protected string         CurrentQuery  = "Docs.Where(x => x.MessageId == 17).Select(x => new { idx = x.MsgIdx, x.Payload.Unk1})";
    protected QueryResult    CurrentResult = null;

    public MagicQuery(PacketExplorer packetExplorer)
    {
        PacketExplorer = packetExplorer;
    }

    public void Draw()
    {
        if (!ImGui.Begin("Magic Query", ImGuiWindowFlags.MenuBar)) return;
        DrawMenuBar();
        DrawQueryArea();

        ImGui.BeginChild("QueryArea");
        DrawQueryResults(CurrentResult);
        ImGui.EndChild();

        ImGui.End();
    }

    public void DrawMenuBar()
    {
        if (ImGui.BeginMenuBar()) {
            if (ImTool.Widgets.IconButton("", "Run Query")) CurrentResult = RunQuery(CurrentQuery);

            ImGui.EndMenuBar();
        }
    }

    private void DrawQueryArea()
    {
        ImGui.InputTextMultiline("###Query", ref CurrentQuery, 100000, new Vector2(-1f, 400));
    }

    private unsafe void DrawQueryResults(QueryResult result)
    {
        if (result == null) return;

        FontManager.PushFont("Regular_Small");
        if (ImGui.BeginTable("Query Result", result.Headers.Length, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable)) {
            foreach (var header in result.Headers) {
                ImGui.TableSetupColumn(header.Item1, ImGuiTableColumnFlags.None, 1.0f);
            }

            ImGui.TableHeadersRow();

            ImGuiListClipper    clipperData;
            ImGuiListClipperPtr clipper = new ImGuiListClipperPtr(&clipperData);
            clipper.Begin(result.Rows.Count);

            while (clipper.Step()) {
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                    var row = result.Rows[i];

                    ImGui.TableNextRow(ImGuiTableRowFlags.None);
                    for (int colIdx = 0; colIdx < result.Headers.Length; colIdx++) {
                        ImGui.TableNextColumn();
                        ImGui.Text($"{row[colIdx]}");
                    }
                }
            }

            clipper.End();

            ImGui.EndTable();
        }

        FontManager.PopFont();
    }

    private QueryResult RunQuery(string query)
    {
        var                     sw             = Stopwatch.StartNew();
        int                     numChunks      = Environment.ProcessorCount;
        var                     filteredChunks = Split(PacketPeepTool.PcktDb.FilteredIndices, numChunks);
        List<Task<QueryResult>> chunkTasks     = new();

        foreach (var chunk in filteredChunks) {
            var task = Task.Factory.StartNew(() => RunQueryOnChunk(query, chunk));
            chunkTasks.Add(task);
        }

        Task.WhenAll(chunkTasks).Wait();

        // Combine all the results
        var combineSw  = Stopwatch.StartNew();
        var results    = chunkTasks.Where(x => x.Result.Rows.Count > 0).Select(x => x.Result).ToArray();
        int numResults = results.Sum(x => x.Rows.Count);
        var combinedResults = new QueryResult
        {
            Headers = results[0].Headers,
            Rows    = new(numResults)
        };

        for (int i = 0; i < results.Length; i++) {
            var resultChunk = results[i];
            combinedResults.Rows.AddRange(resultChunk.Rows);
        }

        combineSw.Stop();

        sw.Stop();
        PacketPeepTool.Log.AddLogInfo(LogCategories.QueryEngine, $"Running query on {PacketPeepTool.PcktDb.FilteredIndices.Count:n} messages took {sw.Elapsed}, combine took: {combineSw.Elapsed}");

        return combinedResults;
    }

    public IEnumerable<IEnumerable<T>> Split<T>(IEnumerable<T> items, int numOfParts)
    {
        int i = 0;
        return items.GroupBy(x => i++ % numOfParts);
    }

    public QueryResult RunQueryOnChunk(string query, IEnumerable<int> filteredIndices)
    {
        var currentSession = PacketPeepTool.PcktDb.Sessions[PacketExplorer.ActiveFilter.SessionName];
        var msgs           = filteredIndices.Select(x => currentSession.Session.Messages[x]);
        var docs           = msgs.Select(MsgToDoc);
        var scriptResult = RunQueryScriptOnDocs(query, new ScriptGlobals
        {
            Docs = docs
        });

        var queryResult = BuildResult(scriptResult);

        return queryResult;
    }

    public ParsedMessageDoc MsgToDoc(Message msg)
    {
        var header = Utils.GetGssMessageHeader(msg);

        var doc = new ParsedMessageDoc
        {
            MsgIdx       = msg.Id,
            FromServer   = msg.FromServer,
            RecivedTime  = msg.Time,
            Channel      = header.Channel,
            ControllerId = header.ControllerId,
            MessageId    = header.MessageId,
            EntityId     = header.EntityId,
            Size         = msg.Data.Length
        };

        var msgObj = PacketParser.ParseMessage(msg);
        if (msgObj != null) {
            msgObj.GetType().GetField("ReadLogs").SetValue(msgObj, null);
            doc.HasAeroParsed = true;
            string  str = JsonConvert.SerializeObject(msgObj);
            dynamic obj = JsonConvert.DeserializeObject(str);
            doc.Payload = obj;
        }
        else {
            doc.HasAeroParsed = false;
        }

        return doc;
    }

    public object RunQueryScriptOnDocs(string query, ScriptGlobals scriptParams)
    {
        var scriptOptions = ScriptOptions.Default
                                         .WithReferences(
                                              typeof(System.Linq.Enumerable).Assembly,
                                              typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly
                                          )
                                         .WithImports("System.Linq", "Microsoft.CSharp");

        var result = CSharpScript.EvaluateAsync(query, scriptOptions, scriptParams).Result;
        return result;
    }

    private QueryResult BuildResult(object result)
    {
        var queryResult = new QueryResult();

        var resultType  = result.GetType();
        var isArrayType = resultType.GetInterfaces().Contains(typeof(IEnumerable));
        var enumerable  = isArrayType ? (IEnumerable<dynamic>) result : new List<dynamic> {result};

        // Headers
        var props = resultType.GetGenericArguments().Last().GetProperties();
        queryResult.Headers = new (string, Type)[props.Length];
        for (int i = 0; i < props.Length; i++) {
            queryResult.Headers[i] = (props[i].Name, null);
        }

        var numResults = enumerable.Count();
        queryResult.Rows = new List<object[]>(numResults);

        try {
            int idx = 0;
            foreach (var item in enumerable) {
                var cols = new object[props.Length];

                try {
                    for (int i = 0; i < props.Length; i++) {
                        var prop  = props[i];
                        var value = prop.GetValue(item);

                        cols[i] = value;
                    }
                }
                catch (Exception e) {
                    Debug.WriteLine(e);
                }

                queryResult.Rows.Add(cols);
            }
        }
        catch (Exception e) {
            Debug.WriteLine(e);
        }

        // Docs.Where(x => x.Payload.GetType().Name == "Character_CombatView").Select(x => new { idx = x.MsgIdx, test = ((AeroMessages.GSS.Character.Character_CombatView)x.Payload).Ammo_0Prop})
        // Docs.Select(x => new { idx = x.MsgIdx, test = x.Payload.GetType().Name})
        // Docs.Select(x => new { MsgIdx = x.MsgIdx,  Name =  x.Payload?.StaticInfoProp?.DisplayName, x.Payload.OwnerIdProp}).Where(x => x.Name != null).Select(x =>x )

        return queryResult;
    }

    public class ScriptGlobals
    {
        public IEnumerable<ParsedMessageDoc> Docs;
    }

    public class QueryResult
    {
        public (string, Type)[] Headers;
        public List<object[]>   Rows;
    }
}