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

        [Header("Visual")]
        public Material groundMaterial;
        public Material[] buildingMaterials;

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
            public int Index = -1;
        }

        void Start()
        {
            if (follow == null)
            {
                var drone = FindAnyObjectByType<Drone.DroneRailController>();
                if (drone != null) follow = drone.transform;
            }

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
                Obstacle = CreateBlock(root, "Obstaculo", PickMaterial(0))
            };

            for (int i = 0; i < segment.Buildings.Length; i++)
            {
                segment.Buildings[i] = CreateBlock(root, $"Predio_{i}", PickMaterial(i));
            }

            return segment;
        }

        Transform CreateBlock(Transform parent, string name, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);

            if (material != null)
            {
                block.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            return block.transform;
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
