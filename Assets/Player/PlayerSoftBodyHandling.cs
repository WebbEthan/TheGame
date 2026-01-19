using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;


[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlayerSoftBodyHandling : MonoBehaviour
{
    #region Follow Logic
    private Rigidbody2D parentRigidbody;
    public Transform FollowTarget;
    public float followStrength = 20f;
    public float followDamping = 5f;
    public float SizeMultiplier;
    public float z = -1;
    Vector2 velocity;
    private void LateUpdate()
    {
        parentRigidbody = FollowTarget.gameObject.GetComponent<Rigidbody2D>();
        // --- SOFT FOLLOW ---
        Vector2 targetPos = FollowTarget.position;
        Vector2 currentPos = transform.position;

        velocity += (targetPos - currentPos) * followStrength * Time.deltaTime;
        velocity *= Mathf.Exp(-followDamping * Time.deltaTime);

        Vector2 nextPos = currentPos + velocity * Time.deltaTime;

        // --- CONTAINMENT ---
        nextPos = ConstrainPointToEllipse(nextPos, targetPos, Vector2.one * 0.5f);

        transform.position = new Vector3 (nextPos.x, nextPos.y, z);

        
    }
    private bool _initialized = false;
    private void Update()
    {
        if (_initialized) UpdateSoftBody();
    }
    Vector2 ConstrainPointToEllipse(Vector2 softPos, Vector2 center, Vector2 radius) // ensures soft body does not leave the parent object
    {
        Vector2 offset = softPos - center;

        float nx = offset.x / radius.x;
        float ny = offset.y / radius.y;

        float dist = nx * nx + ny * ny;

        // Inside ellipse → allowed
        if (dist <= 1f)
            return softPos;

        // Outside → project back to boundary
        float scale = 1f / Mathf.Sqrt(dist);
        offset *= scale;

        return center + offset;
    }
    #endregion
    private void Start()
    {
        meshFilter = gameObject.GetComponent<MeshFilter>();
    }
    #region Mesh Logic
    private MeshFilter meshFilter;
    private struct MeshData
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uv;
        public int[] triangles;
        public int[] edge;
    }

    // A softbodys desired position
    private MeshData restingPosition;
    private float softBodySize;
    public async void Resize()
    {

        softBodySize = FollowTarget.localScale.x * SizeMultiplier;
        restingPosition = await ThreadManager.AwaitTaskResultOnThread(ThreadManager.PlayerThreadID,
            () =>
            {
                return GenerateMeshData(softBodySize);
            });
        applyMesh(restingPosition);
        LinkData();
    }
    private void applyMesh(MeshData data)
    {
        renderedMesh = new Mesh();
        renderedMesh.indexFormat = data.vertices.Length > 65535
            ? IndexFormat.UInt32
            : IndexFormat.UInt16;

        renderedMesh.vertices = data.vertices;
        renderedMesh.normals = data.normals;
        renderedMesh.uv = data.uv;
        renderedMesh.triangles = data.triangles;

        meshFilter.mesh = renderedMesh;
    }

    public int LOD;
    private MeshData GenerateMeshData(float size)
    {
        MeshData data = new MeshData();

        int vertsPerSide = LOD;
        int quadsPerSide = LOD - 1;

        int vertCount = vertsPerSide * vertsPerSide;

        Vector3[] verts = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] edge = new int[vertCount];

        List<int> tris = new List<int>();

        float radius = size * 0.5f;
        float edgeEpsilon = size / vertsPerSide; // thickness of edge detection

        // --- VERTICES ---
        int v = 0;
        for (int y = 0; y < vertsPerSide; y++)
        {
            float ty = (float)y / (vertsPerSide - 1);
            float posY = Mathf.Lerp(-radius, radius, ty);

            for (int x = 0; x < vertsPerSide; x++)
            {
                float tx = (float)x / (vertsPerSide - 1);
                float posX = Mathf.Lerp(-radius, radius, tx);

                Vector2 p = new Vector2(posX, posY);
                float dist = p.magnitude;

                // Clamp to circle
                if (dist > radius)
                    p = p.normalized * radius;

                verts[v] = new Vector3(p.x, p.y, 0f);
                normals[v] = Vector3.forward;
                uvs[v] = new Vector2(tx, ty);

                // Edge detection
                edge[v] = Mathf.Abs(dist - radius) <= edgeEpsilon ? 1 : 0;

                v++;
            }
        }

        // --- TRIANGLES ---
        for (int y = 0; y < quadsPerSide; y++)
        {
            for (int x = 0; x < quadsPerSide; x++)
            {
                int i = y * vertsPerSide + x;

                // Only add triangles if at least one vertex is inside the circle
                if (IsQuadValid(i, vertsPerSide, radius, verts))
                {
                    tris.Add(i);
                    tris.Add(i + vertsPerSide);
                    tris.Add(i + 1);

                    tris.Add(i + 1);
                    tris.Add(i + vertsPerSide);
                    tris.Add(i + vertsPerSide + 1);
                }
            }
        }

        data.vertices = verts;
        data.normals = normals;
        data.uv = uvs;
        data.triangles = tris.ToArray();
        data.edge = edge;

        return data;
    }
    private bool IsQuadValid(int i, int stride, float radius, Vector3[] verts)
    {
        return
            verts[i].magnitude <= radius ||
            verts[i + 1].magnitude <= radius ||
            verts[i + stride].magnitude <= radius ||
            verts[i + stride + 1].magnitude <= radius;
    }

    #region SoftBody
    public int vertexCount;
    public int CollisionCount;
    public float CollisionCheckSizeMultiplier;
    public void UpdateSoftBody()
    {
        // Get CPU bound ready for GPU
        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, transform.lossyScale * SizeMultiplier * CollisionCheckSizeMultiplier, 0f, SoftBodyCollisionLayerMask);
        CollisionCount = hits.Length;
        for (int i = 0; i < MaxSoftBodyCollisions; i++)
        {
            if (i < hits.Length)
            {
                Vector3 pos = hits[i].gameObject.transform.position;
                colliders[i].position = new Vector2(pos.x, pos.y);
                colliders[i].rotation = hits[i].gameObject.transform.eulerAngles.z * Mathf.Deg2Rad;
                colliders[i].InUse = 1;
                switch (hits[i])
                {
                    case BoxCollider2D box:
                        colliders[i].type = 0;
                        Vector3 BoxSize = hits[i].gameObject.transform.lossyScale;
                        colliders[i].size = new Vector2(BoxSize.x, BoxSize.y) / 2;
                        break;

                    case CircleCollider2D circle:
                        colliders[i].type = 1;
                        Vector3 ElipseSize = hits[i].gameObject.transform.lossyScale;
                        colliders[i].size = new Vector2(ElipseSize.x, ElipseSize.y) / 2;
                        break;

                    case PolygonCollider2D poly:
                        colliders[i].type = 2;
                        break;
                }
            }
            else
            {
                colliders[i].InUse = 0;
            }
        }
        // Run GPU threads
        InstanceGPU();
    }


    #region GPU Instancing

    [Header("Soft Body Settings")]
    public ComputeShader softBodyCS;

    private int kernel;
    private ComputeBuffer nodeBuffer;
    private ComputeBuffer colliderBuffer;
    private GraphicsBuffer vertexBuffer;

    public LayerMask SoftBodyCollisionLayerMask;
    public const int MaxSoftBodyCollisions = 8;


    [StructLayout(LayoutKind.Sequential)]
    public struct ColliderData
    {
        public Vector2 position;   // 8 bytes
        public Vector2 size;       // 8 bytes
        public float rotation;     // 4 bytes (in radians)
        public uint type;           // 4 bytes
        public uint InUse;          // 4 bytes
        public uint BufferTo32Bytes; // 4 bytes to ensure 32 bytes
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct SoftBodyNode
    {
        public Vector2 restingPos;   // 8
        public Vector2 velocity;     // 8
        public float invMass;        // 4
        public float padding0;       // 4
        public Vector2 previousPos;     // 8  → 32 bytes
    }

    public float MaxStretch;
    public float Stiffness;
    public float Dampening;
    public CollisionDetectionMode collisionDetectionMode;

    private Mesh renderedMesh;
    private SoftBodyNode[] nodes;
    private ColliderData[] colliders = new ColliderData[MaxSoftBodyCollisions];
    public void ResetState()
    {
        for (int i = 0; i < renderedMesh.vertices.Length; i++)
        {
            nodes[i].velocity = Vector2.zero;
            renderedMesh.vertices[i] = nodes[i].restingPos;
        }
        Debug.Log("Reset SoftBody");
    }
    private void LinkData()
    {
        renderedMesh.SetVertexBufferParams(
            renderedMesh.vertexCount,
            new VertexAttributeDescriptor(
                VertexAttribute.Position,
                VertexAttributeFormat.Float32,
                3
            )
        );

        // Sets Node Data
        nodes = new SoftBodyNode[renderedMesh.vertexCount];
        for (int i = 0; i < nodes.Length; i++)
        {
            Vector3 v = renderedMesh.vertices[i];
            nodes[i] = new SoftBodyNode()
            {
                restingPos = new Vector2(v.x, v.y),
                velocity = Vector2.zero,
                invMass = 1f,
                previousPos = Vector2.zero
            };
        }
        // --- Mesh ---
        renderedMesh = GetComponent<MeshFilter>().mesh;
        renderedMesh.MarkDynamic();

        vertexCount = renderedMesh.vertexCount;

        // --- Safety ---
        if (nodes == null || nodes.Length != vertexCount)
        {
            nodes = new SoftBodyNode[vertexCount];
        }

        // --- Buffers ---
        nodeBuffer = new ComputeBuffer(
            vertexCount,
            32,
            ComputeBufferType.Structured
        );
        nodeBuffer.SetData(nodes);

        colliderBuffer = new ComputeBuffer(
            MaxSoftBodyCollisions,
            32,
            ComputeBufferType.Structured
        );
        colliderBuffer.SetData(colliders);

        // Zero-copy access to mesh vertex buffer
        vertexBuffer = renderedMesh.GetVertexBuffer(0);

        // --- Compute Shader ---
        kernel = softBodyCS.FindKernel("UpdateSoftBody");

        softBodyCS.SetBuffer(kernel, "_Nodes", nodeBuffer);
        softBodyCS.SetBuffer(kernel, "_Vertices", vertexBuffer);
        softBodyCS.SetBuffer(kernel, "_Colliders", colliderBuffer);

        softBodyCS.SetInt("_VertexCount", vertexCount);
        softBodyCS.SetInt("_ColliderCount", MaxSoftBodyCollisions);

        OldPos = new Vector2(transform.position.x, transform.position.y);
        _initialized = true;
    }
    private Vector2 OldPos;
    private void InstanceGPU()
    {
        // Dynamic parameters
        softBodyCS.SetInt("_CollisionMode", (int)collisionDetectionMode);
        softBodyCS.SetFloat("_Stiffness", Stiffness);
        softBodyCS.SetFloat("_Dampening", Dampening);
      //  softBodyCS.SetFloat("_MaxStretch", MaxStretch);
        softBodyCS.SetFloat("_DeltaTime", Time.deltaTime);
        softBodyCS.SetVector("_ParentPosition", new Vector2(transform.position.x, transform.position.y));
        softBodyCS.SetVector("_OldParentPosition", OldPos);
        // Update collider data (fixed-size, no realloc)
        colliderBuffer.SetData(colliders);

        // Dispatch
        int threadGroups = Mathf.CeilToInt(renderedMesh.vertexCount / 64f);
        softBodyCS.Dispatch(kernel, threadGroups, 1, 1);

        OldPos = new Vector2(transform.position.x, transform.position.y);
    }


    private void OnDestroy()
    {
        nodeBuffer?.Release();
        colliderBuffer?.Release();
        vertexBuffer?.Release();
    }

    #endregion
    #endregion
    #endregion
}