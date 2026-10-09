using System;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>
    /// Integridade do drone do jogador. Recebe dano de tiro inimigo e de batida
    /// em predio, e e restaurada pelos coletaveis.
    /// </summary>
    [AddComponentMenu("Remote Chase/Player Health")]
    public class PlayerHealth : MonoBehaviour, IHittable
    {
        [Tooltip("Integridade maxima.")]
        public float maxHealth = 100f;

        [Tooltip("Tempo (s) sem receber dano depois de um golpe. Evita morrer "
               + "instantaneamente ao raspar num predio.")]
        public float invulnerabilityTime = 0.7f;

        [Tooltip("Dano por metro por segundo de impacto ao bater em algo solido.")]
        public float crashDamagePerSpeed = 0.7f;

        /// <summary>Integridade atual.</summary>
        public float Current { get; private set; }

        /// <summary>Integridade de 0 a 1, para a barra do HUD.</summary>
        public float Normalized => maxHealth > 0f ? Mathf.Clamp01(Current / maxHealth) : 0f;

        /// <summary>Verdadeiro enquanto o drone aguenta.</summary>
        public bool IsAlive => Current > 0f;

        /// <summary>Piscando depois de levar dano.</summary>
        public bool IsInvulnerable => _invulnerability > 0f;

        /// <summary>Disparado a cada dano recebido. Parametro = dano aplicado.</summary>
        public event Action<float> OnDamaged;

        /// <summary>Disparado a cada cura. Parametro = quanto curou.</summary>
        public event Action<float> OnHealed;

        /// <summary>Disparado quando a integridade chega a zero.</summary>
        public event Action OnDied;

        float _invulnerability;

        void Awake()
        {
            Current = maxHealth;
        }

        void OnEnable()
        {
            // A batida em predio tambem custa integridade.
            var rail = GetComponent<Drone.DroneRailController>();
            if (rail != null) rail.OnCrash += HandleCrash;
        }

        void OnDisable()
        {
            var rail = GetComponent<Drone.DroneRailController>();
            if (rail != null) rail.OnCrash -= HandleCrash;
        }

        void Update()
        {
            if (_invulnerability > 0f) _invulnerability -= Time.deltaTime;
        }

        void HandleCrash(float impactSpeed)
        {
            TakeHit(impactSpeed * crashDamagePerSpeed, transform.position);
        }

        /// <inheritdoc/>
        public void TakeHit(float damage, Vector3 point)
        {
            if (!IsAlive || IsInvulnerable || damage <= 0f) return;

            Current = Mathf.Max(Current - damage, 0f);
            _invulnerability = invulnerabilityTime;

            OnDamaged?.Invoke(damage);

            if (Current <= 0f) OnDied?.Invoke();
        }

        /// <summary>
        /// Deixa o drone imune por um tempo. Usado pelo dash e pela celula de
        /// escudo. Nunca encurta uma imunidade que ja esteja valendo mais tempo.
        /// </summary>
        public void GrantInvulnerability(float seconds)
        {
            _invulnerability = Mathf.Max(_invulnerability, seconds);
        }

        /// <summary>Segundos restantes de imunidade.</summary>
        public float InvulnerabilityLeft => Mathf.Max(_invulnerability, 0f);

        /// <summary>Recupera integridade, sem passar do maximo.</summary>
        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            float before = Current;
            Current = Mathf.Min(Current + amount, maxHealth);

            float healed = Current - before;
            if (healed > 0f) OnHealed?.Invoke(healed);
        }
    }
}
