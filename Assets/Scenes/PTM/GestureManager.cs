using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

public class GestureManager : MonoBehaviour, IMixedRealityGestureHandler, IMixedRealityInputHandler
{
    public GameObject OverrideFocusedObject { get; set; }
    public GameObject FocusedObject { get; private set; }

    private GameObject selectionSphere;
    private Renderer selectionSphereRenderer;
    private float selectionSphereCurrentScale;
    public Material selectionSphereMaterial;

    private IMixedRealityInputSystem inputSystem = null;
    private IMixedRealityGazeProvider gazeProvider = null;

    private IMixedRealityInputSystem InputSystem => inputSystem ??= CoreServices.InputSystem;
    private IMixedRealityGazeProvider GazeProvider => gazeProvider ??= CoreServices.InputSystem?.GazeProvider;

    void Start()
    {
        // Create selection sphere
        selectionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        selectionSphere.name = "SelectionSphere";
        selectionSphereRenderer = selectionSphere.GetComponent<Renderer>();
        selectionSphereRenderer.material = selectionSphereMaterial;
        selectionSphereRenderer.material.color = new Color(0.5f, 1, 1, 0.3f);
        selectionSphere.layer = LayerMask.NameToLayer("Ignore Raycast");
        selectionSphere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        selectionSphere.transform.position = new Vector3(0.3f, 0, 1.1f);

        // Register the gesture handler
        if (InputSystem != null)
        {
            InputSystem.RegisterHandler<IMixedRealityGestureHandler>(this);
        }
    }

    void OnDestroy()
    {
        if (InputSystem != null)
        {
            InputSystem.UnregisterHandler<IMixedRealityGestureHandler>(this);
        }
    }

    // Implement IMixedRealityGestureHandler interface
    public void OnGestureStarted(InputEventData eventData) { }
    public void OnGestureUpdated(InputEventData eventData) { }
    public void OnGestureUpdated(InputEventData<Vector3> eventData) { }
    public void OnGestureCompleted(InputEventData eventData)
    {
        if (eventData.MixedRealityInputAction.Description == "Select")
        {
            if (FocusedObject != null)
            {
                FocusedObject.SendMessage("OnSelect");
            }

            MySceneManager.Instance.SendMessage("HandleTap", eventData);
        }
    }
    public void OnGestureCompleted(InputEventData<Vector3> eventData)
    {
        if (eventData.MixedRealityInputAction.Description == "Manipulate")
        {
            ManipulateSelectionSphere(eventData.InputData);
        }
    }
    public void OnGestureCanceled(InputEventData eventData) { }

    // Implement IMixedRealityInputHandler interface
    public void OnInputDown(InputEventData eventData) { }
    public void OnInputUp(InputEventData eventData) { }

    private void ManipulateSelectionSphere(Vector3 cumulativeDelta)
    {
        Vector3 viewportDelta = Camera.main.worldToCameraMatrix.MultiplyPoint(Camera.main.transform.position + cumulativeDelta);
        float maxDelta = Mathf.Max(viewportDelta.x, viewportDelta.y, viewportDelta.z);
        float minDelta = Mathf.Min(viewportDelta.x, viewportDelta.y, viewportDelta.z);
        float delta = (Mathf.Abs(maxDelta) > Mathf.Abs(minDelta)) ? maxDelta : minDelta;
        Vector3 currentScale = Vector3.one * selectionSphereCurrentScale;
        Vector3 deltaScale = Vector3.one * delta * 4;
        Vector3 finalScale = currentScale + deltaScale;
        if (finalScale.x < 0) finalScale = Vector3.zero;
        selectionSphere.transform.localScale = finalScale;
    }

    void LateUpdate()
    {
        GameObject oldFocusedObject = FocusedObject;

        if (GazeProvider.GazeTarget != null && OverrideFocusedObject == null)
        {
            FocusedObject = GazeProvider.GazeTarget;
        }
        else
        {
            FocusedObject = OverrideFocusedObject;
        }

        if (FocusedObject != oldFocusedObject)
        {
            // Restart gesture recognition
        }
    }
}
