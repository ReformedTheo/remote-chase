using UnityEngine;

namespace RemoteChase.World
{
    /// <summary>
    /// Parametros da cidade blocada. Cada predio e um cubo primitivo escalado.
    /// </summary>
    [System.Serializable]
    public class CitySettings
    {
        [Tooltip("Quantidade de quarteiroes no eixo X.")]
        public int blocksX = 8;

        [Tooltip("Quantidade de quarteiroes no eixo Z.")]
        public int blocksZ = 8;

        [Tooltip("Distancia entre os centros de dois predios vizinhos.")]
        public float blockSpacing = 22f;

        [Tooltip("Largura/profundidade de um predio.")]
        public Vector2 footprintRange = new Vector2(8f, 14f);

        [Tooltip("Altura minima e maxima dos predios.")]
        public Vector2 heightRange = new Vector2(10f, 60f);

        [Tooltip("Chance de um lote ficar vazio (praca).")]
        [Range(0f, 0.5f)] public float emptyLotChance = 0.12f;

        [Tooltip("Semente do sorteio: a mesma semente gera sempre a mesma cidade.")]
        public int seed = 1337;
    }

    /// <summary>
    /// Monta a cidade: um chao e uma grade de cubos com alturas sorteadas.
    /// Usado pelo menu do Editor, mas serve tambem em runtime.
    /// </summary>
    public static class CityBuilder
    {
        /// <summary>
        /// Cria chao e predios sob <paramref name="parent"/>.
        /// </summary>
        /// <returns>O tamanho total da cidade em metros (X e Z).</returns>
        public static Vector2 Build(Transform parent, CitySettings settings,
                                    Material groundMaterial, Material[] buildingMaterials)
        {
            float sizeX = settings.blocksX * settings.blockSpacing;
            float sizeZ = settings.blocksZ * settings.blockSpacing;

            BuildGround(parent, sizeX, sizeZ, groundMaterial);

            // Random com estado proprio: nao mexe no Random global do jogo.
            var random = new System.Random(settings.seed);

            float originX = -sizeX * 0.5f + settings.blockSpacing * 0.5f;
            float originZ = -sizeZ * 0.5f + settings.blockSpacing * 0.5f;

            for (int x = 0; x < settings.blocksX; x++)
            {
                for (int z = 0; z < settings.blocksZ; z++)
                {
                    if (random.NextDouble() < settings.emptyLotChance) continue;

                    float width = Lerp(random, settings.footprintRange);
                    float depth = Lerp(random, settings.footprintRange);
                    float height = Lerp(random, settings.heightRange);

                    var building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    building.name = $"Predio_{x}_{z}";
                    building.transform.SetParent(parent, false);
                    building.transform.localPosition = new Vector3(
                        originX + x * settings.blockSpacing,
                        height * 0.5f,
                        originZ + z * settings.blockSpacing);
                    building.transform.localScale = new Vector3(width, height, depth);
                    building.isStatic = true;

                    if (buildingMaterials != null && buildingMaterials.Length > 0)
                    {
                        Material material = buildingMaterials[random.Next(buildingMaterials.Length)];
                        building.GetComponent<MeshRenderer>().sharedMaterial = material;
                    }
                }
            }

            return new Vector2(sizeX, sizeZ);
        }

        static void BuildGround(Transform parent, float sizeX, float sizeZ, Material groundMaterial)
        {
            // Um cubo achatado em vez de Plane: da colisao solida e escala livre.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Chao";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(sizeX + 40f, 1f, sizeZ + 40f);
            ground.isStatic = true;

            if (groundMaterial != null)
            {
                ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            }
        }

        static float Lerp(System.Random random, Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        }
    }
}
