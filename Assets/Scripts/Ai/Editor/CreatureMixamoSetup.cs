using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

// Editor tool: Tools > Creature > Set Up Mixamo Creature
// sets up the Mixamo files in Assets/Art/Creature and builds CreatureAnimator.controller
// files: Creature_Model.fbx (T-pose, with skin), Idle, Walk, Run, Attack, Stunned, LookLeft and/or LookRight (.fbx)
// (if only one of LookLeft/LookRight is there, the other side is made as a mirrored copy)
// 1. character: Rig = Humanoid, create its Avatar, extract its textures
// 2. animations: Rig = Humanoid, copy the character's Avatar, set Loop Time, bake root motion into the pose
// 3. controller: Locomotion blend tree (Speed: 0 Idle, 2 Walk, 4.5 Run), Stunned, Attack, LookLeft, LookRight
// (run it again any time; the controller is rebuilt in the same file so the prefab keeps its reference)
// Unity Scripting API: https://docs.unity3d.com/ScriptReference/ModelImporter.html
// Unity Scripting API: https://docs.unity3d.com/ScriptReference/Animations.AnimatorController.html
public static class CreatureMixamoSetup
{
    // where the files are
    private const string folder = "Assets/Art/Creature";
    private const string modelPath = folder + "/Creature_Model.fbx";
    private const string controllerPath = folder + "/CreatureAnimator.controller";

    // blend tree thresholds = the creature's speeds (patrol 2, chase 4.5)
    private const float walkSpeed = 2.0f;
    private const float runSpeed = 4.5f;

    [MenuItem("Tools/Creature/Set Up Mixamo Creature")]
    public static void SetUp()
    {
        // ---- 1. the character ----
        ModelImporter modelImporter = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (modelImporter == null)
        {
            EditorUtility.DisplayDialog("Creature setup", "Can't find " + modelPath + ".\nCheck the folder and the file name.", "OK");
            return;
        }

        // Humanoid rig with its own Avatar (the T-pose has no animation we need)
        modelImporter.animationType = ModelImporterAnimationType.Human;
        modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        modelImporter.importAnimation = false;
        modelImporter.SaveAndReimport();

        // take the textures out of the FBX so the materials can use them
        string textureFolder = folder + "/Textures";
        if (!AssetDatabase.IsValidFolder(textureFolder))
        {
            AssetDatabase.CreateFolder(folder, "Textures");
        }
        modelImporter.ExtractTextures(textureFolder);
        AssetDatabase.Refresh();
        modelImporter.SaveAndReimport();

        // the character's Avatar (the animations copy it)
        Avatar avatar = FindAvatar(modelPath);
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            EditorUtility.DisplayDialog("Creature setup", "The character's Humanoid Avatar couldn't be created.\nWas Creature_Model.fbx downloaded from Mixamo in T-pose, WITH skin?", "OK");
            return;
        }

        // ---- 2. the animations ----
        AnimationClip idle = SetUpClip("Idle", true, avatar);
        AnimationClip walk = SetUpClip("Walk", true, avatar);
        AnimationClip run = SetUpClip("Run", true, avatar);
        AnimationClip attack = SetUpClip("Attack", false, avatar);
        AnimationClip stunned = SetUpClip("Stunned", true, avatar);

        AnimationClip lookLeft;
        AnimationClip lookRight;
        string lookNote;
        SetUpLookClips(avatar, out lookLeft, out lookRight, out lookNote);

        // Idle is the only one that must be there
        if (idle == null)
        {
            EditorUtility.DisplayDialog("Creature setup", "Idle.fbx is required but wasn't found.", "OK");
            return;
        }

        // ---- 3. the Animator Controller (rebuilt in the same file) ----
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }
        ClearController(controller);

        // the parameters CreatureAnimation sets
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Stunned", AnimatorControllerParameterType.Bool);
        controller.AddParameter("LookDirection", AnimatorControllerParameterType.Int);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        // Locomotion blend tree: Idle -> Walk -> Run by Speed (the default state)
        BlendTree tree;
        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0.0f);
        if (walk != null)
        {
            tree.AddChild(walk, walkSpeed);
        }
        if (run != null)
        {
            tree.AddChild(run, runSpeed);
        }
        stateMachine.defaultState = locomotion;

        // Stunned is added first, so it wins over Attack if both happen at once
        if (stunned != null)
        {
            AnimatorState stunnedState = stateMachine.AddState("Stunned");
            stunnedState.motion = stunned;

            // Any State -> Stunned when Stunned = true
            AnimatorStateTransition toStunned = stateMachine.AddAnyStateTransition(stunnedState);
            toStunned.AddCondition(AnimatorConditionMode.If, 0.0f, "Stunned");
            toStunned.canTransitionToSelf = false;
            toStunned.hasExitTime = false;
            toStunned.duration = 0.15f;

            // Stunned -> Locomotion when Stunned = false
            AnimatorStateTransition fromStunned = stunnedState.AddTransition(locomotion);
            fromStunned.AddCondition(AnimatorConditionMode.IfNot, 0.0f, "Stunned");
            fromStunned.hasExitTime = false;
            fromStunned.duration = 0.25f;
        }

        if (attack != null)
        {
            AnimatorState attackState = stateMachine.AddState("Attack");
            attackState.motion = attack;

            // Any State -> Attack on the Attack trigger (each swing restarts the animation)
            AnimatorStateTransition toAttack = stateMachine.AddAnyStateTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0.0f, "Attack");
            toAttack.canTransitionToSelf = true;
            toAttack.hasExitTime = false;
            toAttack.duration = 0.1f;

            // Attack -> Locomotion when the animation is nearly finished
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

            // LookDirection: 1 -> LookRight, -1 -> LookLeft, 0 -> Locomotion
            AddLookTransition(locomotion, rightState, 1);
            AddLookTransition(locomotion, leftState, -1);
            AddLookTransition(rightState, leftState, -1);
            AddLookTransition(leftState, rightState, 1);
            AddLookTransition(rightState, locomotion, 0);
            AddLookTransition(leftState, locomotion, 0);
        }

        // save and select the controller
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Selection.activeObject = controller;

        // list the clips that were not found
        string missing = "";
        if (walk == null)
        {
            missing += " Walk";
        }
        if (run == null)
        {
            missing += " Run";
        }
        if (attack == null)
        {
            missing += " Attack";
        }
        if (stunned == null)
        {
            missing += " Stunned";
        }

        string message = "Done! Rebuilt " + controllerPath + ".\n\nLook around: " + lookNote;
        if (missing != "")
        {
            message += "\n\nNot found (skipped):" + missing;
        }
        EditorUtility.DisplayDialog("Creature setup", message, "OK");
    }

    // LookLeft/LookRight: uses both files if there are two,
    // or makes the other side as a mirrored copy if there is only one
    private static void SetUpLookClips(Avatar avatar, out AnimationClip left, out AnimationClip right, out string note)
    {
        bool hasLeft = AssetImporter.GetAtPath(folder + "/LookLeft.fbx") as ModelImporter != null;
        bool hasRight = AssetImporter.GetAtPath(folder + "/LookRight.fbx") as ModelImporter != null;
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

    // adds a transition that happens when LookDirection equals the given value
    private static void AddLookTransition(AnimatorState from, AnimatorState to, int value)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(AnimatorConditionMode.Equals, value, "LookDirection");
        transition.hasExitTime = false;
        // a longer cross-fade looks smoother
        transition.duration = 0.4f;
    }

    // empties the controller but keeps the same file (and GUID), so references to it still work
    private static void ClearController(AnimatorController controller)
    {
        // remove the old blend trees saved inside the controller file
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(controllerPath);
        foreach (Object asset in assets)
        {
            BlendTree oldTree = asset as BlendTree;
            if (oldTree != null)
            {
                AssetDatabase.RemoveObjectFromAsset(oldTree);
                Object.DestroyImmediate(oldTree, true);
            }
        }

        // remove all layers and parameters, then add an empty base layer
        while (controller.layers.Length > 0)
        {
            controller.RemoveLayer(0);
        }
        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddLayer("Base Layer");
    }

    // sets up one animation file and returns its clip (null if the file is not there)
    private static AnimationClip SetUpClip(string name, bool loop, Avatar avatar)
    {
        List<AnimationClip> clips = SetUpClipFile(name, loop, avatar, null);
        if (clips.Count > 0)
        {
            return clips[0];
        }
        return null;
    }

    // sets up <name>.fbx, and if mirroredName is given also adds a mirrored copy of the clip with that name
    // returns [main clip, mirrored clip] (or an empty list if there is no file)
    private static List<AnimationClip> SetUpClipFile(string name, bool loop, Avatar avatar, string mirroredName)
    {
        List<AnimationClip> result = new List<AnimationClip>();
        string path = folder + "/" + name + ".fbx";

        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("[Creature setup] " + path + " not found, skipped.");
            return result;
        }

        // Humanoid rig that copies the character's Avatar
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = avatar;
        importer.importAnimation = true;
        // needed so the clip list below uses the Humanoid rig
        importer.SaveAndReimport();

        // the clip settings (the mirrored copy uses the same frames)
        ModelImporterClipAnimation source = importer.defaultClipAnimations[0];
        List<ModelImporterClipAnimation> clipSettings = new List<ModelImporterClipAnimation>();
        clipSettings.Add(MakeClipSettings(source, name, loop, false));
        if (mirroredName != null)
        {
            clipSettings.Add(MakeClipSettings(source, mirroredName, loop, true));
        }
        importer.clipAnimations = clipSettings.ToArray();
        importer.SaveAndReimport();

        // load the clip(s) by name
        result.Add(FindClip(path, name));
        if (mirroredName != null)
        {
            result.Add(FindClip(path, mirroredName));
        }
        return result;
    }

    // the import settings for one clip
    private static ModelImporterClipAnimation MakeClipSettings(ModelImporterClipAnimation source, string name, bool loop, bool mirror)
    {
        ModelImporterClipAnimation clip = new ModelImporterClipAnimation();
        clip.name = name;
        clip.takeName = source.takeName;
        clip.firstFrame = source.firstFrame;
        clip.lastFrame = source.lastFrame;
        clip.loopTime = loop;
        clip.mirror = mirror;

        // "Bake Into Pose": the animation never moves or turns the creature itself (the NavMeshAgent does)
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = true;
        return clip;
    }

    // finds the Avatar inside an FBX file
    private static Avatar FindAvatar(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
        {
            Avatar avatar = asset as Avatar;
            if (avatar != null)
            {
                return avatar;
            }
        }
        return null;
    }

    // finds an animation clip with the given name inside an FBX file
    private static AnimationClip FindClip(string path, string clipName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip != null && clip.name == clipName)
            {
                return clip;
            }
        }
        return null;
    }
}
