using UnityEngine;

// Ajustes visuais editáveis no Inspector (menu Tools > Ajustes Visuais).
// Mudanças aplicam na hora, inclusive com o jogo rodando, e ficam salvas no asset
// Assets/Resources/VisualSettings.asset (que também vai para o build).
[CreateAssetMenu(fileName = "VisualSettings", menuName = "Jogo/Ajustes Visuais")]
public class VisualSettings : ScriptableObject
{
    [Header("Brilho dos blocos (backlight)")]
    [Tooltip("Intensidade da luz atrás de cada bloco. Os brilhos dos blocos se somam.")]
    [Range(0f, 1f)] public float blockGlowAlpha = 0.28f;
    [Tooltip("Espalhamento do desfoque, em unidades do mundo.")]
    [Range(0.1f, 3f)] public float blockGlowBlur = 1.29f;
    [Tooltip("Tamanho do núcleo da luz em relação ao bloco (0.75 = 75%).")]
    [Range(0.2f, 1.5f)] public float blockGlowScale = 0.75f;
    [Tooltip("Deslocamento da luz para baixo, em unidades do mundo.")]
    [Range(-0.5f, 1f)] public float blockGlowDrop = 0.2f;

    [Header("Brilho dos botões principais")]
    [Range(0f, 1f)] public float buttonGlowAlpha = 0.18f;
    [Tooltip("Largura do desfoque, em pixels da tela de referência (1080x1920).")]
    [Range(10f, 250f)] public float buttonGlowBlur = 103f;
    [Tooltip("Quanto o brilho passa da borda do botão, em pixels.")]
    [Range(0f, 200f)] public float buttonGlowSpread = 90f;
    [Tooltip("Deslocamento do brilho para baixo, em pixels.")]
    [Range(-40f, 60f)] public float buttonGlowDrop = 10f;

    // Muda a cada edição no Inspector; quem desenha compara para saber se precisa reaplicar.
    public static int Version { get; private set; }

    static VisualSettings instance;

    public static VisualSettings I
    {
        get
        {
            if (!instance) instance = Resources.Load<VisualSettings>("VisualSettings");
            if (!instance) instance = CreateInstance<VisualSettings>(); // valores padrão
            return instance;
        }
    }

    void OnValidate() => Version++;
}
