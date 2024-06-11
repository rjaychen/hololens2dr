using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using UnityEngine;

public class MySceneManager : MonoBehaviour, IMixedRealityPointerHandler
{
    private static MySceneManager _instance;
    public static MySceneManager Instance { get { return _instance; } }
    public enum State
    {
        takingPictures,
        choosingObject,
        viewingStencil
    };

    public State state = State.takingPictures;

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        else if (_instance != this)
            Destroy(gameObject);
    }

    void Start()
    {
        // Register the pointer handler with the input system
        if (CoreServices.InputSystem != null)
        {
            CoreServices.InputSystem.RegisterHandler<IMixedRealityPointerHandler>(this);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    // Handle tap event
    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        GameObject.Find("Speech Manager").GetComponent<AudioSource>().Play();
        switch (state)
        {
            case State.takingPictures:
                // Take a picture from the current location
                ProjectiveTextureMapping.Instance.SendMessage("TakeSnapshot");
                break;
            case State.choosingObject:
                // If currently gazing at an object, remove that object
                if (CoreServices.InputSystem.FocusProvider.PrimaryPointer != null &&
                    CoreServices.InputSystem.FocusProvider.PrimaryPointer.Result != null &&
                    CoreServices.InputSystem.FocusProvider.PrimaryPointer.Result.Details.Object != null)
                {
                    GameObject selectionSphere = GameObject.Find("SelectionSphere");
                    selectionSphere.transform.position = CoreServices.InputSystem.FocusProvider.PrimaryPointer.Result.Details.Point;
                    selectionSphere.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                    // Remove all vertices within the selection sphere
                    ProjectiveTextureMapping.Instance.SendMessage("RemoveSelection");

                    // Auto-advance to the next state
                    AdvanceState();
                }
                break;
            case State.viewingStencil:
                // Toggle stencil shader
                ToggleStencil();
                break;
        }
    }

    public void AdvanceState()
    {
        switch (state)
        {
            case State.takingPictures:
                state = State.choosingObject;
                break;
            case State.choosingObject:
                state = State.viewingStencil;
                break;
            case State.viewingStencil:
                break;
        }
    }

    void ToggleStencil()
    {
        GameObject removedObject = GameObject.Find("RemovedObject");
        GameObject removedObject2 = GameObject.Find("RemovedObject2");
        GameObject stencilSphere = GameObject.Find("StencilSphere");

        if (removedObject == null || removedObject2 == null || stencilSphere == null) return;

        removedObject.GetComponent<Renderer>().enabled = !removedObject.GetComponent<Renderer>().enabled;
        removedObject2.GetComponent<Renderer>().enabled = !removedObject2.GetComponent<Renderer>().enabled;
        stencilSphere.GetComponent<Renderer>().enabled = !stencilSphere.GetComponent<Renderer>().enabled;
    }

    // Implement the remaining interface methods with empty bodies
    public void OnPointerDown(MixedRealityPointerEventData eventData) { }

    public void OnPointerDragged(MixedRealityPointerEventData eventData) { }

    public void OnPointerUp(MixedRealityPointerEventData eventData) { }

    private void OnDestroy()
    {
        // Unregister the pointer handler from the input system
        if (CoreServices.InputSystem != null)
        {
            CoreServices.InputSystem.UnregisterHandler<IMixedRealityPointerHandler>(this);
        }
    }

    private void HandleTap()
    {

    }

}
