using System.IO;
using System.Linq;
using PaulistaBusinessWorld;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Configuração do projeto e builds. Menu: Paulista Business World > ...
// Também pode ser executado em linha de comando com -executeMethod ProjectSetup.BuildWindows / ProjectSetup.BuildWebGL.
[InitializeOnLoad]
public static class ProjectSetup
{
    const string ScenePath = "Assets/Scenes/PaulistaBusinessWorld.unity";

    static ProjectSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ScenePath)) CreateMainScene();
        };
    }

    [MenuItem("Paulista Business World/Recriar cena principal")]
    public static void CreateMainScene()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Paulista Business World").AddComponent<GameRoot>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        ConfigurePlayer();
        Debug.Log("Paulista Business World: cena criada em " + ScenePath + ". Pressione Play.");
    }

    [MenuItem("Paulista Business World/Abrir cena principal")]
    public static void OpenMainScene()
    {
        if (!File.Exists(ScenePath)) CreateMainScene();
        EditorSceneManager.OpenScene(ScenePath);
    }

    static void ConfigurePlayer()
    {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.companyName = "Paulista Business World";
        PlayerSettings.productName = "Paulista Business World";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true; // funciona em qualquer hospedagem estática
        EnsureAlwaysIncludedShader("Standard");
        AssetDatabase.SaveAssets();
    }

    // Os materiais são criados em tempo de execução, então o shader Standard precisa ir no build.
    static void EnsureAlwaysIncludedShader(string name)
    {
        var shader = Shader.Find(name);
        if (shader == null) return;
        var settings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        var so = new SerializedObject(settings);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
        so.ApplyModifiedProperties();
    }

    [MenuItem("Paulista Business World/Build Windows")]
    public static void BuildWindows() =>
        Build(BuildTarget.StandaloneWindows64, "Builds/Windows/PaulistaBusinessWorld.exe");

    [MenuItem("Paulista Business World/Build WebGL")]
    public static void BuildWebGL() => Build(BuildTarget.WebGL, "Builds/WebGL");

    static void Build(BuildTarget target, string output)
    {
        if (!File.Exists(ScenePath)) CreateMainScene();
        ConfigurePlayer();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = target,
            options = BuildOptions.None,
        });
        var s = report.summary;
        Debug.Log($"Build {target}: {s.result}, {s.totalErrors} erro(s), {s.totalSize / (1024 * 1024)} MB em {output}");
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }
}
