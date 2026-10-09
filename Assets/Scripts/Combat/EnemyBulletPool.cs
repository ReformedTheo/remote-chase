using System.Collections.Generic;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>
    /// Pool de tiros inimigos, compartilhado por todos os inimigos da fase.
    /// Fica num objeto so da cena para que nenhum inimigo precise instanciar
    /// nada durante o jogo.
    /// </summary>
    [AddComponentMenu("Remote Chase/Enemy Bullet Pool")]
    public class EnemyBulletPool : MonoBehaviour
    {
        [Tooltip("Quantos tiros o pool cria de uma vez.")]
        public int poolSize = 40;

        [Tooltip("Tamanho do tiro inimigo.")]
        public float bulletSize = 0.9f;

        [Tooltip("Velocidade do tiro inimigo em m/s.")]
        public float bulletSpeed = 55f;

        [Tooltip("Dano por tiro inimigo.")]
        public float bulletDamage = 12f;

        [Tooltip("Material do tiro inimigo.")]
        public Material bulletMaterial;

        readonly Stack<EnemyBullet> _pool = new Stack<EnemyBullet>();
        PlayerHealth _player;

        void Awake()
        {
            _player = FindAnyObjectByType<PlayerHealth>();
            for (int i = 0; i < poolSize; i++) _pool.Push(Create());
        }

        /// <summary>Dispara um tiro inimigo a partir de um ponto, numa direcao.</summary>
        public void Fire(Vector3 position, Vector3 direction)
        {
            EnemyBullet bullet = _pool.Count > 0 ? _pool.Pop() : Create();
            bullet.speed = bulletSpeed;
            bullet.damage = bulletDamage;
            bullet.Launch(position, direction, _player, Recycle);
        }

        void Recycle(EnemyBullet bullet)
        {
            _pool.Push(bullet);
        }

        EnemyBullet Create()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "TiroInimigo";
            body.transform.SetParent(transform, false);
            body.transform.localScale = Vector3.one * bulletSize;

            // Sem collider: o acerto e por distancia.
            Destroy(body.GetComponent<Collider>());

            if (bulletMaterial != null)
            {
                body.GetComponent<MeshRenderer>().sharedMaterial = bulletMaterial;
            }

            var bullet = body.AddComponent<EnemyBullet>();
            body.SetActive(false);
            return bullet;
        }
    }
}
