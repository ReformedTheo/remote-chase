using System;
using UnityEngine;

namespace RemoteChase.Drone
{
    /// <summary>
    /// Movimento no trilho, ao estilo Star Fox / Panzer Dragoon: o drone avanca
    /// sozinho em +Z com velocidade que cresce ao longo da fase, e o jogador so
    /// controla a posicao dentro de um retangulo no plano X/Y.
    ///
    /// O corpo e um Rigidbody cinematico com collider em trigger: quem detecta a
    /// batida e o OnTriggerEnter, nao a fisica -- assim o trilho nunca e empurrado
    /// para fora do lugar por uma colisao.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Remote Chase/Drone Rail Controller")]
    public class DroneRailController : MonoBehaviour, IDroneMotion
    {
        [Header("Avanco no trilho")]
        [Tooltip("Velocidade no comeco da fase, em m/s.")]
        public float startSpeed = 26f;

        [Tooltip("Velocidade no fim da rampa -- a 'VELOCIDADE EXTREMA' do pitch.")]
        public float maxSpeed = 95f;

        [Tooltip("Distancia (m) percorrida ate chegar na velocidade maxima.")]
        public float rampDistance = 1800f;

        [Tooltip("Formato da aceleracao ao longo da fase. Reta = crescimento constante.")]
        public AnimationCurve rampCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Quao rapido a velocidade real alcanca a velocidade alvo (m/s^2). "
               + "Tambem define a recuperacao depois de uma batida.")]
        public float speedRecovery = 18f;

        [Header("Manobra no plano X/Y")]
        [Tooltip("Aceleracao lateral e vertical em m/s^2.")]
        public float maneuverAcceleration = 70f;

        [Tooltip("Arrasto da manobra. Baixo = desliza mais, exige antecipar o desvio.")]
        [Range(0f, 8f)] public float maneuverDrag = 3.2f;

        [Tooltip("Velocidade maxima de manobra em m/s.")]
        public float maxManeuverSpeed = 22f;

        [Header("Limites do trilho")]
        [Tooltip("Altura do centro do trilho em relacao ao chao.")]
        public float railHeight = 14f;

        [Tooltip("Meia largura e meia altura da area jogavel, a partir do centro do trilho.")]
        public Vector2 boundsHalfExtents = new Vector2(12f, 8f);

        [Header("Dash (Barrel Roll)")]
        [Tooltip("Velocidade instantanea do dash em m/s.")]
        public float dashSpeed = 34f;

        [Tooltip("Segundos de espera entre dois dashes.")]
        public float dashCooldown = 1.3f;

        [Tooltip("Segundos de imunidade concedidos pelo dash. E o que torna o dash "
               + "uma esquiva de verdade, e nao so um empurrao.")]
        public float dashInvulnerability = 0.4f;

        [Header("Colisao")]
        [Tooltip("Fracao da velocidade perdida ao bater.")]
        [Range(0f, 1f)] public float crashSpeedPenalty = 0.45f;

        [Tooltip("Empurrao para o centro do trilho ao bater, em m/s.")]
        public float crashPushBack = 8f;

        [Tooltip("Tempo (s) sem registrar nova batida depois de uma colisao.")]
        public float crashCooldown = 0.8f;

        /// <inheritdoc/>
        public event Action<float> OnCrash;

        /// <summary>Disparado ao executar um dash. Parametro = direcao no plano X/Y.</summary>
        public event Action<Vector2> OnDash;

        /// <summary>Dash disponivel agora.</summary>
        public bool DashReady => _dashTimer <= 0f;

        /// <summary>Recarga do dash, de 0 (pronto) a 1 (acabou de usar).</summary>
        public float DashCharge => dashCooldown > 0f ? Mathf.Clamp01(_dashTimer / dashCooldown) : 0f;

        /// <summary>Distancia percorrida na fase, em metros.</summary>
        public float Distance { get; private set; }

        /// <summary>Velocidade de avanco atual em m/s.</summary>
        public float ForwardSpeed { get; private set; }

        /// <summary>
        /// Distancia percorrida na fase atual. Diferente de <see cref="Distance"/>,
        /// que conta desde o inicio da partida e nunca volta -- se zerasse na troca
        /// de fase, o drone seria teleportado para tras no trilho.
        /// </summary>
        public float PhaseDistance => Distance - _phaseStartDistance;

        /// <summary>Progresso da rampa de velocidade da fase atual, de 0 a 1.</summary>
        public float SpeedProgress => rampDistance > 0f
            ? Mathf.Clamp01(PhaseDistance / rampDistance)
            : 1f;

        /// <summary>
        /// Trava a velocidade na inicial, ignorando a rampa. Usado pelo cheat F3,
        /// para a banca inspecionar o cenario sem a aceleracao atrapalhar.
        /// </summary>
        public bool SpeedLocked { get; set; }

        /// <summary>
        /// Comeca uma fase nova com outra curva de velocidade, sem mexer na
        /// posicao: o drone continua de onde estava, so o ritmo muda.
        /// </summary>
        public void BeginPhase(float newStartSpeed, float newMaxSpeed, float newRampDistance)
        {
            startSpeed = newStartSpeed;
            maxSpeed = newMaxSpeed;
            rampDistance = newRampDistance;
            _phaseStartDistance = Distance;
        }

        /// <summary>Centro do trilho no plano X/Y -- o ponto neutro da manobra.</summary>
        public Vector2 RailCenter => new Vector2(_railX, railHeight);

        /// <summary>Deslocamento atual em relacao ao centro do trilho.</summary>
        public Vector2 RailOffset => _offset;

        /// <inheritdoc/>
        public float Speed => Velocity.magnitude;

        /// <inheritdoc/>
        public Vector3 Velocity => new Vector3(_maneuverVelocity.x, _maneuverVelocity.y, ForwardSpeed);

        /// <inheritdoc/>
        public Vector3 ManeuverVelocity => new Vector3(_maneuverVelocity.x, _maneuverVelocity.y, 0f);

        /// <inheritdoc/>
        public float Altitude => transform.position.y;

        /// <inheritdoc/>
        // No trilho nao ha turbo: a aceleracao e a progressao da fase.
        public bool IsBoosting => false;

        /// <summary>Input lido neste frame.</summary>
        public DroneInput CurrentInput { get; private set; }

        [Header("Input")]
        [SerializeField] DroneInputReader inputReader = new DroneInputReader();

        /// <summary>Leitor de input, para ajustar sensibilidade em runtime.</summary>
        public DroneInputReader InputReader => inputReader;

        Rigidbody _body;
        Vector2 _maneuverVelocity;
        Vector2 _offset;      // deslocamento em relacao ao centro do trilho
        float _railX;         // centro do trilho em X (fixo por enquanto)
        float _startZ;
        float _crashTimer;
        float _dashTimer;
        float _phaseStartDistance;
        Combat.PlayerHealth _health;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;

            _railX = transform.position.x;
            _startZ = transform.position.z;
            _offset = new Vector2(0f, transform.position.y - railHeight);
            ForwardSpeed = startSpeed;

            // O mouse serve para a mira, nao para o movimento no trilho.
            inputReader.MouseYawSensitivity = 0f;
        }

        void Update()
        {
            CurrentInput = inputReader.Read(Time.deltaTime);

            if (_crashTimer > 0f) _crashTimer -= Time.deltaTime;
            if (_dashTimer > 0f) _dashTimer -= Time.deltaTime;

            if (CurrentInput.Dash) TryDash();
        }

        /// <summary>
        /// Arranco lateral com imunidade breve. Exige direcao: dash sem comando de
        /// movimento nao faz nada, para nao virar um botao de apertar sem pensar.
        /// </summary>
        void TryDash()
        {
            if (!DashReady) return;

            var direction = new Vector2(CurrentInput.Move.x, CurrentInput.Move.y + CurrentInput.Lift);
            if (direction.sqrMagnitude < 0.04f) return;

            direction.Normalize();
            _maneuverVelocity = direction * dashSpeed;
            _dashTimer = dashCooldown;

            if (_health == null) _health = GetComponent<Combat.PlayerHealth>();
            if (_health != null) _health.GrantInvulnerability(dashInvulnerability);

            OnDash?.Invoke(direction);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            AdvanceRail(dt);
            Maneuver(CurrentInput, dt);

            _body.MovePosition(new Vector3(_railX + _offset.x, railHeight + _offset.y, _startZ + Distance));
        }

        void AdvanceRail(float dt)
        {
            float target = SpeedLocked
                ? startSpeed
                : Mathf.Lerp(startSpeed, maxSpeed, rampCurve.Evaluate(SpeedProgress));
            ForwardSpeed = Mathf.MoveTowards(ForwardSpeed, target, speedRecovery * dt);
            Distance += ForwardSpeed * dt;
        }

        void Maneuver(DroneInput input, float dt)
        {
            // Move.x = lado, Move.y = frente/tras do teclado, que aqui vira subir/descer.
            // O Lift (Espaco/Ctrl) tambem sobe e desce, para quem preferir.
            var command = new Vector2(input.Move.x, Mathf.Clamp(input.Move.y + input.Lift, -1f, 1f));

            _maneuverVelocity += command * (maneuverAcceleration * dt);
            _maneuverVelocity -= _maneuverVelocity * (maneuverDrag * dt);

            // O teto so corta a aceleracao normal. Um dash sai acima dele de
            // proposito; quem traz de volta e o arrasto, o que da o deslize
            // caracteristico no fim do arranco.
            if (_maneuverVelocity.magnitude <= maxManeuverSpeed)
            {
                _maneuverVelocity = Vector2.ClampMagnitude(_maneuverVelocity, maxManeuverSpeed);
            }

            _offset += _maneuverVelocity * dt;

            // Bater no limite da area jogavel mata a velocidade naquele eixo,
            // em vez de o drone ficar vibrando encostado na borda.
            if (Mathf.Abs(_offset.x) > boundsHalfExtents.x)
            {
                _offset.x = Mathf.Sign(_offset.x) * boundsHalfExtents.x;
                _maneuverVelocity.x = 0f;
            }

            if (Mathf.Abs(_offset.y) > boundsHalfExtents.y)
            {
                _offset.y = Mathf.Sign(_offset.y) * boundsHalfExtents.y;
                _maneuverVelocity.y = 0f;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (_crashTimer > 0f) return;
            _crashTimer = crashCooldown;

            float impact = ForwardSpeed;
            ForwardSpeed *= 1f - crashSpeedPenalty;

            // Empurra de volta para o centro do trilho, para nao ficar preso no predio.
            Vector2 toCenter = -_offset.normalized;
            _maneuverVelocity = toCenter * crashPushBack;

            OnCrash?.Invoke(impact);
        }

        /// <inheritdoc/>
        public void ResetToSpawn()
        {
            Distance = 0f;
            ForwardSpeed = startSpeed;
            _offset = Vector2.zero;
            _maneuverVelocity = Vector2.zero;
            _crashTimer = 0f;
            _dashTimer = 0f;
            inputReader.Reset();
            transform.position = new Vector3(_railX, railHeight, _startZ);
        }
    }
}
