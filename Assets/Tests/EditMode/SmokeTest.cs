using System.Linq;
using NUnit.Framework;
using RemoteChase.Drone;
using UnityEditor;
using UnityEngine;

namespace RemoteChase.Tests
{
    /// <summary>
    /// Smoke test: nao testa gameplay, so verifica que o projeto esta montado o
    /// bastante para rodar. Se algum destes quebrar, o build nao vale a pena.
    /// </summary>
    public class SmokeTest
    {
        const string RailScenePath = "Assets/Scenes/Corrida.unity";

        [Test]
        public void CenaDaCorridaEstaNoBuild()
        {
            EditorBuildSettingsScene scene = EditorBuildSettings.scenes
                .FirstOrDefault(s => s.path == RailScenePath);

            Assert.IsNotNull(scene, $"{RailScenePath} nao esta no Build Settings. "
                                  + "Rode Tools > Remote Chase > Criar cena Corrida (rail).");
            Assert.IsTrue(scene.enabled, $"{RailScenePath} esta no Build Settings mas desmarcada.");
        }

        [Test]
        public void CenaDaCorridaAbreComDroneECamera()
        {
            // Aditivo, nao Single: assim rodar o teste no Editor nao fecha a cena
            // em que a pessoa esta trabalhando.
            UnityEngine.SceneManagement.Scene scene =
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    RailScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);

            try
            {
                Assert.IsTrue(scene.IsValid(), "A cena da corrida nao abriu.");

                GameObject[] roots = scene.GetRootGameObjects();

                Assert.IsNotNull(roots.Select(r => r.GetComponentInChildren<DroneRailController>())
                                      .FirstOrDefault(c => c != null),
                                 "A cena nao tem nenhum DroneRailController.");

                Assert.IsNotNull(roots.Select(r => r.GetComponentInChildren<Camera>())
                                      .FirstOrDefault(c => c != null),
                                 "A cena nao tem camera.");

                Assert.IsNotNull(roots.Select(r => r.GetComponentInChildren<Game.GameSession>())
                                      .FirstOrDefault(c => c != null),
                                 "A cena nao tem GameSession: sem ela nao ha vitoria, "
                               + "derrota nem pontuacao.");

                Assert.IsNotNull(roots.Select(r => r.GetComponentInChildren<Combat.PlayerHealth>())
                                      .FirstOrDefault(c => c != null),
                                 "O drone nao tem PlayerHealth.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void RampaDeVelocidadeEstaCoerente()
        {
            var drone = new GameObject("DroneDeTeste");

            try
            {
                var controller = drone.AddComponent<DroneRailController>();

                Assert.Less(controller.startSpeed, controller.maxSpeed,
                            "A velocidade inicial precisa ser menor que a maxima, "
                          + "senao a fase nao acelera.");
                Assert.Greater(controller.rampDistance, 0f,
                               "rampDistance zerada faz o drone comecar na velocidade maxima.");
                Assert.Greater(controller.boundsHalfExtents.x, 0f, "Area jogavel sem largura.");
                Assert.Greater(controller.boundsHalfExtents.y, 0f, "Area jogavel sem altura.");
            }
            finally
            {
                Object.DestroyImmediate(drone);
            }
        }
    }
}
