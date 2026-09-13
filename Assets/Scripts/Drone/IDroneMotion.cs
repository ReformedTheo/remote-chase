using System;
using UnityEngine;

namespace RemoteChase.Drone
{
    /// <summary>
    /// O que a camera, o HUD e o corpo visual precisam saber do drone, independente
    /// de ele estar em voo livre (DroneController) ou no trilho (DroneRailController).
    /// </summary>
    public interface IDroneMotion
    {
        /// <summary>Velocidade total em m/s.</summary>
        float Speed { get; }

        /// <summary>Velocidade em coordenadas do mundo.</summary>
        Vector3 Velocity { get; }

        /// <summary>
        /// Velocidade de manobra: so o que o jogador comandou agora
        /// (X = lado, Y = subir/descer, Z = frente). E o que inclina o corpo.
        /// </summary>
        Vector3 ManeuverVelocity { get; }

        /// <summary>Altura em relacao ao chao.</summary>
        float Altitude { get; }

        /// <summary>Turbo ativo.</summary>
        bool IsBoosting { get; }

        /// <summary>Disparado ao bater. Parametro = velocidade do impacto em m/s.</summary>
        event Action<float> OnCrash;

        /// <summary>Devolve o drone ao estado inicial.</summary>
        void ResetToSpawn();
    }
}
