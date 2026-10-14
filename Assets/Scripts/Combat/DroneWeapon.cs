using System.Collections.Generic;
using RemoteChase.Drone;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>
    /// Canhao hitscan, no modelo de CS/Valorant: o tiro e resolvido na hora por
    /// raycast -- sem tempo de voo, sem queda, sem antecipar o alvo. Se o retículo
    /// esta em cima e o drone esta parado, acerta.
    ///
    /// A imprecisao e angular e vem da manobra: quanto mais o drone se move, mais
    /// o tiro sai puxado para o lado do movimento. Parar de manobrar zera o desvio,
    /// mas deixa voce parado no meio dos obstaculos -- e essa a troca.
    /// </summary>
    [AddComponentMenu("Remote Chase/Drone Weapon")]
    public class DroneWeapon : MonoBehaviour
    {
        [Header("Disparo")]
        [Tooltip("Tiros por segundo.")]
        public float fireRate = 6f;

        [Tooltip("Alcance maximo do tiro em metros.")]
        public float range = 400f;

        [Tooltip("Dano por tiro.")]
        public float damage = 1f;

        [Tooltip("De onde o tiro sai, em coordenadas locais do drone.")]
        public Vector3 muzzleOffset = new Vector3(0f, 0f, 1.2f);

        [Tooltip("Camadas em que o tiro bate.")]
        public LayerMask hitLayers = ~0;

        [Header("Precisao")]
        [Tooltip("Desvio maximo em graus, com o drone manobrando no limite. "
               + "0 faz o tiro sempre sair no retículo.")]
        public float maxDeviationAngle = 7f;

        [Tooltip("Velocidade de manobra (m/s) que ja conta como desvio maximo.")]
        public float instabilityReference = 12f;

        [Header("Áudio (SFX)")]
        // Trazido do DroneWeapon do Rafael. La o disparo era projetil; aqui e
        // hitscan, entao so o trecho de audio veio -- o resto do arquivo dele
        // reverteria a correcao de paralaxe da mira.
        [Tooltip("Arquivo de som do tiro (ex: tiro1).")]
        public AudioClip shootSound;

        [Tooltip("Audio Source responsável por tocar o som do tiro.")]
        public AudioSource audioSource;

        [Header("Visual do tiro")]
        [Tooltip("Material do risco do tiro.")]
        public Material tracerMaterial;

        [Tooltip("Quantos riscos o pool cria de uma vez.")]
        public int poolSize = 16;

        /// <summary>
        /// 0 = drone parado, tiro sai exatamente no retículo. 1 = manobrando forte,
        /// desvio maximo. O HUD usa isto para abrir o retículo.
        /// </summary>
        public float Instability { get; private set; }

        /// <summary>Verdadeiro quando o tiro sai no ponto do retículo.</summary>
        public bool IsSteady => Instability < 0.05f;

        /// <summary>
        /// Nome do que o ultimo tiro encontrou, para o HUD. E so diagnostico:
        /// mostra se o raio esta passando no alvo errado ou acertando sem dar dano.
        /// </summary>
        public string LastHit { get; private set; } = "-";

        DroneAim _aim;
        IDroneMotion _motion;
        readonly Stack<Tracer> _pool = new Stack<Tracer>();
        Transform _poolRoot;
        float _cooldown;

        void Awake()
        {
            _aim = GetComponent<DroneAim>();
            _motion = GetComponent<IDroneMotion>();

            // Se o AudioSource não foi arrastado no Inspector, tenta pegar no mesmo objeto
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            _poolRoot = new GameObject("Tiros").transform;
            for (int i = 0; i < poolSize; i++) _pool.Push(CreateTracer());
        }

        void Update()
        {
            UpdateInstability();

            if (_cooldown > 0f) _cooldown -= Time.deltaTime;

            if (ReadFireInput() && _cooldown <= 0f)
            {
                Fire();
                _cooldown = fireRate > 0f ? 1f / fireRate : 0f;
            }
        }

        void UpdateInstability()
        {
            if (_motion == null) return;

            float maneuverSpeed = _motion.ManeuverVelocity.magnitude;
            Instability = Mathf.Clamp01(maneuverSpeed / Mathf.Max(instabilityReference, 0.01f));
        }

        bool ReadFireInput()
        {
            // O gatilho vem do mesmo leitor que o movimento, para nao duplicar input.
            if (_motion is DroneRailController rail) return rail.CurrentInput.Fire;
            if (_motion is DroneController free) return free.CurrentInput.Fire;
            return false;
        }

        void Fire()
        {
            Vector3 muzzle = transform.TransformPoint(muzzleOffset);

            // O tiro segue o raio da camera, nao a linha do cano: e o unico jeito
            // de acertar exatamente o que esta debaixo do retículo. A origem e
            // empurrada ate a profundidade do drone para o raio nao bater em um
            // predio que esteja entre a camera e o drone.
            Ray aimRay = _aim != null
                ? _aim.AimRay
                : new Ray(muzzle, transform.forward);

            float toDrone = Vector3.Dot(transform.position - aimRay.origin, aimRay.direction);
            Vector3 origin = aimRay.GetPoint(Mathf.Max(toDrone, 0f));
            Vector3 direction = ApplyDeviation(aimRay.direction);

            // Hitscan: o resultado do tiro e decidido aqui, neste frame.
            Vector3 impact = origin + direction * range;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitLayers,
                                QueryTriggerInteraction.Ignore))
            {
                impact = hit.point;
                LastHit = hit.collider.name;

                // GetComponentInParent, nao TryGetComponent: aquele nao e confiavel
                // com interface, e este ainda cobre collider em objeto filho.
                var target = hit.collider.GetComponentInParent<IHittable>();
                if (target != null)
                {
                    target.TakeHit(damage, hit.point);
                    LastHit += " (acertou)";
                }
            }
            else
            {
                LastHit = "nada";
            }

            ShowTracer(muzzle, impact);

            // TOCA O EFEITO SONORO DO TIRO
            if (audioSource != null && shootSound != null)
            {
                audioSource.PlayOneShot(shootSound);
            }
        }

        /// <summary>
        /// Gira a direcao do tiro em direcao ao movimento do drone.
        /// Parado, devolve a direcao intacta e o tiro sai no retículo.
        /// </summary>
        Vector3 ApplyDeviation(Vector3 direction)
        {
            if (_motion == null || maxDeviationAngle <= 0f) return direction;

            Vector3 maneuver = _motion.ManeuverVelocity;
            if (maneuver.sqrMagnitude < 0.0001f) return direction;

            // O desvio e para o lado do movimento, nao aleatorio: assim o jogador
            // consegue aprender a compensar em vez de so torcer.
            float angle = maxDeviationAngle * Instability * Mathf.Deg2Rad;
            return Vector3.RotateTowards(direction, maneuver.normalized, angle, 0f);
        }

        void ShowTracer(Vector3 from, Vector3 to)
        {
            Tracer tracer = _pool.Count > 0 ? _pool.Pop() : CreateTracer();
            tracer.Show(from, to, Recycle);
        }

        void Recycle(Tracer tracer)
        {
            _pool.Push(tracer);
        }

        Tracer CreateTracer()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Tiro";
            body.transform.SetParent(_poolRoot, false);

            // O risco e so visual: sem collider, para nao atrapalhar nenhum raycast.
            Object.Destroy(body.GetComponent<Collider>());

            if (tracerMaterial != null)
            {
                body.GetComponent<MeshRenderer>().sharedMaterial = tracerMaterial;
            }

            var tracer = body.AddComponent<Tracer>();
            body.SetActive(false);
            return tracer;
        }
    }
}
