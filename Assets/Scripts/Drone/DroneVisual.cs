using UnityEngine;

namespace RemoteChase.Drone
{
    /// <summary>
    /// Inclina o corpo do drone (um filho puramente visual) conforme a manobra.
    /// Nao interfere no movimento -- e so leitura de voo para o jogador.
    /// </summary>
    [AddComponentMenu("Remote Chase/Drone Visual")]
    public class DroneVisual : MonoBehaviour
    {
        /// <summary>De onde vem a inclinacao do nariz.</summary>
        public enum PitchSource
        {
            /// <summary>Nariz baixa ao acelerar para frente. Usado no voo livre.</summary>
            ForwardSpeed,

            /// <summary>Nariz sobe ao subir. Usado no trilho, onde o avanco e constante.</summary>
            ClimbRate
        }

        [Tooltip("Objeto que sera inclinado. Se vazio, procura um filho chamado 'Visual'.")]
        public Transform visual;

        [Tooltip("O que faz o nariz subir ou descer.")]
        public PitchSource pitchSource = PitchSource.ForwardSpeed;

        [Tooltip("Inclinacao maxima (graus) para frente/tras e para os lados.")]
        public float maxTilt = 28f;

        [Tooltip("Velocidade de manobra (m/s) que corresponde a inclinacao maxima.")]
        public float tiltReferenceSpeed = 16f;

        [Tooltip("Velocidade com que o corpo alcanca a inclinacao alvo.")]
        public float tiltResponse = 6f;

        [Header("Helices")]
        [Tooltip("Giro visual das helices em graus/s. 0 desliga.")]
        public float rotorSpeed = 900f;

        [Tooltip("Objetos que giram como helices. Opcional.")]
        public Transform[] rotors;

        IDroneMotion _motion;

        void Awake()
        {
            _motion = GetComponent<IDroneMotion>();

            if (visual == null)
            {
                visual = transform.Find("Visual");
            }
        }

        void LateUpdate()
        {
            if (visual == null || _motion == null) return;

            // Velocidade de manobra no referencial do drone.
            Vector3 maneuver = transform.InverseTransformDirection(_motion.ManeuverVelocity);
            float reference = Mathf.Max(tiltReferenceSpeed, 0.01f);

            float pitchInput = pitchSource == PitchSource.ForwardSpeed
                ? maneuver.z          // acelerou para frente -> nariz para baixo
                : -maneuver.y;        // subiu -> nariz para cima

            float pitch = Mathf.Clamp(pitchInput / reference, -1f, 1f) * maxTilt;
            float roll = Mathf.Clamp(-maneuver.x / reference, -1f, 1f) * maxTilt;

            Quaternion target = Quaternion.Euler(pitch, 0f, roll);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target,
                                                    1f - Mathf.Exp(-tiltResponse * Time.deltaTime));

            SpinRotors();
        }

        void SpinRotors()
        {
            if (rotors == null || rotorSpeed <= 0f) return;

            float boost = _motion.IsBoosting ? 1.6f : 1f;
            float step = rotorSpeed * boost * Time.deltaTime;

            for (int i = 0; i < rotors.Length; i++)
            {
                if (rotors[i] == null) continue;
                // Helices adjacentes giram em sentidos opostos, como num quadricoptero.
                float direction = (i % 2 == 0) ? 1f : -1f;
                rotors[i].Rotate(Vector3.up, step * direction, Space.Self);
            }
        }
    }
}
