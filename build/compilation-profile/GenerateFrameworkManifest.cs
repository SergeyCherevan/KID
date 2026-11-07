using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Build.Framework;

namespace KID.Build
{
    // Build-time provenance check. Runtime reader independently validates the resulting schema.
    public sealed class GenerateFrameworkManifest : Microsoft.Build.Utilities.Task
    {
        [Required] public ITaskItem[] Assets { get; set; }
        [Required] public ITaskItem[] Packs { get; set; }
        [Required] public ITaskItem[] PublishFiles { get; set; }
        [Required] public string TargetFramework { get; set; }
        [Required] public string RuntimeIdentifier { get; set; }
        [Required] public string OutputPath { get; set; }

        public override bool Execute()
        {
            var temporary = OutputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // An old successful manifest must never survive a failing generation in obj.
                if (File.Exists(OutputPath)) File.Delete(OutputPath);
                if (TargetFramework != "net10.0-windows" || RuntimeIdentifier != "win-x64")
                    throw new InvalidOperationException("Unsupported framework/RID.");
                var owners = new[] { "Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App" };
                var frameworks = new List<string>();
                var assemblies = new List<KeyValuePair<string, string>>();
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var owner in owners)
                {
                    var expectedId = owner + ".Runtime." + RuntimeIdentifier;
                    var packs = Packs.Where(p => p.GetMetadata("FrameworkName") == owner).ToArray();
                    if (packs.Length != 1) throw new InvalidOperationException("Expected exactly one resolved pack for " + owner);
                    var pack = packs[0];
                    var version = pack.GetMetadata("NuGetPackageVersion");
                    Version parsed;
                    if (pack.GetMetadata("NuGetPackageId") != expectedId ||
                        pack.GetMetadata("RuntimeIdentifier") != RuntimeIdentifier ||
                        !Version.TryParse(version, out parsed) || parsed.Major != 10 || parsed.Minor != 0 || parsed.Build < 0)
                        throw new InvalidOperationException("Invalid runtime pack provenance: " + owner);
                    var packageRoot = Path.GetFullPath(pack.GetMetadata("PackageDirectory"));
                    frameworks.Add("{\"name\":" + Quote(owner) + ",\"runtimePackId\":" + Quote(expectedId) +
                        ",\"runtimePackVersion\":" + Quote(version) + "}");
                    var count = 0;
                    foreach (var asset in Assets.Where(a => a.GetMetadata("NuGetPackageId") == expectedId && a.GetMetadata("AssetType") == "runtime"))
                    {
                        var source = Path.GetFullPath(asset.ItemSpec);
                        var destination = asset.GetMetadata("DestinationSubPath");
                        if (asset.GetMetadata("NuGetPackageVersion") != version || asset.GetMetadata("RuntimeIdentifier") != RuntimeIdentifier ||
                            !source.StartsWith(packageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                            destination != Path.GetFileName(destination) || destination.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 ||
                            !destination.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || destination.Contains(".."))
                            throw new InvalidOperationException("Invalid runtime pack asset: " + source);
                        var final = PublishFiles.Where(f => string.Equals(f.GetMetadata("RelativePath"), destination, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (final.Length != 1)
                            throw new InvalidOperationException("Missing, relocated, or substituted publish asset: " + destination);
                        if (!string.Equals(Path.GetFullPath(final[0].ItemSpec), source, StringComparison.OrdinalIgnoreCase))
                        {
                            // --no-build retains losing NETCore facades in RuntimePackAsset.
                            // Accept SDK conflict resolution only when the selected source has
                            // independent provenance in one of the two allowed runtime packs.
                            var selected = Assets.Where(a => a.GetMetadata("AssetType") == "runtime" &&
                                a.GetMetadata("DestinationSubPath") == destination &&
                                string.Equals(Path.GetFullPath(a.ItemSpec), Path.GetFullPath(final[0].ItemSpec), StringComparison.OrdinalIgnoreCase) &&
                                owners.Any(o => a.GetMetadata("NuGetPackageId") == o + ".Runtime." + RuntimeIdentifier)).ToArray();
                            if (selected.Length != 1)
                                throw new InvalidOperationException("Publish source has no unique framework-pack provenance: " + destination);
                            continue;
                        }
                        if (!paths.Add(destination)) throw new InvalidOperationException("Duplicate managed destination: " + destination);
                        var identity = AssemblyName.GetAssemblyName(source);
                        if (string.IsNullOrEmpty(identity.Name) || !names.Add(identity.Name) ||
                            !string.Equals(destination, identity.Name + ".dll", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Conflicting/invalid managed identity: " + destination);
                        var token = BitConverter.ToString(identity.GetPublicKeyToken() ?? new byte[0]).Replace("-", "").ToLowerInvariant();
                        var json = "{\"framework\":" + Quote(owner) + ",\"path\":" + Quote(destination) +
                            ",\"name\":" + Quote(identity.Name) + ",\"version\":" + Quote(identity.Version.ToString()) +
                            ",\"culture\":" + Quote(identity.CultureName ?? "") + ",\"publicKeyToken\":" + Quote(token) + "}";
                        assemblies.Add(new KeyValuePair<string, string>(destination, json));
                        count++;
                    }
                    if (count == 0) throw new InvalidOperationException("Empty framework pack: " + owner);
                }
                foreach (var anchor in new[] { "System.Private.CoreLib", "PresentationFramework", "System.Runtime", "System.Console", "System.Windows.Forms" })
                    if (!names.Contains(anchor)) throw new InvalidOperationException("Missing full-framework anchor: " + anchor);
                var result = "{\"schemaVersion\":1,\"targetFramework\":" + Quote(TargetFramework) +
                    ",\"runtimeIdentifier\":" + Quote(RuntimeIdentifier) + ",\"frameworks\":[" + string.Join(",", frameworks) +
                    "],\"assemblies\":[" + string.Join(",", assemblies.OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase).Select(a => a.Value)) + "]}\n";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputPath)));
                File.WriteAllText(temporary, result, new UTF8Encoding(false));
                File.Move(temporary, OutputPath);
                Log.LogMessage(MessageImportance.High, "KID framework manifest: {0} managed runtime-pack assets -> {1}", assemblies.Count, OutputPath);
                return true;
            }
            catch (Exception exception)
            {
                Log.LogError("KID framework manifest generation failed: {0}", exception.Message);
                return false;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static string Quote(string value)
        {
            var builder = new StringBuilder("\"");
            foreach (var character in value)
                if (character == '"' || character == '\\') builder.Append('\\').Append(character);
                else if (character < 32) builder.Append("\\u").Append(((int)character).ToString("x4"));
                else builder.Append(character);
            return builder.Append('"').ToString();
        }
    }
}
