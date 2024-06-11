using UnityEngine;

public class Interact : MonoBehaviour
{

    public GameObject listener;

    void OnSelect()
    {
        listener.SendMessage("TakeSnapshot");
    }
}
