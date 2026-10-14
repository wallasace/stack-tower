using UnityEditor;
using UnityEngine;

// Tools > Ajustes Visuais: abre (e cria, se preciso) o asset com os sliders no Inspector.
public static class VisualSettingsMenu
{
    const string Path = "Assets/Resources/VisualSettings.asset";

    [MenuItem("Tools/Ajustes Visuais")]
    public static void Open()
    {
        var settings = AssetDatabase.LoadAssetAtPath<VisualSettings>(Path);
        if (!settings)
        {
            System.IO.Directory.CreateDirectory("Assets/Resources");
            settings = ScriptableObject.CreateInstance<VisualSettings>();
            AssetDatabase.CreateAsset(settings, Path);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
        EditorApplication.ExecuteMenuItem("Window/General/Inspector");
    }
}
