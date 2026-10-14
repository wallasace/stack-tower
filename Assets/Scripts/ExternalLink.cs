using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

// Abre um link externo ao tocar/clicar.
// Na Web, Application.OpenURL roda fora do gesto do usuário e o navegador bloqueia como pop-up.
// Por isso, no toque (pointer down) registramos no navegador um "abrir no próximo soltar",
// que acontece dentro do gesto e não é bloqueado. Nas outras plataformas, abre direto.
public class ExternalLink : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    public string url;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void OpenLinkOnNextPointerUp(string url);

    public void OnPointerDown(PointerEventData e) => OpenLinkOnNextPointerUp(url);
    public void OnPointerClick(PointerEventData e) { }
#else
    public void OnPointerDown(PointerEventData e) { }
    public void OnPointerClick(PointerEventData e) => Application.OpenURL(url);
#endif
}
