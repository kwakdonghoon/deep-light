using System.Collections.Generic;
using UnityEngine;

namespace Abyss
{
    /// <summary>외부 파일 없이 효과음을 합성해서 재생 (game.js 의 WebAudio 효과음 포팅).</summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        const int Rate = 22050;
        bool sfxMuted, musicMuted;
        /// <summary>효과음만 끄기. 설정은 기기에 저장되어 다음 실행에도 유지된다.</summary>
        public bool SfxMuted { get { return sfxMuted; } set { sfxMuted = value; SavePref("deeplight_sfx_muted", value); } }
        /// <summary>배경음만 끄기. 설정은 저장된다.</summary>
        public bool MusicMuted { get { return musicMuted; } set { musicMuted = value; if (music != null) music.mute = value; SavePref("deeplight_music_muted", value); } }
        /// <summary>효과음·음악 모두 (M 키). 둘 다 꺼져 있으면 true.</summary>
        public bool Muted { get { return sfxMuted && musicMuted; } set { SfxMuted = value; MusicMuted = value; } }
        static void SavePref(string key, bool v) { try { PlayerPrefs.SetInt(key, v ? 1 : 0); PlayerPrefs.Save(); } catch (System.Exception) { } }
        static bool LoadPref(string key) { try { return PlayerPrefs.GetInt(key, 0) == 1; } catch (System.Exception) { return false; } }
        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        AudioSource src, music;
        const float MusicVolume = 0.30f; float musicFade;   // 배경음 목표 음량 / 시작할 때 서서히 커지는 진행도(0~1)

        void Start()
        {
            // 배경음: 외부 파일 없이 합성한 32초짜리 심해 루프 (낮은 드론 + 느린 코드 패드 + 드문 오르골 선율 + 물방울)
            music = gameObject.AddComponent<AudioSource>(); music.playOnAwake = false; music.spatialBlend = 0f; music.loop = true;
            music.volume = 0f; music.mute = musicMuted;
            // 수백만 샘플 합성이 첫 화면을 멈추지 않도록 작업 스레드에서 만들고, 끝나면 Update 에서 클립을 만들어 재생한다. (WebGL 은 스레드가 없어 그 자리에서 만든다)
#if UNITY_WEBGL
            musicData = BuildMusicData(); musicReady = true;
#else
            musicTask = System.Threading.Tasks.Task.Run(() => { musicData = BuildMusicData(); });
#endif
        }
        float[] musicData; bool musicReady; System.Threading.Tasks.Task musicTask;

        void Update()
        {
            if (music == null) return;
            if (musicTask != null && musicTask.IsCompleted) { if (musicTask.Status == System.Threading.Tasks.TaskStatus.RanToCompletion) musicReady = true; musicTask = null; }
            if (musicReady && musicData != null)
            {
                musicReady = false;
                var clip = AudioClip.Create("music", musicData.Length, 1, Rate, false); clip.SetData(musicData, 0); musicData = null;
                music.clip = clip; music.Play();
            }
            musicFade = Mathf.Min(1f, musicFade + Time.unscaledDeltaTime / 4f);    // 4초에 걸쳐 서서히 켜진다
            music.volume = MusicVolume * musicFade;
        }

        /// <summary>D단조 분위기의 32초 루프. 드론의 주파수는 루프 길이 안에서 정수 주기가 되도록 맞추고, 패드·선율은 끝을 처음으로 이어 붙여 이음매 소리가 나지 않게 한다.</summary>
        static float[] BuildMusicData()
        {
            const int sec = 32; int n = Rate * sec; var d = new float[n]; const float TwoPi = Mathf.PI * 2f;
            // 1. 드론 (D2 + A2), 16초 주기로 숨 쉬듯 커졌다 작아진다
            foreach (float f0 in new[] { 73.42f, 110.0f })
            {
                float f = Mathf.Round(f0 * sec) / sec;
                for (int i = 0; i < n; i++) { float t = i / (float)Rate; d[i] += 0.085f * (0.6f + 0.4f * Mathf.Sin(TwoPi * 2f * t / sec)) * Mathf.Sin(TwoPi * f * t); }
            }
            // 2. 패드 코드: Dm | Bb | Gm | A (각 8초, 앞뒤 2초씩 겹쳐 부드럽게 이어진다)
            float[][] chords = { new[] { 146.83f, 174.61f, 220.00f }, new[] { 116.54f, 174.61f, 233.08f }, new[] { 98.00f, 146.83f, 233.08f }, new[] { 110.00f, 138.59f, 164.81f } };
            int padLen = Rate * 10;
            for (int c = 0; c < chords.Length; c++)
            {
                int start = c * 8 * Rate;
                foreach (float f in chords[c])
                    for (int j = 0; j < padLen; j++)
                    {
                        float t = j / (float)Rate, env = Mathf.Clamp01(t / 2.5f) * Mathf.Clamp01((10f - t) / 3f);
                        float v = 0.045f * env * (Mathf.Sin(TwoPi * f * t) + 0.35f * Mathf.Sin(TwoPi * f * 2.003f * t));
                        d[(start + j) % n] += v;
                    }
            }
            // 3. 오르골 같은 선율 (D단조 5음계), 소리의 꼬리는 루프 처음으로 이어진다
            float[][] mel = {
                new[] { 2f, 440f }, new[] { 4f, 349.23f }, new[] { 7f, 293.66f }, new[] { 10f, 392f }, new[] { 13f, 440f }, new[] { 14.5f, 523.25f },
                new[] { 18f, 349.23f }, new[] { 21f, 293.66f }, new[] { 24f, 392f }, new[] { 26f, 440f }, new[] { 29f, 587.33f }, new[] { 30.5f, 523.25f } };
            int noteLen = Rate * 4;
            foreach (var m in mel)
            {
                int start = (int)(m[0] * Rate);
                for (int j = 0; j < noteLen; j++)
                {
                    float t = j / (float)Rate, env = Mathf.Exp(-t * 1.5f) * Mathf.Min(1f, t / 0.012f);
                    float v = 0.07f * env * (Mathf.Sin(TwoPi * m[1] * t) + 0.3f * Mathf.Exp(-t * 3f) * Mathf.Sin(TwoPi * m[1] * 2f * t));
                    d[(start + j) % n] += v;
                }
            }
            // 4. 물방울: 짧게 올라가는 높은 소리가 드문드문
            var rng = new System.Random(77);
            for (int b = 0; b < 9; b++)
            {
                int start = (int)(rng.NextDouble() * n); float fa = 700f + (float)rng.NextDouble() * 700f; int bl = (int)(Rate * 0.1f);
                for (int j = 0; j < bl; j++)
                {
                    float t = j / (float)bl, f = fa * (1f + 0.8f * t);
                    d[(start + j) % n] += 0.03f * Mathf.Sin(TwoPi * f * j / Rate) * (1f - t) * (1f - t);
                }
            }
            // 너무 커지지 않게 최대치를 눌러 준다
            float peak = 0.0001f; for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
            float g = peak > 0.6f ? 0.6f / peak : 1f; if (g < 1f) for (int i = 0; i < n; i++) d[i] *= g;
            return d;
        }

        enum Wave { Sine, Square, Triangle, Saw }

        void Awake()
        {
            sfxMuted = LoadPref("deeplight_sfx_muted"); musicMuted = LoadPref("deeplight_music_muted");   // 지난번 설정을 이어받는다
            src = gameObject.AddComponent<AudioSource>(); src.playOnAwake = false; src.spatialBlend = 0f;
            clips[Sfx.Mine] = Make(Noise(0.05f, 0.04f, 1500));
            clips[Sfx.Break] = Make(Mix(Noise(0.12f, 0.07f, 300), Tone(120, 60, 0.12f, Wave.Triangle, 0.08f)));
            clips[Sfx.Swing] = Make(Noise(0.08f, 0.03f, 2500));
            clips[Sfx.Hit] = Make(Tone(300, 120, 0.1f, Wave.Saw, 0.05f));
            clips[Sfx.Hurt] = Make(Tone(200, 70, 0.25f, Wave.Saw, 0.08f));
            clips[Sfx.Pick] = Make(Tone(700, 1200, 0.08f, Wave.Sine, 0.06f));
            clips[Sfx.Craft] = Make(Mix(Tone(440, 880, 0.12f, Wave.Square, 0.05f), Delay(Tone(660, 1320, 0.15f, Wave.Square, 0.05f), 0.09f)));
            clips[Sfx.Chest] = Make(Mix(Tone(520, 1040, 0.1f, Wave.Triangle, 0.07f), Delay(Tone(780, 1560, 0.18f, Wave.Triangle, 0.07f), 0.1f)));
            clips[Sfx.Boss] = Make(Tone(90, 40, 0.8f, Wave.Saw, 0.1f));
            clips[Sfx.Alarm] = Make(Mix(Tone(988, 988, 0.07f, Wave.Square, 0.045f), Delay(Tone(988, 988, 0.07f, Wave.Square, 0.045f), 0.13f)));   // 저산소 경고: 짧은 두 번 삑
        }

        public void Play(Sfx s)
        {
            if (sfxMuted || src == null) return;
            AudioClip c; if (clips.TryGetValue(s, out c)) src.PlayOneShot(c, 2.5f);   // 웹 게인이 작아서 보정
        }

        static AudioClip Make(float[] samples)
        {
            var c = AudioClip.Create("sfx", samples.Length, 1, Rate, false); c.SetData(samples, 0); return c;
        }

        static float[] Tone(float f0, float f1, float dur, Wave wave, float vol)
        {
            int n = Mathf.Max(1, (int)(Rate * dur)); var d = new float[n]; double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = f0 * Mathf.Pow(f1 / f0, t);                         // 지수 주파수 변화
                phase += f / Rate; float p = (float)(phase - System.Math.Floor(phase));
                float s = wave == Wave.Sine ? Mathf.Sin(p * Mathf.PI * 2)
                        : wave == Wave.Square ? (p < 0.5f ? 1f : -1f)
                        : wave == Wave.Triangle ? 4f * Mathf.Abs(p - 0.5f) - 1f
                        : 2f * p - 1f;
                float gain = vol * Mathf.Pow(0.0001f / vol, t);               // 지수 감쇠
                d[i] = s * gain;
            }
            return d;
        }

        static float[] Noise(float dur, float vol, float hp)
        {
            int n = Mathf.Max(1, (int)(Rate * dur)); var d = new float[n]; var rng = new System.Random(12345);
            float rc = 1f / (2f * Mathf.PI * hp), dt = 1f / Rate, a = rc / (rc + dt), prevIn = 0, prevOut = 0;
            for (int i = 0; i < n; i++)
            {
                float x = ((float)rng.NextDouble() * 2f - 1f) * (1f - i / (float)n);
                float y = a * (prevOut + x - prevIn); prevIn = x; prevOut = y;  // 1차 하이패스
                d[i] = y * vol * 3f;
            }
            return d;
        }

        static float[] Delay(float[] s, float sec)
        {
            int off = (int)(Rate * sec); var d = new float[s.Length + off]; System.Array.Copy(s, 0, d, off, s.Length); return d;
        }

        static float[] Mix(float[] a, float[] b)
        {
            var d = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < a.Length; i++) d[i] += a[i];
            for (int i = 0; i < b.Length; i++) d[i] += b[i];
            return d;
        }
    }
}
