using System.Collections.Generic;
using Unity.AI.Navigation;          // NavMeshSurface, NavMeshModifier (AI Navigation package)
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;               // NavMeshObstacle

/// <summary>
/// Project Blackout: editor tool that builds the greybox level (task D1) in the open scene.
///
/// How to use:
///   1. Put this file in  Assets/Scripts/Editor/  (the folder MUST be called "Editor").
///   2. Open the Main scene.
///   3. Menu bar: Tools → Project Blackout → Build Greybox Level.
///   4. Save the scene, select "Level", and click Bake on its NavMesh Surface component.
///
/// Layout (top-down, 1 unit = 1 metre, walls 3 m high, doorways 2.4 m wide):
///
///                               [EXIT AREA]  ← red exit door (locked until generator is on)
///                                    |
///   [START LAB] ======== CORRIDOR (3 m wide) ======== [STORAGE]
///    10 × 10 m      green pressure plate         doorway blocked by a crate
///                            |
///                     [GENERATOR ROOM]  ← orange door (opens via pressure plate)
///                         10 × 10 m, yellow generator inside
///
/// Doors are left OUT of the NavMesh bake (NavMeshModifier) and block the creature with a
/// carving NavMeshObstacle instead, so when a door opens later, the creature can walk through.
/// Everything this tool creates can be moved or edited by hand afterwards.
/// Owner: Linn.
/// </summary>
public static class GreyboxLevelBuilder
{
    private const float WallHeight = 3f;
    private const float WallThickness = 0.2f;
    private const float DoorWidth = 2.4f;
    private const string MaterialFolder = "Assets/Materials/Greybox";

    private static Material floorMat, wallMat, exitDoorMat, genDoorMat, generatorMat, plateMat;

    [MenuItem("Tools/Project Blackout/Build Greybox Level")]
    public static void Build()
    {
        // Don't silently build a second copy.
        GameObject existing = GameObject.Find("Level");
        if (existing != null)
        {
            bool rebuild = EditorUtility.DisplayDialog(
                "Rebuild greybox level?",
                "This scene already has a 'Level' object.\n\nDelete it and build a fresh one? " +
                "Changes you made inside 'Level' will be lost (Ctrl+Z can undo this).",
                "Rebuild", "Cancel");
            if (!rebuild) return;
            Undo.DestroyObjectImmediate(existing);
        }

        CreateMaterials();

        GameObject root = new GameObject("Level");
        Undo.RegisterCreatedObjectUndo(root, "Build Greybox Level");

        Transform floors  = Group("Floors", root.transform);
        Transform walls   = Group("Walls", root.transform);
        Transform doors   = Group("Doors", root.transform);
        Transform props   = Group("Props", root.transform);
        Transform markers = Group("Markers", root.transform);
        Transform patrol  = Group("PatrolPoints", root.transform);

        BuildFloors(floors);
        BuildWalls(walls);
        BuildDoors(doors);
        BuildProps(props);
        BuildMarkers(markers);
        BuildPatrolPoints(patrol);

        // The NavMesh Surface bakes everything under "Level".
        NavMeshSurface surface = root.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("Greybox level built. Next: save the scene, select 'Level' and click Bake on its NavMesh Surface.");
    }

    // ---------------------------------------------------------------- layout

    private static void BuildFloors(Transform p)
    {
        Box("Floor_StartLab",      p, new Vector3(-20f, -0.1f,  0f),   new Vector3(10f, 0.2f, 10f), floorMat);
        Box("Floor_Corridor",      p, new Vector3(  0f, -0.1f,  0f),   new Vector3(30f, 0.2f,  3f), floorMat);
        Box("Floor_Storage",       p, new Vector3( 20f, -0.1f,  0f),   new Vector3(10f, 0.2f, 10f), floorMat);
        Box("Floor_GeneratorRoom", p, new Vector3(  0f, -0.1f, -6.5f), new Vector3(10f, 0.2f, 10f), floorMat);
        Box("Floor_ExitArea",      p, new Vector3(  8f, -0.1f,  3.5f), new Vector3( 4f, 0.2f,  4f), floorMat);
    }

    private static void BuildWalls(Transform p)
    {
        // Start lab: x -25..-15, z -5..5. Doorway to the corridor in the east wall.
        WallAlongZ(p, "Lab_West",  -25f, -5f, 5f);
        WallAlongX(p, "Lab_North",   5f, -25f, -15f);
        WallAlongX(p, "Lab_South",  -5f, -25f, -15f);
        WallAlongZ(p, "Lab_East",  -15f, -5f, 5f, 0f);

        // Corridor: x -15..15, z -1.5..1.5. Exit doorway north (x = 8), generator doorway south (x = 0).
        WallAlongX(p, "Corridor_North",  1.5f, -15f, 15f, 8f);
        WallAlongX(p, "Corridor_South", -1.5f, -15f, 15f, 0f);

        // Storage: x 15..25, z -5..5. Doorway to the corridor in the west wall.
        WallAlongZ(p, "Storage_West",  15f, -5f, 5f, 0f);
        WallAlongZ(p, "Storage_East",  25f, -5f, 5f);
        WallAlongX(p, "Storage_North",  5f, 15f, 25f);
        WallAlongX(p, "Storage_South", -5f, 15f, 25f);

        // Generator room: x -5..5, z -11.5..-1.5 (its north wall is the corridor's south wall).
        WallAlongZ(p, "Gen_West",  -5f, -11.5f, -1.5f);
        WallAlongZ(p, "Gen_East",   5f, -11.5f, -1.5f);
        WallAlongX(p, "Gen_South", -11.5f, -5f, 5f);

        // Exit area: x 6..10, z 1.5..5.5.
        WallAlongZ(p, "Exit_West",   6f, 1.5f, 5.5f);
        WallAlongZ(p, "Exit_East",  10f, 1.5f, 5.5f);
        WallAlongX(p, "Exit_North", 5.5f, 6f, 10f);
    }

    private static void BuildDoors(Transform p)
    {
        Door("GeneratorDoor", p, new Vector3(0f, WallHeight / 2f, -1.5f), new Vector3(DoorWidth, WallHeight, 0.3f), genDoorMat);
        Door("ExitDoor",      p, new Vector3(8f, WallHeight / 2f,  1.5f), new Vector3(DoorWidth, WallHeight, 0.3f), exitDoorMat);
    }

    private static void BuildProps(Transform p)
    {
        Box("Generator",     p, new Vector3( 0f, 1f,    -9.5f), new Vector3(2f,   2f,   1.5f), generatorMat);
        Box("PressurePlate", p, new Vector3(-6f, 0.05f,  0f),   new Vector3(1.6f, 0.1f, 1.6f), plateMat);

        // Invisible trigger the player walks into to win (D6 adds the script).
        GameObject exitTrigger = new GameObject("ExitTrigger");
        exitTrigger.transform.SetParent(p, false);
        exitTrigger.transform.localPosition = new Vector3(8f, 1.5f, 4f);
        BoxCollider trigger = exitTrigger.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3.4f, 3f, 3.4f);
    }

    private static void BuildMarkers(Transform p)
    {
        // Empty position markers (coloured labels in the Scene view). Place prefabs on them in D2.
        Marker("PlayerSpawn",                  p, new Vector3(-20f, 0f, 0f), 3);
        Marker("CreatureSpawn",                p, new Vector3( 11f, 0f, 0f), 6);
        Marker("CrateSpot_BlockingStorageDoor", p, new Vector3(14.1f, 0f, 0f), 1);
        Marker("CrateSpot_HeavyForPlate",      p, new Vector3( 21f, 0f, -3f), 1);
        Marker("CrateSpot_1",                  p, new Vector3(-22f, 0f, -3f), 1);
        Marker("CrateSpot_2",                  p, new Vector3(-17.5f, 0f, 3.5f), 1);
        Marker("CrateSpot_3",                  p, new Vector3( 19f, 0f, 3f), 1);
        Marker("CrateSpot_4",                  p, new Vector3(  3f, 0f, -8f), 1);
    }

    private static void BuildPatrolPoints(Transform p)
    {
        // Only in areas the creature can always reach (not behind closed doors or the blocking crate).
        Vector3[] points =
        {
            new Vector3(-21f, 0f,  3f),
            new Vector3(-19f, 0f, -3f),
            new Vector3(-10f, 0f,  0f),
            new Vector3(  0f, 0f,  0f),
            new Vector3( 11f, 0f,  0f),
        };
        for (int i = 0; i < points.Length; i++)
            Marker($"PatrolPoint_{i + 1}", p, points[i], 4);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>A wall running along the X axis at a fixed z, from x0 to x1, with doorway gaps at the given x centres.</summary>
    private static void WallAlongX(Transform parent, string name, float z, float x0, float x1, params float[] gapCentres)
    {
        int i = 0;
        float start = x0;
        foreach (float gap in Sorted(gapCentres))
        {
            WallSegment(parent, $"{name}_{i++}", start, gap - DoorWidth / 2f, z, true);
            start = gap + DoorWidth / 2f;
        }
        WallSegment(parent, $"{name}_{i}", start, x1, z, true);
    }

    /// <summary>A wall running along the Z axis at a fixed x, from z0 to z1, with doorway gaps at the given z centres.</summary>
    private static void WallAlongZ(Transform parent, string name, float x, float z0, float z1, params float[] gapCentres)
    {
        int i = 0;
        float start = z0;
        foreach (float gap in Sorted(gapCentres))
        {
            WallSegment(parent, $"{name}_{i++}", start, gap - DoorWidth / 2f, x, false);
            start = gap + DoorWidth / 2f;
        }
        WallSegment(parent, $"{name}_{i}", start, z1, x, false);
    }

    private static void WallSegment(Transform parent, string name, float from, float to, float fixedCoord, bool alongX)
    {
        float length = to - from;
        if (length <= 0.01f) return;
        float mid = (from + to) / 2f;
        Vector3 centre = alongX ? new Vector3(mid, WallHeight / 2f, fixedCoord)
                                : new Vector3(fixedCoord, WallHeight / 2f, mid);
        Vector3 size = alongX ? new Vector3(length, WallHeight, WallThickness)
                              : new Vector3(WallThickness, WallHeight, length);
        Box(name, parent, centre, size, wallMat);
    }

    private static List<float> Sorted(float[] values)
    {
        List<float> list = new List<float>(values);
        list.Sort();
        return list;
    }

    private static GameObject Box(string name, Transform parent, Vector3 centre, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = centre;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static void Door(string name, Transform parent, Vector3 centre, Vector3 size, Material mat)
    {
        GameObject door = Box(name, parent, centre, size, mat);

        // Leave the door out of the bake so the NavMesh runs through the doorway...
        NavMeshModifier modifier = door.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;

        // ...and block the creature with a carving obstacle while the door is closed.
        NavMeshObstacle obstacle = door.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = Vector3.one;   // local space, so it matches the door's scale
        obstacle.carving = true;
    }

    private static void Marker(string name, Transform parent, Vector3 position, int labelColour)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        Texture2D icon = EditorGUIUtility.IconContent($"sv_label_{labelColour}").image as Texture2D;
        if (icon != null) EditorGUIUtility.SetIconForObject(go, icon);
    }

    private static Transform Group(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Greybox");

        floorMat     = Mat("GB_Floor",         new Color(0.35f, 0.36f, 0.38f));
        wallMat      = Mat("GB_Wall",          new Color(0.62f, 0.64f, 0.67f));
        exitDoorMat  = Mat("GB_ExitDoor",      new Color(0.80f, 0.15f, 0.15f));
        genDoorMat   = Mat("GB_GeneratorDoor", new Color(0.95f, 0.55f, 0.10f));
        generatorMat = Mat("GB_Generator",     new Color(0.95f, 0.85f, 0.15f));
        plateMat     = Mat("GB_PressurePlate", new Color(0.20f, 0.75f, 0.30f));
        AssetDatabase.SaveAssets();
    }

    private static Material Mat(string name, Color colour)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);   // URP
        else if (m.HasProperty("_Color")) m.SetColor("_Color", colour);      // Built-in
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
