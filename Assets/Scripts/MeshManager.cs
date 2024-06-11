using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using Microsoft.MixedReality.Toolkit;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.Events;
using Unity.VisualScripting;
//using UnityEditor.Experimental.GraphView;
public struct BoundsContainer
{
    public Bounds bounds;
    public bool createMesh;
};

public class MeshManager : MonoBehaviour
{
    [SerializeField]
    private Material webCamMaterial;
    [SerializeField]
    private Material isVisibleMaterial;
    [SerializeField]
    private Material notVisibleMaterial;
    [SerializeField]
    private float boundingRadius = 0.3f;
    [SerializeField]
    Camera cam;
    [SerializeField]
    [Tooltip("HoloLens Runtime uses OpenXR to visualize spatial meshes.")]
    private bool useOpenXRObserver;

    private IMixedRealitySpatialAwarenessSystem spatialAwarenessService;
    private IMixedRealityDataProviderAccess dataProviderAccess;
    private IMixedRealitySpatialAwarenessMeshObserver activeMeshObserver;
    
    Plane[] planes;
    Collider objCollider;
    private bool firstMeshPass = true;
    private GameObject selectionSphere = null;

    // RemoveSurfaceVertices --------
    private bool removingVerts = false;
    private Queue<BoundsContainer> boundingObjectsQueue;
    private bool didCreateRemovedObject = false;
    public UnityEvent RemoveVerticesComplete;
    // ------------------------------
#if UNITY_EDITOR || UNITY_STANDALONE
    private static readonly float FrameTime = .016f;
#else
    private static readonly float FrameTime = .008f;
#endif

    void Start()
    {
        boundingObjectsQueue = new Queue<BoundsContainer>();
        spatialAwarenessService = CoreServices.SpatialAwarenessSystem;
        dataProviderAccess = spatialAwarenessService as IMixedRealityDataProviderAccess;
        if (useOpenXRObserver) // For runtime
        {
            activeMeshObserver = dataProviderAccess.GetDataProvider<IMixedRealitySpatialAwarenessMeshObserver>("OpenXR Spatial Mesh Observer");
        }
        else // For editor
        {
            activeMeshObserver = dataProviderAccess.GetDataProvider<IMixedRealitySpatialAwarenessMeshObserver>("Spatial Object Mesh Observer");
        }
        // meshObservers = dataProviderAccess.GetDataProviders<IMixedRealitySpatialAwarenessMeshObserver>(); //gets all 4 mesh observers

        //foreach(var observer in meshObservers)
        //{
        //    Debug.Log($"{observer.Name} : {observer.Meshes.Count}");
        //}
        // StartCoroutine(PreviewSpatialMesh());
    }

    private IEnumerator PreviewSpatialMesh()
    {
        while (true) {
            planes = GeometryUtility.CalculateFrustumPlanes(cam);
            if (activeMeshObserver.Meshes.Count > 0)
            {
                foreach (SpatialAwarenessMeshObject meshObject in activeMeshObserver.Meshes.Values)
                {
                    GameObject meshGameObject = meshObject.GameObject;
                    var renderer = meshGameObject.GetComponent<MeshRenderer>();
                    objCollider = meshGameObject.GetComponent<Collider>();
                    if (GeometryUtility.TestPlanesAABB(planes, objCollider.bounds))
                    {
                        renderer.material = isVisibleMaterial;
                    }
                    else
                    {
                        renderer.material = notVisibleMaterial;
                    }
                }
            }
            yield return new WaitForSeconds(0.066f);
        }
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

    private IEnumerator RemoveSurfaceVerticesWithinBoundsRoutine()
    {
        if (activeMeshObserver == null)
        {
            Debug.LogError("No Spatial Awareness Mesh Observer found.");
            yield break;
        }

        List<MeshFilter> meshFilters = activeMeshObserver.Meshes.Values.Select(m => m.Filter).ToList();
        // Debug.Log("# Mesh Filters: " + meshFilters.Count);
        float start = Time.realtimeSinceStartup;

        List<Vector3> removedObjectVertices = new List<Vector3>();
        List<Vector3> removedObjectNormals = new List<Vector3>();
        List<int> removedObjectIndices = new List<int>();

        while (boundingObjectsQueue.Count > 0)
        {
            BoundsContainer container = boundingObjectsQueue.Dequeue();
            Bounds bounds = container.bounds;
            // Debug.Log("Sphere bounds: " + bounds);
            
            foreach (MeshFilter filter in meshFilters)
            {
                if (filter == null) continue;

                Mesh mesh = filter.sharedMesh;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

                if (mesh == null || renderer == null || !renderer.bounds.Intersects(bounds)) continue;

                Vector3[] verts = mesh.vertices;
                // Debug.Log(verts.Length);
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
                Debug.Log("Verts to remove from this mesh : " + vertsToRemove.Count);
                int[] indices = mesh.GetTriangles(0);
                Debug.Log($"indices found: {indices.Length}");
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
                
                // This line becomes obsolete, meshes don't need to remove any surface vertices in new MRTK2.
                //if (indices.Length == updatedIndices.Count) continue;

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
                Debug.Log($"# Vertices to remove pre: {removedObjectVertices.Count}");
                MeshCollider collider = filter.gameObject.GetComponent<MeshCollider>();
                if (collider != null)
                {
                    collider.sharedMesh = null;
                    collider.sharedMesh = mesh;
                }
            }

            if (container.createMesh)
            {
                Debug.Log($"# Vertices to remove post: {removedObjectVertices.Count}");
                createRemovedObject(removedObjectVertices, removedObjectNormals, removedObjectIndices);
            }
        }

        Debug.Log("Finished removing vertices.");

        RemoveVerticesComplete?.Invoke();

        removingVerts = false;
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

                BoundsContainer container = new BoundsContainer();
                container.bounds = bounds;
                container.createMesh = createMesh;
                boundingObjectsQueue.Enqueue(container);
            }
        }
    }

    public void GenerateVertexMeshFromSpatial(IEnumerable<GameObject> boundingObjects)
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
        //removedObject2MeshRenderer.material.SetColor("_WireColor", new Color(1, 0, 216.0f / 255.0f, 1));
        //removedObject2MeshRenderer.material.SetColor("_BaseColor", new Color(1, 1, 1, 1));
        //removedObject2MeshRenderer.material.SetFloat("_WireThickness", 400.0f);

        return removedObject;
    }
    /*
        Get the indices of vertices from spatialMesh that are inside the sphere -> indicesList
        From the triangle array of spatialMesh get the triangles that have all the vertex indices inside indicesList -> triangleList
     */

    public GameObject createBoundingMeshGameObject(Vector3[] vertices, int[] triangles)
    {
        // Create and instantiate the new mesh
        GameObject newObject = new GameObject("selectionMesh", typeof(MeshFilter), typeof(MeshCollider), typeof(MeshRenderer));
        Mesh newMesh = new Mesh();
        newMesh.vertices = vertices;
        newMesh.triangles = triangles;
        newObject.GetComponent<MeshFilter>().mesh = newMesh;
        // newObject.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.red);
        return newObject;
    }

    public Mesh combineBoundedMeshes(List<MeshFilter> meshFilters)
    {
        CombineInstance[] combine = new CombineInstance[meshFilters.Count];
        int i = 0;
        while (i < meshFilters.Count)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
            meshFilters[i].gameObject.SetActive(false);

            i++;
        }

        Mesh mesh = new Mesh();
        mesh.CombineMeshes(combine);
        return mesh;
    }

    public void createBoundingMesh(Vector3 pos)
    {
        if (selectionSphere == null)
        {
            selectionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            selectionSphere.GetComponent<MeshRenderer>().enabled = false;
        }
        selectionSphere.transform.position = pos;
        selectionSphere.transform.localScale = new Vector3(boundingRadius,boundingRadius,boundingRadius);
        Bounds bounds = new Bounds();
        Collider boundingCollider = selectionSphere.GetComponent<Collider>();
        if (boundingCollider != null)
        {
            bounds = boundingCollider.bounds;
        }
        List<MeshFilter> meshFilters = activeMeshObserver.Meshes.Values.Select(m => m.Filter).ToList();
        List<MeshFilter> selectionFilters = new List<MeshFilter>();
        List<GameObject> tempGameObjects = new List<GameObject>();
        foreach (MeshFilter filter in meshFilters)
        {
            Mesh mesh = filter.sharedMesh;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null || !renderer.bounds.Intersects(bounds)) continue;

            int[] indices = mesh.GetIndices(0);
            int[] triangleIndices = mesh.GetTriangles(0);
            Vector3[] vertices = mesh.vertices;

            List<int> indicesList = new List<int>();
            List<int> triangleList = new List<int>();

            // Get indices of vertices from this mesh that are inside the sphere
            for(int i = 0; i < vertices.Length; i++)
            {
                if (bounds.Contains(filter.transform.TransformPoint(vertices[i])))
                {
                    indicesList.Add(i);
                }
            }
            // Get triangles with all three vertices within previous indices list
            for(int index = 0; index < triangleIndices.Length; index += 3)
            {
                int indexA = triangleIndices[index];
                int indexB = triangleIndices[index + 1];
                int indexC = triangleIndices[index + 2];
                bool containsA = indicesList.Contains(indexA);
                bool containsB = indicesList.Contains(indexB);
                bool containsC = indicesList.Contains(indexC);
                if (containsA && containsB && containsC)
                {
                    triangleList.Add(indexA);
                    triangleList.Add(indexB);
                    triangleList.Add(indexC);
                }
            }
            Vector3[] newVertices = new Vector3[triangleList.Count];
            int[] newTriangles = new int[triangleList.Count];

            for(int i = 0; i < triangleList.Count; i++)
            {
                newVertices[i] = vertices[triangleList[i]];
                newTriangles[i] = i;
            }
            // Construct the mesh using new vertices and triangles
            Mesh newMesh = new Mesh();
            newMesh.vertices = newVertices;
            newMesh.triangles = newTriangles;
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube); // really whacky way of constructing meshfilters
            MeshFilter newMeshFilter = temp.GetComponent<MeshFilter>();
            newMeshFilter.mesh = newMesh;
            selectionFilters.Add(newMeshFilter);
            tempGameObjects.Add(temp);
        }
        // Now combine meshes into a single object
        GameObject newObject = new GameObject("selectionMesh", typeof(MeshFilter), typeof(MeshCollider), typeof(MeshRenderer));
        newObject.GetComponent<MeshFilter>().mesh = combineBoundedMeshes(selectionFilters);
        newObject.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.red);
        // Destroy Temporary Game Objects
        for (int i = 0; i < tempGameObjects.Count; i++)
        {
            Destroy(tempGameObjects[i]);
        }
    }

    void Update()
    {
        if (activeMeshObserver.Meshes.Count > 0 && firstMeshPass)
        {
            foreach (SpatialAwarenessMeshObject meshObject in activeMeshObserver.Meshes.Values)
            {
                meshObject.GameObject.AddComponent<SpatialMeshPointerHandler>();
            }
            firstMeshPass = false;
        }
    }
}