using UnityEngine;

// Ajusta o RectTransform para a área segura da tela (Screen.safeArea),
// evitando notch, câmera frontal e barra de gestos. Atualiza ao girar ou redimensionar a tela.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    Rect applied;
    Vector2Int screen;

    void Awake() => Apply();

    void Update()
    {
        if (applied != Screen.safeArea || screen.x != Screen.width || screen.y != Screen.height) Apply();
    }

    void Apply()
    {
        var safe = Screen.safeArea;
        applied = safe;
        screen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        var rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
