using System;
using System.Collections;
using RemoteChase.Combat;
using RemoteChase.Drone;
using RemoteChase.World;
using UnityEngine;

namespace RemoteChase.Game
{
    /// <summary>
    /// Controla em que fase o jogo esta e aplica os parametros dela no trilho,
    /// no corredor, nos inimigos e na atmosfera.
    ///
    /// A troca acontece sem recarregar a cena: o drone continua voando, o
    /// cenario e re-tematizado em volta dele e a curva de velocidade recomeca.
    /// </summary>
    [AddComponentMenu("Remote Chase/Phase Director")]
    public class PhaseDirector : MonoBehaviour
    {
        [Tooltip("As fases, em ordem. A ultima concluida vence o jogo.")]
        public PhaseDefinition[] phases = new PhaseDefinition[0];

        [Tooltip("Segundos de cartela na troca de fase.")]
        public float transitionDuration = 2.5f;

        /// <summary>Indice da fase atual.</summary>
        public int CurrentIndex { get; private set; }

        /// <summary>A fase atual, ou nulo se nao houver nenhuma configurada.</summary>
        public PhaseDefinition Current =>
            phases != null && CurrentIndex < phases.Length ? phases[CurrentIndex] : null;

        /// <summary>Esta na ultima fase: concluir significa vencer.</summary>
        public bool IsLastPhase => phases == null || CurrentIndex >= phases.Length - 1;

        /// <summary>Mostrando a cartela de troca de fase.</summary>
        public bool InTransition { get; private set; }

        /// <summary>Disparado ao entrar numa fase. Parametro = indice.</summary>
        public event Action<int> OnPhaseStarted;

        DroneRailController _drone;
        CorridorGenerator _corridor;
        EnemySpawner _spawner;
        GUIStyle _bigStyle;
        GUIStyle _smallStyle;
        float _transitionLeft;

        void Awake()
        {
            _drone = FindAnyObjectByType<DroneRailController>();
            _corridor = FindAnyObjectByType<CorridorGenerator>();
            _spawner = FindAnyObjectByType<EnemySpawner>();
        }

        void Start()
        {
            // Start, nao Awake: o CorridorGenerator monta os segmentos no Start
            // dele, e re-tematizar antes disso nao teria o que repintar.
            Apply(0);
        }

        /// <summary>Entra na fase indicada, aplicando todos os parametros dela.</summary>
        public void Apply(int index)
        {
            if (phases == null || phases.Length == 0) return;

            CurrentIndex = Mathf.Clamp(index, 0, phases.Length - 1);
            PhaseDefinition phase = phases[CurrentIndex];

            if (_drone != null)
            {
                _drone.BeginPhase(phase.startSpeed, phase.maxSpeed, phase.rampDistance);
            }

            if (_corridor != null)
            {
                _corridor.corridorHalfWidth = phase.corridorHalfWidth;
                _corridor.ceilingHeight = phase.ceilingHeight;
                _corridor.heightRange = phase.buildingHeightRange;
                _corridor.obstacleChance = phase.obstacleChance;
                _corridor.Retheme(phase.groundMaterial, phase.buildingMaterials, phase.seed);
            }

            if (_spawner != null)
            {
                _spawner.maxAlive = phase.maxEnemies;
                _spawner.spawnInterval = phase.enemyInterval;
                _spawner.swarmStartsAt = phase.swarmStartsAt;
            }

            RenderSettings.fogColor = phase.fogColor;
            RenderSettings.fogDensity = phase.fogDensity;
            RenderSettings.ambientSkyColor = phase.ambientColor;

            OnPhaseStarted?.Invoke(CurrentIndex);
        }

        /// <summary>
        /// Avanca para a proxima fase. Devolve falso se ja estava na ultima --
        /// nesse caso quem decide o que fazer e a GameSession (vitoria).
        /// </summary>
        public bool Advance()
        {
            if (IsLastPhase) return false;

            Apply(CurrentIndex + 1);
            StartCoroutine(ShowTransition());
            return true;
        }

        IEnumerator ShowTransition()
        {
            InTransition = true;
            _transitionLeft = transitionDuration;

            // Tempo nao escalado: a cartela aparece mesmo se algo tiver mexido
            // no Time.timeScale.
            while (_transitionLeft > 0f)
            {
                _transitionLeft -= Time.unscaledDeltaTime;
                yield return null;
            }

            InTransition = false;
        }

        void OnGUI()
        {
            if (!InTransition || Current == null) return;

            _bigStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _smallStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.85f, 0.9f) }
            };

            // Some suavemente no fim, para nao piscar.
            float alpha = Mathf.Clamp01(_transitionLeft / Mathf.Max(transitionDuration * 0.4f, 0.01f));
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            var area = new Rect(0f, Screen.height * 0.32f, Screen.width, 160f);
            GUILayout.BeginArea(area);
            GUILayout.Label(Current.displayName, _bigStyle);
            if (!string.IsNullOrEmpty(Current.subtitle)) GUILayout.Label(Current.subtitle, _smallStyle);
            GUILayout.EndArea();

            GUI.color = previous;
        }
    }
}
