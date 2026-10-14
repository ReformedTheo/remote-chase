using System;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>
    /// Bateria do drone: o combustivel que o GDR cita na referencia ao Zaxxon.
    /// Drena sozinha durante a fase e e recarregada pelas celulas de energia
    /// coletadas em pista. Zerar e condicao de derrota.
    ///
    /// Origem: a mecanica e a barra sao do PlayerHUDController do Rafael, onde
    /// a bateria vivia junto com a vida. Virou sistema proprio para a GameSession
    /// poder tratar "bateria acabou" como derrota, igual a vida.
    ///
    /// Rebalanceada: no original drenava 4/s, ou seja 25 s de autonomia para uma
    /// fase de 45 a 60 s -- acabava sempre antes do fim, independente do jogador.
    /// O valor padrao daqui cobre a fase inteira com folga apertada, de modo que
    /// coletar celulas seja util sem ser obrigatorio a cada 20 segundos.
    /// </summary>
    [AddComponentMenu("Remote Chase/Drone Energy")]
    public class DroneEnergy : MonoBehaviour
    {
        [Tooltip("Capacidade maxima da bateria.")]
        public float maxEnergy = 100f;

        [Tooltip("Consumo por segundo. Em 1.2, a carga cheia dura ~83 s, "
               + "contra os 45 a 60 s que a fase leva.")]
        public float drainPerSecond = 1.2f;

        [Tooltip("Consumo extra por segundo enquanto o drone manobra, proporcional "
               + "a velocidade de manobra. Voar na linha reta gasta menos.")]
        public float drainPerManeuver = 0.06f;

        /// <summary>Carga atual.</summary>
        public float Current { get; private set; }

        /// <summary>Carga de 0 a 1, para a barra do HUD.</summary>
        public float Normalized => maxEnergy > 0f ? Mathf.Clamp01(Current / maxEnergy) : 0f;

        /// <summary>Ainda ha carga.</summary>
        public bool HasCharge => Current > 0f;

        /// <summary>Disparado quando a bateria zera.</summary>
        public event Action OnDepleted;

        /// <summary>Disparado ao recarregar. Parametro = quanto entrou.</summary>
        public event Action<float> OnRecharged;

        Drone.IDroneMotion _motion;
        bool _depleted;

        void Awake()
        {
            Current = maxEnergy;
            _motion = GetComponent<Drone.IDroneMotion>();
        }

        void Update()
        {
            if (_depleted) return;

            float drain = drainPerSecond;

            if (_motion != null)
            {
                drain += _motion.ManeuverVelocity.magnitude * drainPerManeuver;
            }

            Current = Mathf.Max(Current - drain * Time.deltaTime, 0f);

            if (Current > 0f) return;

            _depleted = true;
            OnDepleted?.Invoke();
        }

        /// <summary>Recarrega a bateria, sem passar do maximo.</summary>
        public void Recharge(float amount)
        {
            if (amount <= 0f || _depleted) return;

            float before = Current;
            Current = Mathf.Min(Current + amount, maxEnergy);

            float gained = Current - before;
            if (gained > 0f) OnRecharged?.Invoke(gained);
        }

        /// <summary>Devolve a bateria ao estado inicial.</summary>
        public void Reset()
        {
            Current = maxEnergy;
            _depleted = false;
        }
    }
}
