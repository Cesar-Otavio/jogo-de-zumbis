using UnityEngine;

namespace Combat
{
    /* Radio squelch clicks and a static bed, generated in code so transmissions sound like a handset
     * even before any recorded voice exists. Made once and reused. */
    public static class RadioSounds
    {
        private const int Rate = 44100;

        // keying the mic: a click, a short burst of filtered noise, a softer tail
        public static AudioClip Squelch(bool open)
        {
            float length = open ? 0.2f : 0.14f;
            int count = (int)(Rate * length);
            var data = new float[count];
            var random = new System.Random(open ? 7 : 11);
            float low = 0.0f, band = 0.0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                // a cheap band-pass around 2 kHz: the crackle of a small speaker
                low += 0.35f * (noise - low);
                band += 0.5f * ((noise - low) - band);
                float burst = Mathf.Exp(-t * (open ? 22.0f : 30.0f));
                float click = t < 0.004f ? (open ? 0.9f : -0.8f) * (1.0f - t / 0.004f) : 0.0f;
                float tone = open ? 0.0f : 0.25f * Mathf.Sin(2.0f * Mathf.PI * 1250.0f * t) * Mathf.Exp(-t * 40.0f);
                data[i] = Mathf.Clamp(click + band * 0.8f * burst + tone, -1.0f, 1.0f);
            }

            AudioClip clip = AudioClip.Create(open ? "RadioSquelchOpen" : "RadioSquelchClose", count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // two seconds of crackling static that loops without a seam
        public static AudioClip Static()
        {
            int count = Rate * 2;
            var data = new float[count];
            var random = new System.Random(3);
            float low = 0.0f;
            for (int i = 0; i < count; i++)
            {
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                low += 0.2f * (noise - low);
                float crackle = random.NextDouble() < 0.0008 ? (float)(random.NextDouble() * 2.0 - 1.0) * 0.8f : 0.0f;
                float flutter = 0.75f + 0.25f * Mathf.Sin(2.0f * Mathf.PI * 3.0f * i / Rate);
                data[i] = ((noise - low) * 0.5f * flutter) + crackle;
            }

            // crossfade the ends so the loop point is silent
            int fade = Rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float a = (float)i / fade;
                data[i] = data[i] * a + data[count - fade + i] * (1.0f - a);
            }

            AudioClip clip = AudioClip.Create("RadioStatic", count - fade, 1, Rate, false);
            var trimmed = new float[count - fade];
            System.Array.Copy(data, trimmed, trimmed.Length);
            clip.SetData(trimmed, 0);
            return clip;
        }
    }
}
