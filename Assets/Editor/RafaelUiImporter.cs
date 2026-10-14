using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RemoteChase.EditorTools
{
    /// <summary>
    /// Ferramenta de uso unico: move a interface montada a mao pelo Rafael
    /// (Canvas, HUD de vida/bateria, menu de pausa, EventSystem, audio) da cena
    /// dele para a cena de jogo principal.
    ///
    /// Existe porque as duas cenas nasceram separadas: a dele foi montada no
    /// editor, a nossa era gerada por codigo. Como os .meta -- e portanto os
    /// GUIDs -- sao os mesmos nos dois projetos, as referencias dos componentes
    /// dele continuam validas depois da mudanca.
    ///
    /// Depois que a UI estiver na cena principal, esta classe e a cena temporaria
    /// _RafaelUI.unity podem ser apagadas.
    /// </summary>
    public static class RafaelUiImporter
    {
        const string TargetScene = "Assets/Scenes/Corrida.unity";
        const string SourceScene = "Assets/Scenes/_RafaelUI.unity";

        /// <summary>Objetos da cena dele que devem vir. O resto e duplicata do que ja temos.</summary>
        static readonly string[] Wanted =
        {
            "Canvas_UI",    // paineis de pausa e opcoes
            "GameHUD",      // canvas do HUD de vida e bateria
            "HUDManager",   // PlayerHUDController
            "GameManager",  // PauseMenuController
            "EventSystem",  // sem ele nenhum botao responde
            "BGM_Manager"   // AudioSource da musica
        };

        [MenuItem("Tools/Remote Chase/Importar UI do Rafael/1 - Listar objetos da cena dele")]
        public static void ListSourceRoots()
        {
            Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);

            var lines = new List<string>();
            foreach (GameObject root in source.GetRootGameObjects())
            {
                string components = string.Join(", ", root.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name));
                lines.Add($"  {root.name}  [{components}]  filhos={root.transform.childCount}");
            }

            Debug.Log($"[Importar UI] Raizes de {SourceScene}:\n{string.Join("\n", lines)}");
            EditorSceneManager.CloseScene(source, true);
        }

        [MenuItem("Tools/Remote Chase/Importar UI do Rafael/2 - Mover UI para a Corrida")]
        public static void MoveUiIntoGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene target = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
            Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);

            var moved = new List<string>();
            var skipped = new List<string>();

            foreach (GameObject root in source.GetRootGameObjects())
            {
                if (!Wanted.Contains(root.name))
                {
                    skipped.Add(root.name);
                    continue;
                }

                // Se a cena de destino ja tem um objeto com esse nome, o dele
                // ganha sufixo em vez de sumir: melhor duplicata visivel do que
                // perda silenciosa.
                if (target.GetRootGameObjects().Any(o => o.name == root.name))
                {
                    root.name += " (Rafael)";
                }

                SceneManager.MoveGameObjectToScene(root, target);
                moved.Add(root.name);
            }

            EditorSceneManager.CloseScene(source, true);
            EditorSceneManager.MarkSceneDirty(target);
            EditorSceneManager.SaveScene(target);

            Debug.Log($"[Importar UI] Movidos: {string.Join(", ", moved)}\n"
                    + $"[Importar UI] Ignorados: {string.Join(", ", skipped)}");
        }
    }
}
