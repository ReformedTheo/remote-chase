using UnityEngine;

namespace RemoteChase.Game
{
    /// <summary>
    /// Os parametros de uma fase. As duas fases do GDR rodam na mesma cena e no
    /// mesmo trilho reto -- o que muda e o ritmo, o aperto do corredor, a
    /// densidade de inimigos e a paleta.
    ///
    /// Ficou assim em vez de uma cena por fase porque toda a interface do jogo
    /// (HUD, menu de pausa, EventSystem) esta montada a mao na cena de jogo:
    /// duplicar a cena duplicaria a UI, e qualquer ajuste teria que ser feito
    /// duas vezes.
    /// </summary>
    [System.Serializable]
    public class PhaseDefinition
    {
        [Tooltip("Nome mostrado na troca de fase.")]
        public string displayName = "Fase";

        [Tooltip("Subtitulo mostrado junto, na transicao.")]
        public string subtitle = "";

        [Header("Objetivo")]
        [Tooltip("Metros a percorrer nesta fase.")]
        public float targetDistance = 2500f;

        [Header("Velocidade")]
        public float startSpeed = 26f;
        public float maxSpeed = 95f;
        public float rampDistance = 1800f;

        [Header("Corredor")]
        [Tooltip("Meia largura livre. Menor = mais apertado.")]
        public float corridorHalfWidth = 18f;

        [Tooltip("Altura do teto. 0 = ceu aberto.")]
        public float ceilingHeight;

        [Tooltip("Altura minima e maxima dos predios/estruturas.")]
        public Vector2 buildingHeightRange = new Vector2(30f, 110f);

        [Tooltip("Chance de um segmento ter obstaculo atravessado.")]
        [Range(0f, 1f)] public float obstacleChance = 0.25f;

        [Header("Inimigos")]
        [Tooltip("Sentinelas simultaneos.")]
        public int maxEnemies = 4;

        [Tooltip("Segundos entre Sentinelas.")]
        public float enemyInterval = 1.1f;

        [Tooltip("A partir de que ponto da fase o Enxame aparece, de 0 a 1.")]
        [Range(0f, 1f)] public float swarmStartsAt = 0.25f;

        [Header("Atmosfera")]
        public Color fogColor = new Color(0.55f, 0.6f, 0.68f);
        public float fogDensity = 0.0035f;
        public Color ambientColor = new Color(0.45f, 0.52f, 0.62f);

        [Header("Materiais")]
        public Material groundMaterial;
        public Material[] buildingMaterials;

        [Tooltip("Semente do sorteio do cenario desta fase.")]
        public int seed = 1337;
    }
}
