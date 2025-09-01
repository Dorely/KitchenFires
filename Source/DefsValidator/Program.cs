using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace DefsValidator
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            // Assembly resolve to load RimWorld managed assemblies and mod assemblies for type resolution
            string managedPath = Environment.GetEnvironmentVariable("RIMWORLD_MANAGED");
            if (string.IsNullOrWhiteSpace(managedPath))
            {
                managedPath = @"C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed";
            }
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                try
                {
                    var an = new AssemblyName(e.Name).Name + ".dll";
                    string candidate = null;
                    if (File.Exists(Path.Combine(managedPath, an))) candidate = Path.Combine(managedPath, an);
                    if (candidate != null) return Assembly.LoadFrom(candidate);
                }
                catch { }
                return null;
            };
            // Resolve repo root relative to this project folder
            string projectDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (projectDir == null) { Console.Error.WriteLine("ERROR: Cannot resolve project directory"); return 2; }
            // DefsValidator/bin/{Config} -> DefsValidator -> Source -> KitchenFires (mod root)
            string modRoot = Path.GetFullPath(Path.Combine(projectDir, "..", "..", "..", ".."));

            // Scan repository defs (pre-copy) to catch errors before packaging
            var searchRoots = new List<string>();
            string commonDefs = Path.Combine(modRoot, "Common", "Defs");
            string v16Defs = Path.Combine(modRoot, "1.6", "Defs");
            if (Directory.Exists(commonDefs)) searchRoots.Add(commonDefs);
            if (Directory.Exists(v16Defs)) searchRoots.Add(v16Defs);

            var xmlFiles = new List<string>();
            foreach (var root in searchRoots)
            {
                xmlFiles.AddRange(Directory.GetFiles(root, "*.xml", SearchOption.AllDirectories));
            }

            Console.WriteLine($"Scanning {xmlFiles.Count} def file(s) under: {string.Join(", ", searchRoots)} ...");

            int errors = 0;
            // Load mod assembly for type checks
            string repoAsmCommon = Path.Combine(modRoot, "Common", "Assemblies", "KitchenFires.dll");
            string modAsmPath = repoAsmCommon;
            Assembly modAsm = null;
            if (File.Exists(modAsmPath))
            {
                try
                {
                    // Preload common game assemblies needed to resolve base types
                    var ac = Path.Combine(managedPath, "Assembly-CSharp.dll");
                    if (File.Exists(ac)) Assembly.LoadFrom(ac);
                    var ue = Path.Combine(managedPath, "UnityEngine.CoreModule.dll");
                    if (File.Exists(ue)) Assembly.LoadFrom(ue);
                    var ueb = Path.Combine(managedPath, "UnityEngine.dll");
                    if (File.Exists(ueb)) Assembly.LoadFrom(ueb);

                    modAsm = Assembly.LoadFrom(modAsmPath);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERROR: Failed to load mod assembly for reflection checks: {ex.Message}");
                    errors++;
                }
            }
            else
            {
                Console.Error.WriteLine("WARN: KitchenFires.dll not found; custom class checks will be skipped.");
            }

            // Rule 1: XML well-formedness and token garbage scan
            foreach (var f in xmlFiles)
            {
                string text = File.ReadAllText(f);
                if (Regex.IsMatch(text, @"\$\d+"))
                {
                    Console.Error.WriteLine($"ERROR: Possible stray substitution token in '{f}'.");
                    errors++;
                }
                try
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(text);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERROR: XML not well-formed in '{f}': {ex.Message}");
                    errors++;
                }
            }

            // Aggregate docs (preserve file path for better messages)
            var allDocs = new List<Tuple<XmlDocument,string>>();
            foreach (var f in xmlFiles)
            {
                try { var x = new XmlDocument(); x.Load(f); allDocs.Add(Tuple.Create(x, f)); }
                catch { }
            }

            // Rule 2: Duplicated defNames across files
            var defNameNodes = new List<Tuple<string, string>>(); // defName, parentName
            foreach (var pair in allDocs)
            {
                var doc = pair.Item1;
                var nodes = doc.SelectNodes("//defName");
                if (nodes == null) continue;
                foreach (XmlNode n in nodes)
                {
                    string parentName = n.ParentNode?.Name ?? "?";
                    defNameNodes.Add(Tuple.Create(n.InnerText.Trim(), parentName));
                }
            }
            var dupGroups = defNameNodes.GroupBy(t => t.Item1).Where(g => g.Count() > 1).ToList();
            foreach (var g in dupGroups)
            {
                Console.Error.WriteLine($"ERROR: Duplicate defName '{g.Key}' found in multiple defs: {string.Join(", ", g.Select(t => t.Item2).Distinct())}");
                errors++;
            }

            // Rule 3: ThoughtDefs with stages that affect mood must have stage descriptions
            foreach (var pair in allDocs)
            {
                var doc = pair.Item1;
                var path = pair.Item2;
                var tds = doc.SelectNodes("//ThoughtDef");
                if (tds == null) continue;
                foreach (XmlNode td in tds)
                {
                    var stages = td.SelectNodes(".//stages/li");
                    if (stages == null) continue;
                    foreach (XmlNode li in stages)
                    {
                        var moodNode = li.SelectSingleNode("baseMoodEffect");
                        if (moodNode == null) continue;
                        if (float.TryParse(moodNode.InnerText.Trim(), out var val) && Math.Abs(val) > 0.0001f)
                        {
                            var sdesc = li.SelectSingleNode("description");
                            if (sdesc == null || string.IsNullOrWhiteSpace(sdesc.InnerText))
                            {
                                string tname = td.SelectSingleNode("defName")?.InnerText ?? "(unknown)";
                                string slabel = li.SelectSingleNode("label")?.InnerText ?? "(stage)";
                                Console.Error.WriteLine($"ERROR: ThoughtDef '{tname}' stage '{slabel}' affects mood but has no stage description. File: {path}");
                                errors++;
                            }
                        }
                    }
                }
            }

            // Rule 4: Incident worker class exists (custom ones only) (custom ones only)
            if (modAsm != null)
            {
                foreach (var pair in allDocs)
            {
                var doc = pair.Item1;
                var path = pair.Item2;
                var nodes = doc.SelectNodes("//IncidentDef/workerClass");
                    if (nodes == null) continue;
                    foreach (XmlNode n in nodes)
                    {
                        string wc = n.InnerText.Trim();
                        if (!wc.StartsWith("KitchenFires.")) continue;
                        var t = modAsm.GetType(wc, false);
                        if (t == null)
                        {
                            string name = n.ParentNode?.SelectSingleNode("defName")?.InnerText ?? "(unknown)";
                            Console.Error.WriteLine($"ERROR: IncidentDef '{name}' workerClass '{wc}' not found in KitchenFires.dll. File: {path}");
                            errors++;
                        }
                    }
                }
            }

            // Rule 5: Hediff custom class exists
            if (modAsm != null)
            {
                foreach (var pair in allDocs)
            {
                var doc = pair.Item1;
                var path = pair.Item2;
                var nodes = doc.SelectNodes("//HediffDef/hediffClass");
                    if (nodes == null) continue;
                    foreach (XmlNode n in nodes)
                    {
                        string hc = n.InnerText.Trim();
                        if (!hc.StartsWith("KitchenFires.")) continue;
                        var t = modAsm.GetType(hc, false);
                        if (t == null)
                        {
                            string name = n.ParentNode?.SelectSingleNode("defName")?.InnerText ?? "(unknown)";
                            Console.Error.WriteLine($"ERROR: HediffDef '{name}' hediffClass '{hc}' not found in KitchenFires.dll. File: {path}");
                            errors++;
                        }
                    }
                }
            }

            // Rule 6: Basic presence checks on IncidentDefs (letter fields)
            foreach (var pair in allDocs)
            {
                var doc = pair.Item1;
                var path = pair.Item2;
                var nodes = doc.SelectNodes("//IncidentDef");
                if (nodes == null) continue;
                foreach (XmlNode inc in nodes)
                {
                    string name = inc.SelectSingleNode("defName")?.InnerText ?? "(unknown)";
                    if (inc.SelectSingleNode("letterLabel") == null || string.IsNullOrWhiteSpace(inc.SelectSingleNode("letterLabel")?.InnerText))
                    {
                        Console.Error.WriteLine($"ERROR: IncidentDef '{name}' missing letterLabel. File: {path}");
                        errors++;
                    }
                    if (inc.SelectSingleNode("letterText") == null || string.IsNullOrWhiteSpace(inc.SelectSingleNode("letterText")?.InnerText))
                    {
                        Console.Error.WriteLine($"ERROR: IncidentDef '{name}' missing letterText. File: {path}");
                        errors++;
                    }
                }
            }

            Console.WriteLine(errors == 0 ? "All def checks passed." : $"Def checks found {errors} error(s).");
            return errors == 0 ? 0 : 1;
        }
    }
}
