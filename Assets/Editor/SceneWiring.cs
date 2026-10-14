using System.Collections.Generic;
using System.Linq;
using RemoteChase.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace RemoteChase.EditorTools
{
    /// <summary>
    /// Refaz as ligacoes que o Inspector nao consegue manter sozinho depois da
    /// uniao das duas cenas, e registra as cenas no Build Settings.
    ///
    /// Quando a UI do Rafael mudou de cena, as referencias dela para objetos que
    /// ficaram para tras (o drone dele, por exemplo) viraram nulas. Isto religa
    /// nos objetos equivalentes da cena principal.
    ///
    /// E seguro rodar quantas vezes quiser: so preenche o que estiver vazio.
    /// </summary>
    public static class SceneWiring
    {
        const string GameScene = "Assets/Scenes/Corrida.unity";
        const string MenuScene = "Assets/Scenes/Menu.unity";
        const string MixerPath = "Assets/MainAudioMixer.mixer";
        const string ShootClipPath = "Assets/Audio/tiro1.ogg";

        [MenuItem("Tools/Remote Chase/Religar cena e Build Settings")]
        public static void Wire()
        {
            RegisterScenes();

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
            var report = new List<string>();

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            AudioMixerGroup sfxGroup = mixer != null
                ? mixer.FindMatchingGroups("SFX").FirstOrDefault()
                : null;

            WirePauseMenu(report, mixer);
            WireWeaponAudio(report, sfxGroup);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Religar] " + string.Join("\n[Religar] ", report));
        }

        /// <summary>
        /// O menu carrega as cenas por nome, entao as duas precisam estar no
        /// Build Settings -- e a Menu precisa ser a primeira, para o jogo abrir nela.
        /// </summary>
        static void RegisterScenes()
        {
            var wanted = new[] { MenuScene, GameScene };
            var scenes = new List<EditorBuildSettingsScene>();

            foreach (string path in wanted)
            {
                if (System.IO.File.Exists(path))
                {
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            // Mantem qualquer outra cena ja registrada, depois destas duas.
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (wanted.Contains(existing.path)) continue;
                scenes.Add(existing);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[Religar] Build Settings: {string.Join(", ", scenes.Select(s => s.path))}");
        }

        static void WirePauseMenu(List<string> report, AudioMixer mixer)
        {
            var pause = Object.FindAnyObjectByType<PauseMenuController>(FindObjectsInactive.Include);
            if (pause == null)
            {
                report.Add("PauseMenuController nao encontrado na cena.");
                return;
            }

            // A referencia apontava para o drone da cena dele, que nao veio junto.
            if (pause.droneAimScript == null)
            {
                pause.droneAimScript = Object.FindAnyObjectByType<DroneAim>(FindObjectsInactive.Include);
                report.Add(pause.droneAimScript != null
                    ? "PauseMenuController.droneAimScript religado no drone da cena."
                    : "Nenhum DroneAim na cena para religar.");
            }

            if (pause.mainAudioMixer == null && mixer != null)
            {
                pause.mainAudioMixer = mixer;
                report.Add("PauseMenuController.mainAudioMixer preenchido.");
            }

            if (pause.gameHUD == null)
            {
                GameObject hud = scenes_FindRoot("GameHUD");
                if (hud != null)
                {
                    pause.gameHUD = hud;
                    report.Add("PauseMenuController.gameHUD religado no GameHUD.");
                }
            }

            EditorUtility.SetDirty(pause);
        }

        static void WireWeaponAudio(List<string> report, AudioMixerGroup sfxGroup)
        {
            var weapon = Object.FindAnyObjectByType<DroneWeapon>(FindObjectsInactive.Include);
            if (weapon == null)
            {
                report.Add("DroneWeapon nao encontrado na cena.");
                return;
            }

            if (weapon.shootSound == null)
            {
                weapon.shootSound = AssetDatabase.LoadAssetAtPath<AudioClip>(ShootClipPath);
                report.Add(weapon.shootSound != null
                    ? "DroneWeapon.shootSound = tiro1.ogg"
                    : $"Clipe {ShootClipPath} nao encontrado.");
            }

            var source = weapon.GetComponent<AudioSource>();
            if (source != null)
            {
                source.playOnAwake = false;

                // Sem o grupo do mixer, o slider de SFX do menu nao afeta o tiro.
                if (source.outputAudioMixerGroup == null && sfxGroup != null)
                {
                    source.outputAudioMixerGroup = sfxGroup;
                    report.Add("AudioSource do tiro roteado para o grupo SFX.");
                }

                weapon.audioSource = source;
                EditorUtility.SetDirty(source);
            }

            EditorUtility.SetDirty(weapon);
        }

        static GameObject scenes_FindRoot(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.GetRootGameObjects().FirstOrDefault(o => o.name == name);
        }
    }
}
