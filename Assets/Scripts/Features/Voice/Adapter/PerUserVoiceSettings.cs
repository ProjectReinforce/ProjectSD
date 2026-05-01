using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace SwDreams.Features.Voice.Adapter
{
    /// <summary>
    /// 룸 내 다른 유저별 보이스 볼륨 설정 (R14).
    /// 키 = ActorNumber (룸 내 유일), 값 = 0~2 곱연산 인자. 기본 1.0.
    ///
    /// 클라이언트 로컬, 동기화 X — 네가 다른 유저 볼륨 어떻게 조절했는지 그 사람은 모름.
    /// 룸 나가면 휘발 (PlayerPrefs 저장 X — 매 룸마다 유저 다름).
    /// 대기실 ↔ 인게임 씬 전환 횡단 (DontDestroyOnLoad).
    ///
    /// 구독자:
    ///   - PerUserVoiceApplier (각 PlayerStub Speaker 측) — Speaker.AudioSource.volume 갱신
    ///   - VoicePanelController / LobbyPlayerEntry — 외부 변경 시 슬라이더 SetValueWithoutNotify
    ///
    /// 셋업: NetworkManager 또는 PlayerPrefs/SettingsManager 같은 DDoL GameObject 에 부착.
    /// 또는 첫 접근 시 lazy 생성 (MenuScene 부트 시점).
    /// </summary>
    public class PerUserVoiceSettings : MonoBehaviourPunCallbacks
    {
        public static PerUserVoiceSettings Instance { get; private set; }

        /// <summary>볼륨 변경 시 발화 — 인자 = 변경된 ActorNumber.</summary>
        public event Action<int> OnVolumeChanged;

        private readonly Dictionary<int, float> volumes = new Dictionary<int, float>();

        /// <summary>
        /// 첫 씬 로드 후 자동 생성 — 사용자가 인스펙터에서 어디에도 부착 안 해도 동작 보장.
        /// 씬에 이미 부착된 인스턴스가 있으면 (Awake 가 셋팅) 그걸 우선 사용.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            if (FindObjectOfType<PerUserVoiceSettings>() != null) return;

            var go = new GameObject("[Auto] PerUserVoiceSettings");
            DontDestroyOnLoad(go);
            go.AddComponent<PerUserVoiceSettings>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>해당 ActorNumber 의 볼륨. 미설정이면 default 1.0.</summary>
        public float GetVolumeFor(int actorNumber)
        {
            return volumes.TryGetValue(actorNumber, out var v) ? v : 1f;
        }

        /// <summary>볼륨 설정. 0~2 클램프. 동일 값이면 이벤트 발화 안 함.</summary>
        public void SetVolumeFor(int actorNumber, float volume)
        {
            volume = Mathf.Clamp(volume, 0f, 2f);
            float current = GetVolumeFor(actorNumber);
            if (Mathf.Approximately(current, volume)) return;

            volumes[actorNumber] = volume;
            OnVolumeChanged?.Invoke(actorNumber);
        }

        /// <summary>룸 나갈 때 호출 — 모든 볼륨 default 로 리셋.</summary>
        public override void OnLeftRoom()
        {
            volumes.Clear();
            // 의도적으로 OnVolumeChanged 발화 X — 룸 떠나는 시점이라 구독자(Applier/UI) 도 정리 중.
        }
    }
}
