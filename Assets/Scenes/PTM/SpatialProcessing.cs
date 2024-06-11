using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using UnityEngine;

public class SpatialProcessing : MonoBehaviour
{
    bool isStencilEnabled = false;
    public Material stencilMaterial; // Drag and drop the stencil material here in the Inspector
    public Material defaultMaterial; // Assign your default material here
    void OnStencilToggle()
    {
        isStencilEnabled = !isStencilEnabled;
        // Use CoreServices to quickly get access to the IMixedRealitySpatialAwarenessSystem
        var spatialAwarenessService = CoreServices.SpatialAwarenessSystem;
        var dataProviderAccess = spatialAwarenessService as IMixedRealityDataProviderAccess;
        var meshObserverName = "Spatial Object Mesh Observer";
        var spatialObjectMeshObserver = dataProviderAccess.GetDataProvider<IMixedRealitySpatialAwarenessMeshObserver>(meshObserverName);
        if (spatialObjectMeshObserver == null)
        {
            Debug.LogError("Spatial Awareness Mesh Observer not found.");
            return;
        }

        foreach (var meshObject in spatialObjectMeshObserver.Meshes.Values)
        {
            MeshRenderer renderer = meshObject.GameObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = isStencilEnabled ? stencilMaterial : defaultMaterial;
            }
        }
    }

}
