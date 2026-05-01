using System.Collections;
using UnityEngine;
using Photon.Pun;
using SwDreams.Features.Voice.Adapter.Data;
using SwDreams.Features.Voice.Domain;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Voice.Adapter
{
    /// <summary>
    /// PlayerStub 프리팹에 부착 (Recorder/Speaker/PhotonVoiceView 와 같은 GameObject).
    /// R3 마이크 필터 드랍 아이템이 호스트 권위로 결정되면, DropSpawner.RPC_ApplyMicFilter 가
    /// 모든 클라에서 본 컴포넌트의 ApplyFilter 를 호출 → AudioFilter 컴포넌트 / AudioSource.pitch 동적 조작.
    ///
    /// 카오스 디자인:
    ///   - 본인 화면에는 알림 표시 0 (Photon Voice 가 자기 음성을 자기에게 안 들려주는 함정 활용)
    ///   - 다른 사람들이 "어 너 마이크 왜 그래?" 반응에서 자기가 깨닫는 게 본질적 재미
    ///
    /// 겹침 처리:
    ///   - 새 ApplyFilter 도착 시 기존 코루틴/필터 즉시 정리 + 새로 적용 (시간 자동 연장 효과)
    ///
    /// 만료:
    ///   - 클라 자체 코루틴. 호스트 권위 RPC 의 시작 시점만 동기화되면 충분 (100ms 차이 무관).
    ///   - 호스트 마이그레이션 시에도 각 클라가 자체 만료라 영향 0.
    /// </summary>
    public class MicFilterController : MonoBehaviour
    {
        private AudioSource audioSrc;

        // 활성 필터 컴포넌트 — Clear 시 Destroy 대상.
        private AudioLowPassFilter lowPass;
        private AudioDistortionFilter distortion;
        private AudioEchoFilter echo;

        // pitch 변경 전 원본값. Clear 시 복원.
        private const float DefaultPitch = 1f;

        private Coroutine expireRoutine;

        private void Awake()
        {
            // 즉시 시도하지만, Photon Voice Speaker 가 Awake 에서 자동 생성하는 케이스 +
            // 자식 GO 에 두는 케이스 대응을 위해 Apply 시점에 한 번 더 탐색 (EnsureAudioSrc).
            EnsureAudioSrc();
        }

        /// <summary>
        /// Speaker.Awake 가 본 Awake 보다 늦게 호출되거나, 자식 GO 에 AudioSource 를 두는 경우 대비 lazy lookup.
        /// AudioFilter 는 AudioSource 와 같은 GO 에 부착돼야 효과 발휘 → audioSrc.gameObject 를 호스트로 사용.
        /// </summary>
        private void EnsureAudioSrc()
        {
            if (audioSrc != null) return;
            audioSrc = GetComponent<AudioSource>();
            if (audioSrc == null)
                audioSrc = GetComponentInChildren<AudioSource>(true);
        }

        private void OnDisable()
        {
            // 풀/씬 전환 등으로 비활성 시 안전 정리.
            ClearFilterImmediate();
        }

        /// <summary>
        /// DropSpawner.RPC_ApplyMicFilter 가 호출. filterIdx = MicFilterDatabase.All 인덱스.
        /// </summary>
        public void ApplyFilter(int filterIdx, float duration)
        {
            var db = GameManager.Instance?.MicFilterDB;
            if (db == null)
            {
                Debug.LogWarning("[MicFilter] MicFilterDB 미할당 — GameManager Inspector 확인.");
                return;
            }
            var data = db.GetByIndex(filterIdx);
            if (data == null)
            {
                Debug.LogWarning($"[MicFilter] filterIdx={filterIdx} 매칭 실패 — Database 인덱스 범위/순서 확인.");
                return;
            }
            EnsureAudioSrc();
            if (audioSrc == null)
            {
                Debug.LogWarning($"[MicFilter] AudioSource 미발견 — '{name}' 또는 자식 어디에도 없음. " +
                                 "PlayerStub 의 Speaker (Photon Voice) 가 AudioSource 를 만들지 못했거나 다른 GO 에 둠. " +
                                 "Speaker 컴포넌트 부착 위치 확인.");
                return;
            }

            // 겹침 처리(C): 기존 필터/타이머 즉시 정리 + 새로 적용.
            ClearFilterImmediate();
            ApplyFilterInternal(data);

            float effectiveDuration = duration > 0f ? duration : data.durationSeconds;
            expireRoutine = StartCoroutine(ExpireAfter(effectiveDuration));
        }

        private void ApplyFilterInternal(MicFilterData data)
        {
            // AudioFilter 는 AudioSource 와 같은 GO 에 부착돼야 신호 chain 에 들어감.
            // audioSrc.gameObject 가 본 컴포넌트 GO 와 다를 수도 있어 (자식 등) 명시적으로 호스트 GO 사용.
            var host = audioSrc.gameObject;

            switch (data.type)
            {
                case MicFilterType.LowPass:
                    lowPass = host.AddComponent<AudioLowPassFilter>();
                    if (lowPass != null) lowPass.cutoffFrequency = data.cutoffFrequency;
                    break;

                case MicFilterType.Distortion:
                    distortion = host.AddComponent<AudioDistortionFilter>();
                    if (distortion != null) distortion.distortionLevel = data.distortionLevel;
                    break;

                case MicFilterType.Echo:
                    echo = host.AddComponent<AudioEchoFilter>();
                    if (echo != null)
                    {
                        echo.delay = data.echoDelayMs;
                        echo.decayRatio = data.echoDecay;
                        echo.dryMix = data.echoDryMix;
                        echo.wetMix = data.echoWetMix;
                    }
                    break;

                case MicFilterType.PitchHelium:
                case MicFilterType.PitchDemon:
                    audioSrc.pitch = data.pitchValue;
                    break;
            }
        }

        private IEnumerator ExpireAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            ClearFilterImmediate();
            expireRoutine = null;
        }

#if UNITY_EDITOR
        // ===== 에디터 디버그 — Play 중 인스펙터 우클릭으로 즉시 적용 =====
        // 0.5% 드랍 확률 + 랜덤 5종이라 자연 검증이 비현실적이라 추가. 검증 완료 후 제거 가능.

        [ContextMenu("[Test] Apply LowPass")] private void TestLowPass() => TestApply(Domain.MicFilterType.LowPass);
        [ContextMenu("[Test] Apply Distortion")] private void TestDistortion() => TestApply(Domain.MicFilterType.Distortion);
        [ContextMenu("[Test] Apply Echo")] private void TestEcho() => TestApply(Domain.MicFilterType.Echo);
        [ContextMenu("[Test] Apply PitchHelium")] private void TestHelium() => TestApply(Domain.MicFilterType.PitchHelium);
        [ContextMenu("[Test] Apply PitchDemon")] private void TestDemon() => TestApply(Domain.MicFilterType.PitchDemon);
        [ContextMenu("[Test] Clear")] private void TestClear() => ClearFilterImmediate();

        private void TestApply(Domain.MicFilterType type)
        {
            var db = GameManager.Instance?.MicFilterDB;
            if (db == null) { Debug.LogWarning("[MicFilter Test] MicFilterDB 미할당."); return; }
            int idx = db.GetIndexOfType(type);
            var data = db.GetByType(type);
            if (idx < 0 || data == null) { Debug.LogWarning($"[MicFilter Test] {type} SO 미등록."); return; }
            ApplyFilter(idx, data.durationSeconds);
        }
#endif

        /// <summary>모든 필터 컴포넌트 제거 + pitch 원복. 코루틴은 호출자가 정리.</summary>
        private void ClearFilterImmediate()
        {
            if (expireRoutine != null)
            {
                StopCoroutine(expireRoutine);
                expireRoutine = null;
            }

            if (lowPass != null) { Destroy(lowPass); lowPass = null; }
            if (distortion != null) { Destroy(distortion); distortion = null; }
            if (echo != null) { Destroy(echo); echo = null; }

            if (audioSrc != null && !Mathf.Approximately(audioSrc.pitch, DefaultPitch))
                audioSrc.pitch = DefaultPitch;
        }
    }
}
