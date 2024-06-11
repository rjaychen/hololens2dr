using Microsoft.MixedReality.Toolkit.Input;
using UnityEngine;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;

public class SpeechManager : MonoBehaviour, IMixedRealitySpeechHandler
{
    [SerializeField]
    GameObject listener;

    [SerializeField]
    Material projectiveTextureMappingMaterial;

    int shaderType = 0;

    private void OnEnable()
    {
        // Register the handler with the input system
        CoreServices.InputSystem?.RegisterHandler<IMixedRealitySpeechHandler>(this);
    }

    private void OnDisable()
    {
        // Unregister the handler from the input system
        CoreServices.InputSystem?.UnregisterHandler<IMixedRealitySpeechHandler>(this);
    }

    public void OnSpeechKeywordRecognized(SpeechEventData eventData)
    {
        if (eventData.Command.Keyword.ToLower() == "take picture")
        {
            listener.SendMessage("TakeSnapshot");
        }
        if (eventData.Command.Keyword.ToLower() == "end picture mode")
        {
            this.GetComponent<AudioSource>().Play();
            MySceneManager.Instance.AdvanceState();
        }
        if (eventData.Command.Keyword.ToLower() == "finalize scan")
        {
            var meshObserverName = "Spatial Object Mesh Observer";
            CoreServices.SpatialAwarenessSystem.SuspendObserver<IMixedRealitySpatialAwarenessMeshObserver>(meshObserverName);
        }
        if (eventData.Command.Keyword.ToLower() == "continue scan")
        {
            var meshObserverName = "Spatial Object Mesh Observer";
            CoreServices.SpatialAwarenessSystem.ResumeObserver<IMixedRealitySpatialAwarenessMeshObserver>(meshObserverName);
        }
        if (eventData.Command.Keyword.ToLower() == "undo picture")
        {
            ProjectiveTextureMapping.Instance.UndoPicture();
        }
        projectiveTextureMappingMaterial.SetInt("_ShaderType", shaderType);
        if (eventData.Command.Keyword.ToLower() == "switch shader") {
            shaderType = (shaderType + 1) % 4;
            projectiveTextureMappingMaterial.SetInt("_ShaderType", shaderType);
        };
    }

}
