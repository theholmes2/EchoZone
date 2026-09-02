using EchoZone.Online.Relay;
using EchoZone.Online.Migration;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Host, Client, 전용 Server를 시작하고 현재 네트워크 실행 상태를 표시하는 테스트용 UI입니다.
/// </summary>
public class NetworkStartUI : MonoBehaviour
{
    [SerializeField] private RelaySessionGlue relaySessionGlue;
    [SerializeField] private HostMigrationCloudCheckpointGlue cloudCheckpointGlue;

    /// <summary>Client가 참가할 Relay Session의 Join Code 입력값입니다.</summary>
    private string joinCode = string.Empty;

    /// <summary>네트워크 상태에 따라 시작 버튼 또는 현재 실행 모드를 표시합니다.</summary>
    private void OnGUI()
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        if (relaySessionGlue != null &&
            (relaySessionGlue.IsReconnecting || relaySessionGlue.IsMigratingHost))
        {
            DrawMigrationStatus();
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
            if (relaySessionGlue != null)
            {
                _ = relaySessionGlue.CreateHostSessionAsync();
            }
        }

        if (GUI.Button(new Rect(20, 70, 150, 40), "Start Client"))
        {
            if (relaySessionGlue != null)
            {
                _ = relaySessionGlue.JoinSessionAsync(joinCode);
            }
        }

        joinCode = GUI.TextField(
            new Rect(180, 70, 150, 40),
            joinCode,
            12);

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

        if (NetworkManager.Singleton.IsHost &&
            relaySessionGlue != null &&
            !string.IsNullOrEmpty(relaySessionGlue.JoinCode))
        {
            GUI.Label(
                new Rect(20, 50, 250, 30),
                $"Join Code: {relaySessionGlue.JoinCode}");

            if (cloudCheckpointGlue != null &&
                GUI.Button(new Rect(20, 90, 190, 40), "Save Cloud Snapshot"))
            {
                _ = cloudCheckpointGlue.SaveCurrentCheckpointAsync();
            }

            if (GUI.Button(new Rect(20, 140, 190, 40), "Test Host Migration"))
            {
                _ = relaySessionGlue.ForceHostExitForMigrationTestAsync();
            }
        }

        if (NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsHost &&
            relaySessionGlue != null &&
            GUI.Button(new Rect(20, 50, 190, 40), "Test Disconnect"))
        {
            relaySessionGlue.ForceClientDisconnectForTest();
        }

        if (NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsHost &&
            relaySessionGlue != null &&
            GUI.Button(new Rect(20, 100, 190, 40), "Test Invalid Ticket"))
        {
            relaySessionGlue.ForceClientDisconnectWithInvalidTicketForTest();
        }

        if (NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsHost &&
            cloudCheckpointGlue != null &&
            GUI.Button(new Rect(20, 150, 220, 40), "Test Cloud Host Permission"))
        {
            _ = cloudCheckpointGlue.TestUnauthorizedClientSaveAsync();
        }
    }

    /// <summary>자동 재접속이 진행 중임을 화면에 표시합니다.</summary>
    private void DrawMigrationStatus()
    {
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), string.Empty);
        GUI.Label(
            new Rect(20, 20, Screen.width - 40, 60),
            relaySessionGlue != null && !string.IsNullOrEmpty(relaySessionGlue.RecoveryStatus)
                ? relaySessionGlue.RecoveryStatus
                : "Relay reconnecting...");
    }
}
