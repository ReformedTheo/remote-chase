using System;
using RemoteChase.Combat;
using RemoteChase.Drone;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RemoteChase.Game
{
    /// <summary>
    /// Regras da fase: pontuacao, condicao de vitoria (chegar ao fim do percurso)
    /// e de derrota (integridade zerada), mais as telas de fim de jogo.
    ///
    /// As telas sao OnGUI pelo mesmo motivo do HUD: nao exigem Canvas montado a mao,
    /// entao a cena continua sendo gerada inteira por codigo.
    /// </summary>
    [AddComponentMenu("Remote Chase/Game Session")]
    public class GameSession : MonoBehaviour
    {
        [Header("Objetivo")]
        [Tooltip("Distancia (m) a percorrer para vencer a fase.")]
        public float targetDistance = 2500f;

        [Header("Pontuacao")]
        [Tooltip("Pontos por inimigo abatido.")]
        public int scorePerKill = 100;

        [Tooltip("Pontos por item coletado.")]
        public int scorePerPickup = 50;

        [Tooltip("Pontos por metro percorrido.")]
        public float scorePerMeter = 1f;

        /// <summary>Pontuacao atual.</summary>
        public int Score => _bonusScore + Mathf.FloorToInt(Distance * scorePerMeter);

        /// <summary>Distancia percorrida na fase atual.</summary>
        public float Distance => _drone != null ? _drone.PhaseDistance : 0f;

        /// <summary>Meta da fase atual, em metros.</summary>
        public float PhaseTarget =>
            _director != null && _director.Current != null
                ? _director.Current.targetDistance
                : targetDistance;

        /// <summary>Progresso do percurso da fase, de 0 a 1.</summary>
        public float Progress => PhaseTarget > 0f ? Mathf.Clamp01(Distance / PhaseTarget) : 0f;

        /// <summary>Nome da fase atual, para o HUD.</summary>
        public string PhaseName =>
            _director != null && _director.Current != null ? _director.Current.displayName : "";

        /// <summary>Inimigos abatidos.</summary>
        public int Kills { get; private set; }

        /// <summary>A partida acabou (por vitoria ou derrota).</summary>
        public bool IsOver { get; private set; }

        /// <summary>Verdadeiro se acabou em vitoria.</summary>
        public bool Won { get; private set; }

        /// <summary>Disparado no fim da partida. Parametro = venceu.</summary>
        public event Action<bool> OnFinished;

        DroneRailController _drone;
        PlayerHealth _health;
        DroneEnergy _energy;
        PhaseDirector _director;
        int _bonusScore;
        string _defeatReason = "Blindagem destruida";
        GUIStyle _title;
        GUIStyle _line;

        void Awake()
        {
            _drone = FindAnyObjectByType<DroneRailController>();
            _health = FindAnyObjectByType<PlayerHealth>();
            _energy = FindAnyObjectByType<DroneEnergy>();
            _director = FindAnyObjectByType<PhaseDirector>();

            // Time.timeScale e global e sobrevive a troca de cena: se a partida
            // anterior terminou com o jogo pausado, e aqui que ele volta a andar.
            Time.timeScale = 1f;
        }

        void OnEnable()
        {
            if (_health != null) _health.OnDied += HandleDeath;
            if (_energy != null) _energy.OnDepleted += HandleEnergyOut;
        }

        void OnDisable()
        {
            if (_health != null) _health.OnDied -= HandleDeath;
            if (_energy != null) _energy.OnDepleted -= HandleEnergyOut;
        }

        void Update()
        {
            if (IsOver)
            {
                ReadRestart();
                return;
            }

            if (Distance < PhaseTarget) return;

            // Chegou ao fim da fase: se houver proxima, troca; se nao, venceu.
            if (_director != null && _director.Advance()) return;

            Finish(true);
        }

        /// <summary>Registra um abate. Chamado pelo gerador do corredor.</summary>
        public void RegisterKill()
        {
            if (IsOver) return;

            Kills++;
            _bonusScore += scorePerKill;
        }

        /// <summary>Registra um item coletado.</summary>
        public void RegisterPickup()
        {
            if (IsOver) return;

            _bonusScore += scorePerPickup;
        }

        void HandleDeath()
        {
            _defeatReason = "Blindagem destruida";
            Finish(false);
        }

        void HandleEnergyOut()
        {
            _defeatReason = "Bateria esgotada";
            Finish(false);
        }

        void Finish(bool won)
        {
            if (IsOver) return;

            IsOver = true;
            Won = won;
            Time.timeScale = 0f;

            // Solta o cursor para a pessoa conseguir sair do jogo.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            OnFinished?.Invoke(won);
        }

        void ReadRestart()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame)
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        void OnGUI()
        {
            if (!IsOver) return;

            _title ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _line ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            // Escurece a tela inteira atras do texto.
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            var area = new Rect(0f, Screen.height * 0.5f - 150f, Screen.width, 300f);
            GUILayout.BeginArea(area);

            _title.normal.textColor = Won ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.45f, 0.4f);
            GUILayout.Label(Won ? "VITORIA" : "DERROTA", _title);

            GUILayout.Space(8f);
            if (!Won) GUILayout.Label(_defeatReason, _line);

            GUILayout.Space(8f);
            GUILayout.Label($"Pontuacao: {Score}", _line);
            GUILayout.Label($"Distancia: {Distance:0} m de {targetDistance:0} m", _line);
            GUILayout.Label($"Inimigos abatidos: {Kills}", _line);
            GUILayout.Space(20f);
            GUILayout.Label("Enter ou R para jogar de novo", _line);

            GUILayout.EndArea();
        }
    }
}
