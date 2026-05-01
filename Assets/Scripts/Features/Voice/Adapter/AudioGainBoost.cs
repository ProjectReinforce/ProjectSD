using UnityEngine;

namespace SwDreams.Features.Voice.Adapter
{
    /// <summary>
    /// AudioSource 출력 직전 sample-level gain 곱 (R14).
    /// AudioSource.volume 의 0~1 cap 을 우회 — gain > 1 로 boost 가능 (마이크 작은 유저 보정용).
    ///
    /// AudioSource 와 같은 GameObject 에 부착해야 OnAudioFilterRead 가 동작 (AudioSource 출력 후처리 hook).
    /// PerUserVoiceApplier 가 매 슬라이더 변경 시 gain 동적 조절.
    ///
    /// 주의: gain 이 너무 높으면 clipping 발생 (음질 깨짐). 권장 0~2.
    /// 1.0 = 변화 없음 (early-out 으로 CPU 0).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioGainBoost : MonoBehaviour
    {
        [Tooltip("0=음소거, 1=원본, 2=2배 boost. PerUserVoiceApplier 가 동적 set.")]
        public float gain = 1f;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            // gain == 1 일 때 곱셈 스킵 — CPU 절약.
            if (Mathf.Approximately(gain, 1f)) return;

            float g = Mathf.Max(0f, gain);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= g;
            }
        }
    }
}
