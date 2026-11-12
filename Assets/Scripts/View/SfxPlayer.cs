using System.Collections.Generic;
using UnityEngine;

namespace Abyss
{
    /// <summary>외부 파일 없이 효과음을 합성해서 재생 (game.js 의 WebAudio 효과음 포팅).</summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        const int Rate = 22050;
        public bool Muted { get; set; }
        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        AudioSource src;

        enum Wave { Sine, Square, Triangle, Saw }

        void Awake()
        {
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
        }

        public void Play(Sfx s)
        {
            if (Muted || src == null) return;
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
