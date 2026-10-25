using UnityEngine;

// Malha criada só para este objeto (ex.: bloco cortado numa largura única).
// Apaga a malha junto com o objeto, para não acumular memória no celular.
[RequireComponent(typeof(MeshFilter))]
public class OwnedMesh : MonoBehaviour
{
    Mesh mesh;

    public void Set(Mesh m)
    {
        if (mesh && mesh != m) Destroy(mesh);
        mesh = m;
        GetComponent<MeshFilter>().sharedMesh = m;
    }

    void OnDestroy()
    {
        if (mesh) Destroy(mesh);
    }
}
