using System;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>Coletaveis de pista. Os tipos Reparo e Energia vem do PowerUp do Rafael.</summary>
    public enum PickupKind
    {
        /// <summary>Celula de Reparo Nanotecnologico: devolve integridade.</summary>
        Reparo,

        /// <summary>Celula de Energia: recarrega a bateria do drone.</summary>
        Energia,

        /// <summary>Celula de Turbo: escudo defletor temporario.</summary>
        Escudo
    }

    /// <summary>
    /// Item coletavel de pista.
    ///
    /// Nao tem collider nenhum, de proposito: o drone detecta batida por
    /// OnTriggerEnter, entao um collider aqui faria coletar um item contar como
    /// bater num predio. A coleta e por distancia.
    /// </summary>
    [AddComponentMenu("Remote Chase/Pickup")]
    public class Pickup : MonoBehaviour
    {
        [Tooltip("O que este item faz ao ser coletado.")]
        public PickupKind kind = PickupKind.Reparo;

        [Tooltip("Integridade devolvida pela celula de reparo.")]
        public float healAmount = 25f;

        [Tooltip("Bateria recarregada pela celula de energia.")]
        public float energyAmount = 30f;

        [Tooltip("Segundos de escudo defletor da celula de turbo.")]
        public float shieldDuration = 5f;

        [Tooltip("Distancia em que o item e coletado.")]
        public float pickupRadius = 2.6f;

        [Tooltip("Velocidade de giro, so visual.")]
        public float spinSpeed = 120f;

        [Tooltip("Quantos metros sobe e desce.")]
        public float bobAmplitude = 0.8f;

        /// <summary>Disparado ao ser coletado.</summary>
        public event Action<Pickup> OnCollected;

        /// <summary>Alvo que pode coletar.</summary>
        public PlayerHealth Player { get; set; }

        /// <summary>Bateria do alvo, usada pela celula de energia.</summary>
        public DroneEnergy Energy { get; set; }

        Vector3 _anchor;
        float _phase;
        bool _available;

        /// <summary>Coloca o item em jogo na posicao local dada.</summary>
        public void Spawn(Vector3 localPosition, float phase)
        {
            _anchor = localPosition;
            _phase = phase;
            _available = true;

            transform.localPosition = localPosition;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!_available) return;

            Vector3 position = _anchor;
            position.y += Mathf.Sin(Time.time * 2f + _phase) * bobAmplitude;
            transform.localPosition = position;
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

            TryCollect();
        }

        void TryCollect()
        {
            if (Player == null) return;

            Vector3 delta = Player.transform.position - transform.position;
            if (delta.sqrMagnitude > pickupRadius * pickupRadius) return;

            if (!Collect()) return;

            _available = false;
            gameObject.SetActive(false);
            OnCollected?.Invoke(this);
        }

        /// <summary>Aplica o efeito. Devolve falso se o item nao serve agora.</summary>
        bool Collect()
        {
            switch (kind)
            {
                case PickupKind.Escudo:
                    Player.GrantInvulnerability(shieldDuration);
                    return true;

                case PickupKind.Energia:
                    if (Energy == null || Energy.Normalized >= 1f) return false;
                    Energy.Recharge(energyAmount);
                    return true;

                default:
                    // Integridade cheia: o reparo fica para tras em vez de
                    // ser desperdicado.
                    if (Player.Normalized >= 1f) return false;
                    Player.Heal(healAmount);
                    return true;
            }
        }
    }
}
