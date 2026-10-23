using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class ConnectMenu : MonoBehaviour
{
    string ip = "192.168.1.5";
    ushort port = 7777;

    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        GUI.matrix = Matrix4x4.Scale(Vector3.one * (Screen.height / 540f));

        if (nm.IsListening)
        {
            string role = nm.IsHost ? "HOST (Player A)" : "CLIENT (Player B)";
            GUI.Label(new Rect(20, 20, 600, 30), role + "  |  Players: " + nm.ConnectedClientsIds.Count);
            return;
        }

        GUI.Label(new Rect(20, 20, 300, 30), "Host ka IP:");
        ip = GUI.TextField(new Rect(20, 50, 300, 40), ip);

        if (GUI.Button(new Rect(20, 110, 300, 60), "HOST (Player A)"))
        {
            SetTransport(nm, "0.0.0.0");
            nm.StartHost();
        }
        if (GUI.Button(new Rect(20, 190, 300, 60), "JOIN (Player B)"))
        {
            SetTransport(nm, ip);
            nm.StartClient();
        }
    }

    void SetTransport(NetworkManager nm, string address)
    {
        var utp = nm.GetComponent<UnityTransport>();
        utp.SetConnectionData(address, port, "0.0.0.0");
    }
}