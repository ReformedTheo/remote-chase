using RemoteChase.Combat;
using RemoteChase.Drone;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RemoteChase.Game
{
    /// <summary>
    /// Os tres cheats de depuracao prometidos no GDR, para a banca avaliar o
    /// jogo sem depender de habilidade:
    ///
    ///   F1 - Modo Deus: imune a choques e projeteis, e a bateria nao esgota.
    ///   F2 - Pulo de Fase: vai direto para a proxima fase.
    ///   F3 - Velocidade Travada: desliga a rampa de aceleracao, para inspecionar
    ///        o cenario com calma.
    ///
    /// Fica sempre ativo porque e requisito de entrega, nao ferramenta interna.
    /// O estado aparece no canto para nao haver duvida sobre o que esta ligado.
    /// </summary>
    [AddComponentMenu("Remote Chase/Debug Cheats")]
    public class DebugCheats : MonoBehaviour
    {
        [Tooltip("Mostra no canto da tela quais cheats estao ligados.")]
        public bool showIndicator = true;

        /// <summary>Modo deus ligado.</summary>
        public bool GodMode { get; private set; }

        /// <summary>Velocidade travada na inicial.</summary>
        public bool SpeedLocked { get; private set; }

        DroneRailController _drone;
        PlayerHealth _health;
        DroneEnergy _energy;
        PhaseDirector _director;
        GameSession _session;
        GUIStyle _style;

        void Awake()
        {
            _drone = FindAnyObjectByType<DroneRailController>();
            _health = FindAnyObjectByType<PlayerHealth>();
            _energy = FindAnyObjectByType<DroneEnergy>();
            _director = FindAnyObjectByType<PhaseDirector>();
            _session = FindAnyObjectByType<GameSession>();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) ToggleGodMode();
            if (keyboard.f2Key.wasPressedThisFrame) SkipPhase();
            if (keyboard.f3Key.wasPressedThisFrame) ToggleSpeedLock();

            KeepGodMode();
        }

        void ToggleGodMode()
        {
            GodMode = !GodMode;
            Debug.Log($"[Cheat] Modo Deus: {(GodMode ? "ligado" : "desligado")}");
        }

        /// <summary>
        /// O modo deus e renovado a cada frame em vez de ser um sinalizador no
        /// PlayerHealth: assim ele reaproveita a invulnerabilidade que ja existe
        /// para o dash e o escudo, sem um segundo caminho de "nao tome dano".
        /// </summary>
        void KeepGodMode()
        {
            if (!GodMode) return;

            if (_health != null) _health.GrantInvulnerability(1f);
            if (_energy != null) _energy.Recharge(100f);
        }

        void SkipPhase()
        {
            if (_director == null)
            {
                Debug.LogWarning("[Cheat] Nao ha PhaseDirector na cena.");
                return;
            }

            if (_director.Advance())
            {
                Debug.Log($"[Cheat] Pulou para a fase {_director.CurrentIndex + 1}.");
                return;
            }

            Debug.Log("[Cheat] Ja esta na ultima fase.");
        }

        void ToggleSpeedLock()
        {
            SpeedLocked = !SpeedLocked;
            if (_drone != null) _drone.SpeedLocked = SpeedLocked;
            Debug.Log($"[Cheat] Velocidade travada: {(SpeedLocked ? "ligada" : "desligada")}");
        }

        void OnGUI()
        {
            if (!showIndicator) return;
            if (_session != null && _session.IsOver) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperRight,
                normal = { textColor = new Color(1f, 0.9f, 0.4f) }
            };

            var area = new Rect(Screen.width - 330f, 10f, 320f, 90f);
            GUILayout.BeginArea(area);
            GUILayout.Label("F1 Modo Deus   F2 Pular fase   F3 Travar velocidade", _style);

            if (GodMode) GUILayout.Label("MODO DEUS LIGADO", _style);
            if (SpeedLocked) GUILayout.Label("VELOCIDADE TRAVADA", _style);

            GUILayout.EndArea();
        }
    }
}
