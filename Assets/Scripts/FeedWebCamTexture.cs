using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class FeedWebCamTexture : MonoBehaviour
{
    [SerializeField]
    private Vector2Int requestedCameraSize = new(896, 504);
    [SerializeField]
    private int cameraFPS = 4;

    public Material material;

    private WebCamTexture webCamTexture;
    private Texture2D m_Texture;

    ConcurrentQueue<byte[]> textureQueue = new ConcurrentQueue<byte[]>();

    void Start()
    {
        // material = Resources.Load<Material>("Material/ProjectorMaterial");
        webCamTexture = new WebCamTexture(requestedCameraSize.x, requestedCameraSize.y, cameraFPS);
        webCamTexture.Play();
        StartCoroutine(TakePhoto());
    }

    private byte[] WebCamToBytes(WebCamTexture _webCamTexture)
    {
        Texture2D _texture2D = new Texture2D(_webCamTexture.width, _webCamTexture.height);
        _texture2D.SetPixels32(_webCamTexture.GetPixels32());
        return ImageConversion.EncodeToJPG(_texture2D);
    }

    private IEnumerator TakePhoto()
    {
        while (true)
        {
            // Send Image Frames
            textureQueue.Enqueue(WebCamToBytes(webCamTexture));
            yield return new WaitForSeconds(0.066f); //15 FPS
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (textureQueue.Count > 0 && textureQueue.TryDequeue(out byte[] data))
        {
            if (m_Texture == null)
                m_Texture = new Texture2D(1, 1);
            bool isLoaded = m_Texture.LoadImage(data);
            if (isLoaded)
            {
                m_Texture.Apply();
                material.mainTexture = m_Texture;
                Matrix4x4 MVP = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false) 
                                * Camera.main.worldToCameraMatrix 
                                * transform.GetComponent<Renderer>().localToWorldMatrix; // use a separate command to create this.
                material.SetMatrix("_MyProjectionMatrix", MVP);
            }
            else
            {
                Debug.LogError("Failed to load image data into texture.");
            }
        }
    }
}
