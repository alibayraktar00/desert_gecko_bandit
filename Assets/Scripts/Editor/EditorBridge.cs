using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DesertGecko.EditorTools
{
    /// <summary>
    /// Dışarıdan (komut satırı, yapay zekâ aracı) açık editörü yönetmek için küçük, ağ kullanmayan köprü.
    /// Temp/EditorBridge/komut.txt dosyasına yazılan tek satırlık komutu çalıştırır, sonucu sonuc.txt'ye yazar.
    /// Temp klasörü git'e girmez. Sadece DesertGecko.EditorTools içindeki metotlar çağrılabilir.
    ///
    /// Komutlar: ping | refresh | call Sinif.Metot | tests PlayMode [Filtre] [Explicit-Kategori]
    /// </summary>
    [InitializeOnLoad]
    public static class EditorBridge
    {
        const string Folder = "Temp/EditorBridge";
        static readonly string CommandPath = Folder + "/komut.txt";
        static readonly string ResultPath = Folder + "/sonuc.txt";
        static readonly string LogPath = Folder + "/log.txt";
        static double nextPoll;

        static EditorBridge()
        {
            Directory.CreateDirectory(Folder);
            EditorApplication.update += Poll;
            // Derleme hataları ve konsol mesajları dosyadan okunabilsin.
            CompilationPipeline.assemblyCompilationFinished += (assembly, messages) =>
            {
                foreach (CompilerMessage m in messages)
                    if (m.type == CompilerMessageType.Error)
                        AppendLog($"DERLEME {m.file}:{m.line} {m.message}");
            };
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type != LogType.Log || message.Contains("[DesertGecko]"))
                    AppendLog($"{type} {message}");
            };
            // Play moduna girerken domain yeniden yüklenir; sonucu alabilmek için her yüklemede kaydol.
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Callbacks());
            Write($"yuklendi {DateTime.Now:HH:mm:ss}");
        }

        static void Write(string text) => File.WriteAllText(ResultPath, text);
        static void AppendLog(string text) => File.AppendAllText(LogPath, text + "\n");

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || !File.Exists(CommandPath))
                return;
            nextPoll = EditorApplication.timeSinceStartup + 0.25;

            string command = File.ReadAllText(CommandPath).Trim();
            File.Delete(CommandPath);
            try
            {
                Write(Run(command));
            }
            catch (Exception e)
            {
                Write($"HATA {command}\n{(e.InnerException ?? e)}");
            }
        }

        static string Run(string command)
        {
            string[] parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            switch (parts.Length > 0 ? parts[0] : "")
            {
                case "ping":
                    string scene = EditorSceneManager.GetActiveScene().path;
                    return $"tamam oynuyor={EditorApplication.isPlaying} derleniyor={EditorApplication.isCompiling} " +
                           $"sahne={scene} kirli={EditorSceneManager.GetActiveScene().isDirty}";
                case "refresh":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return "tamam refresh";
                case "call":
                    return Call(parts[1]);
                case "tests":
                    return RunTests(parts);
                default:
                    return "bilinmeyen komut: " + command;
            }
        }

        static string Call(string target)
        {
            int dot = target.LastIndexOf('.');
            Type type = typeof(EditorBridge).Assembly.GetType("DesertGecko.EditorTools." + target.Substring(0, dot));
            MethodInfo method = type?.GetMethod(target.Substring(dot + 1), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                return "metot bulunamadı: " + target;
            object result = method.Invoke(null, null);
            return "tamam " + target + " " + result;
        }

        static string RunTests(string[] parts)
        {
            var filter = new Filter { testMode = parts.Length > 1 && parts[1] == "EditMode" ? TestMode.EditMode : TestMode.PlayMode };
            if (parts.Length > 2 && parts[2] != "-")
            {
                if (parts[2].Contains("("))
                    filter.testNames = new[] { parts[2] };
                else
                    filter.groupNames = new[] { parts[2] };
            }
            if (parts.Length > 3)
                filter.categoryNames = new[] { parts[3] };

            File.Delete(ResultPath + ".test");
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(filter));
            return "calisiyor";
        }

        class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new System.Text.StringBuilder($"bitti gecen={result.PassCount} kalan={result.FailCount} atlanan={result.SkipCount} sure={result.Duration:0}\n");
                Append(result, sb);
                File.WriteAllText(ResultPath + ".test", sb.ToString());
            }

            static void Append(ITestResultAdaptor r, System.Text.StringBuilder sb)
            {
                if (!r.HasChildren && r.TestStatus == TestStatus.Failed)
                    sb.AppendLine($"KALDI {r.FullName}\n{r.Message}\n");
                if (r.HasChildren)
                    foreach (ITestResultAdaptor child in r.Children)
                        Append(child, sb);
            }
        }
    }
}
