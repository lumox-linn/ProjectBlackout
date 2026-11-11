using UnityEngine;

/// <summary>
/// Makes textures repeat by size instead of stretching on scaled greybox cubes.
/// Put it ONCE on the Floors / Walls parent (or the Ceiling): it handles every child.
/// </summary>
[ExecuteAlways]
public class AutoTile : MonoBehaviour
{
    [Tooltip("How many metres one copy of the texture covers.")]
    [SerializeField] private float metresPerTile = 2f;

    [Tooltip("Tick for FLOORS and CEILINGS (texture on the top), untick for walls.")]
    [SerializeField] private bool horizontal = false;

    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    private void OnEnable() { Apply(); }
    private void OnValidate() { Apply(); }
    private void OnDisable() { ClearAll(); }   // removing/unticking the component undoes the tiling

    [ContextMenu("Apply tiling")]
    public void Apply()
    {
        if (!isActiveAndEnabled) return;
        var block = new MaterialPropertyBlock();
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
        {
            Vector3 size = r.transform.lossyScale;
            Vector2 tiles = horizontal
                ? new Vector2(size.x, size.z)
                : new Vector2(Mathf.Max(size.x, size.z), size.y);
            tiles /= Mathf.Max(0.01f, metresPerTile);

            r.GetPropertyBlock(block);
            block.SetVector(BaseMapST, new Vector4(tiles.x, tiles.y, 0f, 0f));
            r.SetPropertyBlock(block);
        }
    }

    [ContextMenu("Clear tiling")]
    public void ClearAll()
    {
        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
            r.SetPropertyBlock(null);
    }
}