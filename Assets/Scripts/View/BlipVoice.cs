using UnityEngine;

namespace Hwatu.View
{
    /// <summary>
    /// [3단계·A] 차사의 웅얼거림 — 이 게임의 첫 소리. 대화 타자기가 글자마다 부르는
    /// 절차 합성 블립(외부 오디오 에셋 금지). 사인 기반 그레인 3종(저음, 빠른 감쇠)을
    /// AudioClip.Create로 한 번 굽고, 글자마다 셋 중 랜덤을 피치 ±8%로 재생한다.
    ///
    /// AudioListener는 메인 카메라에 1개만 보장한다(코드 생성 씬은 리스너가 없다).
    /// 마스터 볼륨은 상수로 노출한다. 추후 녹음 소스로 교체할 수 있도록 클립 배열
    /// 주입점(<see cref="Clips"/>)을 둔다 — null이면 절차 합성 그레인을 쓴다.
    /// </summary>
    public static class BlipVoice
    {
        /// <summary>마스터 볼륨 상수 (블립 PlayOneShot 볼륨 스케일).</summary>
        public const float MasterVolume = 0.30f;

        /// <summary>런타임 볼륨 노브 (기본 = MasterVolume). 0이면 무음.</summary>
        public static float Volume = MasterVolume;

        /// <summary>
        /// [교체점] 녹음 소스 주입: 비우지 않으면 절차 그레인 대신 이 클립들을 재생한다.
        /// 게임 시작 시 한 번 채워 두면 이후 합성 없이 이 배열에서 랜덤 재생한다.
        /// </summary>
        public static AudioClip[] Clips;

        // ── 절차 그레인 규격 (사양: 90~150Hz 3단계, 배음 1~2개, 0.05~0.08초, 빠른 감쇠) ──
        private static readonly float[] GrainFreqs = { 96f, 120f, 150f };   // ㅡ · ㅓ · ㅏ 느낌
        private static readonly float[] GrainDurations = { 0.06f, 0.07f, 0.055f };
        private const int SampleRate = 44100;
        private const float PitchJitter = 0.08f;

        private static AudioClip[] _grains;
        private static AudioSource _source;

        /// <summary>공백·문장부호를 제외한 "발음되는" 글자만 블립을 낸다.</summary>
        public static bool IsVoicedChar(char c)
        {
            if (char.IsWhiteSpace(c)) return false;
            if (char.IsPunctuation(c) || char.IsSymbol(c)) return false;
            return true;
        }

        /// <summary>글자 하나에 해당하는 블립 1회 재생 (셋 중 랜덤 · 피치 ±8%).</summary>
        public static void PlayBlip()
        {
            var source = EnsureSource();
            if (source == null) return;
            var bank = (Clips != null && Clips.Length > 0) ? Clips : EnsureGrains();
            if (bank == null || bank.Length == 0) return;

            var clip = bank[Random.Range(0, bank.Length)];
            if (clip == null) return;
            source.pitch = 1f + Random.Range(-PitchJitter, PitchJitter);
            source.PlayOneShot(clip, Mathf.Clamp01(Volume));
        }

        /// <summary>메인 카메라에 AudioListener를 1개만 보장한다 (이미 있으면 아무것도 안 한다).</summary>
        public static void EnsureAudioListener()
        {
            var existing = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            if (existing != null && existing.Length > 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            cam.gameObject.AddComponent<AudioListener>();
        }

        private static AudioSource EnsureSource()
        {
            EnsureAudioListener();
            if (_source != null) return _source;
            var go = new GameObject("BlipVoice") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;       // 2D
            _source.loop = false;
            _source.bypassEffects = true;
            _source.bypassListenerEffects = true;
            return _source;
        }

        private static AudioClip[] EnsureGrains()
        {
            if (_grains != null) return _grains;
            _grains = new AudioClip[GrainFreqs.Length];
            for (int i = 0; i < GrainFreqs.Length; i++)
                _grains[i] = BakeGrain(GrainFreqs[i], GrainDurations[i], i);
            return _grains;
        }

        // 사인(기본 + 배음 1~2) × (짧은 어택 + 빠른 지수 감쇠) 엔벨로프.
        private static AudioClip BakeGrain(float f0, float durSec, int index)
        {
            int n = Mathf.Max(1, (int)(SampleRate * durSec));
            var data = new float[n];
            float decayRate = 5f / durSec;           // dur 끝에서 ≈ e^-5 (거의 0)
            const float attack = 0.003f;             // 3ms 어택으로 클릭음 방지
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t / attack) * Mathf.Exp(-t * decayRate);
                float w = 2f * Mathf.PI * f0 * t;
                // 배음 1~2개를 섞는다 (그레인마다 살짝 다른 배음 비율).
                float harm = index == 0 ? 0.35f : (index == 1 ? 0.5f : 0.25f);
                float sample = Mathf.Sin(w) + harm * Mathf.Sin(2f * w) + 0.15f * Mathf.Sin(3f * w);
                data[i] = sample / (1f + harm + 0.15f) * env * 0.9f;
            }
            var clip = AudioClip.Create($"blip_grain_{index}", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
