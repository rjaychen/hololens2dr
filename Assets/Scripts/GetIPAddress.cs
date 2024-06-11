using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;

public class GetIPAddress : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        string myIP = Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(f =>
                                       f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();
        Debug.Log(myIP);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
