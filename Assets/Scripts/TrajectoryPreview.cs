using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws a physics-accurate dotted throw trajectory.
///
/// The dot texture is generated in code at runtime — no texture asset needed.
/// The material is also created in code if none is assigned, so the LineRenderer
/// only needs to exist; everything else is handled here.
///
/// If you want a custom look, assign your own material in the Inspector and
/// the script will still override the texture with the generated dot.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class TrajectoryPreview : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────

    [Header("Simulation")]
    [Tooltip("Max simulation iterations.")]
    [SerializeField] private int   maxIterations = 150;

    [Tooltip("Total path length at which the line ends.")]
    [SerializeField] private float maxPathLength = 40f;

    [Tooltip("Layers the trajectory bounces off. Exclude Player, Triggers, Stone.")]
    [SerializeField] private LayerMask collisionMask = ~0;

    [Tooltip("Nudge off surface after bounce to prevent immediate re-hit.")]
    [SerializeField] private float surfaceOffset = 0.02f;

    [Header("Dots")]
    [Tooltip("How many dot-gap cycles fit along the full path length. Higher = more, smaller dots.")]
    [SerializeField] private float dotsPerMeter = 1.5f;

    [Tooltip("0–1. How much of each tile the dot fills. 0.5 = dot and gap equal size.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float dotFill = 0.55f;

    [Tooltip("Width of the line in world units.")]
    [SerializeField] private float lineWidth = 0.04f;

    [Tooltip("Dot color.")]
    [SerializeField] private Color dotColor = Color.white;

    [Header("Animation")]
    [Tooltip("Speed at which dots flow toward the target. 0 = static.")]
    [SerializeField] private float scrollSpeed = 2f;

    // ─── Private ───────────────────────────────────────────────────────────

    private LineRenderer _line;
    private Material     _mat;
    private string       _texProp;   // "_BaseMap" (URP) or "_MainTex" (Built-in)

    // Dirty flag
    private Vector3 _cachedStart;
    private Vector3 _cachedVelocity;
    private int     _cachedBounceCount = -1;

    // ─── Unity ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.textureMode       = LineTextureMode.Tile;
        _line.alignment         = LineAlignment.View;
        _line.startWidth        = lineWidth;
        _line.endWidth          = lineWidth;
        _line.useWorldSpace     = true;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows    = false;

        SetupMaterial();
    }

    private void Update()
    {
        if (_line.positionCount == 0 || _mat == null) return;

        // Animate dots flowing toward target
        Vector2 offset = _mat.GetTextureOffset(_texProp);
        offset.x -= scrollSpeed * Time.deltaTime;
        _mat.SetTextureOffset(_texProp, offset);
    }

    // ─── Public API ────────────────────────────────────────────────────────

    public void DrawTrajectory(Vector3 startPos, Vector3 initialVelocity, int bounceCount)
    {
        if (startPos        == _cachedStart       &&
            initialVelocity == _cachedVelocity    &&
            bounceCount     == _cachedBounceCount)
            return;

        _cachedStart       = startPos;
        _cachedVelocity    = initialVelocity;
        _cachedBounceCount = bounceCount;

        Simulate(startPos, initialVelocity, bounceCount);
    }

    public void Clear()
    {
        _line.positionCount = 0;
        _cachedBounceCount  = -1;
    }

    // ─── Material & Texture ────────────────────────────────────────────────

    private void SetupMaterial()
    {
        // If no material assigned, build one using the best available transparent shader
        if (_line.sharedMaterial == null)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Particles/Standard Unlit")                  ??
                Shader.Find("Sprites/Default")                           ??
                Shader.Find("Unlit/Transparent");

            if (shader == null)
            {
                Debug.LogError("[TrajectoryPreview] No suitable transparent shader found. "
                             + "Assign a transparent URP material manually.", this);
                return;
            }

            _mat = new Material(shader);
            _line.material = _mat;
        }
        else
        {
            // Use a material instance so we don't modify the shared asset
            _mat = _line.material;
        }

        // Enable transparency on URP Particles/Unlit if needed
        if (_mat.HasProperty("_Surface"))
        {
            _mat.SetFloat("_Surface", 1f);                          // 0=Opaque, 1=Transparent
            _mat.SetFloat("_Blend",   0f);                          // 0=Alpha blend
            _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        // Detect which texture property this shader uses
        _texProp = _mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";

        // Apply color
        if (_mat.HasProperty("_BaseColor"))    _mat.SetColor("_BaseColor",    dotColor);
        if (_mat.HasProperty("_Color"))         _mat.SetColor("_Color",        dotColor);
        if (_mat.HasProperty("_TintColor"))     _mat.SetColor("_TintColor",    dotColor);

        ApplyDotTexture();
    }

    private void ApplyDotTexture()
    {
        if (_mat == null) return;

        Texture2D dot = GenerateDotTexture(64, dotFill);
        _mat.SetTexture(_texProp, dot);

        // Tiling: how many dot-cycles fit along maxPathLength
        float tiling = dotsPerMeter * maxPathLength;
        _mat.SetTextureScale(_texProp, new Vector2(tiling, 1f));
    }

    /// <summary>
    /// Generates a white circle on a transparent background at runtime.
    /// Soft edge for anti-aliasing.
    /// </summary>
    private static Texture2D GenerateDotTexture(int size, float fill)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;
        float r    = half * fill;
        float edge = 1.5f;  // AA width in pixels

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx   = x - half + 0.5f;
            float dy   = y - half + 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float a    = 1f - Mathf.InverseLerp(r - edge, r + edge, dist);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply();
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }

    // ─── Simulation ────────────────────────────────────────────────────────

    private void Simulate(Vector3 startPos, Vector3 vel, int maxBounces)
    {
        var points = new List<Vector3>(maxIterations) { startPos };

        Vector3 pos        = startPos;
        int     bounces    = 0;
        float   pathLength = 0f;
        float   dt         = Time.fixedDeltaTime;

        for (int i = 0; i < maxIterations; i++)
        {
            float speed = vel.magnitude;
            if (speed < 0.001f) break;

            float   stepDist = speed * dt;
            Vector3 nextVel  = vel + Physics.gravity * dt;
            Vector3 nextPos  = pos + vel * dt;

            if (Physics.Raycast(pos, vel.normalized, out RaycastHit hit, stepDist, collisionMask))
            {
                points.Add(hit.point);
                pathLength += hit.distance;

                if (bounces >= maxBounces) break;

                float remaining = 1f - (hit.distance / stepDist);
                vel  = Vector3.Reflect(vel, hit.normal);
                vel += Physics.gravity * dt * remaining;
                pos  = hit.point + hit.normal * surfaceOffset;
                bounces++;
            }
            else
            {
                pathLength += Vector3.Distance(pos, nextPos);
                pos = nextPos;
                vel = nextVel;
                points.Add(pos);
            }

            if (pathLength >= maxPathLength) break;
        }

        _line.positionCount = points.Count;
        _line.SetPositions(points.ToArray());
    }
}
