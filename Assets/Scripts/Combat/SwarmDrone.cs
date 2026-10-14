using System;
using RemoteChase.Drone;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>
    /// Drone Enxame (kamikaze), o segundo tipo de inimigo do GDR: unidade leve
    /// que mergulha em trajetoria direta contra o jogador. O ataque dele e a
    /// propria colisao -- nao atira.
    ///
    /// Diferenca estrutural para o Sentinela: o Sentinela e filho do
    /// EnemySpawner, que viaja com o drone, entao fica parado na tela. Este
    /// vive em espaco de mundo e calcula a propria velocidade como
    /// "acompanhar o trilho + fechar a distancia", para que a aproximacao seja
    /// a mesma em 26 ou em 95 m/s. Se fosse filho do spawner, o movimento do
    /// referencial somaria ao dele e a velocidade de mergulho dobraria.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    [AddComponentMenu("Remote Chase/Swarm Drone")]
    public class SwarmDrone : MonoBehaviour, IHittable
    {
        [Header("Mergulho")]
        [Tooltip("Velocidade com que fecha a distancia ate o drone, em m/s. "
               + "E relativa: ja descontado o avanco do trilho.")]
        public float closeSpeed = 26f;

        [Tooltip("Quao rapido ele corrige a mira enquanto mergulha. Baixo = da "
               + "para desviar no ultimo segundo; alto = persegue sem perdoar.")]
        [Range(0.5f, 10f)] public float steerResponse = 2.4f;

        [Tooltip("Balanco lateral durante a aproximacao, em metros. Dificulta o tiro.")]
        public float weaveAmplitude = 3f;

        [Tooltip("Velocidade do balanco lateral.")]
        public float weaveSpeed = 3.2f;

        [Header("Impacto")]
        [Tooltip("Distancia do drone em que a colisao acontece.")]
        public float hitRadius = 2.2f;

        [Tooltip("Dano causado ao se chocar com o jogador.")]
        public float impactDamage = 18f;

        [Header("Permanencia")]
        [Tooltip("Quantos metros atras do jogador ele desiste e some.")]
        public float despawnBehind = 25f;

        [Tooltip("Tempo maximo em cena, em segundos.")]
        public float lifetime = 12f;

        [Header("Morte")]
        [Tooltip("Duracao (s) do encolhimento ao morrer.")]
        public float deathDuration = 0.12f;

        /// <summary>Disparado ao ser abatido pelo jogador.</summary>
        public event Action<SwarmDrone, Vector3> OnKilled;

        /// <summary>Vivo e mergulhando.</summary>
        public bool IsAlive { get; private set; }

        /// <summary>Drone do jogador, alvo do mergulho.</summary>
        public DroneRailController Target { get; set; }

        /// <summary>Integridade do jogador, para o dano de impacto.</summary>
        public PlayerHealth TargetHealth { get; set; }

        Vector3 _baseScale;
        Vector3 _velocity;
        float _deathTimer;
        float _lifeTimer;
        float _phase;
        Collider _collider;

        void Awake()
        {
            _collider = GetComponent<Collider>();
            _baseScale = transform.localScale;
        }

        /// <summary>Coloca o enxame em cena numa posicao de mundo.</summary>
        public void Spawn(Vector3 worldPosition, float phase)
        {
            transform.position = worldPosition;
            transform.localScale = _baseScale;

            _phase = phase;
            _deathTimer = 0f;
            _lifeTimer = lifetime;
            _velocity = Vector3.forward * (Target != null ? Target.ForwardSpeed : 0f);
            IsAlive = true;

            if (_collider != null) _collider.enabled = true;
            gameObject.SetActive(true);
        }

        /// <inheritdoc/>
        public void TakeHit(float damage, Vector3 point)
        {
            if (!IsAlive) return;

            IsAlive = false;
            _deathTimer = deathDuration;
            if (_collider != null) _collider.enabled = false;

            OnKilled?.Invoke(this, point);

            if (deathDuration <= 0f) gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsAlive)
            {
                Shrink();
                return;
            }

            if (Target == null)
            {
                gameObject.SetActive(false);
                return;
            }

            Dive();
            CheckImpact();
            CheckExpiry();
        }

        void Dive()
        {
            float dt = Time.deltaTime;
            Vector3 toTarget = Target.transform.position - transform.position;

            // Velocidade desejada: acompanha o trilho e, por cima disso, fecha a
            // distancia ate o jogador. Assim o mergulho tem o mesmo ritmo no
            // comeco e no fim da fase, mesmo com o trilho acelerando.
            Vector3 desired = Vector3.forward * Target.ForwardSpeed
                            + toTarget.normalized * closeSpeed;

            // Zigue-zague perpendicular ao mergulho, para nao ser um alvo em linha reta.
            Vector3 side = Vector3.Cross(toTarget.normalized, Vector3.up).normalized;
            desired += side * (Mathf.Sin(Time.time * weaveSpeed + _phase) * weaveAmplitude);

            _velocity = Vector3.Lerp(_velocity, desired, 1f - Mathf.Exp(-steerResponse * dt));
            transform.position += _velocity * dt;

            if (_velocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(_velocity);
            }
        }

        void CheckImpact()
        {
            Vector3 toTarget = Target.transform.position - transform.position;
            if (toTarget.sqrMagnitude > hitRadius * hitRadius) return;

            if (TargetHealth != null)
            {
                TargetHealth.TakeHit(impactDamage, transform.position);
            }

            // O kamikaze se destroi no impacto, mas isso nao conta como abate:
            // quem acertou foi ele.
            IsAlive = false;
            gameObject.SetActive(false);
        }

        void CheckExpiry()
        {
            _lifeTimer -= Time.deltaTime;

            bool passed = transform.position.z < Target.transform.position.z - despawnBehind;
            if (!passed && _lifeTimer > 0f) return;

            IsAlive = false;
            gameObject.SetActive(false);
        }

        void Shrink()
        {
            if (_deathTimer <= 0f) return;

            _deathTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(_deathTimer / Mathf.Max(deathDuration, 0.0001f));
            transform.localScale = _baseScale * t;

            if (_deathTimer <= 0f) gameObject.SetActive(false);
        }
    }
}
