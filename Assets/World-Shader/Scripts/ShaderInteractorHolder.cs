using UnityEngine;
 
[ExecuteInEditMode]
public class ShaderInteractorHolder : MonoBehaviour
{
    ShaderInteractorPosition[] interactors;
    Vector4[] positions = new Vector4[100];
    float[] radiuses = new float[100];
    Vector4[] boxBounds = new Vector4[100];
    Vector4[] rotations = new Vector4[100];
 
    [Range(0, 1)]
    public float shapeCutoff;
    [Range(0, 1)]
    public float shapeSmoothness = 0.1f;
    // Start is called before the first frame update
    void Start()
    {
        FindInteractors();
    }
    private void OnEnable()
    {
        FindInteractors();
    }
 
 
    void FindInteractors()
    {
        interactors = FindObjectsOfType<ShaderInteractorPosition>();
    }
 
    // Update is called once per frame
    void Update()
    {
        FindInteractors();
        for (int i = 0; i < interactors.Length; i++)
        {
            positions[i] = interactors[i].transform.position;
            radiuses[i] = interactors[i].radius;
            boxBounds[i] = interactors[i].transform.localScale;
            rotations[i] = interactors[i].transform.eulerAngles;
        }
        Shader.SetGlobalVectorArray("_ShaderInteractorsPositions", positions);
        Shader.SetGlobalFloatArray("_ShaderInteractorsRadiuses", radiuses);
        Shader.SetGlobalVectorArray("_ShaderInteractorsBoxBounds", boxBounds);
        Shader.SetGlobalVectorArray("_ShaderInteractorRotation", rotations);
 
        Shader.SetGlobalFloat("_ShapeCutoff", shapeCutoff);
        Shader.SetGlobalFloat("_ShapeSmoothness", shapeSmoothness);
 
    }
}