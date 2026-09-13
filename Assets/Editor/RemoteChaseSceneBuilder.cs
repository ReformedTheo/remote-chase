using System.IO;
using RemoteChase.CameraRig;
using RemoteChase.Combat;
using RemoteChase.Drone;
using RemoteChase.UI;
using RemoteChase.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RemoteChase.EditorTools
{
    /// <summary>
    /// Monta a cena de jogo inteira (cidade, drone e camera) a partir de primitivas,
    /// sem precisar arrastar nada no Inspector.
    /// </summary>
    public static class RemoteChaseSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/City.unity";
        const string RailScenePath = "Assets/Scenes/Corrida.unity";
        const string MeshFolder = "Assets/Meshes";
        const string MaterialFolder = "Assets/Materials";
        const string DroneMeshPath = MeshFolder + "/DroneArrow.asset";

        [MenuItem("Tools/Remote Chase/Criar cena City")]
        public static void CreateCityScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenePath)
                && !EditorUtility.DisplayDialog("Remote Chase",
                        $"{ScenePath} ja existe e sera sobrescrito. Continuar?",
                        "Sobrescrever", "Cancelar"))
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material ground = GetOrCreateMaterial("Chao", new Color(0.16f, 0.17f, 0.20f), 0.9f);
            Material[] buildings =
            {
                GetOrCreateMaterial("PredioA", new Color(0.36f, 0.39f, 0.45f), 0.65f),
                GetOrCreateMaterial("PredioB", new Color(0.28f, 0.31f, 0.36f), 0.6f),
                GetOrCreateMaterial("PredioC", new Color(0.45f, 0.42f, 0.38f), 0.7f)
            };
            Material droneMaterial = GetOrCreateMaterial("Drone", new Color(0.95f, 0.35f, 0.12f), 0.35f);

            CreateLighting();

            var cityRoot = new GameObject("Cidade");
            var settings = new CitySettings();
            CityBuilder.Build(cityRoot.transform, settings, ground, buildings);

            GameObject drone = CreateDrone(droneMaterial, settings);
            CreateCamera(drone.transform);

            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Remote Chase] Cena criada em {ScenePath}. Aperte Play para pilotar.");
        }

        [MenuItem("Tools/Remote Chase/Criar cena Corrida (rail)")]
        public static void CreateRailScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(RailScenePath)
                && !EditorUtility.DisplayDialog("Remote Chase",
                        $"{RailScenePath} ja existe e sera sobrescrito. Continuar?",
                        "Sobrescrever", "Cancelar"))
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material ground = GetOrCreateMaterial("Chao", new Color(0.16f, 0.17f, 0.20f), 0.9f);
            Material[] buildings =
            {
                GetOrCreateMaterial("PredioA", new Color(0.36f, 0.39f, 0.45f), 0.65f),
                GetOrCreateMaterial("PredioB", new Color(0.28f, 0.31f, 0.36f), 0.6f),
                GetOrCreateMaterial("PredioC", new Color(0.45f, 0.42f, 0.38f), 0.7f)
            };
            Material droneMaterial = GetOrCreateMaterial("Drone", new Color(0.95f, 0.35f, 0.12f), 0.35f);

            CreateLighting();

            GameObject drone = CreateRailDrone(droneMaterial);

            var corridorRoot = new GameObject("Corredor");
            var corridor = corridorRoot.AddComponent<CorridorGenerator>();
            corridor.follow = drone.transform;
            corridor.groundMaterial = ground;
            corridor.buildingMaterials = buildings;

            CreateRailCamera(drone.transform);

            EnsureFolder(Path.GetDirectoryName(RailScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, RailScenePath);
            AddSceneToBuildSettings(RailScenePath);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Remote Chase] Cena do trilho criada em {RailScenePath}. Aperte Play.");
        }

        static GameObject CreateRailDrone(Material material)
        {
            var drone = new GameObject("Drone");

            var body = drone.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            // Trigger, nao colisor solido: quem trata a batida e o OnTriggerEnter.
            var collider = drone.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.6f, 0.5f, 2f);
            collider.center = new Vector3(0f, 0f, 0.2f);
            collider.isTrigger = true;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(drone.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = GetOrCreateDroneMesh();
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;

            Transform[] rotors = CreateRotors(visual.transform, material);

            var controller = drone.AddComponent<DroneRailController>();
            // O drone nasce no centro do trilho, na altura que o controller usa.
            drone.transform.position = new Vector3(0f, controller.railHeight, 0f);

            var droneVisual = drone.AddComponent<DroneVisual>();
            droneVisual.visual = visual.transform;
            droneVisual.rotors = rotors;
            // No trilho o avanco e constante, entao o nariz responde a subida.
            droneVisual.pitchSource = DroneVisual.PitchSource.ClimbRate;

            drone.AddComponent<DroneAim>();

            var weapon = drone.AddComponent<DroneWeapon>();
            weapon.projectileMaterial = GetOrCreateMaterial(
                "Tiro", new Color(1f, 0.85f, 0.2f), 0.5f);

            drone.AddComponent<DroneHud>();

            return drone;
        }

        static void CreateRailCamera(Transform target)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 1200f;

            cameraObject.AddComponent<AudioListener>();

            var follow = cameraObject.AddComponent<RailCamera>();
            follow.target = target;
        }

        static void CreateLighting()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(52f, 130f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.color = new Color(1f, 0.96f, 0.9f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.52f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.32f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.14f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0035f;
            RenderSettings.fogColor = new Color(0.55f, 0.6f, 0.68f);
        }

        static GameObject CreateDrone(Material material, CitySettings city)
        {
            var drone = new GameObject("Drone");
            // Nasce acima do nivel dos predios, no canto da cidade.
            drone.transform.position = new Vector3(
                -city.blocksX * city.blockSpacing * 0.5f - 15f,
                city.heightRange.y * 0.6f,
                -city.blocksZ * city.blockSpacing * 0.5f - 15f);
            drone.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            var body = drone.AddComponent<Rigidbody>();
            body.mass = 1.2f;

            var collider = drone.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.6f, 0.5f, 2f);
            collider.center = new Vector3(0f, 0f, 0.2f);

            // Corpo visual: o "triangulo" gerado por codigo, salvo como asset.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(drone.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = GetOrCreateDroneMesh();
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;

            Transform[] rotors = CreateRotors(visual.transform, material);

            var controller = drone.AddComponent<DroneController>();
            controller.ceilingHeight = city.heightRange.y + 60f;

            var droneVisual = drone.AddComponent<DroneVisual>();
            droneVisual.visual = visual.transform;
            droneVisual.rotors = rotors;
            droneVisual.pitchSource = DroneVisual.PitchSource.ForwardSpeed;

            drone.AddComponent<DroneHud>();

            return drone;
        }

        static Transform[] CreateRotors(Transform parent, Material material)
        {
            Vector3[] positions =
            {
                new Vector3(0.72f, 0.18f, -0.72f),
                new Vector3(-0.72f, 0.18f, -0.72f),
                new Vector3(0.28f, 0.18f, 0.72f),
                new Vector3(-0.28f, 0.18f, 0.72f)
            };

            var rotors = new Transform[positions.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                var rotor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rotor.name = $"Helice{i}";
                // As helices sao so decorativas: sem colisao propria.
                Object.DestroyImmediate(rotor.GetComponent<Collider>());
                rotor.transform.SetParent(parent, false);
                rotor.transform.localPosition = positions[i];
                rotor.transform.localScale = new Vector3(0.55f, 0.02f, 0.55f);
                rotor.GetComponent<MeshRenderer>().sharedMaterial = material;
                rotors[i] = rotor.transform;
            }

            return rotors;
        }

        static void CreateCamera(Transform target)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 900f;

            cameraObject.AddComponent<AudioListener>();

            var follow = cameraObject.AddComponent<DroneFollowCamera>();
            follow.target = target;
        }

        static Mesh GetOrCreateDroneMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(DroneMeshPath);
            if (existing != null) return existing;

            EnsureFolder(MeshFolder);
            Mesh mesh = DroneMeshBuilder.CreateArrow();
            AssetDatabase.CreateAsset(mesh, DroneMeshPath);
            return mesh;
        }

        static Material GetOrCreateMaterial(string name, Color color, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.1f);

            EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Cria a pasta no AssetDatabase se ainda nao existir (aceita caminhos aninhados).</summary>
        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            if (scenes.Exists(s => s.path == scenePath)) return;

            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
