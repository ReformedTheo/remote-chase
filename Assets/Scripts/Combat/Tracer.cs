using System;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>
    /// Bala visual: um risco curto que viaja do cano ate o ponto de impacto.
    ///
    /// Isto e SO visual. O tiro ja foi resolvido por raycast no frame do disparo
    /// (hitscan), entao a velocidade daqui nao muda nada de gameplay -- ela existe
    /// para a bala parecer uma bala, e nao um feixe continuo ate o alvo.
    /// Vem de um pool: nunca e destruida, so desativada.
    /// </summary>
    [AddComponentMenu("Remote Chase/Tracer")]
    public class Tracer : MonoBehaviour
    {
        [Tooltip("Velocidade visual da bala em m/s. Nao afeta o acerto.")]
        public float visualSpeed = 700f;

        [Tooltip("Comprimento do risco em metros.")]
        public float length = 7f;

        [Tooltip("Espessura do risco.")]
        public float thickness = 0.14f;

        Vector3 _origin;
        Vector3 _direction;
        float _distance;
        float _travelled;
        Action<Tracer> _onDone;

        /// <summary>Lanca a bala visual entre dois pontos do mundo.</summary>
        public void Show(Vector3 from, Vector3 to, Action<Tracer> onDone)
        {
            Vector3 delta = to - from;
            _distance = delta.magnitude;

            if (_distance < 0.01f)
            {
                onDone?.Invoke(this);
                return;
            }

            _origin = from;
            _direction = delta / _distance;
            _travelled = 0f;
            _onDone = onDone;

            transform.rotation = Quaternion.LookRotation(_direction);
            gameObject.SetActive(true);
            Place();
        }

        void Update()
        {
            _travelled += visualSpeed * Time.deltaTime;

            if (_travelled >= _distance + length)
            {
                gameObject.SetActive(false);
                _onDone?.Invoke(this);
                _onDone = null;
                return;
            }

            Place();
        }

        /// <summary>Posiciona o risco entre a cauda e a ponta da bala.</summary>
        void Place()
        {
            // A ponta para no impacto e a cauda continua andando: a bala "entra"
            // no alvo em vez de sumir de uma vez.
            float head = Mathf.Min(_travelled, _distance);
            float tail = Mathf.Clamp(_travelled - length, 0f, _distance);
            float visible = head - tail;

            if (visible <= 0.01f)
            {
                transform.localScale = Vector3.zero;
                return;
            }

            transform.position = _origin + _direction * (tail + visible * 0.5f);
            transform.localScale = new Vector3(thickness, thickness, visible);
        }
    }
}
