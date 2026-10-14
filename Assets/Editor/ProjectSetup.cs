using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;

// Cria a cena principal e configura o projeto para celular (retrato) e navegador (tela cheia).
// As configurações de build são aplicadas sozinhas toda vez que o Unity carrega os scripts;
// Tools > Configurar Projeto também cria a cena, se ainda não existir.
[InitializeOnLoad]
public static class ProjectSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string ProductName = "Stack Tower";
    const string BundleId = "com.wallasace.stacktower";

    static ProjectSetup() => EditorApplication.delayCall += ApplyPlayerSettings;

    [MenuItem("Tools/Configurar Projeto")]
    public static void Setup()
    {
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        if (!System.IO.File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        ApplyPlayerSettings();
        AssetDatabase.SaveAssets();
    }

    static void ApplyPlayerSettings()
    {
        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = "wallasace";

        // Celular: só retrato; o jogo cuida da área segura (notch) pela UI
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
        PlayerSettings.Android.renderOutsideSafeArea = true;

        // Navegador: canvas ocupa a janela inteira (template em Assets/WebGLTemplates/Responsive)
        // e compressão gzip com descompressão no navegador, que funciona no itch.io e em qualquer servidor
        PlayerSettings.WebGL.template = "PROJECT:Responsive";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        // PC: janela redimensionável
        PlayerSettings.resizableWindow = true;
    }
}
