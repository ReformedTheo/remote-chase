using UnityEngine;
using UnityEngine.UI;
using RemoteChase.Drone;

namespace RemoteChase.UI
{
    [AddComponentMenu("Remote Chase/Player HUD Controller")]
    public class PlayerHUDController : MonoBehaviour
    {
        [Header("Referências Visuais (Arraste os objetos Fill)")]
        [SerializeField] private Image vidaFillImage;
        [SerializeField] private Image energiaFillImage;

        [Header("Atributos de Vida")]
        [SerializeField] private float maxHealth = 100f;
        private float currentHealth;

        [Header("Atributos de Bateria / Energia")]
        [SerializeField] private float maxEnergy = 100f;
        private float currentEnergy;
        [SerializeField] private float batteryDrainRate = 4f;

        [Header("Configuração de Dano por Batida")]
        [Tooltip("Multiplicador aplicado à velocidade de impacto para definir o dano na vida.")]
        [SerializeField] private float crashDamageMultiplier = 2.5f;

        private IDroneMotion _droneMotion;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentEnergy => currentEnergy;
        public float MaxEnergy => maxEnergy;

        void Awake()
        {
            currentHealth = maxHealth;
            currentEnergy = maxEnergy;

            // Procura o componente de movimento do drone no mesmo objeto ou na cena
            _droneMotion = GetComponent<IDroneMotion>();
            if (_droneMotion == null) _droneMotion = FindAnyObjectByType<DroneController>();
            if (_droneMotion == null) _droneMotion = FindAnyObjectByType<DroneRailController>();
        }

        void OnEnable()
        {
            // Subscreve ao evento de batida existente na arquitetura do drone
            if (_droneMotion != null)
            {
                _droneMotion.OnCrash += HandleCrash;
            }
        }

        void OnDisable()
        {
            // Remove a subscrição para evitar erros de memória
            if (_droneMotion != null)
            {
                _droneMotion.OnCrash -= HandleCrash;
            }
        }

        void Update()
        {
            if (currentEnergy > 0f)
            {
                currentEnergy -= batteryDrainRate * Time.deltaTime;
                currentEnergy = Mathf.Max(currentEnergy, 0f);
            }

            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            if (vidaFillImage != null)
            {
                vidaFillImage.fillAmount = currentHealth / maxHealth;
            }

            if (energiaFillImage != null)
            {
                energiaFillImage.fillAmount = currentEnergy / maxEnergy;
            }
        }

        /// <summary>
        /// Acionado automaticamente sempre que o drone sofre uma batida física ou no trilho.
        /// </summary>
        void HandleCrash(float impactSpeed)
        {
            float damage = impactSpeed * crashDamageMultiplier;
            TakeDamage(damage);
            Debug.Log($"Batida detetada! Velocidade: {impactSpeed:0.0} m/s | Dano aplicado: {damage:0.0}");
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;

            currentHealth -= amount;
            currentHealth = Mathf.Max(currentHealth, 0f);

            if (currentHealth <= 0f)
            {
                HandlePlayerDeath();
            }
        }

        public void AddEnergy(float amount)
        {
            if (amount <= 0f) return;
            currentEnergy += amount;
            currentEnergy = Mathf.Min(currentEnergy, maxEnergy);
        }

        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            currentHealth += amount;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }

        void HandlePlayerDeath()
        {
            Debug.Log("O drone foi destruído! Game Over.");
        }
    }
}
