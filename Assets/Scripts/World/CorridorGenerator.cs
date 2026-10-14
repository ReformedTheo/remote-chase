using System.Collections.Generic;
using UnityEngine;

namespace RemoteChase.World
{
    /// <summary>
    /// Gera o canyon de predios por onde o drone passa. Os segmentos sao reciclados:
    /// o que fica para tras volta para a frente e e re-sorteado, entao o custo nao
    /// cresce com o tamanho da fase e nao ha alocacao a cada segundo.
    /// </summary>
    [AddComponentMenu("Remote Chase/Corridor Generator")]
    public class CorridorGenerator : MonoBehaviour
    {
        [Header("Alvo")]
        [Tooltip("Quem define o avanco. Se vazio, procura o drone na cena.")]
        public Transform follow;

        [Header("Trilho")]
        [Tooltip("Comprimento de um segmento em metros.")]
        public float segmentLength = 50f;

        [Tooltip("Quantos segmentos ficam montados a frente do drone.")]
        public int segmentsAhead = 14;

        [Tooltip("Quantos segmentos ficam para tras antes de serem reciclados.")]
        public int segmentsBehind = 2;

        [Tooltip("Meia largura livre do corredor: os predios comecam depois disso.")]
        public float corridorHalfWidth = 18f;

        [Header("Predios")]
        [Tooltip("Quantos predios por lado em cada segmento.")]
        public int buildingsPerSide = 3;

        [Tooltip("Largura minima e maxima de um predio.")]
        public Vector2 widthRange = new Vector2(10f, 22f);

        [Tooltip("Altura minima e maxima de um predio.")]
        public Vector2 heightRange = new Vector2(30f, 110f);

        [Tooltip("Quanto um predio pode recuar para trás da parede do corredor.")]
        public float setbackRange = 10f;

        [Header("Obstaculos")]
        [Tooltip("Chance de um segmento ter um bloco atravessado no meio do corredor.")]
        [Range(0f, 1f)] public float obstacleChance = 0.25f;

        // Os inimigos sairam daqui: eles agora vivem no EnemySpawner, que viaja
        // junto com o drone para os alvos ficarem parados na tela (estilo Aim Lab).
        // O corredor cuida so do cenario e dos coletaveis em pista.

        [Header("Coletaveis")]
        [Tooltip("Centro da area onde os itens aparecem (X, altura).")]
        public Vector2 pickupAreaCenter = new Vector2(0f, 14f);

        [Tooltip("Meia largura e meia altura da area de itens.")]
        public Vector2 pickupAreaHalfExtents = new Vector2(11f, 6f);

        [Tooltip("Quantos itens no maximo por segmento.")]
        public int pickupsPerSegment = 1;

        [Tooltip("Chance de a vaga de item ser preenchida. Baixa de proposito: "
               + "cura tem que ser algo que voce procura, nao algo que cai no colo.")]
        [Range(0f, 1f)] public float pickupChance = 0.22f;

        [Tooltip("Tamanho do item coletavel.")]
        public float pickupSize = 1.8f;

        [Header("Arte (opcional)")]
        [Tooltip("Prefab do predio. Vazio = cubo primitivo. O prefab precisa ter "
               + "tamanho base 1x1x1, porque o gerador dimensiona por localScale.")]
        public GameObject buildingPrefab;

        [Tooltip("Prefab do item de reparo. Vazio = capsula primitiva.")]
        public GameObject repairPrefab;

        [Tooltip("Prefab do item de escudo. Vazio = capsula primitiva.")]
        public GameObject shieldPrefab;

        [Tooltip("Prefab da celula de energia. Vazio = capsula primitiva.")]
        public GameObject energyPrefab;

        [Header("Visual")]
        public Material groundMaterial;
        public Material[] buildingMaterials;
        public Material pickupMaterial;

        [Tooltip("Material da celula de escudo.")]
        public Material shieldMaterial;

        [Tooltip("Material da celula de energia.")]
        public Material energyMaterial;

        Game.GameSession _session;
        Combat.PlayerHealth _playerHealth;
        Combat.DroneEnergy _playerEnergy;

        [Tooltip("Semente do sorteio: a mesma semente gera sempre a mesma fase.")]
        public int seed = 1337;

        readonly List<Segment> _segments = new List<Segment>();
        int _firstIndex;
        int _nextIndex;

        /// <summary>Um trecho do corredor. Os objetos sao criados uma vez e reposicionados.</summary>
        class Segment
        {
            public Transform Root;
            public Transform Ground;
            public Transform[] Buildings;
            public Transform Obstacle;
            public Combat.Pickup[] Pickups;
            public int Index = -1;
        }

        void Start()
        {
            if (follow == null)
            {
                var drone = FindAnyObjectByType<Drone.DroneRailController>();
                if (drone != null) follow = drone.transform;
            }

            _session = FindAnyObjectByType<Game.GameSession>();
            _playerHealth = FindAnyObjectByType<Combat.PlayerHealth>();
            _playerEnergy = FindAnyObjectByType<Combat.DroneEnergy>();

            for (int i = 0; i < segmentsAhead + segmentsBehind + 1; i++)
            {
                Segment segment = CreateSegment(i);
                Place(segment, _nextIndex++);
                _segments.Add(segment);
            }
        }

        void Update()
        {
            if (follow == null) return;

            int currentIndex = Mathf.FloorToInt(follow.position.z / segmentLength);

            // Recicla tudo que ficou longe demais para tras.
            while (_firstIndex < currentIndex - segmentsBehind)
            {
                Segment recycled = _segments[0];
                _segments.RemoveAt(0);
                Place(recycled, _nextIndex++);
                _segments.Add(recycled);
                _firstIndex++;
            }
        }

        Segment CreateSegment(int poolIndex)
        {
            var root = new GameObject($"Segmento_{poolIndex}").transform;
            root.SetParent(transform, false);

            var segment = new Segment
            {
                Root = root,
                Ground = CreateBlock(root, "Chao", groundMaterial),
                Buildings = new Transform[buildingsPerSide * 2],
                Obstacle = CreateBlock(root, "Obstaculo", PickMaterial(0)),
                Pickups = new Combat.Pickup[Mathf.Max(pickupsPerSegment, 0)]
            };

            for (int i = 0; i < segment.Buildings.Length; i++)
            {
                segment.Buildings[i] = CreateBlock(root, $"Predio_{i}", PickMaterial(i));
            }

            for (int i = 0; i < segment.Pickups.Length; i++)
            {
                segment.Pickups[i] = CreatePickup(root, i);
            }

            return segment;
        }

        Combat.Pickup CreatePickup(Transform parent, int index)
        {
            // Os tipos sao distribuidos na criacao e nao mudam depois: assim cada
            // item mantem o prefab e o material do seu tipo ao ser reciclado.
            // Reparo e Energia vem do PowerUp do Rafael; Escudo e o terceiro.
            Combat.PickupKind kind = (Combat.PickupKind)(index % 3);

            GameObject prefab = kind switch
            {
                Combat.PickupKind.Energia => energyPrefab,
                Combat.PickupKind.Escudo => shieldPrefab,
                _ => repairPrefab
            };

            Material material = kind switch
            {
                Combat.PickupKind.Energia => energyMaterial,
                Combat.PickupKind.Escudo => shieldMaterial,
                _ => pickupMaterial
            };

            GameObject body = CreateVisual(parent, $"{kind}_{index}", prefab,
                                           PrimitiveType.Capsule, material);
            body.transform.localScale = Vector3.one * pickupSize;

            // Sem collider: se tivesse, encostar no item dispararia o
            // OnTriggerEnter do drone e contaria como batida. A coleta e por distancia.
            foreach (Collider collider in body.GetComponentsInChildren<Collider>())
            {
                Destroy(collider);
            }

            var pickup = body.AddComponent<Combat.Pickup>();
            pickup.kind = kind;
            pickup.Player = _playerHealth;
            pickup.Energy = _playerEnergy;
            pickup.OnCollected += HandlePickupCollected;
            body.SetActive(false);
            return pickup;
        }

        /// <summary>
        /// Cria um objeto de cena a partir do prefab de arte, ou da primitiva
        /// correspondente quando ainda nao ha prefab. E o ponto unico onde a
        /// troca "bloco cinza -> modelo da Ramune" acontece.
        /// </summary>
        GameObject CreateVisual(Transform parent, string name, GameObject prefab,
                                PrimitiveType fallback, Material material)
        {
            GameObject body = prefab != null
                ? Instantiate(prefab, parent)
                : GameObject.CreatePrimitive(fallback);

            body.name = name;
            if (prefab == null) body.transform.SetParent(parent, false);

            // Com prefab, o material e o do proprio modelo: nao sobrescreve a arte.
            if (prefab == null && material != null)
            {
                body.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            return body;
        }

        void HandlePickupCollected(Combat.Pickup pickup)
        {
            if (_session != null) _session.RegisterPickup();
        }

        Transform CreateBlock(Transform parent, string name, Material material)
        {
            return CreateVisual(parent, name, buildingPrefab, PrimitiveType.Cube, material).transform;
        }

        Material PickMaterial(int index)
        {
            if (buildingMaterials == null || buildingMaterials.Length == 0) return null;
            return buildingMaterials[index % buildingMaterials.Length];
        }

        /// <summary>Reposiciona e re-sorteia um segmento para o indice dado.</summary>
        void Place(Segment segment, int index)
        {
            segment.Index = index;

            float baseZ = index * segmentLength;
            segment.Root.position = new Vector3(0f, 0f, baseZ);

            // Semente derivada do indice: o segmento 42 e sempre igual, mesmo
            // depois de reciclado. Facilita repetir uma fase e depurar.
            var random = new System.Random(seed + index);

            segment.Ground.localPosition = new Vector3(0f, -0.5f, segmentLength * 0.5f);
            segment.Ground.localScale = new Vector3(corridorHalfWidth * 4f, 1f, segmentLength);

            for (int i = 0; i < segment.Buildings.Length; i++)
            {
                bool rightSide = i % 2 == 0;
                float width = Range(random, widthRange);
                float height = Range(random, heightRange);
                float depth = Range(random, widthRange);
                float setback = (float)random.NextDouble() * setbackRange;
                float z = (i / 2 + (float)random.NextDouble()) * (segmentLength / buildingsPerSide);

                float x = corridorHalfWidth + setback + width * 0.5f;
                if (!rightSide) x = -x;

                segment.Buildings[i].localPosition = new Vector3(x, height * 0.5f, z);
                segment.Buildings[i].localScale = new Vector3(width, height, depth);
            }

            PlaceObstacle(segment, random);
            PlacePickups(segment, random);
        }

        void PlacePickups(Segment segment, System.Random random)
        {
            if (segment.Pickups == null) return;

            for (int i = 0; i < segment.Pickups.Length; i++)
            {
                Combat.Pickup pickup = segment.Pickups[i];
                if (pickup == null) continue;

                if (random.NextDouble() >= pickupChance)
                {
                    pickup.gameObject.SetActive(false);
                    continue;
                }

                float x = pickupAreaCenter.x
                        + ((float)random.NextDouble() * 2f - 1f) * pickupAreaHalfExtents.x;
                float y = pickupAreaCenter.y
                        + ((float)random.NextDouble() * 2f - 1f) * pickupAreaHalfExtents.y;
                float z = (float)random.NextDouble() * segmentLength;

                pickup.Spawn(new Vector3(x, y, z), (float)random.NextDouble() * 6.28f);
            }
        }

        void PlaceObstacle(Segment segment, System.Random random)
        {
            bool active = random.NextDouble() < obstacleChance;
            segment.Obstacle.gameObject.SetActive(active);
            if (!active) return;

            // Um bloco atravessado no corredor, deixando passagem de um lado.
            float width = corridorHalfWidth * (0.5f + (float)random.NextDouble() * 0.5f);
            float height = 6f + (float)random.NextDouble() * 10f;
            float centerY = 8f + (float)random.NextDouble() * 12f;
            float x = (random.NextDouble() < 0.5 ? -1f : 1f) * (corridorHalfWidth - width * 0.5f);

            segment.Obstacle.localPosition = new Vector3(x, centerY, segmentLength * 0.5f);
            segment.Obstacle.localScale = new Vector3(width, height, 4f);
        }

        static float Range(System.Random random, Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        }
    }
}
