using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// ---------------------------------------------------------------------------
// CreatureMixamoSetup  (B9, Editor-only tool)
//
// Menu: Tools > Creature > Set Up Mixamo Creature
//
// Expects these files in Assets/Art/Creature/ (downloaded from Mixamo):
//   Creature_Model.fbx   the character, T-pose, WITH skin
//   Idle.fbx  Walk.fbx  Run.fbx  Attack.fbx  Stunned.fbx       (required: Idle)
//   LookLeft.fbx and/or LookRight.fbx                          (optional)
//     - only one of them? The other side is made automatically as a
//       MIRRORED copy of it (Unity can mirror Humanoid animations).
//   (animations downloaded "Without Skin", walk/run "In Place")
//
// It does the import setup that is easy to get wrong by hand:
//   1. Character: Rig = Humanoid, create its Avatar, extract its textures.
//   2. Animations: Rig = Humanoid, copy the character's Avatar, set Loop Time,
//      bake root motion into the pose (the NavMeshAgent moves the creature).
//   3. Builds CreatureAnimator.controller:
//        Locomotion (1D blend tree on Speed: 0 Idle, 2 Walk, 4.5 Run) = default
//        Any State -> Stunned   (Stunned = true),  Stunned -> Locomotion (false)
//        Any State -> Attack    (Attack trigger),  Attack -> Locomotion (exit time)
//        LookDirection (int):  1 -> LookRight,  -1 -> LookLeft,  0 -> Locomotion
// Run it again any time to rebuild everything (e.g. after adding a clip).
// The controller is rebuilt IN PLACE, so the prefab keeps its reference to it.
//
// APIs: Unity ModelImporter and AnimatorController (UnityEditor.Animations):
//   https://docs.unity3d.com/ScriptReference/ModelImporter.html
//   https://docs.unity3d.com/ScriptReference/Animations.AnimatorController.html
// ---------------------------------------------------------------------------
public static class CreatureMixamoSetup
{
    private const string Folder = "Assets/Art/Creature";
    private const string ModelPath = Folder + "/Creature_Model.fbx";
    private const string ControllerPath = Folder + "/CreatureAnimator.controller";

    // Blend tree thresholds = the creature's speeds (patrol 2, chase 4.5).
    private const float WalkSpeed = 2f;
    private const float RunSpeed = 4.5f;

    [MenuItem("Tools/Creature/Set Up Mixamo Creature")]
    public static void SetUp()
    {
        // ---- 1. The character ----
        ModelImporter modelImporter = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (modelImporter == null)
        {
            EditorUtility.DisplayDialog("Creature setup",
                "Can't find " + ModelPath + ".\nCheck the folder and the file name.", "OK");
            return;
        }

        modelImporter.animationType = ModelImporterAnimationType.Human;
        modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        modelImporter.importAnimation = false; // the T-pose has no animation we need
        modelImporter.SaveAndReimport();

        string textureFolder = Folder + "/Textures";
        if (!AssetDatabase.IsValidFolder(textureFolder))
        {
            AssetDatabase.CreateFolder(Folder, "Textures");
        }
        modelImporter.ExtractTextures(textureFolder);
        AssetDatabase.Refresh();
        modelImporter.SaveAndReimport();

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            EditorUtility.DisplayDialog("Creature setup",
                "The character's Humanoid Avatar couldn't be created.\n" +
                "Was Creature_Model.fbx downloaded from Mixamo in T-pose, WITH skin?", "OK");
            return;
        }

        // ---- 2. The animations ----
        AnimationClip idle = SetUpClip("Idle", true, avatar);
        AnimationClip walk = SetUpClip("Walk", true, avatar);
        AnimationClip run = SetUpClip("Run", true, avatar);
        AnimationClip attack = SetUpClip("Attack", false, avatar);
        AnimationClip stunned = SetUpClip("Stunned", true, avatar);
        SetUpLookClips(avatar, out AnimationClip lookLeft, out AnimationClip lookRight, out string lookNote);

        if (idle == null)
        {
            EditorUtility.DisplayDialog("Creature setup", "Idle.fbx is required but wasn't found.", "OK");
            return;
        }

        // ---- 3. The Animator Controller (rebuilt in place, same file) ----
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }
        ClearController(controller);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Stunned", AnimatorControllerParameterType.Bool);
        controller.AddParameter("LookDirection", AnimatorControllerParameterType.Int);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        // Locomotion blend tree: Idle -> Walk -> Run by Speed.
        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        if (walk != null) tree.AddChild(walk, WalkSpeed);
        if (run != null) tree.AddChild(run, RunSpeed);
        stateMachine.defaultState = locomotion;

        // Stunned first, so it wins over Attack if both happen at once.
        if (stunned != null)
        {
            AnimatorState stunnedState = stateMachine.AddState("Stunned");
            stunnedState.motion = stunned;

            AnimatorStateTransition toStunned = stateMachine.AddAnyStateTransition(stunnedState);
            toStunned.AddCondition(AnimatorConditionMode.If, 0f, "Stunned");
            toStunned.canTransitionToSelf = false;
            toStunned.hasExitTime = false;
            toStunned.duration = 0.15f;

            AnimatorStateTransition fromStunned = stunnedState.AddTransition(locomotion);
            fromStunned.AddCondition(AnimatorConditionMode.IfNot, 0f, "Stunned");
            fromStunned.hasExitTime = false;
            fromStunned.duration = 0.25f;
        }

        if (attack != null)
        {
            AnimatorState attackState = stateMachine.AddState("Attack");
            attackState.motion = attack;

            AnimatorStateTransition toAttack = stateMachine.AddAnyStateTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.canTransitionToSelf = true; // each swing restarts the animation
            toAttack.hasExitTime = false;
            toAttack.duration = 0.1f;

            AnimatorStateTransition fromAttack = attackState.AddTransition(locomotion);
            fromAttack.hasExitTime = true;
            fromAttack.exitTime = 0.9f;
            fromAttack.duration = 0.15f;
        }

        if (lookLeft != null && lookRight != null)
        {
            AnimatorState leftState = stateMachine.AddState("LookLeft");
            leftState.motion = lookLeft;
            AnimatorState rightState = stateMachine.AddState("LookRight");
            rightState.motion = lookRight;

            AddIntTransition(locomotion, rightState, 1);
            AddIntTransition(locomotion, leftState, -1);
            AddIntTransition(rightState, leftState, -1);
            AddIntTransition(leftState, rightState, 1);
            AddIntTransition(rightState, locomotion, 0);
            AddIntTransition(leftState, locomotion, 0);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Selection.activeObject = controller;

        string missing = string.Join(", ", new[]
        {
            walk == null ? "Walk" : null, run == null ? "Run" : null, attack == null ? "Attack" : null,
            stunned == null ? "Stunned" : null
        }.Where(n => n != null));

        EditorUtility.DisplayDialog("Creature setup",
            "Done! Rebuilt " + ControllerPath + "." +
            "\n\nLook around: " + lookNote +
            (missing.Length > 0 ? "\n\nNot found (skipped): " + missing : ""), "OK");
    }

    // LookLeft/LookRight: use both files if there are two; if there's only one,
    // make the other side as a mirrored copy of it.
    private static void SetUpLookClips(Avatar avatar, out AnimationClip left, out AnimationClip right, out string note)
    {
        bool hasLeft = AssetImporter.GetAtPath(Folder + "/LookLeft.fbx") is ModelImporter;
        bool hasRight = AssetImporter.GetAtPath(Folder + "/LookRight.fbx") is ModelImporter;
        left = null;
        right = null;

        if (hasLeft && hasRight)
        {
            left = SetUpClip("LookLeft", false, avatar);
            right = SetUpClip("LookRight", false, avatar);
            note = "using LookLeft.fbx and LookRight.fbx.";
        }
        else if (hasLeft)
        {
            List<AnimationClip> clips = SetUpClipFile("LookLeft", false, avatar, "LookRight");
            left = clips[0];
            right = clips[1];
            note = "using LookLeft.fbx, plus a mirrored copy as LookRight.";
        }
        else if (hasRight)
        {
            List<AnimationClip> clips = SetUpClipFile("LookRight", false, avatar, "LookLeft");
            right = clips[0];
            left = clips[1];
            note = "using LookRight.fbx, plus a mirrored copy as LookLeft.";
        }
        else
        {
            note = "no LookLeft.fbx or LookRight.fbx found, so it stays in Idle.";
        }
    }

    private static void AddIntTransition(AnimatorState from, AnimatorState to, int value)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(AnimatorConditionMode.Equals, value, "LookDirection");
        transition.hasExitTime = false;
        transition.duration = 0.4f; // a longer cross-fade looks smoother
    }

    // Empties the controller but keeps the same asset (and GUID), so references to it survive.
    private static void ClearController(AnimatorController controller)
    {
        foreach (BlendTree oldTree in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>())
        {
            AssetDatabase.RemoveObjectFromAsset(oldTree);
            Object.DestroyImmediate(oldTree, true);
        }

        while (controller.layers.Length > 0)
        {
            controller.RemoveLayer(0);
        }
        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddLayer("Base Layer");
    }

    // Sets up one animation file and returns its clip (null if the file isn't there).
    private static AnimationClip SetUpClip(string name, bool loop, Avatar avatar)
    {
        List<AnimationClip> clips = SetUpClipFile(name, loop, avatar, null);
        return clips.Count > 0 ? clips[0] : null;
    }

    // Sets up <name>.fbx. If mirroredName is given, also adds a mirrored copy of the
    // clip with that name. Returns [main clip, mirrored clip] (or empty if no file).
    private static List<AnimationClip> SetUpClipFile(string name, bool loop, Avatar avatar, string mirroredName)
    {
        List<AnimationClip> result = new List<AnimationClip>();
        string path = Folder + "/" + name + ".fbx";
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("[Creature setup] " + path + " not found, skipped.");
            return result;
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = avatar;
        importer.importAnimation = true;
        importer.SaveAndReimport(); // needed so the clip list below uses the Humanoid rig

        ModelImporterClipAnimation source = importer.defaultClipAnimations[0];
        List<ModelImporterClipAnimation> clipSettings = new List<ModelImporterClipAnimation>
        {
            MakeClipSettings(source, name, loop, false)
        };
        if (mirroredName != null)
        {
            clipSettings.Add(MakeClipSettings(source, mirroredName, loop, true));
        }
        importer.clipAnimations = clipSettings.ToArray();
        importer.SaveAndReimport();

        AnimationClip[] loaded = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
        result.Add(loaded.FirstOrDefault(c => c.name == name));
        if (mirroredName != null)
        {
            result.Add(loaded.FirstOrDefault(c => c.name == mirroredName));
        }
        return result;
    }

    private static ModelImporterClipAnimation MakeClipSettings(
        ModelImporterClipAnimation source, string name, bool loop, bool mirror)
    {
        return new ModelImporterClipAnimation
        {
            name = name,
            takeName = source.takeName,
            firstFrame = source.firstFrame,
            lastFrame = source.lastFrame,
            loopTime = loop,
            mirror = mirror,
            // "Bake Into Pose": the animation never moves or turns the creature itself.
            lockRootRotation = true,
            lockRootHeightY = true,
            lockRootPositionXZ = true,
            keepOriginalOrientation = true,
            keepOriginalPositionY = true,
            keepOriginalPositionXZ = true
        };
    }
}
