using System;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>
    /// Tiro inimigo: uma esfera que viaja de verdade, para dar tempo de desviar.
    /// O tiro do jogador e hitscan; o do inimigo nao, de proposito -- se fosse
    /// instantaneo nao haveria jogo, so dano inevitavel.
    ///
    /// Vem do <see cref="EnemyBulletPool"/>: nunca e destruido, so desativado.
    /// </summary>
    [AddComponentMenu("Remote Chase/Enemy Bullet")]
    public class EnemyBullet : MonoBehaviour
    {
        [Tooltip("Velocidade em m/s.")]
        public float speed = 55f;

        [Tooltip("Dano causado ao drone.")]
        public float damage = 12f;

        [Tooltip("Tempo de vida em segundos.")]
        public float lifetime = 4f;

        [Tooltip("Distancia do drone em que o tiro conta como acerto.")]
        public float hitRadius = 1.8f;

        Vector3 _direction;
        float _age;
        Transform _player;
        PlayerHealth _health;
        Action<EnemyBullet> _onDone;

        /// <summary>Coloca o tiro em movimento.</summary>
        public void Launch(Vector3 position, Vector3 direction, PlayerHealth target,
                           Action<EnemyBullet> onDone)
        {
            transform.position = position;
            _direction = direction.normalized;
            _health = target;
            _player = target != null ? target.transform : null;
            _age = 0f;
            _onDone = onDone;
            gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            transform.position += _direction * (speed * dt);

            // Acerto por distancia, nao por collider: o drone detecta batida com
            // OnTriggerEnter, entao um collider aqui contaria como bater em predio.
            if (_player != null
                && (_player.position - transform.position).sqrMagnitude < hitRadius * hitRadius)
            {
                if (_health != null) _health.TakeHit(damage, transform.position);
                Despawn();
                return;
            }

            _age += dt;
            if (_age >= lifetime) Despawn();
        }

        void Despawn()
        {
            gameObject.SetActive(false);
            _onDone?.Invoke(this);
            _onDone = null;
        }
    }
}
