using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Windows build alır. Menü: Desert Gecko → Windows Build Al.
    /// Komut satırı: -executeMethod DesertGecko.EditorTools.BuildScript.BuildWindows [-buildOutput klasör]
    /// </summary>
    public static class BuildScript
    {
        const string DefaultOutput = "Builds/Windows";
        const string ExeName = "DesertGeckoBandit.exe";

        [MenuItem("Desert Gecko/Windows Build Al")]
        public static void BuildWindows()
        {
            string output = ArgumentValue("-buildOutput") ?? DefaultOutput;

            // Test sırasında tam ekran yerine pencerede açılsın.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = System.IO.Path.Combine(output, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[DesertGecko] Build sonucu: {summary.result}, {summary.totalErrors} hata, {summary.totalSize / (1024 * 1024)} MB -> {options.locationPathName}");

            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static string ArgumentValue(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
