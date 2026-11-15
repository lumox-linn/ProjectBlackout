using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Finds assets the game never uses and can move them to Assets/_Unused.
// "Used" = everything the build scenes and the render pipeline settings depend on.
// Moving keeps each file's GUID, so anything needed later can simply be moved back.
public static class UnusedAssetCleaner
{
    private const string UnusedFolder = "Assets/_Unused";

    // Never moved: teammates' folders (until they say OK) and Unity/package folders.
    private static readonly string[] Protected =
{
        "Assets/Aprial_testing",
        "Assets/UI Assets/Audio",
        "Assets/UI Assets/Fonts",
        "Assets/UI Assets/Images",
        "Assets/UI Assets/WenrexaAssetsUI_SciFI",
        "Assets/Settings",
        "Assets/TextMesh Pro",
        UnusedFolder,
    };

    [MenuItem("Tools/Blackout/1. Report unused assets")]
    private static void Report() => Run(false);

    [MenuItem("Tools/Blackout/2. Move unused assets to _Unused")]
    private static void Move()
    {
        if (EditorUtility.DisplayDialog("Move unused assets",
                "Move every unused asset (outside the protected folders) into Assets/_Unused?\n\nSave your scene first.",
                "Move", "Cancel"))
            Run(true);
    }

    private static void Run(bool move)
    {
        // 1) Starting points: scenes in the build + render pipeline assets
        var roots = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled) roots.Add(s.path);
        AddPipeline(roots, GraphicsSettings.defaultRenderPipeline);
        for (int i = 0; i < QualitySettings.names.Length; i++)
            AddPipeline(roots, QualitySettings.GetRenderPipelineAssetAt(i));

        // 2) Everything they depend on, directly or indirectly
        var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));

        // 3) Every other file in Assets is unused, unless it's in the always-keep list
        var unused = new List<string>();
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/") || AssetDatabase.IsValidFolder(path)) continue;
            if (used.Contains(path) || AlwaysKeep(path)) continue;
            unused.Add(path);
        }
        unused.Sort();

        // 4) Report: size per folder + full list, saved next to the Assets folder
        var perFolder = new Dictionary<string, long>();
        long total = 0;
        foreach (string p in unused)
        {
            long size = new FileInfo(p).Length;
            total += size;
            string top = TopFolder(p);
            perFolder[top] = perFolder.TryGetValue(top, out long t) ? t + size : size;
        }
        var sb = new StringBuilder();
        sb.AppendLine($"Unused: {unused.Count} files, {Mb(total)} (protected folders not counted)");
        foreach (var kv in perFolder.OrderByDescending(k => k.Value))
            sb.AppendLine($"  {Mb(kv.Value),10}  {kv.Key}");
        sb.AppendLine();
        foreach (string p in unused) sb.AppendLine(p);
        File.WriteAllText("UnusedAssetsReport.txt", sb.ToString());
        Debug.Log(sb.ToString(0, Mathf.Min(sb.Length, 3000)) + "\n... full list in UnusedAssetsReport.txt (project folder)");

        if (!move) return;

        // 5) Move, keeping the folder structure: Assets/X/Y.png -> Assets/_Unused/X/Y.png
        foreach (string p in unused)
            Directory.CreateDirectory(Path.GetDirectoryName(Target(p)));
        AssetDatabase.Refresh();

        var errors = new List<string>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string p in unused)
            {
                string err = AssetDatabase.MoveAsset(p, Target(p));
                if (!string.IsNullOrEmpty(err)) errors.Add(p + ": " + err);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }

        AssetDatabase.Refresh();
        DeleteEmptyFolders();
        AssetDatabase.Refresh();
        Debug.Log($"Moved {unused.Count - errors.Count} files to {UnusedFolder}." +
                  (errors.Count > 0 ? "\nErrors:\n" + string.Join("\n", errors) : ""));
    }

    private static bool AlwaysKeep(string path)
    {
        foreach (string f in Protected)
            if (path.StartsWith(f + "/")) return true;

        string p = path.ToLowerInvariant();
        return p.EndsWith(".cs") || p.EndsWith(".asmdef") || p.EndsWith(".asmref")
            || p.EndsWith(".unity")                       // scenes: ask the owner first
            || p.EndsWith(".inputactions")
            || p.Contains("/editor/") || p.Contains("/resources/")   // loaded by name, not by reference
            || p.Contains("/streamingassets/") || p.Contains("/plugins/") || p.Contains("/gizmos/")
            || p.Contains("license") || p.Contains("licence") || p.Contains("readme") || p.EndsWith(".pdf");
    }

    private static void AddPipeline(List<string> roots, Object asset)
    {
        if (asset != null) roots.Add(AssetDatabase.GetAssetPath(asset));
    }

    private static string Target(string path) => UnusedFolder + path.Substring("Assets".Length);

    private static string TopFolder(string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length <= 2) return "(Assets root)";
        return parts.Length > 3 ? parts[1] + "/" + parts[2] : parts[1];
    }

    private static string Mb(long bytes) => (bytes / 1048576f).ToString("0.0") + " MB";

    private static void DeleteEmptyFolders()
    {
        var folders = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && AssetDatabase.IsValidFolder(p) && !p.StartsWith(UnusedFolder))
            .OrderByDescending(p => p.Length);     // deepest first
        foreach (string f in folders)
            if (Directory.Exists(f) && Directory.GetDirectories(f).Length == 0 &&
                Directory.GetFiles(f).All(x => x.EndsWith(".meta")))
                AssetDatabase.DeleteAsset(f);
    }
}