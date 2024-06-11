using UnityEngine;
using System.Threading;
using System.Net.Sockets;
using System.IO;
using System.Collections.Concurrent;
using System.Net;
using System.Linq;
using UnityEngine.UI;
using System.Threading.Tasks;
using System.Collections;
//using UnityEngine.Windows.WebCam;
//using System.Collections.Generic;

public class TCPImageStream : MonoBehaviour
{
    [SerializeField]
    private bool useUserIP = false;
    [SerializeField]
    private string ipAddressString = "192.168.1.27";
    [SerializeField]
    private int ipPort = 3810;
    [SerializeField]
    private GameObject m_Canvas;
    [SerializeField]
    private LayerMask spatialMask;
    [SerializeField]
    private float defaultDepth = 4f;
    [SerializeField]
    private float scaleOffset = 1.0f;
    [SerializeField]
    private Vector2Int requestedCameraSize = new(896, 504);
    [SerializeField]
    private int cameraFPS = 4;
    [SerializeField]
    private GameObject pointObject;

    public Material sampleMaterial;
    public Texture2D m_Texture = null;

    Renderer m_CanvasRenderer = null;

    Thread m_NetworkThread;
    bool m_NetworkRunning;

    private WebCamTexture webCamTexture;

    ConcurrentQueue<byte[]> sendQueue = new ConcurrentQueue<byte[]>();
    ConcurrentQueue<byte[]> imageQueue = new ConcurrentQueue<byte[]>();
    ConcurrentQueue<Vector2[]> bboxQueue = new ConcurrentQueue<Vector2[]>();

    private string getLocalIPv4()
    {
        return Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(f =>
                                f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();
    }

    private void OnEnable()
    {
        m_NetworkRunning = true;
        m_NetworkThread = new Thread(NetworkThread);
        m_NetworkThread.Start();
        if (!useUserIP)
            ipAddressString = getLocalIPv4();
        // GameObject.Find("/Display IP/Text").GetComponent<Text>().text = ipAddressString;
    }
    private void OnDisable()
    {
        m_NetworkRunning = false;
        if (m_NetworkThread != null)
        {
            if (!m_NetworkThread.Join(100))
            {
                m_NetworkThread.Abort();
            }
        }
        // photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
    }

    //private async Task TakePhotos()
    //{
    //    while (true)
    //    {
    //        Texture2D _texture2D = new Texture2D(webCamTexture.width, webCamTexture.height);
    //        _texture2D.SetPixels32(webCamTexture.GetPixels32());
    //        await Task.Delay(32);
    //        var jpg_data = ImageConversion.EncodeToJPG(_texture2D);
    //        await Task.Delay(32);
    //        sendQueue.Enqueue(jpg_data);
    //    }
    //}

    private IEnumerator TakePhoto()
    {
        while (true)
        {
            // Send Image Frames
            sendQueue.Enqueue(WebCamToBytes(webCamTexture));
            yield return new WaitForSeconds(0.066f);
        }
    }

    private IEnumerator UpdateQuad()
    {
        while (true) {
            if (imageQueue.Count > 0 && imageQueue.TryDequeue(out byte[] data))
            {
                if (m_Texture == null)
                    m_Texture = new Texture2D(1, 1);
                bool isLoaded = m_Texture.LoadImage(data);
                if (isLoaded)
                {
                    m_Texture.Apply();
                    sampleMaterial.mainTexture = m_Texture;
                }
                else
                {
                    Debug.LogError("Failed to load image data into texture.");
                }
            }

            if (bboxQueue.Count > 0 && bboxQueue.TryDequeue(out Vector2[] viewportCoords))
            {
                // Screen to Viewport
                Vector3 center = Vector3.zero;
                Vector3[] worldCoords = new Vector3[viewportCoords.Length];
                for (int i = 0; i < viewportCoords.Length; i++)
                {
                    worldCoords[i] = Camera.main.ViewportToWorldPoint(new Vector3(viewportCoords[i].x, viewportCoords[i].y, defaultDepth));
                    center += worldCoords[i];
                }
                float quadWidth = Mathf.Abs(worldCoords[1].x - worldCoords[0].x) * scaleOffset;
                float quadHeight = Mathf.Abs(worldCoords[2].y - worldCoords[0].y) * scaleOffset;
                center /= worldCoords.Length;

                // Debug.Log(center);
                m_Canvas.transform.position = center;
                pointObject.transform.position = center;
                m_Canvas.transform.localScale = new Vector3(quadWidth, quadHeight, 1f);
                //renderImageInWorld();
            }
            yield return new WaitForSeconds(0.066f);
        }
    }


    private void renderImageInWorld()
    {
        // Debug.Log("Called render");
        Matrix4x4 cameraToWorldMatrix = Camera.main.cameraToWorldMatrix;
        Matrix4x4 worldToCameraMatrix = cameraToWorldMatrix.inverse;

        Matrix4x4 projectionMatrix = Camera.main.projectionMatrix;

        
        m_Texture.wrapMode = TextureWrapMode.Clamp;
        m_CanvasRenderer.sharedMaterial.SetTexture("_MainTex", m_Texture);
        m_CanvasRenderer.sharedMaterial.SetMatrix("_WorldToCameraMatrix", worldToCameraMatrix);
        m_CanvasRenderer.sharedMaterial.SetMatrix("_CameraProjectionMatrix", projectionMatrix);
        m_CanvasRenderer.sharedMaterial.SetFloat("_VignetteScale", 1.0f);
      
        Vector3 position = cameraToWorldMatrix.GetColumn(3) - cameraToWorldMatrix.GetColumn(2);
        // Rotate the canvas object so that it faces the user.
        Quaternion rotation = Quaternion.LookRotation(-cameraToWorldMatrix.GetColumn(2), cameraToWorldMatrix.GetColumn(1));
        m_Canvas.transform.position = position;
        m_Canvas.transform.rotation = rotation;
        // Debug.Log("update position");
    }

    private void Start()
    {
        // m_CanvasRenderer = m_Canvas.GetComponent<Renderer>() as Renderer;
        // m_CanvasRenderer.material = new Material(Shader.Find("AR/HolographicImageBlend"));
        webCamTexture = new WebCamTexture(requestedCameraSize.x, requestedCameraSize.y, cameraFPS);
        webCamTexture.Play();
        StartCoroutine(TakePhoto());
        // StartCoroutine(UpdateQuad()); // does this cause hresult error? 
        // TakePhotos();
        // StartCoroutine(UpdateQuad());
    }

    private void NetworkThread()
    {
        TcpClient client = new TcpClient();
        //IPAddress ipAddress = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0];
        //IPEndPoint ipLocalEndPoint = new IPEndPoint(ipAddress, ipPort);
        //TcpClient client = new TcpClient(ipLocalEndPoint);
        client.Connect(ipAddressString, ipPort);
        Debug.Log($"Connecting to {ipAddressString}/{ipPort}");
        // GameObject.Find("/Display IP/Text").GetComponent<Text>().text = $"Connecting to {ipAddressString}/{ipPort}";
        using (var stream = client.GetStream())
        {
            BinaryReader reader = new BinaryReader(stream);
            BinaryWriter writer = new BinaryWriter(stream);
            try
            {
                while (m_NetworkRunning && client.Connected && stream.CanRead && stream.CanWrite)
                {
                    if (sendQueue.Count > 0 && sendQueue.TryDequeue(out byte[] data))
                    {
                        byte[] lengthPrefix = System.BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length));
                        writer.Write(lengthPrefix);
                        writer.Write(data);
                        writer.Flush();
                    }

                    if (stream.DataAvailable) {
                        //GameObject.Find("/Display IP/Text").GetComponent<Text>().text = $"Received payload";

                        // Read the total length of the data (big-endian int32)
                        int totalLength = IPAddress.NetworkToHostOrder(reader.ReadInt32());

                        // Read the bbox data (6 integers)
                        int bbox_x = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                        int bbox_y = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                        int bbox_w = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                        int bbox_h = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                        int height = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                        int width = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                        Vector2[] screenCoords = ImageToViewport((float)bbox_x, (float)bbox_y,
                                                               (float)bbox_w, (float)bbox_h,
                                                               (float)height, (float)width);
                        bboxQueue.Enqueue(screenCoords);

                        // Read the image data
                        byte[] imageData = reader.ReadBytes(totalLength - 24); // Subtract 24 bytes for the bbox data

                        imageQueue.Enqueue(imageData); // Enqueue the data
                    }
                 
                }

            }
            catch(IOException e) { Debug.Log($"Network Error: {e}"); }
        }
    }

    private byte[] WebCamToBytes(WebCamTexture _webCamTexture)
    {
        Texture2D _texture2D = new Texture2D(_webCamTexture.width, _webCamTexture.height);
        _texture2D.SetPixels32(_webCamTexture.GetPixels32());
        return ImageConversion.EncodeToJPG(_texture2D);
    }

    private Vector2[] ImageToViewport(float bbox_x, float bbox_y, float bbox_w, float bbox_h, float height, float width)
    {
        // Debug.Log($"{bbox_x}, {bbox_y}, {bbox_w}, {bbox_h}, {height}, {width}");

        // Image Space to Screen Space 
        Vector2[] viewportCoords = new Vector2[4];

        viewportCoords[0] = new Vector2(bbox_x / width, bbox_y / height); // top left
        viewportCoords[1] = new Vector2((bbox_x + bbox_w) / width, bbox_y / height);
        viewportCoords[2] = new Vector2(bbox_x / width, (bbox_y + bbox_h) / height); // bottom left
        viewportCoords[3] = new Vector2((bbox_x + bbox_w) / width, (bbox_y + bbox_h) / height);

        return viewportCoords;
    }

    private Vector3? CastOnSpatialMap(Vector3 positionOnPlane, Transform transform)
    {
        if (Physics.SphereCast(transform.position, 0.15f,
                (positionOnPlane - transform.position),
                out var hitInfo, 10, spatialMask))
        {
            return hitInfo.point;
        }
        return null;
    }

    void Update()
    {
        if (imageQueue.Count > 0 && imageQueue.TryDequeue(out byte[] data))
        {
            if (m_Texture == null)
                m_Texture = new Texture2D(1, 1);
            bool isLoaded = m_Texture.LoadImage(data);
            if (isLoaded)
            {
                m_Texture.Apply();
                sampleMaterial.mainTexture = m_Texture;
            }
            else
            {
                Debug.LogError("Failed to load image data into texture.");
            }
        }

        //if (bboxQueue.Count > 0 && bboxQueue.TryDequeue(out Vector2[] viewportCoords))
        //{
        //    // Screen to Viewport
        //    Vector3 center = Vector3.zero;
        //    Vector3[] worldCoords = new Vector3[viewportCoords.Length];
        //    for (int i = 0; i < viewportCoords.Length; i++)
        //    {
        //        worldCoords[i] = Camera.main.ViewportToWorldPoint(new Vector3(viewportCoords[i].x, viewportCoords[i].y, defaultDepth));
        //        center += worldCoords[i];
        //    }
        //    float quadWidth = Mathf.Abs(worldCoords[1].x - worldCoords[0].x) * scaleOffset;
        //    float quadHeight = Mathf.Abs(worldCoords[2].y - worldCoords[0].y) * scaleOffset;
        //    center /= worldCoords.Length;

        //    m_Canvas.transform.position = center;
        //    m_Canvas.transform.localScale = new Vector3(quadWidth, quadHeight, 1f);
        //    // pointObject.transform.position = center; // Point object to debug hologram centers

        //    Matrix4x4 cameraToWorldMatrix = Camera.main.cameraToWorldMatrix;
        //    Quaternion rotation = Quaternion.LookRotation(-cameraToWorldMatrix.GetColumn(2), cameraToWorldMatrix.GetColumn(1));
        //    m_Canvas.transform.rotation = rotation;
        //}
    }
}