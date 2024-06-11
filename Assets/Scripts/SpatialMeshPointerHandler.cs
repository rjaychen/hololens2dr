using Microsoft.MixedReality.Toolkit.Input;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;

public class SpatialMeshPointerHandler : MonoBehaviour, IMixedRealityFocusHandler, IMixedRealityPointerHandler
{
    private Color color_IdleState = Color.white;
    private Color color_OnHover = Color.cyan;
    private Color color_OnSelect = Color.blue;
    private Material material;
    private GameObject boundingSphere = null;


    [SerializeField]
    MeshManager meshManager;

    private void Awake()
    {
        material = GetComponent<Renderer>().material;
        meshManager = FindObjectOfType<MeshManager>();
    }

    void IMixedRealityFocusHandler.OnFocusEnter(FocusEventData eventData)
    {
        // material.color = color_OnHover;
        //material.SetColor("_BaseColor", color_OnHover);
        // Debug.Log("Hovering on " + gameObject.name);
        meshManager.runtimeDebuggerText.GetComponent<Text>().text = $"hovering on {gameObject.name}";
    }

    void IMixedRealityFocusHandler.OnFocusExit(FocusEventData eventData)
    {
        //material.color = color_IdleState;
        //material.SetColor("_BaseColor", color_IdleState);
    }

    void IMixedRealityPointerHandler.OnPointerUp(
         MixedRealityPointerEventData eventData)
    { }

    void IMixedRealityPointerHandler.OnPointerDown(
         MixedRealityPointerEventData eventData)
    { }

    void IMixedRealityPointerHandler.OnPointerDragged(
         MixedRealityPointerEventData eventData)
    { }

    void IMixedRealityPointerHandler.OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        //material.SetColor("_BaseColor", color_OnSelect);//  = color_OnSelect;
        var spawnPosition = eventData.Pointer.Result.Details.Point;
        StartCoroutine(meshManager.createBoundingMesh(spawnPosition));
        meshManager.runtimeDebuggerText.GetComponent<Text>().text = $"clicked {gameObject.name}";
        //meshManager.GenerateVertexMeshFromSpatial(new List<GameObject>() { boundingSphere });
    }

}
