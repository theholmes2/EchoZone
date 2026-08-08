using Unity.Netcode;
using UnityEngine;

public class NetworkStartUI : MonoBehaviour
{
    private void OnGUI()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        if (!NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsServer)
        {
            DrawStartButtons();
            return;
        }

        DrawNetworkStatus();
    }

    private void DrawStartButtons()
    {
        if (GUI.Button(new Rect(20, 20, 150, 40), "Start Host"))
        {
            NetworkManager.Singleton.StartHost();
        }

        if (GUI.Button(new Rect(20, 70, 150, 40), "Start Client"))
        {
            NetworkManager.Singleton.StartClient();
        }

        if (GUI.Button(new Rect(20, 120, 150, 40), "Start Server"))
        {
            NetworkManager.Singleton.StartServer();
        }
    }

    private void DrawNetworkStatus()
    {
        string mode;

        if (NetworkManager.Singleton.IsHost)
        {
            mode = "Host";
        }
        else if (NetworkManager.Singleton.IsServer)
        {
            mode = "Server";
        }
        else
        {
            mode = "Client";
        }

        GUI.Label(new Rect(20, 20, 200, 30), $"Mode: {mode}");
    }
}