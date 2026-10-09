using System;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>
    /// Inimigo esferico que morre com um unico tiro. Fica em vaivem no plano do
    /// corredor e atira no drone quando ele se aproxima.
    ///
    /// Vem do pool do CorridorGenerator: ao morrer so se desativa, e volta quando
    /// o segmento e reciclado.
    ///
    /// O collider NAO e trigger de proposito -- o raycast do tiro do jogador ignora
    /// triggers, entao um inimigo em trigger seria impossivel de acertar.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    [AddComponentMenu("Remote Chase/Enemy")]
    public class Enemy : MonoBehaviour, IHittable
    {
        [Header("Movimento")]
        [Tooltip("Quantos metros o inimigo percorre para os lados. 0 deixa parado.")]
        public float strafeAmplitude = 5f;

        [Tooltip("Velocidade do vaivem lateral.")]
        public float strafeSpeed = 0.8f;

        [Tooltip("Quantos metros o inimigo sobe e desce.")]
        public float bobAmplitude = 2.2f;

        [Tooltip("Velocidade do sobe e desce. Diferente da lateral de proposito: "
               + "as duas juntas fazem um oito, mais dificil de acertar que uma reta.")]
        public float bobSpeed = 1.3f;

        [Header("Ataque")]
        [Tooltip("Distancia (m) em que o inimigo comeca a atirar no drone.")]
        public float attackRange = 120f;

        [Tooltip("Segundos entre um tiro e outro.")]
        public float fireInterval = 1.6f;

        [Tooltip("Erro de mira em metros. Maior = mais facil de desviar.")]
        public float aimError = 2.5f;

        [Header("Permanencia")]
        [Tooltip("Segundos em cena antes de desistir e sumir sozinho. 0 = fica para sempre.")]
        public float lifetime = 7f;

        [Header("Morte")]
        [Tooltip("Duracao (s) do encolhimento ao morrer. 0 some na hora.")]
        public float deathDuration = 0.14f;

        /// <summary>Disparado quando o inimigo e abatido. Parametro = onde levou o tiro.</summary>
        public event Action<Enemy, Vector3> OnKilled;

        /// <summary>Falso enquanto ele esta morrendo ou ja morto.</summary>
        public bool IsAlive { get; private set; }

        /// <summary>Pool de onde saem os tiros deste inimigo.</summary>
        public EnemyBulletPool BulletPool { get; set; }

        /// <summary>Alvo dos tiros.</summary>
        public Transform Player { get; set; }

        Vector3 _anchor;
        Vector3 _baseScale;
        float _phase;
        float _deathTimer;
        float _fireTimer;
        float _lifeTimer;
        Collider _collider;

        void Awake()
        {
            _collider = GetComponent<Collider>();
            _baseScale = transform.localScale;
        }

        /// <summary>Coloca o inimigo em jogo na posicao local dada.</summary>
        public void Spawn(Vector3 localPosition, float phase)
        {
            _anchor = localPosition;
            _phase = phase;
            _deathTimer = 0f;
            _lifeTimer = lifetime;
            // Primeiro tiro sai escalonado, para varios inimigos nao atirarem juntos.
            _fireTimer = fireInterval * (0.3f + phase * 0.1f);
            IsAlive = true;

            transform.localPosition = localPosition;
            transform.localScale = _baseScale;
            if (_collider != null) _collider.enabled = true;
            gameObject.SetActive(true);
        }

        /// <inheritdoc/>
        public void TakeHit(float damage, Vector3 point)
        {
            if (!IsAlive) return;

            IsAlive = false;
            _deathTimer = deathDuration;

            // Some da fisica na hora: nao da para acertar duas vezes nem bater
            // num inimigo que ja morreu.
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

            Move();
            TryFire();
            TryExpire();
        }

        /// <summary>
        /// Some sozinho depois de um tempo. Nao conta como abate: o alvo
        /// simplesmente saiu de cena, e o jogador perdeu a chance.
        /// </summary>
        void TryExpire()
        {
            if (lifetime <= 0f) return;

            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer > 0f) return;

            IsAlive = false;
            gameObject.SetActive(false);
        }

        void Move()
        {
            Vector3 position = _anchor;
            position.x += Mathf.Sin(Time.time * strafeSpeed + _phase) * strafeAmplitude;
            position.y += Mathf.Sin(Time.time * bobSpeed + _phase * 1.7f) * bobAmplitude;
            transform.localPosition = position;
        }

        void TryFire()
        {
            if (BulletPool == null || Player == null) return;

            Vector3 toPlayer = Player.position - transform.position;

            // So atira quando o drone esta perto e ainda vindo -- atirar nas costas
            // de quem ja passou so gasta tiro.
            if (toPlayer.z > 0f || toPlayer.sqrMagnitude > attackRange * attackRange) return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer > 0f) return;

            _fireTimer = fireInterval;

            Vector3 aim = Player.position
                        + new Vector3(UnityEngine.Random.Range(-aimError, aimError),
                                      UnityEngine.Random.Range(-aimError, aimError),
                                      0f);

            BulletPool.Fire(transform.position, aim - transform.position);
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
