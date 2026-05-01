using System.Collections;
using Photon.Pun;
using UnityEngine;

namespace SwDreams.Features.Voice.Adapter
{
    /// <summary>
    /// PlayerStub 의 Speaker AudioSource 에 PerUserVoiceSettings 의 볼륨을 적용 (R14).
    /// Speaker / AudioSource 와 같은 GameObject 또는 자식에 부착.
    ///
    /// 적용 공식: gainBoost.gain = clamp(perUser, 0, 2)  ← AudioSource.volume cap(0~1) 우회
    ///   - master voice gain 은 AudioMixer 의 "VoiceGain" Exposed Param 이 처리 (R12 / AudioManager)
    ///   - perUser 0~2 — 1 초과는 OnAudioFilterRead 후처리로 진짜 boost (마이크 작은 유저 보정)
    ///   - gain 너무 높이면 clipping 위험 — spec 권장 max 2
    ///
    /// IsMine 가드: 자기 PlayerStub 면 비활성 — Photon Voice self-mute 표준 (자기 목소리 재생 안 함).
    /// </summary>
    [DisallowMultipleComponent]
    public class PerUserVoiceApplier : MonoBehaviour
    {
        [SerializeField] private AudioGainBoost gainBoost;
        [SerializeField, Tooltip("gainBoost 미연결 시 fallback 으로 audioSrc.volume cap 적용 (0~1).")]
        private AudioSource audioSrc;

        private PhotonView pv;
        private int cachedActorNumber = -1;
        private bool subscribed;
        private Coroutine ownerWaitCoroutine;

        private void Awake()
        {
            pv = GetComponentInParent<PhotonView>();
            if (audioSrc == null)
            {
                audioSrc = GetComponent<AudioSource>();
                if (audioSrc == null) audioSrc = GetComponentInChildren<AudioSource>();
            }
            if (gainBoost == null)
            {
                gainBoost = GetComponent<AudioGainBoost>();
                if (gainBoost == null) gainBoost = GetComponentInChildren<AudioGainBoost>();
            }
        }

        private void OnEnable()
        {
            // 자기 자신 — 자기 목소리는 재생 안 함. 컴포넌트 비활성으로 충분.
            if (pv == null || pv.IsMine)
            {
                enabled = false;
                return;
            }

            // PhotonView.Owner 는 spawn 직후 한 프레임 늦게 valid 일 수 있음.
            if (pv.Owner == null)
            {
                ownerWaitCoroutine = StartCoroutine(WaitForOwnerThenBind());
                return;
            }

            Bind();
        }

        private void OnDisable()
        {
            // stale 구독/coroutine 차단 — 풀 반환/씬 전환 안전.
            if (ownerWaitCoroutine != null)
            {
                StopCoroutine(ownerWaitCoroutine);
                ownerWaitCoroutine = null;
            }
            Unbind();
        }

        private IEnumerator WaitForOwnerThenBind()
        {
            // 최대 300 프레임 (60fps 기준 5초) 대기 — Photon RoomEnter race 보호.
            for (int i = 0; i < 300; i++)
            {
                if (pv == null) yield break;
                if (pv.Owner != null) break;
                yield return null;
            }
            ownerWaitCoroutine = null;
            if (pv != null && pv.Owner != null) Bind();
        }

        private void Bind()
        {
            cachedActorNumber = pv.Owner.ActorNumber;
            ApplyVolume();

            var settings = PerUserVoiceSettings.Instance;
            if (settings != null && !subscribed)
            {
                settings.OnVolumeChanged += OnVolumeChanged;
                subscribed = true;
            }
        }

        private void Unbind()
        {
            var settings = PerUserVoiceSettings.Instance;
            if (settings != null && subscribed)
            {
                settings.OnVolumeChanged -= OnVolumeChanged;
            }
            subscribed = false;
            cachedActorNumber = -1;
        }

        private void OnVolumeChanged(int actorNumber)
        {
            if (actorNumber != cachedActorNumber) return;
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (cachedActorNumber < 0) return;

            var settings = PerUserVoiceSettings.Instance;
            float perUser = settings != null ? settings.GetVolumeFor(cachedActorNumber) : 1f;

            // 우선순위: AudioGainBoost (0~2 boost 가능) → 없으면 audioSrc.volume fallback (0~1 cap).
            if (gainBoost != null)
            {
                gainBoost.gain = Mathf.Clamp(perUser, 0f, 2f);
                // gainBoost 가 후처리하므로 audioSrc.volume 는 1.0 으로 고정 (cap 충돌 방지).
                if (audioSrc != null) audioSrc.volume = 1f;
            }
            else if (audioSrc != null)
            {
                audioSrc.volume = Mathf.Clamp01(perUser);
            }
        }
    }
}
