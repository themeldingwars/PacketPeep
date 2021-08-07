using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Sift
{
    public class Result
    {
        public Dictionary<string, Environment> Environments;
        public List<Branch> Branches;
        public List<Patch> Patches;
        public Dictionary<ushort, MatrixProtocol> MatrixProtocols;
        public Dictionary<ushort, GssProtocol> GssProtocols;
        
        public Result()
        {
            Environments = new();
            Branches = new();
            Patches = new();
            MatrixProtocols = new();
            GssProtocols = new();
        }

        public static Result Load(string dir)
        {
            string patchDir = Path.Join(dir, "Patches");
            string matrixDir = Path.Join(dir, "Matrix");
            string gssDir = Path.Join(dir, "GSS");
            
            Result result = new Result();
            
            foreach (string patchFile in Directory.GetFiles(patchDir, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    Patch patch = JsonConvert.DeserializeObject<Patch>(File.ReadAllText(patchFile));
                    
                    if(!result.Environments.ContainsKey(patch.Environment))
                        result.Environments.Add(patch.Environment, new Environment());


                    Environment env = result.Environments[patch.Environment];
                    Branch branch;
                    
                    if (!env.ContainsKey(patch.Branch))
                    {
                        branch = new Branch();
                        result.Branches.Add(branch);
                        env.Add(patch.Branch, branch);
                    }
                    else
                    {
                        branch = env[patch.Branch];
                    }
                    
                    result.Patches.Add(patch);
                    branch.Add(patch.Version, patch);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    break;
                }
            }
            
            foreach (string matrixFile in Directory.GetFiles(matrixDir, "*.json", SearchOption.AllDirectories))
            {
                if (ushort.TryParse(Path.GetFileNameWithoutExtension(matrixFile), out ushort version))
                {
                    MatrixProtocol proto = JsonConvert.DeserializeObject<MatrixProtocol>(File.ReadAllText(matrixFile));
                    result.MatrixProtocols.Add(version, proto);
                }
            }
            
            foreach (string gssFile in Directory.GetFiles(gssDir, "*.json", SearchOption.AllDirectories))
            {
                if (ushort.TryParse(Path.GetFileNameWithoutExtension(gssFile), out ushort version))
                {
                    GssProtocol proto = JsonConvert.DeserializeObject<GssProtocol>(File.ReadAllText(gssFile));
                    result.GssProtocols.Add(version, proto);
                }
            }
            
            return result;
        }

        public class Environment : Dictionary<string, Branch> { }
        public class Branch : Dictionary<string, Patch> { }
        
        public class Patch
        {
            [JsonProperty(Order = 0)] public string Environment { get; set; }
            [JsonProperty(Order = 1)] public string Branch { get; set; }
            [JsonProperty(Order = 2)] public string Version { get; set; }
            [JsonProperty(Order = 3)] public DateTime ExeBuildTime { get; set; }
            [JsonProperty(Order = 4)] public string ExeSHA256 { get; set; }
            [JsonProperty(Order = 5)] public long ExeSize { get; set; }
            [JsonProperty(Order = 6)] public ushort MatrixProtocolVersion { get; set; }
            [JsonProperty(Order = 7)] public ushort GSSProtocolVersion { get; set; }
        }
        
        public class Namespace
        {
            [JsonProperty(Order = 1)] public Dictionary<string, byte> Views { get; set; }
            [JsonProperty(Order = 2)] public Dictionary<string, byte> Messages { get; set; }
            [JsonProperty(Order = 3)] public Dictionary<string, byte> Commands { get; set; }
            [JsonProperty(Order = 4)] public Dictionary<string, Namespace> Children { get; set; }
        }
        public class MatrixProtocol : Namespace
        {
        }
        public class GssProtocol : Namespace
        {
            [JsonProperty(Order = 0)] public bool HasRoutedMultipleMessage { get; set; }
        }
    }
}