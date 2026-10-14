using System.IO;
using RemoteChase.CameraRig;
using RemoteChase.Combat;
using RemoteChase.Drone;
using RemoteChase.Game;
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

        /// <summary>
        /// ATENCAO: recria a cena do zero e JOGA FORA tudo que foi montado a mao
        /// nela -- Canvas, HUD, menu de pausa, posicionamento ajustado no editor.
        ///
        /// Fazia sentido quando a cena era 100% gerada. Desde que a UI passou a
        /// ser montada no editor, isto virou ferramenta de ultimo caso: serve para
        /// recomecar do zero, nao para atualizar a cena existente.
        /// </summary>
        [MenuItem("Tools/Remote Chase/PERIGO - Recriar cena Corrida do zero")]
        public static void CreateRailScene()
        {
            if (!Application.isBatchMode
                && !EditorUtility.DisplayDialog(
                        "Recriar a cena do zero?",
                        "Isto apaga TUDO que foi montado a mao na Corrida.unity: "
                      + "Canvas, HUD, menu de pausa e qualquer ajuste feito no editor.\n\n"
                      + "So continue se a intencao for mesmo recomecar.",
                        "Apagar e recriar", "Cancelar"))
            {
                return;
            }

            // Em batchmode nao existe ninguem para responder dialogo: o build pela
            // linha de comando chama este mesmo metodo e travaria aqui.
            if (!Application.isBatchMode)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

                if (File.Exists(RailScenePath)
                    && !EditorUtility.DisplayDialog("Remote Chase",
                            $"{RailScenePath} ja existe e sera sobrescrito. Continuar?",
                            "Sobrescrever", "Cancelar"))
                {
                    return;
                }
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

            // Regras da fase antes do corredor: o gerador procura a sessao no Start
            // para registrar abates e coletas.
            var sessionRoot = new GameObject("Regras da fase");
            sessionRoot.AddComponent<GameSession>();
            sessionRoot.AddComponent<DebugCheats>();
            CreatePhases(sessionRoot);

            var bulletPoolRoot = new GameObject("Tiros inimigos");
            var bulletPool = bulletPoolRoot.AddComponent<EnemyBulletPool>();
            bulletPool.bulletMaterial = GetOrCreateMaterial(
                "TiroInimigo", new Color(1f, 0.25f, 0.6f), 0.4f);

            var corridorRoot = new GameObject("Corredor");
            var corridor = corridorRoot.AddComponent<CorridorGenerator>();
            corridor.follow = drone.transform;
            corridor.groundMaterial = ground;
            corridor.buildingMaterials = buildings;
            corridor.pickupMaterial = GetOrCreateMaterial(
                "Reparo", new Color(0.25f, 0.95f, 0.55f), 0.7f);
            corridor.shieldMaterial = GetOrCreateMaterial(
                "Escudo", new Color(0.3f, 0.7f, 1f), 0.8f);

            // Campo de alvos separado do cenario: ele viaja junto com o drone,
            // entao os inimigos ficam parados na tela (Drone Sentinela do GDR).
            var spawnerRoot = new GameObject("Alvos");
            var spawner = spawnerRoot.AddComponent<EnemySpawner>();
            spawner.drone = drone.GetComponent<DroneRailController>();
            spawner.enemyMaterial = GetOrCreateMaterial(
                "Inimigo", new Color(0.95f, 0.25f, 0.1f), 0.6f);
            spawner.swarmMaterial = GetOrCreateMaterial(
                "Enxame", new Color(1f, 0.6f, 0.05f), 0.5f);

            CreateRailCamera(drone.transform);

            EnsureFolder(Path.GetDirectoryName(RailScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, RailScenePath);
            AddSceneToBuildSettings(RailScenePath);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Remote Chase] Cena do trilho criada em {RailScenePath}. Aperte Play.");
        }

        /// <summary>
        /// As duas fases do GDR. Fase 1 e avenida aberta e larga, para aprender;
        /// Fase 2 e tunel industrial fechado por teto, mais estreito e mais rapido.
        /// </summary>
        public static void CreatePhases(GameObject host)
        {
            Material surfaceGround = GetOrCreateMaterial("Chao", new Color(0.16f, 0.17f, 0.20f), 0.9f);
            Material[] surfaceBuildings =
            {
                GetOrCreateMaterial("PredioA", new Color(0.36f, 0.39f, 0.45f), 0.65f),
                GetOrCreateMaterial("PredioB", new Color(0.28f, 0.31f, 0.36f), 0.6f),
                GetOrCreateMaterial("PredioC", new Color(0.45f, 0.42f, 0.38f), 0.7f)
            };

            Material industrialGround = GetOrCreateMaterial(
                "ChaoIndustrial", new Color(0.1f, 0.1f, 0.12f), 0.4f);
            Material[] industrialWalls =
            {
                GetOrCreateMaterial("DutoA", new Color(0.22f, 0.2f, 0.19f), 0.35f),
                GetOrCreateMaterial("DutoB", new Color(0.3f, 0.26f, 0.2f), 0.45f),
                GetOrCreateMaterial("DutoC", new Color(0.16f, 0.18f, 0.2f), 0.3f)
            };

            var director = host.AddComponent<PhaseDirector>();
            director.phases = new[]
            {
                new PhaseDefinition
                {
                    displayName = "FASE 1 - CORREDOR CORPORATIVO",
                    subtitle = "Avenidas aereas da superficie",
                    targetDistance = 2200f,
                    startSpeed = 26f,
                    maxSpeed = 90f,
                    rampDistance = 1700f,
                    corridorHalfWidth = 18f,
                    ceilingHeight = 0f,
                    buildingHeightRange = new Vector2(30f, 110f),
                    obstacleChance = 0.25f,
                    maxEnemies = 4,
                    enemyInterval = 1.2f,
                    swarmStartsAt = 0.3f,
                    fogColor = new Color(0.55f, 0.6f, 0.68f),
                    fogDensity = 0.0035f,
                    ambientColor = new Color(0.45f, 0.52f, 0.62f),
                    groundMaterial = surfaceGround,
                    buildingMaterials = surfaceBuildings,
                    seed = 1337
                },
                new PhaseDefinition
                {
                    displayName = "FASE 2 - NUCLEO INDUSTRIAL",
                    subtitle = "Velocidade extrema em espaco fechado",
                    targetDistance = 1800f,
                    // Comeca onde a Fase 1 terminou, em vez de voltar ao ritmo
                    // inicial: a sensacao e de continuidade, nao de recomeco.
                    startSpeed = 85f,
                    maxSpeed = 130f,
                    rampDistance = 1200f,
                    corridorHalfWidth = 11f,
                    ceilingHeight = 26f,
                    buildingHeightRange = new Vector2(18f, 30f),
                    obstacleChance = 0.45f,
                    maxEnemies = 5,
                    enemyInterval = 0.9f,
                    swarmStartsAt = 0f,
                    fogColor = new Color(0.16f, 0.12f, 0.1f),
                    fogDensity = 0.012f,
                    ambientColor = new Color(0.3f, 0.22f, 0.18f),
                    groundMaterial = industrialGround,
                    buildingMaterials = industrialWalls,
                    seed = 90210
                }
            };
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

            drone.AddComponent<PlayerHealth>();
            drone.AddComponent<DroneEnergy>();
            drone.AddComponent<DroneAim>();
            drone.AddComponent<AudioSource>();

            var weapon = drone.AddComponent<DroneWeapon>();
            weapon.tracerMaterial = GetOrCreateMaterial(
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
