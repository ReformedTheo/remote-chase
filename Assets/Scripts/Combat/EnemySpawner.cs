using System.Collections.Generic;
using RemoteChase.Drone;
using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>
    /// Campo de alvos no estilo Aim Lab, conforme o Drone Sentinela do GDR.
    ///
    /// O truque esta no parentesco: este objeto acompanha o Z do drone a cada
    /// frame, e os inimigos sao filhos dele com Z local fixo. Como o referencial
    /// inteiro viaja junto com o jogador, os alvos ficam parados na tela sem
    /// precisar sincronizar velocidade nenhuma -- se o drone acelera de 26 para
    /// 95 m/s, eles acompanham sozinhos.
    ///
    /// A amplitude do vaivem lateral cresce com o avanco da fase: no comeco os
    /// alvos sao estaticos (treino de mira), no fim ficam moveis.
    /// </summary>
    [AddComponentMenu("Remote Chase/Enemy Spawner")]
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Alvo")]
        [Tooltip("Drone do jogador. Se vazio, procura na cena.")]
        public DroneRailController drone;

        [Header("Spawn")]
        [Tooltip("Quantos inimigos no maximo ao mesmo tempo.")]
        public int maxAlive = 4;

        [Tooltip("Segundos entre dois surgimentos.")]
        public float spawnInterval = 1.1f;

        [Tooltip("A que distancia a frente do drone o inimigo aparece.")]
        public float spawnDistance = 85f;

        [Tooltip("Segundos que o alvo fica em cena antes de desistir e sumir.")]
        public float lifetime = 7f;

        [Header("Area de combate")]
        [Tooltip("Centro da area de spawn (X, altura). Deve bater com o trilho do drone.")]
        public Vector2 areaCenter = new Vector2(0f, 14f);

        [Tooltip("Meia largura e meia altura da area. Manter dentro do alcance de "
               + "manobra do drone, senao nasce alvo inalcancavel.")]
        public Vector2 areaHalfExtents = new Vector2(11f, 6f);

        [Header("Progressao do desafio")]
        [Tooltip("Amplitude do vaivem lateral no comeco da fase. 0 = alvo parado.")]
        public float strafeAtStart = 0f;

        [Tooltip("Amplitude do vaivem lateral no fim da fase.")]
        public float strafeAtEnd = 7f;

        [Tooltip("Amplitude do sobe e desce no fim da fase.")]
        public float bobAtEnd = 3f;

        [Header("Visual")]
        [Tooltip("Diametro do inimigo.")]
        public float enemySize = 2.6f;

        [Tooltip("Material do inimigo.")]
        public Material enemyMaterial;

        [Tooltip("Prefab do drone inimigo. Vazio = esfera primitiva. O prefab "
               + "precisa ter tamanho base 1x1x1 e um Collider nao-trigger, "
               + "senao o tiro do jogador nao encontra o alvo.")]
        public GameObject enemyPrefab;

        /// <summary>Inimigos abatidos nesta partida.</summary>
        public int Kills { get; private set; }

        Game.GameSession _session;
        EnemyBulletPool _bulletPool;
        readonly Stack<Enemy> _pool = new Stack<Enemy>();
        readonly List<Enemy> _alive = new List<Enemy>();
        float _spawnTimer;
        float _railZ;

        void Start()
        {
            if (drone == null) drone = FindAnyObjectByType<DroneRailController>();

            _session = FindAnyObjectByType<Game.GameSession>();
            _bulletPool = FindAnyObjectByType<EnemyBulletPool>();

            // Pool com folga: alguns alvos ainda estao encolhendo ao morrer
            // enquanto outros ja nasceram.
            int poolSize = Mathf.Max(maxAlive * 3, 6);
            for (int i = 0; i < poolSize; i++) _pool.Push(Create(i));
        }

        void Update()
        {
            FollowDrone();
            RecycleFinished();
            TrySpawn();
        }

        /// <summary>
        /// Cola o referencial no drone. Isto, e so isto, e o que faz os alvos
        /// parecerem parados enquanto o percurso avanca.
        /// </summary>
        void FollowDrone()
        {
            if (drone == null) return;

            _railZ = drone.transform.position.z;
            transform.position = new Vector3(0f, 0f, _railZ);
        }

        void RecycleFinished()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                Enemy enemy = _alive[i];
                if (enemy != null && enemy.gameObject.activeSelf) continue;

                _alive.RemoveAt(i);
                if (enemy != null) _pool.Push(enemy);
            }
        }

        void TrySpawn()
        {
            if (drone == null || _alive.Count >= maxAlive) return;

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f) return;

            _spawnTimer = spawnInterval;

            Enemy enemy = _pool.Count > 0 ? _pool.Pop() : Create(_alive.Count);

            // Quanto mais adiantada a fase, mais os alvos se mexem.
            float progress = drone.SpeedProgress;
            enemy.strafeAmplitude = Mathf.Lerp(strafeAtStart, strafeAtEnd, progress);
            enemy.bobAmplitude = Mathf.Lerp(0f, bobAtEnd, progress);
            enemy.lifetime = lifetime;

            var local = new Vector3(
                areaCenter.x + Random.Range(-areaHalfExtents.x, areaHalfExtents.x),
                areaCenter.y + Random.Range(-areaHalfExtents.y, areaHalfExtents.y),
                spawnDistance);

            enemy.Spawn(local, Random.Range(0f, 6.28f));
            _alive.Add(enemy);
        }

        Enemy Create(int index)
        {
            // Com prefab de arte, usa o modelo e o material dele; sem prefab,
            // cai na esfera cinza. E o ponto unico de troca quando a arte chegar.
            GameObject body = enemyPrefab != null
                ? Instantiate(enemyPrefab, transform)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);

            body.name = $"Sentinela_{index}";
            if (enemyPrefab == null)
            {
                body.transform.SetParent(transform, false);
                if (enemyMaterial != null)
                {
                    body.GetComponent<MeshRenderer>().sharedMaterial = enemyMaterial;
                }
            }

            body.transform.localScale = Vector3.one * enemySize;

            var enemy = body.AddComponent<Enemy>();
            enemy.BulletPool = _bulletPool;
            enemy.Player = drone != null ? drone.transform : null;
            enemy.OnKilled += HandleKilled;

            body.SetActive(false);
            return enemy;
        }

        void HandleKilled(Enemy enemy, Vector3 point)
        {
            Kills++;
            if (_session != null) _session.RegisterKill();
        }
    }
}
