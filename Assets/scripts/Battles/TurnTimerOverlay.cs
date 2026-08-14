using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TurnTimerOverlay : MonoBehaviour
{
    [SerializeField] private float radius = 0.5f;
    [SerializeField] private int segments = 60;
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.7f);

    private MeshFilter _meshFilter;
    private Mesh _mesh;
    private float _lastRatio = -1f;

    void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _mesh = new Mesh { name = "TurnTimerMesh" };
        _meshFilter.mesh = _mesh;

        var mr = GetComponent<MeshRenderer>();
        mr.sortingLayerName = "Default";
        mr.sortingOrder = 13;
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = shadowColor;
        mr.material = mat;
    }

    public void SetFill(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        if (Mathf.Approximately(_lastRatio, ratio)) return;
        _lastRatio = ratio;
        BuildMesh(ratio);
    }

    private void BuildMesh(float ratio)
    {
        _mesh.Clear();
        if (ratio <= 0f) return;

        int triCount = Mathf.Max(1, Mathf.CeilToInt(segments * ratio));
        var verts = new Vector3[triCount + 2];
        var tris = new int[triCount * 3];

        verts[0] = Vector3.zero;
        float totalDeg = ratio * 360f;

        for (int i = 0; i <= triCount; i++)
        {
            float t = (float)i / triCount;
            float deg = 90f - totalDeg * t;
            float rad = deg * Mathf.Deg2Rad;
            verts[i + 1] = new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f);
        }

        for (int i = 0; i < triCount; i++)
        {
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        _mesh.vertices = verts;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
    }
}
