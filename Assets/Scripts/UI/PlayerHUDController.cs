using UnityEngine;
using UnityEngine.UI;
using RemoteChase.Combat;

namespace RemoteChase.UI
{
    /// <summary>
    /// HUD de vida e bateria com as imagens de preenchimento (Rafael Moura).
    ///
    /// Integracao: na versao original este script era dono da vida -- tinha
    /// currentHealth, assinava OnCrash e aplicava dano. No projeto principal quem
    /// faz isso e o PlayerHealth, que a GameSession ja escuta para a tela de
    /// derrota. Com os dois donos, cada batida tirava dano duas vezes.
    ///
    /// Entao aqui ele virou camada visual: le o PlayerHealth e o DroneEnergy e
    /// so desenha. O layout, os sprites e a logica de fillAmount sao os dele.
    /// </summary>
    [AddComponentMenu("Remote Chase/Player HUD Controller")]
    public class PlayerHUDController : MonoBehaviour
    {
        [Header("Referências Visuais (Arraste os objetos Fill)")]
        [SerializeField] private Image vidaFillImage;
        [SerializeField] private Image energiaFillImage;

        [Header("Fontes de dado")]
        [Tooltip("Integridade do drone. Se vazio, procura na cena.")]
        [SerializeField] private PlayerHealth health;

        [Tooltip("Bateria do drone. Se vazio, procura na cena.")]
        [SerializeField] private DroneEnergy energy;

        /// <summary>Vida atual, para quem ainda consultava o HUD.</summary>
        public float CurrentHealth => health != null ? health.Current : 0f;

        /// <summary>Vida maxima.</summary>
        public float MaxHealth => health != null ? health.maxHealth : 0f;

        /// <summary>Bateria atual.</summary>
        public float CurrentEnergy => energy != null ? energy.Current : 0f;

        /// <summary>Bateria maxima.</summary>
        public float MaxEnergy => energy != null ? energy.maxEnergy : 0f;

        void Awake()
        {
            if (health == null) health = FindAnyObjectByType<PlayerHealth>();
            if (energy == null) energy = FindAnyObjectByType<DroneEnergy>();
        }

        void Update()
        {
            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            if (vidaFillImage != null && health != null)
            {
                vidaFillImage.fillAmount = health.Normalized;
            }

            if (energiaFillImage != null && energy != null)
            {
                energiaFillImage.fillAmount = energy.Normalized;
            }
        }

        /// <summary>Repassa dano para o PlayerHealth. Mantido para quem ja chamava.</summary>
        public void TakeDamage(float amount)
        {
            if (health != null) health.TakeHit(amount, transform.position);
        }

        /// <summary>Repassa cura para o PlayerHealth.</summary>
        public void Heal(float amount)
        {
            if (health != null) health.Heal(amount);
        }

        /// <summary>Repassa recarga para o DroneEnergy.</summary>
        public void AddEnergy(float amount)
        {
            if (energy != null) energy.Recharge(amount);
        }
    }
}
