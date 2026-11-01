using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ProjectBlackout.PlayerTest;

namespace ProjectBlackout.EditorTools
{
    /// <summary>
    /// One-click scene creator. It never modifies Linn's existing scenes or Project Settings.
    /// Use Tools > Project Blackout > Create Player Test Scene.
    /// </summary>
    public static class BlackoutPlayerTestSceneBuilder
    {
        private const string Root = "Assets/BlackoutPlayerTest";
        private const string ScenePath = Root + "/Scenes/PlayerTest.unity";
        private const string PlayerPrefabPath = Root + "/Prefabs/Blackout_TestPlayer.prefab";
        private const string CratePrefabPath = Root + "/Prefabs/MetalCrate.prefab";

        [MenuItem("Tools/Project Blackout/Create Player Test Scene")]
        public static void CreateTestScene()
        {
            if (File.Exists(ScenePath) || File.Exists(PlayerPrefabPath) || File.Exists(CratePrefabPath))
            {
                EditorUtility.DisplayDialog("PlayerTest already created",
                    "A generated scene/prefab already exists in Assets/BlackoutPlayerTest. " +
                    "To protect your edits, this tool will NOT overwrite it. Open PlayerTest.unity instead. " +
                    "If you intentionally need to regenerate it, back up or rename the generated files first.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Prefabs");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.19f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 12f;
            RenderSettings.fogEndDistance = 37f;
            RenderSettings.fogColor = new Color(0.045f, 0.07f, 0.09f);
            RenderSettings.skybox = null;

            Material floor = MakeMaterial("Floor_Grey", new Color(0.16f, 0.19f, 0.23f));
            Material wall = MakeMaterial("Wall_BlueGrey", new Color(0.22f, 0.28f, 0.32f));
            Material ceiling = MakeMaterial("Ceiling_Dark", new Color(0.08f, 0.11f, 0.14f));
            Material trim = MakeMaterial("Trim_Cyan", new Color(0.03f, 0.46f, 0.49f), true);
            Material metal = MakeMaterial("Magnetic_Teal", new Color(0.07f, 0.63f, 0.68f));
            Material concrete = MakeMaterial("NonMagnetic_Orange", new Color(0.75f, 0.37f, 0.19f));
            Material warning = MakeMaterial("Warning_Red", new Color(0.86f, 0.20f, 0.18f), true);
            Material target = MakeMaterial("Impact_Target", new Color(0.85f, 0.71f, 0.21f));
            Material glove = MakeMaterial("Glove_Dark", new Color(0.13f, 0.17f, 0.21f));

            GameObject environment = new GameObject("ENVIRONMENT - safe to edit in test scene");
            GameObject props = new GameObject("PHYSICS OBJECTS - magnet tests");
            GameObject lights = new GameObject("LIGHTS - atmosphere");
            GameObject markers = new GameObject("TEST MARKERS - non-interactive");

            // Layout: centre corridor (x -4 to 4), and left/right test rooms through side gaps.
            Box("Floor", environment.transform, new Vector3(0f, -0.25f, 0f),
                new Vector3(34f, 0.5f, 24f), floor);
            Box("Ceiling", environment.transform, new Vector3(0f, 4.6f, 0f),
                new Vector3(34f, 0.25f, 24f), ceiling);
            Box("North_Wall", environment.transform, new Vector3(0f, 2.2f, 12f),
                new Vector3(34f, 4.4f, 0.4f), wall);
            Box("South_Wall", environment.transform, new Vector3(0f, 2.2f, -12f),
                new Vector3(34f, 4.4f, 0.4f), wall);
            Box("West_Wall", environment.transform, new Vector3(-17f, 2.2f, 0f),
                new Vector3(0.4f, 4.4f, 24f), wall);
            Box("East_Wall", environment.transform, new Vector3(17f, 2.2f, 0f),
                new Vector3(0.4f, 4.4f, 24f), wall);
            // Two open doorways, one on each side of the corridor.
            foreach (float x in new[] { -4f, 4f })
            {
                Box("Divider_South_" + x, environment.transform,
                    new Vector3(x, 2.2f, -6.9f), new Vector3(0.35f, 4.4f, 10.2f), wall);
                Box("Divider_North_" + x, environment.transform,
                    new Vector3(x, 2.2f, 6.9f), new Vector3(0.35f, 4.4f, 10.2f), wall);
                Box("Doorway_Header_" + x, environment.transform,
                    new Vector3(x, 3.95f, 0f), new Vector3(0.4f, 0.5f, 3.8f), wall);
                Box("Doorway_Trim_Left_" + x, markers.transform,
                    new Vector3(x, 1.8f, -1.85f), new Vector3(0.5f, 3.6f, 0.08f), trim, false);
                Box("Doorway_Trim_Right_" + x, markers.transform,
                    new Vector3(x, 1.8f, 1.85f), new Vector3(0.5f, 3.6f, 0.08f), trim, false);
            }

            // Simple glowing ceiling strips suggest an underground facility.
            foreach (float z in new[] { -8f, -2f, 4f, 9f })
                Box("Corridor_Ceiling_Strip_" + z, markers.transform,
                    new Vector3(0f, 4.44f, z), new Vector3(4.3f, 0.05f, 0.14f), trim, false);
            Box("LEFT ROOM marker", markers.transform, new Vector3(-10f, 0.035f, -5f),
                new Vector3(4f, 0.05f, 0.12f), trim, false);
            Box("RIGHT ROOM marker", markers.transform, new Vector3(10f, 0.035f, -5f),
                new Vector3(4f, 0.05f, 0.12f), warning, false);

            CreatePointLight(lights.transform, "Emergency_Red_Left", new Vector3(-11f, 3.65f, 1f),
                new Color(1f, 0.23f, 0.21f), 4.0f, 12f);
            CreatePointLight(lights.transform, "Emergency_Red_Right", new Vector3(11f, 3.65f, 1f),
                new Color(1f, 0.22f, 0.18f), 4.0f, 12f);
            CreatePointLight(lights.transform, "Corridor_Cool", new Vector3(0f, 3.9f, -4f),
                new Color(0.25f, 0.75f, 1f), 3.0f, 11f);
            CreatePointLight(lights.transform, "MagnetRoom_Cool", new Vector3(-10f, 3.9f, -2f),
                new Color(0.25f, 0.85f, 0.9f), 2.5f, 10f);

            // A hidden capsule renderer leaves collision geometry but avoids seeing your own body.
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Blackout_TestPlayer";
            player.transform.position = new Vector3(0f, 1.06f, -9.5f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.GetComponent<MeshRenderer>().enabled = false;
            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.35f;
            cc.center = Vector3.zero;
            cc.stepOffset = 0.3f;
            player.AddComponent<BlackoutFPSInput>();
            BlackoutMouseLook look = player.AddComponent<BlackoutMouseLook>();
            BlackoutPlayerHealth health = player.AddComponent<BlackoutPlayerHealth>();
            health.maxHealth = 3;

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.07f, 0.09f);
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            cameraObject.AddComponent<AudioListener>();
            look.SetCameraPivot(cameraObject.transform);

            GameObject flashlightObject = new GameObject("Flashlight - Spot Light");
            flashlightObject.transform.SetParent(cameraObject.transform, false);
            flashlightObject.transform.localPosition = new Vector3(0.17f, -0.12f, 0.05f);
            Light spot = flashlightObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(0.82f, 0.91f, 1f);
            spot.range = 19f;
            spot.spotAngle = 70f;
            spot.intensity = 6f;
            spot.shadows = LightShadows.None;
            BlackoutFlashlightController flashlightScript =
                cameraObject.AddComponent<BlackoutFlashlightController>();
            flashlightScript.SetLight(spot);

            GameObject holdPoint = new GameObject("MagnetHoldPoint");
            holdPoint.transform.SetParent(cameraObject.transform, false);
            holdPoint.transform.localPosition = new Vector3(0f, -0.18f, 2.65f);
            BlackoutMagnetGlove magnet = cameraObject.AddComponent<BlackoutMagnetGlove>();
            magnet.SetReferences(camera, holdPoint.transform);

            // Small visible glove placeholder made from primitives; no imported character model needed.
            GameObject gloveVisual = new GameObject("MagnetGlove_Placeholder (visual only)");
            gloveVisual.transform.SetParent(cameraObject.transform, false);
            Box("Glove_Body", gloveVisual.transform, new Vector3(0.47f, -0.42f, 0.85f),
                new Vector3(0.22f, 0.16f, 0.45f), glove, false, true);
            Box("Glove_Coil", gloveVisual.transform, new Vector3(0.47f, -0.35f, 1.0f),
                new Vector3(0.16f, 0.08f, 0.11f), trim, false, true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(player, PlayerPrefabPath,
                InteractionMode.AutomatedAction);

            GameObject crate = Box("MetalCrate_Corridor", props.transform,
                new Vector3(0.65f, 0.55f, -3.7f), Vector3.one * 1.1f, metal);
            MakeMagnetic(crate, 2.0f);
            PrefabUtility.SaveAsPrefabAssetAndConnect(crate, CratePrefabPath,
                InteractionMode.AutomatedAction);
            GameObject crateAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CratePrefabPath);
            if (crateAsset != null)
            {
                DuplicateCrate(crateAsset, props.transform, "MetalCrate_LeftRoom", new Vector3(-9f, 0.55f, 0.2f));
                DuplicateCrate(crateAsset, props.transform, "MetalCrate_RightRoom", new Vector3(9f, 0.55f, -2f));
            }

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "MetalBarrel_LeftRoom";
            barrel.transform.SetParent(props.transform);
            barrel.transform.position = new Vector3(-11f, 0.75f, -4f);
            barrel.transform.localScale = new Vector3(0.72f, 0.75f, 0.72f);
            barrel.GetComponent<Renderer>().sharedMaterial = metal;
            MakeMagnetic(barrel, 1.7f);

            GameObject nonMagnetic = Box("OrangeCube_NON_MAGNETIC", props.transform,
                new Vector3(10.5f, 0.65f, 2.5f), new Vector3(1.3f, 1.3f, 1.3f), concrete);
            nonMagnetic.AddComponent<Rigidbody>().mass = 4f;
            Box("Impact_Target_Back_Wall", markers.transform,
                new Vector3(0f, 1.8f, 11.75f), new Vector3(2.3f, 2.3f, 0.16f), target);
            Box("Crate_Obstacle_Static", props.transform,
                new Vector3(-0.85f, 0.55f, 3.8f), Vector3.one * 1.1f, wall);

            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                EditorUtility.DisplayDialog("Save failed", "Could not save PlayerTest.unity.", "OK");
                return;
            }
            Selection.activeGameObject = player;
            SceneAsset newSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorGUIUtility.PingObject(newSceneAsset);
            Debug.Log("Project Blackout PlayerTest created at " + ScenePath +
                ". Press Play, then test WASD / Mouse / F / RMB / LMB / H.");
            EditorUtility.DisplayDialog("PlayerTest created",
                "Created Assets/BlackoutPlayerTest/Scenes/PlayerTest.unity\n\n" +
                "The scene contains a Player prefab, a MetalCrate prefab, simple lab rooms, " +
                "physics objects and a flashlight. Press Play to try it.\n\n" +
                "This did not alter Linn's MainLevel or SampleScene.", "Great");
        }

        private static void MakeMagnetic(GameObject gameObject, float mass)
        {
            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            gameObject.AddComponent<BlackoutMagneticObject>();
            gameObject.AddComponent<BlackoutImpactNoise>();
        }

        private static void DuplicateCrate(GameObject prefab, Transform parent, string name, Vector3 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent);
            instance.transform.position = position;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position,
            Vector3 size, Material material, bool collider = true, bool positionIsLocal = false)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            if (positionIsLocal) obj.transform.localPosition = position;
            else obj.transform.position = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        private static void CreatePointLight(Transform parent, string name,
            Vector3 position, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent);
            lightObject.transform.position = position;
            Light lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Point;
            lightComponent.color = color;
            lightComponent.intensity = intensity;
            lightComponent.range = range;
            lightComponent.shadows = LightShadows.None;
        }

        private static Material MakeMaterial(string name, Color color, bool emissive = false)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material material = new Material(shader);
            material.name = name;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.2f);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
