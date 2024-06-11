using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using Microsoft.MixedReality.Toolkit;
using MIConvexHull;

public class RemoveSurfaceVertices : MonoBehaviour
{
    private static RemoveSurfaceVertices instance;

    public static RemoveSurfaceVertices Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<RemoveSurfaceVertices>();
                if (instance == null)
                {
                    GameObject singleton = new GameObject(typeof(RemoveSurfaceVertices).Name);
                    instance = singleton.AddComponent<RemoveSurfaceVertices>();
                    DontDestroyOnLoad(singleton);
                }
            }
            return instance;
        }
    }

    struct BoundsContainer
    {
        public Bounds bounds;
        public bool createMesh;
    };

    [Tooltip("The amount, if any, to expand each bounding volume by.")]
    public float BoundsExpansion = 0.0f;

    [Tooltip("The amount, if any, to expand the convex hull by.")]
    public float ConvexHullExpansion = 0.1f;

    public UnityEvent RemoveVerticesComplete;

    private bool removingVerts = false;
    private Queue<BoundsContainer> boundingObjectsQueue;

#if UNITY_EDITOR || UNITY_STANDALONE
    private static readonly float FrameTime = .016f;
#else
    private static readonly float FrameTime = .008f;
#endif

    public Material removedObjectMaterial;
    public Material removedObjectWireframeMaterial;
    [Tooltip("The projective texture mapping material.")]
    public Material PTMMaterial;
    private bool didCreateRemovedObject = false;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(this.gameObject);
        boundingObjectsQueue = new Queue<BoundsContainer>();
    }

    public void RemoveSurfaceVerticesWithinBounds(IEnumerable<GameObject> boundingObjects)
    {
        if (boundingObjects == null) return;

        if (!removingVerts)
        {
            removingVerts = true;
            AddBoundingObjectsToQueue(boundingObjects, false);
            StartCoroutine(RemoveSurfaceVerticesWithinBoundsRoutine());
        }
        else
        {
            AddBoundingObjectsToQueue(boundingObjects, false);
        }
    }

    public void RemoveSurfaceVerticesWithinBoundsAndGenerateMesh(IEnumerable<GameObject> boundingObjects)
    {
        if (boundingObjects == null) return;

        if (!removingVerts)
        {
            removingVerts = true;
            AddBoundingObjectsToQueue(boundingObjects, true);
            StartCoroutine(RemoveSurfaceVerticesWithinBoundsRoutine());
        }
        else
        {
            AddBoundingObjectsToQueue(boundingObjects, true);
        }
    }

    private void AddBoundingObjectsToQueue(IEnumerable<GameObject> boundingObjects, bool createMesh)
    {
        foreach (GameObject item in boundingObjects)
        {
            Bounds bounds = new Bounds();

            Collider boundingCollider = item.GetComponent<Collider>();
            if (boundingCollider != null)
            {
                bounds = boundingCollider.bounds;

                if (BoundsExpansion > 0.0f)
                {
                    bounds.Expand(BoundsExpansion);
                }

                BoundsContainer container = new BoundsContainer();
                container.bounds = bounds;
                container.createMesh = createMesh;
                boundingObjectsQueue.Enqueue(container);
            }
        }
    }

    private IEnumerator RemoveSurfaceVerticesWithinBoundsRoutine()
    {
        IMixedRealitySpatialAwarenessMeshObserver meshObserver = CoreServices.GetSpatialAwarenessSystemDataProvider<IMixedRealitySpatialAwarenessMeshObserver>();
        if (meshObserver == null)
        {
            Debug.LogError("No Spatial Awareness Mesh Observer found.");
            yield break;
        }

        List<MeshFilter> meshFilters = meshObserver.Meshes.Values.Select(m => m.Filter).ToList();
        float start = Time.realtimeSinceStartup;

        List<Vector3> removedObjectVertices = new List<Vector3>();
        List<Vector3> removedObjectNormals = new List<Vector3>();
        List<int> removedObjectIndices = new List<int>();

        while (boundingObjectsQueue.Count > 0)
        {
            BoundsContainer container = boundingObjectsQueue.Dequeue();
            Bounds bounds = container.bounds;

            foreach (MeshFilter filter in meshFilters)
            {
                if (filter == null) continue;

                Mesh mesh = filter.sharedMesh;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

                if (mesh == null || renderer == null || !renderer.bounds.Intersects(bounds)) continue;

                Vector3[] verts = mesh.vertices;
                List<int> vertsToRemove = new List<int>();

                for (int i = 0; i < verts.Length; ++i)
                {
                    if (bounds.Contains(filter.transform.TransformPoint(verts[i])))
                    {
                        vertsToRemove.Add(i);
                    }

                    if ((Time.realtimeSinceStartup - start) > FrameTime)
                    {
                        yield return null;
                        start = Time.realtimeSinceStartup;
                    }
                }

                if (vertsToRemove.Count == 0) continue;

                int[] indices = mesh.GetTriangles(0);
                List<int> updatedIndices = new List<int>();
                List<int> removedIndices = new List<int>();
                List<int> boundaryVertices = new List<int>();

                for (int index = 0; index < indices.Length; index += 3)
                {
                    int indexA = indices[index];
                    int indexB = indices[index + 1];
                    int indexC = indices[index + 2];
                    bool containsA = vertsToRemove.Contains(indexA);
                    bool containsB = vertsToRemove.Contains(indexB);
                    bool containsC = vertsToRemove.Contains(indexC);
                    if (containsA || containsB || containsC)
                    {
                        removedIndices.Add(indexA);
                        removedIndices.Add(indexB);
                        removedIndices.Add(indexC);
                        if (!containsA && !boundaryVertices.Contains(indexA)) boundaryVertices.Add(indexA);
                        if (!containsB && !boundaryVertices.Contains(indexB)) boundaryVertices.Add(indexB);
                        if (!containsC && !boundaryVertices.Contains(indexC)) boundaryVertices.Add(indexC);
                    }
                    else
                    {
                        updatedIndices.Add(indexA);
                        updatedIndices.Add(indexB);
                        updatedIndices.Add(indexC);
                    }

                    if ((Time.realtimeSinceStartup - start) > FrameTime)
                    {
                        yield return null;
                        start = Time.realtimeSinceStartup;
                    }
                }

                if (indices.Length == updatedIndices.Count) continue;

                mesh.SetTriangles(updatedIndices.ToArray(), 0);
                mesh.RecalculateBounds();
                yield return null;
                start = Time.realtimeSinceStartup;

                if (container.createMesh)
                {
                    SortedDictionary<int, int> vertexMap = new SortedDictionary<int, int>();
                    vertsToRemove.AddRange(boundaryVertices);
                    vertsToRemove.Sort();
                    for (int k = 0; k < vertsToRemove.Count; k++)
                    {
                        int index = vertsToRemove[k];
                        vertexMap.Add(index, k + removedObjectVertices.Count);
                    }
                    for (int k = 0; k < vertsToRemove.Count; k++)
                    {
                        int index = vertsToRemove[k];
                        Vector3 vertex = filter.transform.localToWorldMatrix.MultiplyPoint(verts[index]);
                        removedObjectVertices.Add(vertex);
                        Vector3 normal = mesh.normals[index];
                        removedObjectNormals.Add(normal);
                    }
                    for (int k = 0; k < removedIndices.Count; k++)
                    {
                        int oldIndex = removedIndices[k];
                        int newIndex = vertexMap[oldIndex];
                        removedObjectIndices.Add(newIndex);
                    }

                    yield return null;
                    start = Time.realtimeSinceStartup;
                }

                MeshCollider collider = filter.gameObject.GetComponent<MeshCollider>();
                if (collider != null)
                {
                    collider.sharedMesh = null;
                    collider.sharedMesh = mesh;
                }
            }

            if (container.createMesh)
            {
                createRemovedObject(removedObjectVertices, removedObjectNormals, removedObjectIndices);
            }
        }

        Debug.Log("Finished removing vertices.");

        RemoveVerticesComplete?.Invoke();

        removingVerts = false;
    }

    GameObject createRemovedObject(List<Vector3> vertices, List<Vector3> normals, List<int> indices)
    {
        GameObject removedObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        removedObject.AddComponent<MeshFilter>();
        removedObject.AddComponent<MeshCollider>();
        removedObject.AddComponent<MeshRenderer>();
        //GameObject removedObject = new GameObject("RemovedObject", typeof(MeshFilter), typeof(MeshCollider), typeof(MeshRenderer));
        //MeshFilter removedObjectMeshFilter = removedObject.GetComponent<MeshFilter>();
        MeshRenderer removedObjectMeshRenderer = removedObject.GetComponent<MeshRenderer>();
        //MeshCollider removedObjectMeshCollider = removedObject.GetComponent<MeshCollider>();
        Mesh removedObjectMesh = new Mesh();
        removedObjectMesh.SetVertices(vertices);
        removedObjectMesh.SetNormals(normals);
        removedObjectMesh.SetTriangles(indices.ToArray(), 0);

        // Update the mesh of the removed object
        //var convexHullMesh = getConvexHullMesh(vertices);
        //removedObjectMeshFilter.mesh = convexHullMesh;
        //removedObjectMeshCollider.sharedMesh = convexHullMesh;
        removedObjectMeshRenderer.material = removedObjectMaterial;
        Debug.Log("Finished creating removed mesh.");

        // Remove all vertices within the computed convex hull mesh
        didCreateRemovedObject = true;
        RemoveSurfaceVerticesWithinBounds(new List<GameObject>() { removedObject });
        Debug.Log("Finished removing vertices within convex hull");

        // Create a copy of the removed object to display a wireframe of it
        GameObject removedObject2 = new GameObject("RemovedObject2", typeof(MeshFilter), typeof(MeshCollider), typeof(MeshRenderer));
        MeshFilter removedObject2MeshFilter = removedObject2.GetComponent<MeshFilter>();
        MeshRenderer removedObject2MeshRenderer = removedObject2.GetComponent<MeshRenderer>();
        MeshCollider removedObject2MeshCollider = removedObject2.GetComponent<MeshCollider>();
        removedObject2MeshFilter.mesh = removedObjectMesh;
        removedObject2MeshCollider.sharedMesh = removedObjectMesh;
        removedObject2MeshRenderer.material = removedObjectWireframeMaterial;
        removedObject2MeshRenderer.material.SetColor("_WireColor", new Color(1, 0, 216.0f / 255.0f, 1));
        removedObject2MeshRenderer.material.SetColor("_BaseColor", new Color(1, 1, 1, 1));
        removedObject2MeshRenderer.material.SetFloat("_WireThickness", 400.0f);

        return removedObject;
    }
    Mesh getConvexHullMesh(IEnumerable<Vector3> points)
    {
        Mesh mesh = new Mesh();
        List<int> triangles = new List<int>();

        // Convert vec3s to IVectors
        var verts = points.Select(x => new MIVertex(x)).ToList();

        // Find convex hull
        var convexHull = MIConvexHull.ConvexHull.Create(verts);

        // Extract triangle indices
        var convexPoints = convexHull.Points.ToList();
        foreach (var face in convexHull.Faces)
        {
            triangles.Add(convexPoints.IndexOf(face.Vertices[0]));
            triangles.Add(convexPoints.IndexOf(face.Vertices[1]));
            triangles.Add(convexPoints.IndexOf(face.Vertices[2]));
        }

        Vector3 averagePos = new Vector3(0, 0, 0);
        Vector3[] vertices = convexHull.Points.Select(x => x.ToVec()).ToArray();
        // Compute average position
        foreach (var vertex in vertices)
        {
            averagePos += vertex;
        }
        averagePos /= vertices.Length;
        // Expand convex hull
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] += (vertices[i] - averagePos).normalized * ConvexHullExpansion;
        }

        // Update the mesh object and compute normals
        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        return mesh;
    }

}