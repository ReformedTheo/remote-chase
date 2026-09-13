using RemoteChase.Drone;
using UnityEngine;

namespace RemoteChase.CameraRig
{
    /// <summary>
    /// Camera de terceira pessoa: segue o drone por tras com atraso, olha um ponto
    /// a frente dele e se aproxima quando um predio entra no caminho.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Remote Chase/Drone Follow Camera")]
    public class DroneFollowCamera : MonoBehaviour
    {
        [Header("Alvo")]
        [Tooltip("Drone a seguir. Se vazio, procura um DroneController na cena.")]
        public Transform target;

        [Header("Posicao")]
        [Tooltip("Deslocamento em relacao ao drone: X = lado, Y = altura, Z = distancia (negativo = atras).")]
        public Vector3 offset = new Vector3(0f, 4.5f, -9f);

        [Tooltip("Tempo (s) que a camera leva para alcancar a posicao alvo. Maior = mais preguicosa.")]
        public float followSmoothTime = 0.22f;

        [Tooltip("Velocidade com que a camera acompanha o giro do drone.")]
        public float rotationResponse = 6f;

        [Header("Mira")]
        [Tooltip("Quantos metros a frente do drone a camera olha.")]
        public float lookAhead = 6f;

        [Tooltip("Quanto a camera olha acima do drone.")]
        public float lookHeight = 1.5f;

        [Header("Turbo")]
        [Tooltip("Campo de visao normal.")]
        public float baseFieldOfView = 60f;

        [Tooltip("Quanto o campo de visao abre no turbo.")]
        public float boostFieldOfViewBonus = 12f;

        [Header("Colisao")]
        [Tooltip("Impede a camera de atravessar predios.")]
        public bool avoidObstacles = true;

        [Tooltip("Raio da esfera usada para testar obstaculos.")]
        public float obstacleProbeRadius = 0.4f;

        [Tooltip("Distancia minima do drone quando a camera e empurrada por um obstaculo.")]
        public float minDistance = 2f;

        [Tooltip("Camadas consideradas obstaculo.")]
        public LayerMask obstacleLayers = ~0;

        Camera _camera;
        DroneController _drone;
        Vector3 _velocity;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            ResolveTarget();
        }

        void OnEnable()
        {
            ResolveTarget();
            if (target != null)
            {
                // Comeca ja no lugar certo, sem o "voo" inicial da suavizacao.
                transform.position = DesiredPosition(out _);
                transform.rotation = Quaternion.LookRotation(LookPoint() - transform.position, Vector3.up);
            }
        }

        void ResolveTarget()
        {
            if (target == null)
            {
                _drone = FindAnyObjectByType<DroneController>();
                if (_drone != null) target = _drone.transform;
            }
            else if (_drone == null)
            {
                _drone = target.GetComponent<DroneController>();
            }
        }

        void LateUpdate()
        {
            if (target == null)
            {
                ResolveTarget();
                if (target == null) return;
            }

            Vector3 desired = DesiredPosition(out Vector3 pivot);

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity,
                                                    followSmoothTime);

            Quaternion lookRotation = Quaternion.LookRotation(LookPoint() - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation,
                                                  1f - Mathf.Exp(-rotationResponse * Time.deltaTime));

            UpdateFieldOfView();

            // pivot so existe para deixar claro de onde parte o teste de obstaculo.
            Debug.DrawLine(pivot, transform.position, Color.cyan);
        }

        Vector3 DesiredPosition(out Vector3 pivot)
        {
            // O offset acompanha so o giro em Y do drone: a camera nao capota junto.
            Quaternion yaw = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            pivot = target.position;
            Vector3 desired = pivot + yaw * offset;

            if (!avoidObstacles) return desired;

            Vector3 toCamera = desired - pivot;
            float distance = toCamera.magnitude;
            if (distance < 0.01f) return desired;

            if (Physics.SphereCast(pivot, obstacleProbeRadius, toCamera / distance,
                                   out RaycastHit hit, distance, obstacleLayers,
                                   QueryTriggerInteraction.Ignore)
                && hit.collider.transform.root != target.root)
            {
                float clamped = Mathf.Max(hit.distance - obstacleProbeRadius, minDistance);
                desired = pivot + toCamera / distance * clamped;
            }

            return desired;
        }

        Vector3 LookPoint()
        {
            return target.position + target.forward * lookAhead + Vector3.up * lookHeight;
        }

        void UpdateFieldOfView()
        {
            if (_camera == null) return;

            float targetFov = baseFieldOfView;
            if (_drone != null && _drone.IsBoosting) targetFov += boostFieldOfViewBonus;

            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, 4f * Time.deltaTime);
        }
    }
}
