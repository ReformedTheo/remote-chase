using System.Collections.Generic;
using RemoteChase.Drone;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>
    /// Canhao do drone. O tiro so sai reto na mira quando o drone esta parado:
    /// manobrando, o projetil herda a velocidade lateral do drone e passa longe
    /// do retículo. Quem quer acertar precisa soltar o comando de movimento --
    /// e ficar parado no meio dos obstaculos e o risco que paga a precisao.
    /// </summary>
    [AddComponentMenu("Remote Chase/Drone Weapon")]
    public class DroneWeapon : MonoBehaviour
    {
        [Header("Disparo")]
        [Tooltip("Tiros por segundo.")]
        public float fireRate = 6f;

        [Tooltip("Velocidade do projetil em m/s, somada a do drone.")]
        public float projectileSpeed = 260f;

        [Tooltip("Dano por tiro.")]
        public float damage = 1f;

        [Tooltip("De onde o tiro sai, em coordenadas locais do drone.")]
        public Vector3 muzzleOffset = new Vector3(0f, 0f, 1.2f);

        [Header("Precisao")]
        [Tooltip("Quanto da velocidade de manobra o projetil herda. "
               + "1 = desvio total ao se mover, 0 = sempre acerta a mira.")]
        [Range(0f, 2f)] public float maneuverDrift = 1f;

        [Tooltip("Velocidade de manobra (m/s) a partir da qual a mira ja e considerada "
               + "totalmente instavel. Serve so para o retorno visual do retículo.")]
        public float instabilityReference = 12f;

        [Header("Projetil")]
        [Tooltip("Tamanho do projetil.")]
        public float projectileSize = 0.35f;

        [Tooltip("Material do projetil.")]
        public Material projectileMaterial;

        [Tooltip("Quantos projeteis o pool cria de uma vez.")]
        public int poolSize = 32;

        /// <summary>
        /// 0 = drone parado, tiro sai exatamente na mira. 1 = manobrando forte,
        /// o tiro passa longe. O HUD usa isto para abrir o retículo.
        /// </summary>
        public float Instability { get; private set; }

        /// <summary>Verdadeiro quando o tiro sai reto na mira.</summary>
        public bool IsSteady => Instability < 0.05f;

        DroneAim _aim;
        IDroneMotion _motion;
        readonly Stack<Projectile> _pool = new Stack<Projectile>();
        Transform _poolRoot;
        float _cooldown;

        void Awake()
        {
            _aim = GetComponent<DroneAim>();
            _motion = GetComponent<IDroneMotion>();

            _poolRoot = new GameObject("Projeteis").transform;
            for (int i = 0; i < poolSize; i++) _pool.Push(CreateProjectile());
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
            Vector3 aimPoint = _aim != null ? _aim.AimPoint : muzzle + transform.forward * 100f;

            Vector3 direction = (aimPoint - muzzle).normalized;
            Vector3 velocity = direction * projectileSpeed;

            // O desvio: o projetil sai com a velocidade lateral que o drone tinha.
            // Parado, isso e zero e o tiro vai exatamente para o retículo.
            if (_motion != null)
            {
                velocity += _motion.ManeuverVelocity * maneuverDrift;
            }

            Projectile projectile = _pool.Count > 0 ? _pool.Pop() : CreateProjectile();
            projectile.damage = damage;
            projectile.Launch(muzzle, velocity, Recycle);
        }

        void Recycle(Projectile projectile)
        {
            _pool.Push(projectile);
        }

        Projectile CreateProjectile()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Tiro";
            body.transform.SetParent(_poolRoot, false);
            // O projetil e alongado no eixo do movimento: le melhor em alta velocidade.
            body.transform.localScale = new Vector3(projectileSize, projectileSize, projectileSize * 5f);

            Object.Destroy(body.GetComponent<Collider>());

            if (projectileMaterial != null)
            {
                body.GetComponent<MeshRenderer>().sharedMaterial = projectileMaterial;
            }

            var projectile = body.AddComponent<Projectile>();
            body.SetActive(false);
            return projectile;
        }
    }
}
