using System;
using System.Threading.Tasks;
using EchoZone.Online.Authentication;
using TMPro;
using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>타이틀의 기존 갱신에서 전적 조회와 uGUI 표시만 연결합니다.</summary>
    public sealed class PlayHistoryGlue : MonoBehaviour
    {
        /// <summary>조회 엔드포인트와 페이지 표시 규칙입니다.</summary>
        [SerializeField] private PlayHistoryConfig config;
        /// <summary>실행 중 공유 인증을 참조하며 새 인증 Service를 만들지 않습니다.</summary>
        [SerializeField] private UnityAuthenticationGlue authentication;
        /// <summary>로그인한 타이틀에서만 활성화할 조회 버튼입니다.</summary>
        [SerializeField] private UnityEngine.UI.Button openButton;
        /// <summary>기존 메뉴 위에서 입력을 차단하는 전적 패널입니다.</summary>
        [SerializeField] private GameObject panel;
        /// <summary>목록을 다시 요청하는 버튼입니다.</summary>
        [SerializeField] private UnityEngine.UI.Button retryButton;
        /// <summary>조회 응답을 무효화하고 기존 타이틀로 돌아가는 버튼입니다.</summary>
        [SerializeField] private UnityEngine.UI.Button closeButton;
        /// <summary>이전 목록 페이지를 표시합니다.</summary>
        [SerializeField] private UnityEngine.UI.Button previousButton;
        /// <summary>다음 목록 페이지를 표시합니다.</summary>
        [SerializeField] private UnityEngine.UI.Button nextButton;
        /// <summary>현재 페이지에서 회차를 선택하는 재사용 행입니다.</summary>
        [SerializeField] private UnityEngine.UI.Button[] rows;
        /// <summary>조회·빈 목록·오류·페이지 안내를 표시합니다.</summary>
        [SerializeField] private TMP_Text status;
        /// <summary>선택한 회차의 모든 요약 정보를 표시합니다.</summary>
        [SerializeField] private TMP_Text detail;
        /// <summary>조회 중임을 표시하며 입력을 가로채지 않는 회전 아이콘입니다.</summary>
        [SerializeField] private RectTransform spinner;
        private readonly PlayHistoryQueryBrick query = new();
        private readonly PlayHistoryService service = new();
        private CompletedPlayRecord[] records = Array.Empty<CompletedPlayRecord>();
        private WealthRankingResult ranking = new();
        private bool IsRanking => config != null && config.Kind == TitleRecordKind.WealthRanking;
        private int Count => IsRanking ? ranking.Entries.Length : records.Length;
        private UnityEngine.Events.UnityAction[] rowActions;
        private int page;
        private bool allowed;
        private string Account => authentication != null && authentication.Service.IsSignedIn ? authentication.Service.PlayerId : "";
        private int PageSize => Math.Max(1, Math.Min(rows?.Length ?? 1, config != null ? config.RowsPerPage : 5));

        private void Awake()
        {
            openButton?.onClick.AddListener(Open);
            closeButton?.onClick.AddListener(Close);
            retryButton?.onClick.AddListener(Retry);
            previousButton?.onClick.AddListener(Previous);
            nextButton?.onClick.AddListener(Next);
            rowActions = new UnityEngine.Events.UnityAction[rows?.Length ?? 0];
            for (int i = 0; i < rowActions.Length; i++)
            { int index = i; rowActions[i] = () => Select(page * PageSize + index); rows[i]?.onClick.AddListener(rowActions[i]); }
            Close();
        }

        /// <summary>기존 타이틀 갱신에서 계정·온라인 작업 상태만 검사합니다. Cloud 폴링은 하지 않습니다.</summary>
        public void ManualUpdate(bool canOpen)
        {
            allowed = canOpen && !string.IsNullOrEmpty(Account);
            if (openButton != null) { openButton.interactable = allowed; openButton.gameObject.SetActive(allowed); }
            if (query.Opened && (!allowed || query.PlayerId != Account || panel == null || !panel.activeInHierarchy)) Close();
            RefreshSpinner(Time.unscaledDeltaTime);
        }

        private void RefreshSpinner(float delta)
        {
            if (spinner == null) return;
            bool waiting = query.Opened && query.Busy;
            spinner.gameObject.SetActive(waiting);
            if (waiting) spinner.Rotate(0, 0, -(config != null ? config.SpinnerDegreesPerSecond : 240) * Mathf.Max(0, delta));
        }

        private void Open()
        {
            if (!allowed || query.Opened || panel == null) return;
            query.Open(Account); records = Array.Empty<CompletedPlayRecord>(); page = 0;
            panel.SetActive(true); panel.transform.SetAsLastSibling();
            if (detail != null) detail.text = "회차를 선택하면 상세 전적이 표시됩니다.";
            _ = Load();
        }
        private void Close()
        {
            query.Close(); records = Array.Empty<CompletedPlayRecord>(); ranking = new(); page = 0;
            RefreshSpinner(0);
            if (panel != null) panel.SetActive(false);
            if (detail != null) detail.text = "";
            if (status != null) status.text = "";
        }
        private void Retry() { if (query.Opened) _ = Load(); }
        private async Task Load()
        {
            if (!query.TryBegin(out int token)) return;
            if (spinner != null) spinner.localRotation = Quaternion.identity;
            RefreshSpinner(0);
            string player = query.PlayerId;
            records = Array.Empty<CompletedPlayRecord>(); ranking = new(); page = 0;
            if (detail != null) detail.text = "";
            RefreshRows(); SetStatus(IsRanking ? "보유금 랭킹을 불러오는 중..." : "전적을 불러오는 중...");
            try
            {
                if (config == null) throw new InvalidOperationException("전적 Config가 연결되지 않았습니다.");
                if (IsRanking)
                {
                    var loaded = await service.LoadRanking(config);
                    if (this == null || !query.Accepts(token, Account)) return;
                    ranking = loaded ?? new WealthRankingResult();
                    ranking.Entries ??= Array.Empty<WealthRankingEntry>();
                }
                else
                {
                    var loaded = await service.Load(config);
                    if (this == null || !query.Accepts(token, Account)) return;
                    records = loaded ?? Array.Empty<CompletedPlayRecord>();
                }
                query.Complete(token, player); RefreshRows();
                if (Count > 0) Select(0);
            }
            catch (Exception)
            {
                if (this == null || !query.Accepts(token, Account)) return;
                query.Complete(token, player); RefreshRows();
                SetStatus(IsRanking ? "랭킹 조회 실패 · 연결/Cloud 함수/검색 인덱스를 확인하고 재시도해 주세요." : "전적 조회에 실패했습니다. 연결·Cloud 배포 상태를 확인하고 재시도해 주세요.");
            }
        }
        private void Previous() { if (page > 0) { page--; RefreshRows(); } }
        private void Next() { if ((page + 1) * PageSize < Count) { page++; RefreshRows(); } }
        private void SetStatus(string value) { if (status != null) status.text = value; }
        private void RefreshRows()
        {
            RefreshSpinner(0);
            for (int i = 0; i < (rows?.Length ?? 0); i++)
            {
                if (rows[i] == null) continue;
                int index = page * PageSize + i;
                bool visible = i < PageSize && index < Count;
                rows[i].gameObject.SetActive(visible);
                rows[i].interactable = visible && !query.Busy;
                if (!visible) continue;
                var text = rows[i].GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                {
                    if (IsRanking) { var r = ranking.Entries[index]; text.text = $"{r.Rank}위  {r.DisplayName}{(r.IsSelf ? " (나)" : "")}\n보유금 {r.Balance:N0}"; }
                    else { var entry = records[index]; text.text = $"{LocalTime(entry.CompletedAtUtc)}\n확보 {entry.Summary.ExtractedCargo:N0} · 사망 {entry.Summary.Deaths}"; }
                }
            }
            if (retryButton != null) retryButton.interactable = !query.Busy;
            if (previousButton != null) previousButton.interactable = !query.Busy && page > 0;
            if (nextButton != null) nextButton.interactable = !query.Busy && (page + 1) * PageSize < Count;
            if (IsRanking)
            {
                SetStatus($"확정 보유금 상위 {Count}명 · 내 보유금 {(ranking.OwnBalance.HasValue ? ranking.OwnBalance.Value.ToString("N0") : "지갑 준비 전")}");
                if (detail != null && Count == 0) detail.text = "등록된 랭킹이 없습니다.\n기존 계정은 다음 입장 또는 랭킹 조회 때 등록됩니다.";
                return;
            }
            SetStatus(records.Length == 0 ? "완료한 전적이 없습니다. 탈출 정산 후 기록됩니다." :
                $"최근 {records.Length}회 · {page + 1}/{(records.Length + PageSize - 1) / PageSize} 페이지 · 계정별 최근 100회 보관");
        }
        private static string LocalTime(string utc) => DateTimeOffset.TryParse(utc, out var time) ? time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : utc;
        private void Select(int index)
        {
            if (detail == null || index < 0 || index >= Count) return;
            if (IsRanking)
            {
                var row = ranking.Entries[index];
                detail.text = $"<b>{row.Rank}위 · {row.DisplayName}</b>\n\n확정 보유금 {row.Balance:N0}\n\n" +
                    "탈출 정산으로 Cloud에 확정된 보유금 기준입니다.\n운반 중인 장물은 포함하지 않습니다.\n동일 금액은 같은 순위입니다.\n\n기존 계정은 입장 또는 랭킹 조회 후 등록됩니다.\n로그인 아이디 대신 임시 공개 별칭을 표시합니다.";
                return;
            }
            var entry = records[index]; var r = entry.Summary;
            detail.text = $"<b>탈출 완료</b>  {LocalTime(entry.CompletedAtUtc)}\n" +
                $"시작 {LocalTime(r.StartedAtUtc)}\n종료 승인 {LocalTime(r.EndedAtUtc)}\n실제 플레이 {TimeSpan.FromSeconds(r.PlayedSeconds):hh\\:mm\\:ss}\n\n" +
                $"총 절도액  {r.StolenMoney:N0}\n턴 건물  {r.RobbedBuildings}\n경찰 처치  {r.PoliceKills}    플레이어 처치  {r.PlayerKills}\n사망  {r.Deaths}\n" +
                $"받은 현상금  {r.BountyMoney:N0}\n유효 신고  {r.Reports}\n받은 신고 포상금  {r.ReportRewardMoney:N0}\n탈출 확보 장물  {r.ExtractedCargo:N0}\n\n" +
                $"<size=65%>회차 {r.RoundId}\n계정 {r.PlayerId}\n월드 {r.WorldId}\nRun {r.RunId}\n세션 {r.SessionId}</size>";
        }
        private void OnDestroy()
        {
            query.Close(); openButton?.onClick.RemoveListener(Open); closeButton?.onClick.RemoveListener(Close);
            retryButton?.onClick.RemoveListener(Retry); previousButton?.onClick.RemoveListener(Previous); nextButton?.onClick.RemoveListener(Next);
            for (int i = 0; i < (rowActions?.Length ?? 0); i++) rows[i]?.onClick.RemoveListener(rowActions[i]);
        }
    }
}
