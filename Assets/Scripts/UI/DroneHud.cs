using RemoteChase.Combat;
using RemoteChase.Drone;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RemoteChase.UI
{
    /// <summary>
    /// HUD minimo em OnGUI: velocidade, altitude, distancia e ultima batida.
    /// Feito em OnGUI de proposito -- nao exige Canvas nem nada montado a mao.
    /// Serve tanto para o voo livre quanto para o trilho.
    /// </summary>
    [AddComponentMenu("Remote Chase/Drone HUD")]
    public class DroneHud : MonoBehaviour
    {
        [Tooltip("Mostra a lista de controles no canto.")]
        public bool showControls = true;

        [Tooltip("Desenha o retículo da mira.")]
        public bool showReticle = true;

        IDroneMotion _motion;
        DroneRailController _rail;
        DroneAim _aim;
        DroneWeapon _weapon;
        float _lastCrashSpeed;
        float _lastCrashTime = -99f;
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
        }

        void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);
        }

        void OnEnable()
        {
            if (_motion != null) _motion.OnCrash += HandleCrash;
        }

        void OnDisable()
        {
            if (_motion != null) _motion.OnCrash -= HandleCrash;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && _motion != null)
            {
                _motion.ResetToSpawn();
            }
        }

        void HandleCrash(float impactSpeed)
        {
            _lastCrashSpeed = impactSpeed;
            _lastCrashTime = Time.time;
        }

        void OnGUI()
        {
            if (_motion == null) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                normal = { textColor = Color.white }
            };

            DrawReticle();

            GUILayout.BeginArea(new Rect(16f, 16f, 280f, 220f));

            GUILayout.Label($"Velocidade: {_motion.Speed * 3.6f:0} km/h", _style);
            GUILayout.Label($"Altitude: {_motion.Altitude:0.0} m", _style);

            if (_rail != null)
            {
                GUILayout.Label($"Distancia: {_rail.Distance:0} m", _style);
                GUILayout.Label($"Aceleracao da fase: {_rail.SpeedProgress * 100f:0}%", _style);
            }

            if (_motion.IsBoosting) GUILayout.Label("TURBO", _style);

            if (Time.time - _lastCrashTime < 1.5f)
            {
                GUILayout.Label($"Batida! -{_lastCrashSpeed:0} m/s", _style);
            }

            GUILayout.EndArea();

            if (!showControls) return;

            GUILayout.BeginArea(new Rect(16f, Screen.height - 110f, 420f, 100f));
            GUILayout.Label(_rail != null
                ? "WASD mover   Mouse mirar   Clique atirar   R reiniciar"
                : "WASD mover   Q/E girar   Espaco/Ctrl subir e descer   R reiniciar", _style);
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

            if (_pixel == null)
            {
                _pixel = new Texture2D(1, 1);
                _pixel.SetPixel(0, 0, Color.white);
                _pixel.Apply();
            }

            float instability = _weapon != null ? _weapon.Instability : 0f;

            // GUI usa Y de cima para baixo; a mira vem em coordenadas de tela.
            var center = new Vector2(_aim.ScreenPosition.x, Screen.height - _aim.ScreenPosition.y);

            float gap = Mathf.Lerp(6f, 26f, instability);
            const float arm = 10f;
            const float thickness = 2f;

            Color previous = GUI.color;
            GUI.color = Color.Lerp(new Color(0.4f, 1f, 0.5f), new Color(1f, 0.35f, 0.3f), instability);

            // Braços: esquerda, direita, cima, baixo.
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
