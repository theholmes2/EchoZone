using EchoZone.Online.Relay;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Canvas 타이틀 화면의 방 생성·코드 참가·종료 입력을 Relay Session 흐름에 연결합니다.</summary>
public sealed class NetworkStartUI : MonoBehaviour
{
    [SerializeField] private RelaySessionGlue relaySessionGlue;
    [SerializeField] private GameObject titleCanvas;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject joinMenuPanel;
    [SerializeField] private GameObject statusPanel;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button showJoinButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button joinBackButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text statusText;

    /// <summary>중복 생성·참가 요청을 막는 현재 비동기 작업 상태입니다.</summary>
    private bool requestPending;

    /// <summary>Canvas 버튼 이벤트를 네트워크 명령에 연결하고 첫 메뉴를 표시합니다.</summary>
    private void Awake()
    {
        createRoomButton?.onClick.AddListener(CreateRoom);
        showJoinButton?.onClick.AddListener(ShowJoinMenu);
        quitButton?.onClick.AddListener(QuitGame);
        joinRoomButton?.onClick.AddListener(JoinRoom);
        joinBackButton?.onClick.AddListener(ShowMainMenu);
        joinCodeInput?.onValueChanged.AddListener(HandleJoinCodeChanged);
        ShowMainMenu();
    }

    /// <summary>등록한 버튼과 입력 이벤트를 제거합니다.</summary>
    private void OnDestroy()
    {
        createRoomButton?.onClick.RemoveListener(CreateRoom);
        showJoinButton?.onClick.RemoveListener(ShowJoinMenu);
        quitButton?.onClick.RemoveListener(QuitGame);
        joinRoomButton?.onClick.RemoveListener(JoinRoom);
        joinBackButton?.onClick.RemoveListener(ShowMainMenu);
        joinCodeInput?.onValueChanged.RemoveListener(HandleJoinCodeChanged);
    }

    /// <summary>연결·복구 상태에 맞춰 타이틀 메뉴와 상태 카드를 전환합니다.</summary>
    private void Update()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || titleCanvas == null) return;
        bool recovering = relaySessionGlue != null &&
            (relaySessionGlue.IsReconnecting || relaySessionGlue.IsMigratingHost);
        bool connected = manager.IsClient || manager.IsServer;
        statusPanel?.SetActive(recovering || connected || requestPending);
        if (mainMenuPanel != null && (recovering || connected)) mainMenuPanel.SetActive(false);
        if (joinMenuPanel != null && (recovering || connected)) joinMenuPanel.SetActive(false);

        if (statusText == null) return;
        if (recovering)
            statusText.text = string.IsNullOrWhiteSpace(relaySessionGlue.RecoveryStatus)
                ? "세션을 복구하는 중입니다..." : relaySessionGlue.RecoveryStatus;
        else if (manager.IsHost)
            statusText.text = $"방 생성 완료\n참가 코드  {relaySessionGlue?.JoinCode}";
        else if (manager.IsClient)
            statusText.text = "방에 참가했습니다";
        else if (requestPending)
            statusText.text = "연결하는 중입니다...";
    }

    /// <summary>방 생성·참가·종료 버튼이 있는 첫 화면을 표시합니다.</summary>
    private void ShowMainMenu()
    {
        mainMenuPanel?.SetActive(true);
        joinMenuPanel?.SetActive(false);
        statusPanel?.SetActive(false);
    }

    /// <summary>참가 코드 입력 화면을 표시하고 입력창에 포커스를 줍니다.</summary>
    private void ShowJoinMenu()
    {
        mainMenuPanel?.SetActive(false);
        joinMenuPanel?.SetActive(true);
        statusPanel?.SetActive(false);
        joinCodeInput?.ActivateInputField();
        HandleJoinCodeChanged(joinCodeInput != null ? joinCodeInput.text : string.Empty);
    }

    /// <summary>새 Relay 방과 Host를 생성합니다.</summary>
    private async void CreateRoom()
    {
        if (requestPending || relaySessionGlue == null) return;
        requestPending = true; SetButtonsInteractable(false);
        bool succeeded = await relaySessionGlue.CreateHostSessionAsync();
        requestPending = false;
        if (!succeeded) ShowMainMenu();
        SetButtonsInteractable(true);
    }

    /// <summary>입력한 참가 코드로 Relay 방에 Client로 접속합니다.</summary>
    private async void JoinRoom()
    {
        string code = joinCodeInput != null ? joinCodeInput.text.Trim() : string.Empty;
        if (requestPending || relaySessionGlue == null || string.IsNullOrEmpty(code)) return;
        requestPending = true; SetButtonsInteractable(false);
        bool succeeded = await relaySessionGlue.JoinSessionAsync(code);
        requestPending = false;
        if (!succeeded) ShowJoinMenu();
        SetButtonsInteractable(true);
    }

    /// <summary>참가 코드 유무에 따라 참가 확인 버튼을 활성화합니다.</summary>
    private void HandleJoinCodeChanged(string value)
    {
        if (joinRoomButton != null)
            joinRoomButton.interactable = !requestPending && !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>비동기 요청 중 메뉴 버튼의 중복 입력을 막습니다.</summary>
    private void SetButtonsInteractable(bool interactable)
    {
        if (createRoomButton != null) createRoomButton.interactable = interactable;
        if (showJoinButton != null) showJoinButton.interactable = interactable;
        if (quitButton != null) quitButton.interactable = interactable;
        if (joinBackButton != null) joinBackButton.interactable = interactable;
        HandleJoinCodeChanged(joinCodeInput != null ? joinCodeInput.text : string.Empty);
    }

    /// <summary>빌드에서는 게임을 종료하고 Editor에서는 Play Mode를 종료합니다.</summary>
    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
