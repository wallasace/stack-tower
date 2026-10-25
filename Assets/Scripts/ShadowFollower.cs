using UnityEngine;

// Mantém a sombra projetada colada no botão/painel dono dela: mesma âncora, tamanho e visibilidade.
public class ShadowFollower : MonoBehaviour
{
    public GameObject shadow;
    public Vector2 offset;

    public void Sync()
    {
        if (!shadow) return;
        var src = (RectTransform)transform;
        var rt = (RectTransform)shadow.transform;
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.pivot = src.pivot;
        rt.anchoredPosition = src.anchoredPosition + offset;
        rt.sizeDelta = src.sizeDelta;
        rt.localScale = src.localScale;
    }

    void LateUpdate() => Sync();
    void OnEnable() { if (shadow) shadow.SetActive(true); }
    void OnDisable() { if (shadow) shadow.SetActive(false); }
    void OnDestroy() { if (shadow) Destroy(shadow); }
}
