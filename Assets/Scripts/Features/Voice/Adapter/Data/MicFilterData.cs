using UnityEngine;
using SwDreams.Features.Voice.Domain;

namespace SwDreams.Features.Voice.Adapter.Data
{
    /// <summary>
    /// 단일 마이크 필터 SO. 5종 enum 별로 1개 인스턴스 권장.
    /// type 에 맞는 파라미터만 사용됨 (다른 필드는 무시).
    ///
    /// 인스펙터 채움 가이드:
    ///   - LowPass: cutoffFrequency (Hz). 1500 이하면 명확히 먹먹.
    ///   - Distortion: distortionLevel (0~1). 0.5+ 명확.
    ///   - Echo: echoDelayMs / echoDecay / echoDryMix / echoWetMix.
    ///   - Pitch: pitchValue. Helium 1.5~1.8 / Demon 0.6~0.7.
    ///   - 공통 duration: 기본 15초. 인스펙터에서 SO 단위 조정 가능.
    /// </summary>
    [CreateAssetMenu(fileName = "MicFilter_New", menuName = "ProjectSD/Data/MicFilterData")]
    public class MicFilterData : ScriptableObject
    {
        [Header("공통")]
        [Tooltip("필터 종류. SO 1개당 1종 고정.")]
        public MicFilterType type = MicFilterType.LowPass;

        [Tooltip("필터 적용 지속 시간(초). 만료 후 자동 정리.")]
        [Min(0.1f)] public float durationSeconds = 15f;

        [Tooltip("픽업 표시용 라벨 (디버그/Localization 키 후속).")]
        public string displayLabel = "괴상한 마이크";

        // ===== LowPass =====
        [Header("LowPass (type == LowPass 시)")]
        [Tooltip("AudioLowPassFilter.cutoffFrequency (Hz). 10~22000.")]
        [Range(10f, 22000f)] public float cutoffFrequency = 1200f;

        // ===== Distortion =====
        [Header("Distortion (type == Distortion 시)")]
        [Tooltip("AudioDistortionFilter.distortionLevel (0~1).")]
        [Range(0f, 1f)] public float distortionLevel = 0.6f;

        // ===== Echo =====
        [Header("Echo (type == Echo 시)")]
        [Tooltip("AudioEchoFilter.delay (ms). 10~5000.")]
        [Range(10f, 5000f)] public float echoDelayMs = 400f;

        [Tooltip("AudioEchoFilter.decayRatio (0~1).")]
        [Range(0f, 1f)] public float echoDecay = 0.5f;

        [Tooltip("AudioEchoFilter.dryMix (0~1). 원본 비율.")]
        [Range(0f, 1f)] public float echoDryMix = 1f;

        [Tooltip("AudioEchoFilter.wetMix (0~1). 메아리 비율.")]
        [Range(0f, 1f)] public float echoWetMix = 0.7f;

        // ===== Pitch =====
        [Header("Pitch (type == PitchHelium / PitchDemon 시)")]
        [Tooltip("AudioSource.pitch. Helium 1.5~1.8 / Demon 0.6~0.7. 음정+속도 동시 변경.")]
        [Range(0.3f, 3f)] public float pitchValue = 1.6f;
    }
}
