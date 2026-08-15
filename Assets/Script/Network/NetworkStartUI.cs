using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Host, Client, 전용 Server를 시작하고 현재 네트워크 실행 상태를 표시하는 테스트용 UI입니다.
/// </summary>
public class NetworkStartUI : MonoBehaviour
{
    /// <summary>네트워크 상태에 따라 시작 버튼 또는 현재 실행 모드를 표시합니다.</summary>
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

    /// <summary>Host, Client, 전용 Server를 시작할 수 있는 버튼을 표시합니다.</summary>
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

    /// <summary>현재 실행 중인 네트워크 모드를 화면에 표시합니다.</summary>
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
