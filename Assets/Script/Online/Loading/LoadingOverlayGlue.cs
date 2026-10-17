using System;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace EchoZone.Online.Loading
{
    /// <summary>기존 온라인 작업의 실제 결과를 표시하고 안전한 소유자 재시도·정리 콜백만 실행합니다.</summary>
    public sealed class LoadingOverlayGlue : MonoBehaviour
    {
        /// <summary>표시 지연·단계 제한·성공 보간을 조절하는 설정입니다.</summary>
        [SerializeField] private OnlineLoadingConfig config;
        /// <summary>입력 차단을 포함한 로딩 패널입니다.</summary>
        [SerializeField] private GameObject panel;
        /// <summary>완료 단계와 현재 단계 및 오류를 보여줍니다.</summary>
        [SerializeField] private TMP_Text status;
        /// <summary>0~100% 표시 게이지입니다.</summary>
        [SerializeField] private UnityEngine.UI.Slider progress;
        /// <summary>응답 대기 중임을 회전으로 표시하는 장식용 아이콘입니다.</summary>
        [SerializeField] private RectTransform spinner;
        private readonly LoadingPresentationBrick presentation = new();
        /// <summary>기존 작업 소유자의 안전한 진입점으로 연결합니다.</summary>
        [SerializeField] private UnityEngine.UI.Button retryButton;
        /// <summary>계정을 유지한 채 로컬 세션을 정리합니다.</summary>
        [SerializeField] private UnityEngine.UI.Button titleButton;
        private readonly LoadingProgressBrick state = new();
        private string[] stages = Array.Empty<string>();
        private Func<Task> retry;
        private Func<Task> leave;
        private Action abandon;
        private Task pending;
        private bool active;
        private bool returned;
        private bool acting;
        private double deadline;
        /// <summary>작업 소유자가 완료 지점을 보고할 순수 상태입니다.</summary>
        public LoadingProgressBrick State => state;
        /// <summary>정리되지 않은 요청은 새 온라인 요청을 차단합니다.</summary>
        public bool IsBlocking => active || acting;
        /// <summary>타이틀은 표시하되 취소 불가능한 이전 요청이 끝날 때까지 입장을 막습니다.</summary>
        public bool CanStartOperation => !IsBlocking && IsDrained;
        /// <summary>이전 SDK 요청이 끝나 새 시도가 가능한지 나타냅니다.</summary>
        public bool IsDrained => pending == null || pending.IsCompleted;
        /// <summary>현재 작업 설정을 제공합니다.</summary>
        public OnlineLoadingConfig Config => config;

        private void Awake()
        {
            retryButton?.onClick.AddListener(Retry);
            titleButton?.onClick.AddListener(Return);
            panel?.SetActive(false);
        }
        private void OnDestroy()
        {
            state.Invalidate(); abandon?.Invoke();
            retryButton?.onClick.RemoveListener(Retry); titleButton?.onClick.RemoveListener(Return);
        }
        /// <summary>SDK 복합 요청을 가짜 하위 완료로 나누지 않고 실제 확인 가능한 단계만 만듭니다.</summary>
        public long Begin(LoadingOperationKind kind, Action invalidate, Func<Task> returnToTitle, Func<Task> retryAction, bool recoveryTransition = false)
        {
            if ((!IsDrained && !recoveryTransition) || acting) return -1;
            stages = kind <= LoadingOperationKind.SignUp
                ? new[] { kind == LoadingOperationKind.Initialization ? "계정 서비스를 준비하는 중" : "로그인 정보를 확인하는 중" }
                : kind >= LoadingOperationKind.Reconnect
                ? new[] { "기존 세션·호스트 선출 확인", "Relay·네트워크 연결 복구", "Snapshot·플레이어·월드 복원" }
                : new[] { "개인 지갑 확인", "세션·Relay·NGO 연결", "플레이어·월드 데이터 준비" };
            var weights = new double[stages.Length];
            for (int i = 0; i < weights.Length; i++) weights[i] = config != null && config.StageWeights != null && i < config.StageWeights.Length ? config.StageWeights[i] : 1;
            long id = state.Begin(kind, weights);
            presentation.Reset();
            if (progress != null) progress.value = 0;
            if (spinner != null) spinner.localRotation = Quaternion.identity;
            abandon = invalidate; leave = returnToTitle; retry = retryAction;
            returned = false; active = true; SetDeadline(); return id;
        }
        /// <summary>취소 불가능한 SDK 작업이 끝나기 전 새 요청을 허용하지 않습니다.</summary>
        public void Track(Task task) => pending = pending != null && !pending.IsCompleted ? Task.WhenAll(pending, task) : task;
        /// <summary>소유자가 실제 완료한 단계만 반영하고 다음 단계 제한 시간을 시작합니다.</summary>
        public void Complete(long id, int stage)
        {
            int old = state.Stage;
            if (state.Complete(id, stage) && state.Stage != old) SetDeadline();
        }
        /// <summary>소유자의 최종 검증 성공에만 100% 표시를 허용합니다.</summary>
        public void Succeed(long id)
        {
            bool shown = state.ShouldShow(config != null ? config.ShowDelaySeconds : .2);
            if (state.Succeed(id) && !shown) { active = false; panel?.SetActive(false); }
        }
        /// <summary>실패 세대를 즉시 무효화하고 소유자의 늦은 응답 적용도 중단합니다.</summary>
        public void Fail(long id, string reason)
        {
            if (state.Fail(id, reason)) abandon?.Invoke();
        }
        /// <summary>응답이 끝난 계정 인증 실패는 로딩을 닫아 타이틀에서 입력을 수정하게 합니다. 미완료 요청과 다른 온라인 작업은 닫지 않습니다.</summary>
        public void FinishAuthenticationFailure(long id, string reason)
        {
            if (!state.Accepts(id) || !IsDrained || acting ||
                (state.Kind != LoadingOperationKind.SignIn && state.Kind != LoadingOperationKind.SignUp)) return;
            Fail(id, reason);
            active = false;
            returned = true;
            panel?.SetActive(false);
            if (spinner != null) spinner.gameObject.SetActive(false);
        }
        /// <summary>기존 타이틀 Update가 호출하며 자체 Update를 추가하지 않습니다.</summary>
        public void ManualUpdate(float delta)
        {
            if (!active) { panel?.SetActive(false); return; }
            state.Tick(delta, config != null ? config.FinishSeconds : .3);
            if (!state.Succeeded && !state.Failed && Time.realtimeSinceStartupAsDouble >= deadline)
                Fail(state.Generation, "제한 시간을 초과했습니다. 이전 요청 종료 전에는 재시도할 수 없습니다.");
            state.SetActions(IsDrained && !acting && retry != null, !acting && leave != null);
            presentation.Tick(delta, state.Progress, state.Succeeded, state.Failed,
                config != null ? config.StepBlendSeconds : .4, config != null ? config.FinishSeconds : .3);
            if (spinner != null)
            {
                bool spinning = !state.Succeeded && !state.Failed;
                spinner.gameObject.SetActive(spinning);
                if (spinning) spinner.Rotate(0, 0, -(config != null ? config.SpinnerDegreesPerSecond : 240) * Mathf.Max(0, delta));
            }
            if (state.Succeeded && presentation.Progress >= 1) { active = false; panel?.SetActive(false); return; }
            bool visible = !returned && state.ShouldShow(config != null ? config.ShowDelaySeconds : .2);
            panel?.SetActive(visible);
            if (retryButton != null) retryButton.interactable = state.CanRetry;
            if (titleButton != null) titleButton.interactable = state.CanReturn;
            if (progress != null) progress.value = (float)presentation.Progress;
            if (status == null) return;
            var text = new StringBuilder(); text.AppendLine($"{OperationTitle(state.Kind)}  {presentation.Progress:P0}");
            for (int i = 0; i < stages.Length; i++) text.AppendLine($"{(state.Completed[i] ? "[완료]" : i == state.Stage ? "[진행]" : "[대기]")} {stages[i]}");
            if (state.Failed) text.AppendLine(state.Error);
            if (state.Failed && !IsDrained) text.AppendLine("이전 SDK 요청 종료 대기 중 · 타이틀 복귀는 가능");
            status.text = text.ToString();
        }
        private static string OperationTitle(LoadingOperationKind kind) => kind switch
        {
            LoadingOperationKind.Initialization => "계정 서비스 준비",
            LoadingOperationKind.SignIn => "로그인",
            LoadingOperationKind.SignUp => "회원가입",
            LoadingOperationKind.Create => "방 생성",
            LoadingOperationKind.Join => "방 참가",
            LoadingOperationKind.Reconnect => "재접속",
            _ => "호스트 마이그레이션"
        };
        private void SetDeadline() => deadline = Time.realtimeSinceStartupAsDouble + (config != null ? config.Timeout(state.Kind, state.Stage) : 60);
        private async void Retry()
        {
            if (!state.CanRetry || acting) return;
            var action = retry; state.Invalidate(); active = false; acting = true;
            try { if (leave != null && state.Kind <= LoadingOperationKind.SignUp) await leave(); }
            finally { acting = false; }
            if (this != null && action != null) await action();
        }
        private async void Return()
        {
            if (!state.CanReturn || acting) return;
            state.Invalidate(); abandon?.Invoke(); acting = true;
            try { await leave(); }
            finally { if (this != null) { acting = false; returned = true; active = false; panel?.SetActive(false); } }
        }
    }
}
