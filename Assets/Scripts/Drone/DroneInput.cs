using UnityEngine;
using UnityEngine.InputSystem;

namespace RemoteChase.Drone
{
    /// <summary>
    /// Estado de input do drone em um frame, ja normalizado em [-1, 1].
    /// </summary>
    public struct DroneInput
    {
        /// <summary>X = strafe (esquerda/direita), Y = frente/tras.</summary>
        public Vector2 Move;

        /// <summary>Giro em torno do eixo Y. Positivo = horario.</summary>
        public float Yaw;

        /// <summary>Subir (positivo) ou descer (negativo).</summary>
        public float Lift;

        /// <summary>Turbo segurado.</summary>
        public bool Boost;

        /// <summary>Gatilho de tiro segurado.</summary>
        public bool Fire;

        public static readonly DroneInput None = default;
    }

    /// <summary>
    /// Le teclado, mouse e gamepad direto do Input System e devolve um <see cref="DroneInput"/>.
    /// Fica isolado aqui de proposito: se um dia o projeto passar a usar o asset
    /// InputSystem_Actions, basta trocar o corpo de <see cref="Read"/>.
    /// </summary>
    [System.Serializable]
    public class DroneInputReader
    {
        [Tooltip("Quanto o mouse contribui para o giro. 0 desliga o mouse.")]
        public float MouseYawSensitivity = 0.08f;

        [Tooltip("Tempo (s) para um eixo ir de 0 ate 1. Maior = comandos mais moles.")]
        public float AxisSmoothing = 0.12f;

        Vector2 _move;
        float _yaw;
        float _lift;

        /// <summary>Le o estado atual dos dispositivos. Chamar uma vez por frame.</summary>
        public DroneInput Read(float deltaTime)
        {
            Vector2 rawMove = Vector2.zero;
            float rawYaw = 0f;
            float rawLift = 0f;
            bool boost = false;
            bool fire = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) rawMove.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) rawMove.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) rawMove.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) rawMove.x -= 1f;

                if (keyboard.eKey.isPressed) rawYaw += 1f;
                if (keyboard.qKey.isPressed) rawYaw -= 1f;

                if (keyboard.spaceKey.isPressed) rawLift += 1f;
                if (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed) rawLift -= 1f;

                boost |= keyboard.leftShiftKey.isPressed;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (MouseYawSensitivity > 0f)
                {
                    rawYaw += mouse.delta.ReadValue().x * MouseYawSensitivity;
                }

                fire |= mouse.leftButton.isPressed;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                rawMove += gamepad.leftStick.ReadValue();
                rawYaw += gamepad.rightStick.ReadValue().x;
                rawLift += gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();
                boost |= gamepad.buttonSouth.isPressed;
                fire |= gamepad.rightShoulder.isPressed;
            }

            rawMove = Vector2.ClampMagnitude(rawMove, 1f);
            rawYaw = Mathf.Clamp(rawYaw, -1f, 1f);
            rawLift = Mathf.Clamp(rawLift, -1f, 1f);

            // Suavizacao: o comando nao "liga" instantaneamente, o que ja da
            // parte da sensacao de peso/inercia antes mesmo da fisica.
            float step = AxisSmoothing > 0f ? deltaTime / AxisSmoothing : 1f;
            _move = Vector2.MoveTowards(_move, rawMove, step);
            _yaw = Mathf.MoveTowards(_yaw, rawYaw, step);
            _lift = Mathf.MoveTowards(_lift, rawLift, step);

            return new DroneInput
            {
                Move = _move,
                Yaw = _yaw,
                Lift = _lift,
                Boost = boost,
                Fire = fire
            };
        }

        /// <summary>Zera os eixos suavizados (usar ao pausar ou reposicionar o drone).</summary>
        public void Reset()
        {
            _move = Vector2.zero;
            _yaw = 0f;
            _lift = 0f;
        }
    }
}
