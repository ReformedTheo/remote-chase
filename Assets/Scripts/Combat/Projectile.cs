using System;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>Algo que pode levar tiro. Os inimigos vao implementar isto.</summary>
    public interface IHittable
    {
        void TakeHit(float damage, Vector3 point);
    }

    /// <summary>
    /// Projetil em linha reta, com deteccao de impacto por raycast do trecho
    /// percorrido no frame -- assim ele nao atravessa um predio em alta velocidade.
    /// Vem de um pool: nunca e destruido, so desativado.
    /// </summary>
    [AddComponentMenu("Remote Chase/Projectile")]
    public class Projectile : MonoBehaviour
    {
        [Tooltip("Tempo de vida em segundos antes de sumir sozinho.")]
        public float lifetime = 2.5f;

        [Tooltip("Camadas em que o tiro bate.")]
        public LayerMask hitLayers = ~0;

        [Tooltip("Dano entregue a quem implementa IHittable.")]
        public float damage = 1f;

        Vector3 _velocity;
        float _age;
        Action<Projectile> _onDespawn;

        /// <summary>Coloca o projetil em movimento.</summary>
        public void Launch(Vector3 position, Vector3 velocity, Action<Projectile> onDespawn)
        {
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(velocity.normalized));
            _velocity = velocity;
            _age = 0f;
            _onDespawn = onDespawn;
            gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 step = _velocity * dt;
            float distance = step.magnitude;

            if (distance > 0f
                && Physics.Raycast(transform.position, step / distance, out RaycastHit hit, distance,
                                   hitLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.TryGetComponent(out IHittable target))
                {
                    target.TakeHit(damage, hit.point);
                }

                transform.position = hit.point;
                Despawn();
                return;
            }

            transform.position += step;

            _age += dt;
            if (_age >= lifetime) Despawn();
        }

        void Despawn()
        {
            gameObject.SetActive(false);
            _onDespawn?.Invoke(this);
            _onDespawn = null;
        }
    }
}
