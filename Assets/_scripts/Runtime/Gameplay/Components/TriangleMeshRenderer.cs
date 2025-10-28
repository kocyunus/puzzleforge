using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Yunus.Game.Gameplay
{
    /// <summary>
    /// Custom triangle mesh renderer replacing external asset dependencies.
    /// 
    /// DESIGN:
    /// - Generates triangle mesh from 3 vertices (vertexA, vertexB, vertexC)
    /// - Uses vertex colors (mesh.colors) for color data
    /// - Applies colors via MaterialPropertyBlock (no material duplication)
    /// - Custom shader (Custom/SimpleTriangleColor) blends vertex color with material color
    /// - Double-sided mesh (front + back facing triangles for filled appearance)
    /// 
    /// FEATURES:
    /// - Editor real-time updates via OnValidate() and EditorApplication.update
    /// - Live Inspector color editing with visual feedback
    /// - Efficient color updates without creating new materials
    /// - Supports both Play mode and Editor mode
    /// 
    /// DEPENDENCIES:
    /// - Custom/SimpleTriangleColor shader (required)
    /// - MeshFilter and MeshRenderer components (auto-added if missing)
    /// </summary>
    public class TriangleMeshRenderer : MonoBehaviour
    {
        [Header("Vertices")]
        [SerializeField] private Vector3 vertexA = new(0, 0.5f, 0);
        [SerializeField] private Vector3 vertexB = new(-0.5f, -0.5f, 0);
        [SerializeField] private Vector3 vertexC = new(0.5f, -0.5f, 0);

        [Header("Color")]
        [SerializeField] private Color triangleColor = Color.white;

        private Mesh triangleMesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock mpb;

        // For editor mode color change detection
        private Color lastColor;

        private void Start()
        {
            Initialize();
            UpdateMesh();
            UpdateColor();
        }

        /// <summary>
        /// Initializes mesh, material, and renderer components.
        /// Creates MeshFilter, MeshRenderer, and MaterialPropertyBlock on first call.
        /// Sets up shader and applies default material settings (glossiness).
        /// </summary>
        private void Initialize()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            // Create material with custom shader
            var shader = Shader.Find("Custom/SimpleTriangleColor");
            if (shader != null)
            {
                var mat = new Material(shader);
                // Set glossiness for visual consistency
                mat.SetFloat("_Glossiness", 0.8f);
                
                if (Application.isPlaying)
                    meshRenderer.material = mat;
                else
                    meshRenderer.sharedMaterial = mat;
            }

            // Initialize MaterialPropertyBlock for efficient updates
            if (mpb == null)
                mpb = new MaterialPropertyBlock();

            // Create mesh if not exists
            if (triangleMesh == null)
            {
                triangleMesh = new Mesh();
                triangleMesh.name = "TriangleMesh";
                meshFilter.mesh = triangleMesh;
            }
        }

        /// <summary>
        /// Rebuilds the triangle mesh from vertices.
        /// 
        /// MESH STRUCTURE:
        /// - Vertices: 3 points (A, B, C)
        /// - Triangles: 6 indices (0,1,2 = front, 0,2,1 = back)
        /// - Colors: 3 colors (one per vertex)
        /// 
        /// WHY DOUBLE-SIDED?
        /// Both front and back faces are defined so the triangle appears filled
        /// from all angles, not just one side.
        /// </summary>
        public void UpdateMesh()
        {
            if (triangleMesh == null)
                return;

            // Clear old data
            triangleMesh.Clear();

            // Set vertices
            triangleMesh.vertices = new Vector3[] { vertexA, vertexB, vertexC };

            // Set triangles (double-sided: front + back)
            // Front: 0,1,2 | Back: 0,2,1 (winding order reversed for backface)
            triangleMesh.triangles = new int[] { 0, 1, 2, 0, 2, 1 };

            // Update normals for proper lighting
            triangleMesh.RecalculateNormals();
            triangleMesh.RecalculateBounds();
        }

        /// <summary>
        /// Updates triangle color using vertex colors and MaterialPropertyBlock.
        /// 
        /// WHY VERTEX COLORS?
        /// - No material duplication needed
        /// - MaterialPropertyBlock applies changes without creating new materials
        /// - Efficient for many color updates (one block per frame)
        /// - Custom shader blends vertex color with material _Color property
        /// </summary>
        public void UpdateColor()
        {
            if (triangleMesh == null)
                return;

            // Set vertex colors (must match vertex count = 3)
            Color[] colors = new Color[3];
            for (int i = 0; i < 3; i++)
                colors[i] = triangleColor;
            triangleMesh.colors = colors;

            // Apply via MaterialPropertyBlock (efficient, no material copies)
            if (mpb != null && meshRenderer != null)
            {
                mpb.SetColor("_Color", triangleColor);
                meshRenderer.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Sets triangle color (public API).
        /// Called by ShapeGenerator when assigning shape colors.
        /// </summary>
        public void SetColor(Color newColor)
        {
            triangleColor = newColor;
            UpdateColor();
        }

        /// <summary>
        /// Sets triangle vertices (public API).
        /// Allows dynamic vertex repositioning.
        /// </summary>
        public void SetVertices(Vector3 a, Vector3 b, Vector3 c)
        {
            vertexA = a;
            vertexB = b;
            vertexC = c;
            UpdateMesh();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Forces complete mesh rebuild when properties change in Inspector.
        /// Called automatically when any serialized field is modified.
        /// </summary>
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                Initialize();

                // Force complete mesh rebuild
                if (triangleMesh != null)
                {
                    // Clear old data
                    triangleMesh.Clear();

                    // Rebuild vertices
                    triangleMesh.vertices = new Vector3[] { vertexA, vertexB, vertexC };
                    
                    // Rebuild triangles (double-sided)
                    triangleMesh.triangles = new int[] { 0, 1, 2, 0, 2, 1 };

                    // Apply vertex colors
                    Color[] colors = new Color[3];
                    for (int i = 0; i < 3; i++)
                        colors[i] = triangleColor;
                    triangleMesh.colors = colors;

                    // Recalculate normals for lighting
                    triangleMesh.RecalculateNormals();
                    triangleMesh.RecalculateBounds();
                }

                // Force material update with new color
                if (meshRenderer != null)
                {
                    var shader = Shader.Find("Custom/SimpleTriangleColor");
                    if (shader != null)
                    {
                        meshRenderer.sharedMaterial.shader = shader;
                        meshRenderer.sharedMaterial.SetColor("_Color", triangleColor);
                    }
                }
            }
        }

        /// <summary>
        /// Editor-only: Continuous color update handler.
        /// Registered with EditorApplication.update to detect color changes in real-time.
        /// </summary>
        private void EditorUpdate()
        {
            // Editor mode: continuous update when color changes
            if (!Application.isPlaying && triangleColor != lastColor)
            {
                UpdateColor();
                lastColor = triangleColor;
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                // Register for editor updates
                EditorApplication.update += EditorUpdate;
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                // Unregister from editor updates
                EditorApplication.update -= EditorUpdate;
            }
        }
#endif
    }
}
