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

    [SerializeField]
    public GameObject runtimeDebuggerText;

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

    public IEnumerator createBoundingMesh(Vector3 pos)
    {            
        if (selectionSphere == null)
            {
                selectionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                selectionSphere.GetComponent<MeshRenderer>().enabled = false;
            }
            selectionSphere.transform.position = pos;
            selectionSphere.transform.localScale = new Vector3(boundingRadius, boundingRadius, boundingRadius);
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
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (bounds.Contains(filter.transform.TransformPoint(vertices[i])))
                    {
                        indicesList.Add(i);
                    }
                }
                // Get triangles with all three vertices within previous indices list
                for (int index = 0; index < triangleIndices.Length; index += 3)
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

                for (int i = 0; i < triangleList.Count; i++)
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
            newObject.GetComponent<MeshRenderer>().material = isVisibleMaterial;
            gameObject.GetComponent<AudioSource>().Play();
            // Destroy Temporary Game Objects
            for (int i = 0; i < tempGameObjects.Count; i++)
            {
                Destroy(tempGameObjects[i]);
            }
            yield return new WaitForSeconds(0.015f);
    }

    void Update()
    {
        if (activeMeshObserver.Meshes.Count > 0 && firstMeshPass)
        {
            foreach (SpatialAwarenessMeshObject meshObject in activeMeshObserver.Meshes.Values)
            {
                if (meshObject.GameObject.GetComponent<SpatialMeshPointerHandler>() == null)
                    meshObject.GameObject.AddComponent<SpatialMeshPointerHandler>();
            }
        }
    }
}