using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using EchoZone.Online.Migration;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Online.Reconnect
{
    /// <summary>
    /// 인증 PlayerId와 NGO ClientId를 연결하고 서버 메모리의 임시 플레이어 상태를 관리하는 Glue입니다.
    /// </summary>
    public sealed class NetworkPlayerSessionCacheGlue : MonoBehaviour
    {
        /// <summary>CacheEntry 관련 기능과 데이터를 제공하는 형식입니다.</summary>
        private sealed class CacheEntry
        {
            /// <summary>Snapshot 값을 저장합니다.</summary>
            public PlayerSessionSnapshot Snapshot;
            /// <summary>ExpiresAt 값을 저장합니다.</summary>
            public float ExpiresAt = float.PositiveInfinity;
            /// <summary>Ticket 값을 저장합니다.</summary>
            public string Ticket = string.Empty;
            /// <summary>IsConnected 값을 저장합니다.</summary>
            public bool IsConnected;
        }

        [SerializeField] private RelaySessionConfig config;
        [SerializeField] private ItemCatalog itemCatalog;

        /// <summary>cachedStates 값을 저장합니다.</summary>
        private readonly Dictionary<string, CacheEntry> cachedStates = new();
        /// <summary>playerIdsByClientId 값을 저장합니다.</summary>
        private readonly Dictionary<ulong, string> playerIdsByClientId = new();
        /// <summary>restoreAuthorizedClientIds 값을 저장합니다.</summary>
        private readonly HashSet<ulong> restoreAuthorizedClientIds = new();

        /// <summary>Instance 값을 제공합니다.</summary>
        public static NetworkPlayerSessionCacheGlue Instance { get; private set; }

        /// <summary>Snapshot의 ItemId를 실제 아이템 정의로 복원하는 카탈로그입니다.</summary>
        public ItemCatalog ItemCatalog => itemCatalog;

        /// <summary>서버 연결 승인에서 기록한 계정 ID를 제공합니다. RPC로 받은 계정 문자열을 사용하지 않습니다.</summary>
        public bool TryGetPlayerId(ulong clientId, out string playerId) => playerIdsByClientId.TryGetValue(clientId, out playerId);

        /// <summary>명시적인 새 방 생성에서만 이전 방의 연결·티켓 캐시를 폐기합니다. 마이그레이션에는 호출하지 않습니다.</summary>
        public void ResetForNewSession()
        {
            cachedStates.Clear(); playerIdsByClientId.Clear(); restoreAuthorizedClientIds.Clear();
            migrationAuthorizedPlayerIds.Clear();
        }

        /// <summary>새 Host가 이전 Host의 티켓 대신 Session Snapshot으로 승인할 PlayerId입니다.</summary>
        private readonly HashSet<string> migrationAuthorizedPlayerIds = new();

        /// <summary>Awake 작업을 수행합니다.</summary>
        private void Awake()
        {
            Instance = this;
        }

        /// <summary>Start 작업을 수행합니다.</summary>
        private void Start()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return;
            }

            networkManager.ConnectionApprovalCallback = HandleConnectionApproval;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        /// <summary>OnDestroy 작업을 수행합니다.</summary>
        private void OnDestroy()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                if (networkManager.ConnectionApprovalCallback == HandleConnectionApproval)
                {
                    networkManager.ConnectionApprovalCallback = null;
                }

                networkManager.OnClientConnectedCallback -= HandleClientConnected;
                networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Update 작업을 수행합니다.</summary>
        private void Update()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            List<string> expiredPlayerIds = null;
            foreach (KeyValuePair<string, CacheEntry> pair in cachedStates)
            {
                if (pair.Value.ExpiresAt > Time.unscaledTime)
                {
                    continue;
                }

                expiredPlayerIds ??= new List<string>();
                expiredPlayerIds.Add(pair.Key);
            }

            if (expiredPlayerIds == null)
            {
                return;
            }

            for (int i = 0; i < expiredPlayerIds.Count; i++)
            {
                cachedStates.Remove(expiredPlayerIds[i]);
            }
        }

        /// <summary>서버가 연결 요청의 PlayerId를 읽고 PlayerObject 생성을 승인합니다.</summary>
        private void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            string serializedCredential = request.Payload != null
                ? Encoding.UTF8.GetString(request.Payload)
                : string.Empty;

            bool validFormat = ReconnectCredential.TryDeserialize(
                serializedCredential,
                out ReconnectCredential credential);
            bool approved = validFormat &&
                ValidateCredential(request.ClientNetworkId, credential);

            response.Approved = approved;
            response.CreatePlayerObject = approved;
            response.Position = Vector3.up * 2f;
            response.Pending = false;
            response.Reason = approved
                ? string.Empty
                : "Reconnect credential is invalid or expired.";

            if (approved)
            {
                playerIdsByClientId[request.ClientNetworkId] = credential.PlayerId;
                EchoZone.Online.OnlineDebugLog.Info(
                    $"Session credential approved. ClientId: {request.ClientNetworkId}, PlayerId: {ShortenPlayerId(credential.PlayerId)}, Reconnect: {!string.IsNullOrEmpty(credential.Ticket)}",
                    this);
            }
        }

        /// <summary>새 PlayerObject에 동일 PlayerId의 캐시가 있으면 복원합니다.</summary>
        private void HandleClientConnected(ulong clientId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.IsServer ||
                !playerIdsByClientId.TryGetValue(clientId, out string playerId) ||
                !networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return;
            }

            PlayerSessionCacheReporter reporter =
                client.PlayerObject.GetComponent<PlayerSessionCacheReporter>();
            PlayerReconnectTicketBridge ticketBridge =
                client.PlayerObject.GetComponent<PlayerReconnectTicketBridge>();
            if (reporter == null || ticketBridge == null)
            {
                return;
            }

            PlayerSessionSnapshot snapshot = null;
            bool restoreAuthorized = restoreAuthorizedClientIds.Remove(clientId);
            if (restoreAuthorized &&
                cachedStates.TryGetValue(playerId, out CacheEntry entry) &&
                entry.ExpiresAt > Time.unscaledTime)
            {
                snapshot = entry.Snapshot;
            }

            string nextTicket = GenerateTicket();
            cachedStates[playerId] = new CacheEntry
            {
                Snapshot = snapshot,
                ExpiresAt = float.PositiveInfinity,
                Ticket = nextTicket,
                IsConnected = true
            };

            reporter.Initialize(playerId, snapshot, itemCatalog);
            ticketBridge.AssignTicket(nextTicket);

            EchoZone.Online.OnlineDebugLog.Info(
                snapshot != null
                    ? $"Session cache restored. PlayerId: {ShortenPlayerId(playerId)}, Health: {snapshot.Health}, Slots: {snapshot.InventorySlots.Count}"
                    : $"Session cache not found. New state used. PlayerId: {ShortenPlayerId(playerId)}",
                this);
        }

        /// <summary>끊긴 PlayerId의 최신 복사본에 설정된 유예 만료 시간을 적용합니다.</summary>
        private void HandleClientDisconnected(ulong clientId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.IsServer ||
                !playerIdsByClientId.TryGetValue(clientId, out string playerId))
            {
                return;
            }

            playerIdsByClientId.Remove(clientId);
            restoreAuthorizedClientIds.Remove(clientId);

            if (cachedStates.TryGetValue(playerId, out CacheEntry entry))
            {
                float cacheSeconds = config != null
                    ? config.ReconnectStateCacheSeconds
                    : 0f;
                entry.ExpiresAt = Time.unscaledTime + cacheSeconds;
                entry.IsConnected = false;
                EchoZone.Online.OnlineDebugLog.Info(
                    $"Session cache held for {cacheSeconds} seconds. PlayerId: {ShortenPlayerId(playerId)}, Health: {entry.Snapshot.Health}",
                    this);
            }
            else
            {
                Debug.LogWarning(
                    $"Session cache snapshot was missing on disconnect. PlayerId: {ShortenPlayerId(playerId)}",
                    this);
            }
        }

        /// <summary>서버 PlayerObject가 보고한 최신 상태를 해당 PlayerId의 메모리에 기록합니다.</summary>
        /// <param name="playerId">Unity Authentication PlayerId입니다.</param>
        /// <param name="snapshot">현재 인벤토리와 스탯 복사본입니다.</param>
        public void SaveLiveSnapshot(string playerId, PlayerSessionSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(playerId) || snapshot == null)
            {
                return;
            }

            if (cachedStates.TryGetValue(playerId, out CacheEntry entry))
            {
                entry.Snapshot = snapshot;
                return;
            }

            cachedStates[playerId] = new CacheEntry
            {
                Snapshot = snapshot,
                ExpiresAt = float.PositiveInfinity,
                IsConnected = true
            };
        }

        /// <summary>
        /// 새 Host가 시작되기 전에 이전 플레이어 상태를 캐시에 심고 최초 마이그레이션 연결을 승인합니다.
        /// 이후에는 새 Host가 발급한 일회성 티켓 검증으로 돌아갑니다.
        /// </summary>
        public void PrepareHostMigration(
            IReadOnlyList<HostMigrationPlayerSnapshot> players)
        {
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                HostMigrationPlayerSnapshot player = players[i];
                if (player == null ||
                    string.IsNullOrWhiteSpace(player.PlayerId) ||
                    player.State == null)
                {
                    continue;
                }

                cachedStates[player.PlayerId] = new CacheEntry
                {
                    Snapshot = player.State,
                    ExpiresAt = float.PositiveInfinity,
                    Ticket = string.Empty,
                    IsConnected = false
                };
                migrationAuthorizedPlayerIds.Add(player.PlayerId);
            }
        }

        /// <summary>최초 접속 또는 미만료 재접속 티켓인지 서버 메모리에서 검사합니다.</summary>
        private bool ValidateCredential(
            ulong clientId,
            ReconnectCredential credential)
        {
            if (migrationAuthorizedPlayerIds.Remove(credential.PlayerId) &&
                cachedStates.ContainsKey(credential.PlayerId))
            {
                restoreAuthorizedClientIds.Add(clientId);
                return true;
            }

            if (!cachedStates.TryGetValue(credential.PlayerId, out CacheEntry entry))
            {
                return string.IsNullOrEmpty(credential.Ticket);
            }

            if (entry.IsConnected ||
                entry.ExpiresAt <= Time.unscaledTime ||
                string.IsNullOrEmpty(credential.Ticket) ||
                !SecureTicketEquals(entry.Ticket, credential.Ticket))
            {
                return false;
            }

            restoreAuthorizedClientIds.Add(clientId);
            return true;
        }

        /// <summary>설정된 난수 바이트 길이로 다음 재접속에 사용할 티켓을 생성합니다.</summary>
        private string GenerateTicket()
        {
            int byteLength = config != null
                ? config.ReconnectTicketByteLength
                : 32;
            byte[] randomBytes = new byte[byteLength];

            using RandomNumberGenerator generator = RandomNumberGenerator.Create();
            generator.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        /// <summary>문자별 비교 시간을 일정하게 유지하며 서버 티켓과 요청 티켓을 비교합니다.</summary>
        private static bool SecureTicketEquals(string expected, string actual)
        {
            if (expected == null || actual == null || expected.Length != actual.Length)
            {
                return false;
            }

            int difference = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                difference |= expected[i] ^ actual[i];
            }

            return difference == 0;
        }

        /// <summary>진단 로그에서 전체 인증 식별자를 노출하지 않도록 앞부분만 반환합니다.</summary>
        private static string ShortenPlayerId(string playerId)
        {
            const int visibleCharacterCount = 8;
            if (string.IsNullOrEmpty(playerId) ||
                playerId.Length <= visibleCharacterCount)
            {
                return playerId;
            }

            return playerId.Substring(0, visibleCharacterCount);
        }
    }
}
