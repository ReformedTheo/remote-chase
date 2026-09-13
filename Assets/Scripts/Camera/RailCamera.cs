using RemoteChase.Drone;
using UnityEngine;

namespace RemoteChase.CameraRig
{
    /// <summary>
    /// Camera do modo trilho: fica atras do drone, acompanha as manobras com um
    /// atraso e abre o campo de visao conforme a velocidade sobe -- e o que vende
    /// a sensacao de "velocidade extrema" sem mexer na velocidade real.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Remote Chase/Rail Camera")]
    public class RailCamera : MonoBehaviour
    {
        [Header("Alvo")]
        [Tooltip("Drone a seguir. Se vazio, procura na cena.")]
        public Transform target;

        [Header("Posicao")]
        [Tooltip("X = lado, Y = altura, Z = distancia (negativo = atras do drone).")]
        public Vector3 offset = new Vector3(0f, 3f, -12f);

        [Tooltip("Atraso da camera nas manobras. Maior = mais preguicosa, da mais peso.")]
        public float maneuverLag = 0.18f;

        [Tooltip("Quanto a camera exagera a manobra do drone (0 = colada, 1 = acompanha inteiro).")]
        [Range(0f, 1f)] public float maneuverFollow = 0.75f;

        [Header("Mira")]
        [Tooltip("Quantos metros a frente do drone a camera olha.")]
        public float lookAhead = 25f;

        [Header("Velocidade")]
        [Tooltip("Campo de visao na velocidade inicial.")]
        public float baseFieldOfView = 60f;

        [Tooltip("Quanto o campo de visao abre na velocidade maxima.")]
        public float speedFieldOfViewBonus = 26f;

        Camera _camera;
        DroneRailController _drone;
        Vector3 _smoothVelocity;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            ResolveTarget();
        }

        void OnEnable()
        {
            ResolveTarget();
            if (target != null) transform.position = DesiredPosition();
        }

        void ResolveTarget()
        {
            if (target == null)
            {
                _drone = FindAnyObjectByType<DroneRailController>();
                if (_drone != null) target = _drone.transform;
            }
            else if (_drone == null)
            {
                _drone = target.GetComponent<DroneRailController>();
            }
        }

        void LateUpdate()
        {
            if (target == null)
            {
                ResolveTarget();
                if (target == null) return;
            }

            Vector3 desired = DesiredPosition();

            // Z acompanha o drone na hora (senao a camera fica para tras no trilho);
            // X e Y sao os que ganham o atraso da manobra.
            Vector3 position = Vector3.SmoothDamp(transform.position, desired, ref _smoothVelocity,
                                                  maneuverLag);
            position.z = desired.z;
            transform.position = position;

            transform.rotation = Quaternion.LookRotation(
                target.position + Vector3.forward * lookAhead - transform.position, Vector3.up);

            UpdateFieldOfView();
        }

        Vector3 DesiredPosition()
        {
            // A camera parte do centro do trilho e so acompanha parte da manobra:
            // com maneuverFollow < 1 o drone "escapa" um pouco do centro da tela,
            // o que ajuda a ler o quanto ele saiu do eixo.
            Vector2 center = _drone != null
                ? _drone.RailCenter
                : new Vector2(target.position.x, target.position.y);

            float x = Mathf.Lerp(center.x, target.position.x, maneuverFollow) + offset.x;
            float y = Mathf.Lerp(center.y, target.position.y, maneuverFollow) + offset.y;
            return new Vector3(x, y, target.position.z + offset.z);
        }

        void UpdateFieldOfView()
        {
            if (_camera == null || _drone == null) return;

            float targetFov = baseFieldOfView + speedFieldOfViewBonus * _drone.SpeedProgress;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, 3f * Time.deltaTime);
        }
    }
}
