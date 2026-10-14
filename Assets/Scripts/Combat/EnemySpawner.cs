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

        [Header("Drone Enxame (kamikaze)")]
        [Tooltip("A partir de que ponto da fase o Enxame comeca a aparecer, de 0 a 1. "
               + "Deixa a primeira parte so com alvos parados, para aprender a mira.")]
        [Range(0f, 1f)] public float swarmStartsAt = 0.25f;

        [Tooltip("Quantos Enxames no maximo ao mesmo tempo.")]
        public int maxSwarmAlive = 3;

        [Tooltip("Segundos entre dois Enxames no fim da fase. No comeco e o dobro.")]
        public float swarmInterval = 4f;

        [Tooltip("A que distancia a frente do drone o Enxame surge.")]
        public float swarmSpawnDistance = 150f;

        [Tooltip("Diametro do Enxame. Menor que o Sentinela: alvo mais dificil.")]
        public float swarmSize = 1.8f;

        [Tooltip("Material do Enxame.")]
        public Material swarmMaterial;

        [Tooltip("Prefab do Enxame. Vazio = capsula primitiva.")]
        public GameObject swarmPrefab;

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
        PlayerHealth _playerHealth;
        readonly Stack<Enemy> _pool = new Stack<Enemy>();
        readonly List<Enemy> _alive = new List<Enemy>();
        readonly Stack<SwarmDrone> _swarmPool = new Stack<SwarmDrone>();
        readonly List<SwarmDrone> _swarmAlive = new List<SwarmDrone>();
        Transform _swarmRoot;
        float _spawnTimer;
        float _swarmTimer;
        float _railZ;

        void Start()
        {
            if (drone == null) drone = FindAnyObjectByType<DroneRailController>();

            _session = FindAnyObjectByType<Game.GameSession>();
            _bulletPool = FindAnyObjectByType<EnemyBulletPool>();
            _playerHealth = FindAnyObjectByType<PlayerHealth>();

            // Pool com folga: alguns alvos ainda estao encolhendo ao morrer
            // enquanto outros ja nasceram.
            int poolSize = Mathf.Max(maxAlive * 3, 6);
            for (int i = 0; i < poolSize; i++) _pool.Push(Create(i));

            // Container proprio, parado na origem: o Enxame se move em espaco de
            // mundo. Se fosse filho deste objeto, que acompanha o drone, o
            // movimento do referencial somaria ao mergulho dele.
            _swarmRoot = new GameObject("Enxame").transform;

            int swarmPoolSize = Mathf.Max(maxSwarmAlive * 2, 4);
            for (int i = 0; i < swarmPoolSize; i++) _swarmPool.Push(CreateSwarm(i));
        }

        void Update()
        {
            FollowDrone();
            RecycleFinished();
            TrySpawn();

            RecycleSwarm();
            TrySpawnSwarm();
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

        void RecycleSwarm()
        {
            for (int i = _swarmAlive.Count - 1; i >= 0; i--)
            {
                SwarmDrone swarm = _swarmAlive[i];
                if (swarm != null && swarm.gameObject.activeSelf) continue;

                _swarmAlive.RemoveAt(i);
                if (swarm != null) _swarmPool.Push(swarm);
            }
        }

        void TrySpawnSwarm()
        {
            if (drone == null || _swarmAlive.Count >= maxSwarmAlive) return;

            // So entra depois que o jogador teve tempo de aprender a mira nos
            // alvos parados.
            float progress = drone.SpeedProgress;
            if (progress < swarmStartsAt) return;

            _swarmTimer -= Time.deltaTime;
            if (_swarmTimer > 0f) return;

            // Comeca raro e vai apertando ate o intervalo cheio no fim da fase.
            float ramp = Mathf.InverseLerp(swarmStartsAt, 1f, progress);
            _swarmTimer = Mathf.Lerp(swarmInterval * 2f, swarmInterval, ramp);

            SwarmDrone swarm = _swarmPool.Count > 0 ? _swarmPool.Pop() : CreateSwarm(_swarmAlive.Count);

            // Nasce fora da area de manobra, para vir de fora da tela e obrigar
            // a virar a mira em vez de ja aparecer debaixo do retículo.
            float side = Random.value < 0.5f ? -1f : 1f;
            var world = new Vector3(
                areaCenter.x + side * (areaHalfExtents.x + Random.Range(2f, 9f)),
                areaCenter.y + Random.Range(-areaHalfExtents.y, areaHalfExtents.y),
                drone.transform.position.z + swarmSpawnDistance);

            swarm.Spawn(world, Random.Range(0f, 6.28f));
            _swarmAlive.Add(swarm);
        }

        SwarmDrone CreateSwarm(int index)
        {
            GameObject body = swarmPrefab != null
                ? Instantiate(swarmPrefab, _swarmRoot)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);

            body.name = $"Enxame_{index}";
            if (swarmPrefab == null)
            {
                body.transform.SetParent(_swarmRoot, false);
                if (swarmMaterial != null)
                {
                    body.GetComponent<MeshRenderer>().sharedMaterial = swarmMaterial;
                }
            }

            body.transform.localScale = Vector3.one * swarmSize;

            var swarm = body.AddComponent<SwarmDrone>();
            swarm.Target = drone;
            swarm.TargetHealth = _playerHealth;
            swarm.OnKilled += HandleSwarmKilled;

            body.SetActive(false);
            return swarm;
        }

        void HandleSwarmKilled(SwarmDrone swarm, Vector3 point)
        {
            Kills++;
            if (_session != null) _session.RegisterKill();
        }
    }
}
