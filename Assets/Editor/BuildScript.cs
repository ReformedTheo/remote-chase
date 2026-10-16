using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RemoteChase.EditorTools
{
    /// <summary>
    /// Build pela linha de comando, sem abrir o editor:
    ///
    ///   Unity.exe -batchmode -projectPath &lt;projeto&gt;
    ///             -executeMethod RemoteChase.EditorTools.BuildScript.BuildWindows
    ///             -outputPath "C:\caminho\RemoteChase.exe"
    ///             -logFile build.log
    ///
    /// Ele regenera a cena antes de buildar, entao o executavel sempre sai com a
    /// versao atual dos scripts -- nao adianta o codigo estar novo e a cena velha.
    /// </summary>
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Corrida.unity";
        const string DefaultOutput = "Builds/Windows/RemoteChase.exe";

        [MenuItem("Tools/Remote Chase/Build Windows")]
        public static void BuildWindows()
        {
            string output = ReadArgument("-outputPath") ?? DefaultOutput;

            // A cena NAO e regerada aqui: desde que a UI passou a ser montada a
            // mao, regerar apagaria o trabalho feito no editor. O build usa a
            // cena como ela esta versionada.
            if (!File.Exists(ScenePath))
            {
                Fail($"A cena {ScenePath} nao existe. Abortando.");
                return;
            }

            string directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            Log($"Buildando para {output}...");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Log($"Build OK: {summary.totalSize / (1024 * 1024)} MB em "
                  + $"{summary.totalTime.TotalSeconds:0} s -> {output}");
                Exit(0);
                return;
            }

            Fail($"Build falhou: {summary.result}, {summary.totalErrors} erro(s).");
        }

        [MenuItem("Tools/Remote Chase/Build Linux")]
        public static void BuildLinux()
        {
            string output = ReadArgument("-outputPath") ?? "Builds/Linux/RemoteChase.x86_64";

            if (!File.Exists(ScenePath))
            {
                Fail($"A cena {ScenePath} nao existe. Abortando.");
                return;
            }

            string directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = output,
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            Log($"Buildando Linux para {output}...");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Log($"Build OK: {summary.totalSize / (1024 * 1024)} MB em "
                  + $"{summary.totalTime.TotalSeconds:0} s -> {output}");
                Exit(0);
                return;
            }

            Fail($"Build falhou: {summary.result}, {summary.totalErrors} erro(s). "
               + "Se o erro for de modulo ausente, falta o Linux Build Support.");
        }

        [MenuItem("Tools/Remote Chase/Build WebGL")]
        public static void BuildWebGL()
        {
            string output = ReadArgument("-outputPath") ?? "Builds/WebGL";

            // A cena NAO e regerada aqui: desde que a UI passou a ser montada a
            // mao, regerar apagaria o trabalho feito no editor. O build usa a
            // cena como ela esta versionada.
            if (!File.Exists(ScenePath))
            {
                Fail($"A cena {ScenePath} nao existe. Abortando.");
                return;
            }

            // Sem compressao: um build comprimido exige que o servidor mande
            // Content-Encoding br/gzip. Assim ele roda em qualquer servidor
            // estatico sem configurar nada -- fica maior, mas nao quebra.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

            Log("Trocando a plataforma para WebGL (reimporta os assets, demora)...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.WebGL, BuildTarget.WebGL);

            Directory.CreateDirectory(output);

            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            Log($"Buildando WebGL para {output}...");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Log($"Build OK: {summary.totalSize / (1024 * 1024)} MB em "
                  + $"{summary.totalTime.TotalSeconds:0} s -> {output}");
                Exit(0);
                return;
            }

            Fail($"Build falhou: {summary.result}, {summary.totalErrors} erro(s).");
        }

        /// <summary>
        /// As cenas marcadas no Build Settings, na ordem. A primeira e a que abre
        /// o jogo. Antes isto era a Corrida fixa no codigo, o que deixava o menu
        /// de fora do build e quebrava o botao de voltar ao menu.
        /// </summary>
        static string[] EnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();
        }

        /// <summary>Le um argumento nomeado da linha de comando do Unity.</summary>
        static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }

            return null;
        }

        static void Log(string message) => Debug.Log($"[Build] {message}");

        static void Fail(string message)
        {
            Debug.LogError($"[Build] {message}");
            Exit(1);
        }

        static void Exit(int code)
        {
            // Em batchmode e preciso encerrar explicitamente para devolver o
            // codigo de saida; no editor, so deixa a janela aberta.
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
