using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ProjectBlackout.PlayerTest;

/// <summary>
/// Aprial's player setup tools. Only touches files in Assets/Aprial_testing.
///   Tools > Project Blackout > Setup Player Animator      (rebuilds Ch11_Animator, keeps a backup)
///   Tools > Project Blackout > Install Glove Viewmodel    (puts the gauntlet on the player camera)
/// </summary>
public static class BlackoutPlayerSetupTools
{
    private const string Root = "Assets/Aprial_testing/";
    private const string ControllerPath = Root + "Character/Ch11/Ch11_Animator.controller";
    private const string BackupPath = Root + "Character/Ch11/Ch11_Animator_Backup.controller";
    private const string AnimDir = Root + "Character/Animations/";
    private const string PlayerPrefabPath = Root + "Prefabs/Blackout_TestPlayer.prefab";
    private const string GloveModelPath = Root + "Prefabs/HAND FBX LOW Poly.fbx";
    private const string ViewmodelName = "MagnetGlove_Viewmodel";
    private const string PlaceholderName = "MagnetGlove_Placeholder (visual only)";

    // ------------------------------------------------------------------ Animator

    [MenuItem("Tools/Project Blackout/Setup Player Animator")]
    public static void SetupPlayerAnimator()
    {
        // Death should play once, everything else loops.
        SetLoop(AnimDir + "Ch11_nonPBR@Death (1).fbx", false);

        AnimationClip idle = LoadClip("Ch11_nonPBR@Idle (1).fbx");
        AnimationClip walk = LoadClip("Ch11_nonPBR@Walking (1).fbx");
        AnimationClip run = LoadClip("Ch11_nonPBR@Running (1).fbx");
        AnimationClip death = LoadClip("Ch11_nonPBR@Death (1).fbx");
        AnimationClip boostRun = LoadClip("Ch11_nonPBR@temp-Running (1).fbx");
        AnimationClip carryRun = LoadClip("Ch11_nonPBR@Standing Run Forward.fbx");
        AnimationClip carryBack = LoadClip("Ch11_nonPBR@Standing Walk Back.fbx");

        if (new[] { idle, walk, run, death, boostRun, carryRun, carryBack }.Any(c => c == null))
        {
            EditorUtility.DisplayDialog("Setup Player Animator",
                "Some animation files were not found in " + AnimDir + ". See the Console for which one.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(BackupPath) == null)
            AssetDatabase.CopyAsset(ControllerPath, BackupPath);

        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            Debug.LogError("[PlayerSetup] Controller not found: " + ControllerPath);
            return;
        }

        // Parameters
        foreach (AnimatorControllerParameter p in ctrl.parameters)
            ctrl.RemoveParameter(p);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Forward", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Carrying", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Boosted", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        // Clear the base layer
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        foreach (AnimatorStateTransition t in sm.anyStateTransitions.ToArray())
            sm.RemoveAnyStateTransition(t);
        foreach (ChildAnimatorState s in sm.states.ToArray())
        {
            if (s.state.motion is BlendTree bt && AssetDatabase.IsSubAsset(bt))
                Object.DestroyImmediate(bt, true);
            sm.RemoveState(s.state);
        }

        // Normal movement: back = Walking reversed, idle, walk, Running
        AnimatorState loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree locoTree, 0);
        SetupTree(locoTree, (walk, -1f, -1f), (idle, 0f, 1f), (walk, 0.5f, 1f), (run, 1f, 1f));

        // Carrying a crate: Standing Walk Back / Standing Run Forward (arm pointing forward)
        AnimatorState carry = ctrl.CreateBlendTreeInController("Carry Locomotion", out BlendTree carryTree, 0);
        SetupTree(carryTree, (carryBack, -1f, 1f), (idle, 0f, 1f), (walk, 0.5f, 1f), (carryRun, 1f, 1f));

        AnimatorState boost = sm.AddState("Hit Boost");
        boost.motion = boostRun;

        AnimatorState dead = sm.AddState("Death");
        dead.motion = death;

        sm.defaultState = loco;
        PlaceStates(sm);

        // Transitions
        AddTransition(loco, carry, 0.15f, ("Carrying", AnimatorConditionMode.If));
        AddTransition(carry, loco, 0.15f, ("Carrying", AnimatorConditionMode.IfNot));
        AddTransition(boost, loco, 0.25f, ("Boosted", AnimatorConditionMode.IfNot), ("Carrying", AnimatorConditionMode.IfNot));
        AddTransition(boost, carry, 0.25f, ("Boosted", AnimatorConditionMode.IfNot), ("Carrying", AnimatorConditionMode.If));

        AnimatorStateTransition toBoost = sm.AddAnyStateTransition(boost);
        toBoost.AddCondition(AnimatorConditionMode.If, 0f, "Boosted");
        toBoost.duration = 0.1f;
        toBoost.canTransitionToSelf = false;

        AnimatorStateTransition toDeath = sm.AddAnyStateTransition(dead);
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        toDeath.duration = 0.1f;
        toDeath.canTransitionToSelf = false;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("[PlayerSetup] Ch11_Animator rebuilt (backup: " + BackupPath + ").");
        EditorUtility.DisplayDialog("Setup Player Animator",
            "Done!\n\nNormal: Idle / Walking / Running\nCarrying: Standing Walk Back / Standing Run Forward\nAfter hit: temp-Running\n\nBackup saved as Ch11_Animator_Backup.", "OK");
    }

    private static void SetupTree(BlendTree tree, params (AnimationClip clip, float threshold, float timeScale)[] children)
    {
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Forward";
        tree.useAutomaticThresholds = false;
        foreach (var c in children)
            tree.AddChild(c.clip, c.threshold);

        ChildMotion[] motions = tree.children;
        for (int i = 0; i < motions.Length; i++)
            motions[i].timeScale = children[i].timeScale;
        tree.children = motions;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, float duration,
        params (string param, AnimatorConditionMode mode)[] conditions)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = duration;
        foreach (var c in conditions)
            t.AddCondition(c.mode, 0f, c.param);
    }

    private static void PlaceStates(AnimatorStateMachine sm)
    {
        ChildAnimatorState[] states = sm.states;
        for (int i = 0; i < states.Length; i++)
        {
            switch (states[i].state.name)
            {
                case "Locomotion": states[i].position = new Vector3(300, 100); break;
                case "Carry Locomotion": states[i].position = new Vector3(300, 220); break;
                case "Hit Boost": states[i].position = new Vector3(600, 160); break;
                case "Death": states[i].position = new Vector3(600, 0); break;
            }
        }
        sm.states = states;
    }

    private static AnimationClip LoadClip(string file)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(AnimDir + file)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null)
            Debug.LogError("[PlayerSetup] No animation clip found in " + AnimDir + file);
        return clip;
    }

    private static void SetLoop(string path, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        bool changed = false;
        foreach (ModelImporterClipAnimation c in clips)
        {
            if (c.loopTime != loop) { c.loopTime = loop; changed = true; }
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    // ------------------------------------------------------------------ Glove viewmodel

    [MenuItem("Tools/Project Blackout/Install Glove Viewmodel")]
    public static void InstallGloveViewmodel()
    {
        GameObject gloveModel = AssetDatabase.LoadAssetAtPath<GameObject>(GloveModelPath);
        if (gloveModel == null)
        {
            EditorUtility.DisplayDialog("Install Glove Viewmodel", "Glove model not found:\n" + GloveModelPath, "OK");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Camera cam = root.GetComponentInChildren<Camera>(true);
            BlackoutMagnetGlove glove = root.GetComponentInChildren<BlackoutMagnetGlove>(true);
            BlackoutFPSInput movement = root.GetComponentInChildren<BlackoutFPSInput>(true);
            if (cam == null || glove == null)
            {
                EditorUtility.DisplayDialog("Install Glove Viewmodel", "Player prefab has no Camera or BlackoutMagnetGlove.", "OK");
                return;
            }

            // Replace an older install.
            Transform old = cam.transform.Find(ViewmodelName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject vm = new GameObject(ViewmodelName);
            vm.transform.SetParent(cam.transform, false);
            vm.layer = cam.gameObject.layer;

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(gloveModel, vm.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            // Fit the model to about 22 cm and centre it on the viewmodel root.
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = new Bounds(vm.transform.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
                foreach (Renderer r in renderers)
                {
                    Bounds wb = r.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 corner = wb.center + Vector3.Scale(wb.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        b.Encapsulate(vm.transform.InverseTransformPoint(corner));
                    }
                }
                float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
                float scale = size > 0.0001f ? 0.22f / size : 1f;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = -b.center * scale;
            }

            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
            foreach (Renderer r in renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.gameObject.layer = cam.gameObject.layer;
            }

            BlackoutGloveViewmodel viewmodel = vm.AddComponent<BlackoutGloveViewmodel>();
            SerializedObject vmSo = new SerializedObject(viewmodel);
            vmSo.FindProperty("glove").objectReferenceValue = glove;
            vmSo.FindProperty("movement").objectReferenceValue = movement;
            vmSo.ApplyModifiedPropertiesWithoutUndo();
            vm.transform.localPosition = viewmodel.idlePosition;

            SerializedObject gloveSo = new SerializedObject(glove);
            gloveSo.FindProperty("gloveVisual").objectReferenceValue = vm;
            gloveSo.ApplyModifiedPropertiesWithoutUndo();

            // Hide the old cube placeholder (kept, not deleted).
            Transform placeholder = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == PlaceholderName);
            if (placeholder != null) placeholder.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[PlayerSetup] Glove viewmodel installed on " + PlayerPrefabPath);
            EditorUtility.DisplayDialog("Install Glove Viewmodel",
                "Done! The gauntlet is under Main Camera > " + ViewmodelName + ".\n\n" +
                "If it faces the wrong way, rotate the CHILD model (not the " + ViewmodelName + " object).\n" +
                "Tweak Idle/Aim Position on the BlackoutGloveViewmodel component.", "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
    // ------------------------------------------------------------------ Glove on the left hand

    [MenuItem("Tools/Project Blackout/Add Glove To Left Hand")]
    public static void AddGloveToLeftHand()
    {
        GameObject gloveModel = AssetDatabase.LoadAssetAtPath<GameObject>(GloveModelPath);
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Animator body = root.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.avatar != null && a.avatar.isHuman);
            if (body == null || gloveModel == null)
            {
                EditorUtility.DisplayDialog("Add Glove To Left Hand", "Could not find the Humanoid body Animator or the glove model.", "OK");
                return;
            }

            BlackoutGloveHandMount mount = body.GetComponent<BlackoutGloveHandMount>();
            if (mount == null) mount = body.gameObject.AddComponent<BlackoutGloveHandMount>();

            SerializedObject so = new SerializedObject(mount);
            so.FindProperty("gloveModel").objectReferenceValue = gloveModel;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            EditorUtility.DisplayDialog("Add Glove To Left Hand",
                "Done! BlackoutGloveHandMount added to " + body.name + ".\n\n" +
                "Press Play, look down at your left hand, and tweak Position/Rotation Offset on that component " +
                "(copy the values after you stop Play, because Play-mode changes are not saved).", "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
