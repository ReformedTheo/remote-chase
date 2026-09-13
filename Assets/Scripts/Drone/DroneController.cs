using System;
using UnityEngine;

namespace RemoteChase.Drone
{
    /// <summary>
    /// Voo arcade com inercia forte: o drone acelera nas direcoes comandadas, mas
    /// soltar o comando quase nao freia -- ele desliza e precisa ser antecipado.
    /// A fisica so gira em Y; a inclinacao do corpo e visual (ver DroneVisual).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Remote Chase/Drone Controller")]
    public class DroneController : MonoBehaviour, IDroneMotion
    {
        [Header("Horizontal")]
        [Tooltip("Aceleracao horizontal em m/s^2 com o comando no maximo.")]
        public float acceleration = 22f;

        [Tooltip("Velocidade horizontal de cruzeiro em m/s. Acima disso o ar 'segura' o drone.")]
        public float maxSpeed = 18f;

        [Tooltip("Arrasto horizontal. Baixo = muita deriva. Este e o valor que define a inercia.")]
        [Range(0f, 2f)] public float horizontalDrag = 0.25f;

        [Header("Turbo")]
        [Tooltip("Multiplicador de aceleracao e de velocidade maxima com o turbo segurado.")]
        public float boostMultiplier = 1.8f;

        [Header("Vertical")]
        [Tooltip("Aceleracao vertical em m/s^2 ao subir/descer.")]
        public float liftAcceleration = 14f;

        [Tooltip("Velocidade vertical maxima em m/s.")]
        public float maxVerticalSpeed = 9f;

        [Tooltip("Forca com que o drone segura a altitude quando nao ha comando vertical. "
               + "Baixo = ele afunda e sobe sozinho, alto = altitude travada.")]
        [Range(0f, 12f)] public float hoverStiffness = 3.5f;

        [Header("Giro")]
        [Tooltip("Aceleracao angular do giro em graus/s^2.")]
        public float yawAcceleration = 320f;

        [Tooltip("Velocidade de giro maxima em graus/s.")]
        public float maxYawSpeed = 110f;

        [Tooltip("Arrasto do giro. Baixo = o drone continua girando depois de soltar.")]
        [Range(0f, 8f)] public float yawDrag = 2.2f;

        [Header("Limites do mundo")]
        [Tooltip("Altura maxima de voo. 0 desliga o teto.")]
        public float ceilingHeight = 120f;

        [Header("Colisao")]
        [Tooltip("Quanto da velocidade sobra depois de bater. 0 = para tudo, 1 = nao perde nada.")]
        [Range(0f, 1f)] public float crashSpeedRetained = 0.35f;

        [Tooltip("Velocidade minima de impacto (m/s) para contar como batida.")]
        public float crashMinSpeed = 4f;

        /// <summary>Disparado ao bater em algo acima de <see cref="crashMinSpeed"/>. Parametro = velocidade do impacto.</summary>
        public event Action<float> OnCrash;

        /// <summary>Velocidade total atual em m/s.</summary>
        public float Speed => _body != null ? _body.linearVelocity.magnitude : 0f;

        /// <summary>Velocidade horizontal atual em m/s.</summary>
        public float HorizontalSpeed
        {
            get
            {
                if (_body == null) return 0f;
                Vector3 v = _body.linearVelocity;
                v.y = 0f;
                return v.magnitude;
            }
        }

        /// <inheritdoc/>
        public Vector3 Velocity => _body != null ? _body.linearVelocity : Vector3.zero;

        /// <inheritdoc/>
        // No voo livre a manobra e todo o movimento horizontal.
        public Vector3 ManeuverVelocity
        {
            get
            {
                Vector3 v = Velocity;
                v.y = 0f;
                return v;
            }
        }

        /// <summary>Altura do drone em relacao ao chao logo abaixo (ou ao mundo, se nao houver chao).</summary>
        public float Altitude { get; private set; }

        /// <summary>Turbo segurado neste frame.</summary>
        public bool IsBoosting { get; private set; }

        [Header("Input")]
        [Tooltip("Sensibilidade do mouse e suavizacao dos eixos.")]
        [SerializeField] DroneInputReader inputReader = new DroneInputReader();

        /// <summary>Input lido neste frame, para quem precisar (ex.: DroneVisual).</summary>
        public DroneInput CurrentInput { get; private set; }

        /// <summary>Leitor de input, exposto para ajustar sensibilidade em runtime.</summary>
        public DroneInputReader InputReader => inputReader;

        Rigidbody _body;
        Vector3 _spawnPosition;
        Quaternion _spawnRotation;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.useGravity = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // O corpo fisico so gira em Y: o tilt e feito pelo visual, entao a
            // fisica nao "cai de lado" ao raspar num predio.
            _body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _body.linearDamping = 0f;   // o arrasto e aplicado a mao, so no plano horizontal
            _body.angularDamping = 0f;  // idem para o giro

            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
        }

        void Update()
        {
            CurrentInput = InputReader.Read(Time.deltaTime);
            IsBoosting = CurrentInput.Boost && CurrentInput.Move.sqrMagnitude > 0.01f;
            Altitude = MeasureAltitude();
        }

        void FixedUpdate()
        {
            DroneInput input = CurrentInput;
            float dt = Time.fixedDeltaTime;
            float boost = IsBoosting ? boostMultiplier : 1f;

            ApplyHorizontal(input, boost, dt);
            ApplyVertical(input, dt);
            ApplyYaw(input, dt);
            ApplyCeiling();
        }

        void ApplyHorizontal(DroneInput input, float boost, float dt)
        {
            // O comando e relativo ao nariz do drone (que so gira em Y).
            Vector3 command = transform.right * input.Move.x + transform.forward * input.Move.y;
            command.y = 0f;

            _body.AddForce(command * (acceleration * boost), ForceMode.Acceleration);

            Vector3 horizontal = _body.linearVelocity;
            horizontal.y = 0f;

            // Arrasto proprio, so no plano horizontal: mantem o controle vertical limpo.
            _body.AddForce(-horizontal * horizontalDrag, ForceMode.Acceleration);

            // Teto de velocidade suave: acima do limite o ar segura cada vez mais
            // forte, em vez de cortar a velocidade de uma vez.
            float limit = maxSpeed * boost;
            float excess = horizontal.magnitude - limit;
            if (excess > 0f)
            {
                _body.AddForce(-horizontal.normalized * (excess / Mathf.Max(dt, 0.0001f) * 0.25f),
                               ForceMode.Acceleration);
            }
        }

        void ApplyVertical(DroneInput input, float dt)
        {
            float verticalSpeed = _body.linearVelocity.y;

            // Empuxo base: anula a gravidade para o drone pairar.
            float thrust = -Physics.gravity.y;

            if (Mathf.Abs(input.Lift) > 0.01f)
            {
                thrust += input.Lift * liftAcceleration;
            }
            else
            {
                // Sem comando: segura a altitude, mas de leve -- com inercia forte
                // o drone ainda sobe/afunda um pouco antes de estabilizar.
                thrust += -verticalSpeed * hoverStiffness;
            }

            _body.AddForce(Vector3.up * thrust, ForceMode.Acceleration);

            // Teto de velocidade vertical.
            float overspeed = Mathf.Abs(verticalSpeed) - maxVerticalSpeed;
            if (overspeed > 0f)
            {
                _body.AddForce(Vector3.up * (-Mathf.Sign(verticalSpeed) * overspeed / Mathf.Max(dt, 0.0001f) * 0.25f),
                               ForceMode.Acceleration);
            }
        }

        void ApplyYaw(DroneInput input, float dt)
        {
            float yawSpeed = _body.angularVelocity.y * Mathf.Rad2Deg;

            float torque = input.Yaw * yawAcceleration;
            torque -= yawSpeed * yawDrag;

            float overspeed = Mathf.Abs(yawSpeed) - maxYawSpeed;
            if (overspeed > 0f)
            {
                torque -= Mathf.Sign(yawSpeed) * overspeed / Mathf.Max(dt, 0.0001f) * 0.25f;
            }

            _body.AddTorque(Vector3.up * (torque * Mathf.Deg2Rad), ForceMode.Acceleration);
        }

        void ApplyCeiling()
        {
            if (ceilingHeight <= 0f || transform.position.y < ceilingHeight) return;

            Vector3 position = transform.position;
            position.y = ceilingHeight;
            transform.position = position;

            Vector3 velocity = _body.linearVelocity;
            if (velocity.y > 0f)
            {
                velocity.y = 0f;
                _body.linearVelocity = velocity;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            float impact = collision.relativeVelocity.magnitude;
            if (impact < crashMinSpeed) return;

            _body.linearVelocity *= crashSpeedRetained;
            _body.angularVelocity *= crashSpeedRetained;
            InputReader.Reset();

            OnCrash?.Invoke(impact);
        }

        float MeasureAltitude()
        {
            // Ignora o proprio drone e mede ate o que estiver abaixo (chao ou predio).
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 500f,
                                ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.transform.root != transform)
            {
                return hit.distance;
            }

            return transform.position.y;
        }

        /// <summary>Devolve o drone ao ponto onde a cena comecou, parado.</summary>
        public void ResetToSpawn()
        {
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            InputReader.Reset();
        }
    }
}
