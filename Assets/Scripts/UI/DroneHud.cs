using RemoteChase.Combat;
using RemoteChase.Drone;
using RemoteChase.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RemoteChase.UI
{
    /// <summary>
    /// HUD do jogo em OnGUI: integridade, pontuacao, velocidade, progresso da fase
    /// e o retículo da mira. Em OnGUI de proposito -- nao exige Canvas montado a
    /// mao, entao a cena inteira continua sendo gerada por codigo.
    /// </summary>
    [AddComponentMenu("Remote Chase/Drone HUD")]
    public class DroneHud : MonoBehaviour
    {
        [Tooltip("Mostra a lista de controles no canto.")]
        public bool showControls = true;

        [Tooltip("Desenha o retículo da mira.")]
        public bool showReticle = true;

        [Tooltip("Mostra o que o ultimo tiro encontrou. So para depuracao.")]
        public bool showLastHit;

        IDroneMotion _motion;
        DroneRailController _rail;
        DroneAim _aim;
        DroneWeapon _weapon;
        PlayerHealth _health;
        GameSession _session;

        float _lastCrashTime = -99f;
        float _lastHealTime = -99f;
        GUIStyle _style;
        Texture2D _pixel;

        void Awake()
        {
            _motion = GetComponent<IDroneMotion>();
            if (_motion == null) _motion = FindAnyObjectByType<DroneRailController>();
            if (_motion == null) _motion = FindAnyObjectByType<DroneController>();

            _rail = _motion as DroneRailController;
            _aim = GetComponent<DroneAim>();
            _weapon = GetComponent<DroneWeapon>();
            _health = GetComponent<PlayerHealth>();
            _session = FindAnyObjectByType<GameSession>();
        }

        void OnEnable()
        {
            if (_motion != null) _motion.OnCrash += HandleCrash;
            if (_health != null) _health.OnHealed += HandleHeal;
        }

        void OnDisable()
        {
            if (_motion != null) _motion.OnCrash -= HandleCrash;
            if (_health != null) _health.OnHealed -= HandleHeal;
        }

        void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);
        }

        void Update()
        {
            // Reiniciar posicao so faz sentido com a partida em andamento; depois
            // do fim quem cuida do recomeco e a GameSession.
            if (_session != null && _session.IsOver) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && _motion != null)
            {
                _motion.ResetToSpawn();
            }
        }

        void HandleCrash(float impactSpeed) => _lastCrashTime = Time.time;

        void HandleHeal(float amount) => _lastHealTime = Time.time;

        void OnGUI()
        {
            if (_motion == null) return;
            if (_session != null && _session.IsOver) return;

            // Com o jogo pausado o OnGUI continua rodando, entao sem isto o
            // retículo ficaria desenhado por cima do menu de pausa.
            if (Time.timeScale == 0f) return;

            EnsureStyle();
            DrawReticle();
            DrawStatus();
            DrawControls();
        }

        void EnsureStyle()
        {
            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.white }
            };

            if (_pixel != null) return;

            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        void DrawStatus()
        {
            // A barra de vida agora e a do Rafael, em Canvas, com os sprites.
            // Esta aqui so aparece se o HUD dele nao estiver na cena.
            bool hasCanvasHud = FindAnyObjectByType<PlayerHUDController>() != null;
            if (_health != null && !hasCanvasHud) DrawHealthBar();

            GUILayout.BeginArea(new Rect(16f, 200f, 320f, 240f));

            if (_session != null)
            {
                if (!string.IsNullOrEmpty(_session.PhaseName))
                {
                    GUILayout.Label(_session.PhaseName, _style);
                }

                GUILayout.Label($"Pontos: {_session.Score}", _style);
                GUILayout.Label($"Percurso: {_session.Progress * 100f:0}%"
                              + $"  ({_session.Distance:0} / {_session.PhaseTarget:0} m)", _style);
                GUILayout.Label($"Abatidos: {_session.Kills}", _style);
            }

            GUILayout.Label($"Velocidade: {_motion.Speed * 3.6f:0} km/h", _style);

            if (_rail != null)
            {
                GUILayout.Label($"Aceleracao da fase: {_rail.SpeedProgress * 100f:0}%", _style);
            }

            if (showLastHit && _weapon != null)
            {
                GUILayout.Label($"Ultimo tiro: {_weapon.LastHit}", _style);
            }

            if (_rail != null)
            {
                GUILayout.Label(_rail.DashReady
                    ? "Dash: pronto (Shift + direcao)"
                    : $"Dash: recarregando {(1f - _rail.DashCharge) * 100f:0}%", _style);
            }

            if (_health != null && _health.InvulnerabilityLeft > 0.5f)
            {
                GUILayout.Label($"ESCUDO {_health.InvulnerabilityLeft:0.0}s", _style);
            }

            if (Time.time - _lastCrashTime < 1.2f) GUILayout.Label("BATIDA", _style);
            if (Time.time - _lastHealTime < 1.2f) GUILayout.Label("REPARADO", _style);

            GUILayout.EndArea();
        }

        void DrawHealthBar()
        {
            const float width = 260f;
            const float height = 18f;
            var back = new Rect(16f, 16f, width, height);

            Color previous = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(back, _pixel);

            // Verde cheio -> vermelho quase zerado: da para ler sem numero nenhum.
            float fill = _health.Normalized;
            GUI.color = Color.Lerp(new Color(1f, 0.3f, 0.25f), new Color(0.4f, 1f, 0.5f), fill);
            GUI.DrawTexture(new Rect(back.x, back.y, width * fill, height), _pixel);

            GUI.color = previous;
            GUI.Label(new Rect(back.x + 8f, back.y - 1f, width, height),
                      $"Integridade {_health.Current:0}", _style);
        }

        void DrawControls()
        {
            if (!showControls) return;

            GUILayout.BeginArea(new Rect(16f, Screen.height - 86f, 460f, 80f));
            GUILayout.Label("WASD mover   Mouse mirar   Clique atirar   R reiniciar posicao", _style);
            if (_weapon != null) GUILayout.Label("Pare de manobrar para o tiro sair reto.", _style);
            GUILayout.EndArea();
        }

        /// <summary>
        /// Retículo em cruz. Ele abre e fica vermelho conforme o drone manobra --
        /// e o retorno visual de que o tiro vai sair torto.
        /// </summary>
        void DrawReticle()
        {
            if (!showReticle || _aim == null) return;

            float instability = _weapon != null ? _weapon.Instability : 0f;

            // GUI usa Y de cima para baixo; a mira vem em coordenadas de tela.
            var center = new Vector2(_aim.ScreenPosition.x, Screen.height - _aim.ScreenPosition.y);

            float gap = Mathf.Lerp(6f, 26f, instability);
            const float arm = 10f;
            const float thickness = 2f;

            Color previous = GUI.color;
            GUI.color = Color.Lerp(new Color(0.4f, 1f, 0.5f), new Color(1f, 0.35f, 0.3f), instability);

            GUI.DrawTexture(new Rect(center.x - gap - arm, center.y - thickness * 0.5f, arm, thickness), _pixel);
            GUI.DrawTexture(new Rect(center.x + gap, center.y - thickness * 0.5f, arm, thickness), _pixel);
            GUI.DrawTexture(new Rect(center.x - thickness * 0.5f, center.y - gap - arm, thickness, arm), _pixel);
            GUI.DrawTexture(new Rect(center.x - thickness * 0.5f, center.y + gap, thickness, arm), _pixel);

            // Ponto central so quando o tiro esta confiavel.
            if (_weapon != null && _weapon.IsSteady)
            {
                GUI.DrawTexture(new Rect(center.x - 1.5f, center.y - 1.5f, 3f, 3f), _pixel);
            }

            GUI.color = previous;
        }
    }
}
