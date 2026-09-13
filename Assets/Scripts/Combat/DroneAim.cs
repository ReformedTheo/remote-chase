using UnityEngine;
using UnityEngine.InputSystem;

namespace RemoteChase.Combat
{
    /// <summary>
    /// Retículo controlado pelo mouse, independente do movimento do drone
    /// (estilo Star Fox 64). O cursor do sistema fica preso e escondido: a mira
    /// anda pelo delta do mouse, entao ela nunca escapa da janela.
    /// </summary>
    [AddComponentMenu("Remote Chase/Drone Aim")]
    public class DroneAim : MonoBehaviour
    {
        [Header("Sensibilidade")]
        [Tooltip("Pixels de retículo por pixel de mouse.")]
        public float sensitivity = 1.4f;

        [Tooltip("Quanto da tela a mira pode percorrer, do centro para a borda.")]
        [Range(0.1f, 1f)] public float screenRange = 0.82f;

        [Tooltip("Puxa a mira de volta ao centro quando o mouse esta parado (0 desliga).")]
        [Range(0f, 8f)] public float recenterSpeed = 0f;

        [Header("Alcance")]
        [Tooltip("Distancia a frente do drone onde a mira encosta no mundo.")]
        public float aimDistance = 140f;

        [Header("Cursor")]
        [Tooltip("Prende e esconde o cursor do sistema durante o jogo.")]
        public bool lockCursor = true;

        /// <summary>Posicao do retículo em pixels de tela.</summary>
        public Vector2 ScreenPosition { get; private set; }

        /// <summary>Ponto do mundo para onde a mira aponta.</summary>
        public Vector3 AimPoint { get; private set; }

        Camera _camera;

        void Awake()
        {
            _camera = Camera.main;
            ScreenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        void OnEnable()
        {
            if (!lockCursor) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDisable()
        {
            if (!lockCursor) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            MoveReticle();
            AimPoint = ProjectToWorld();
        }

        void MoveReticle()
        {
            Vector2 position = ScreenPosition;

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                position += new Vector2(delta.x, delta.y) * sensitivity;
            }

            // Stick direito do controle move a mira do mesmo jeito.
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                position += gamepad.rightStick.ReadValue() * (sensitivity * 600f * Time.deltaTime);
            }

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            if (recenterSpeed > 0f)
            {
                position = Vector2.Lerp(position, center, recenterSpeed * Time.deltaTime);
            }

            // A mira anda dentro de um retangulo centrado, nunca ate a borda da tela.
            Vector2 limit = center * screenRange;
            position.x = Mathf.Clamp(position.x, center.x - limit.x, center.x + limit.x);
            position.y = Mathf.Clamp(position.y, center.y - limit.y, center.y + limit.y);

            ScreenPosition = position;
        }

        /// <summary>
        /// Converte o retículo num ponto do mundo: onde o raio da camera cruza o
        /// plano vertical que esta <see cref="aimDistance"/> metros a frente do drone.
        /// </summary>
        Vector3 ProjectToWorld()
        {
            Ray ray = _camera.ScreenPointToRay(ScreenPosition);
            Vector3 planePoint = transform.position + Vector3.forward * aimDistance;
            var plane = new Plane(Vector3.back, planePoint);

            if (plane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            return planePoint;
        }
    }
}
