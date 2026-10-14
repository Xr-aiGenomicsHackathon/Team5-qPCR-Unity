using UnityEngine;

namespace Team5.qPCR
{
    public sealed class QpcrMachineAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioSource loopSource;
        [SerializeField] private AudioClip powerOnClip;
        [SerializeField] private AudioClip drawerClip;
        [SerializeField] private AudioClip buttonClip;
        [SerializeField] private AudioClip errorClip;
        [SerializeField] private AudioClip runLoopClip;

        public void Configure(AudioSource oneShot, AudioSource looping)
        {
            oneShotSource = oneShot;
            loopSource = looping;
        }

        private void Awake()
        {
            if (oneShotSource == null)
                oneShotSource = gameObject.AddComponent<AudioSource>();
            if (loopSource == null)
                loopSource = gameObject.AddComponent<AudioSource>();

            ConfigureSource(oneShotSource, false, 0.72f);
            ConfigureSource(loopSource, true, 0.18f);
            powerOnClip ??= CreateTone("qPCR power", 480f, 0.42f, true);
            drawerClip ??= CreateMotor("qPCR drawer", 0.68f);
            buttonClip ??= CreateTone("qPCR button", 720f, 0.07f, false);
            errorClip ??= CreateTone("qPCR error", 175f, 0.22f, false);
            runLoopClip ??= CreateMotor("qPCR run", 1.2f);
        }

        public void PlayPowerOn() => PlayOneShot(powerOnClip, 0.9f);
        public void PlayDrawer() => PlayOneShot(drawerClip, 0.55f);
        public void PlayButton() => PlayOneShot(buttonClip, 0.55f);
        public void PlayError() => PlayOneShot(errorClip, 0.8f);

        public void StartRunLoop()
        {
            if (loopSource == null || runLoopClip == null)
                return;
            loopSource.clip = runLoopClip;
            loopSource.loop = true;
            loopSource.Play();
        }

        public void StopRunLoop()
        {
            if (loopSource != null)
                loopSource.Stop();
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (oneShotSource != null && clip != null)
                oneShotSource.PlayOneShot(clip, volume);
        }

        private static void ConfigureSource(AudioSource source, bool loop, float volume)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.minDistance = 0.2f;
            source.maxDistance = 6f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        private static AudioClip CreateTone(string name, float frequency, float duration, bool rising)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var t = index / (float)sampleRate;
                var envelope = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                var hz = rising ? frequency + 420f * (t / duration) : frequency;
                samples[index] = Mathf.Sin(2f * Mathf.PI * hz * t) * envelope * 0.18f;
            }
            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateMotor(string name, float duration)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[sampleCount];
            var random = new System.Random(2805);
            for (var index = 0; index < sampleCount; index++)
            {
                var t = index / (float)sampleRate;
                var envelope = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((duration - t) / 0.1f);
                var motor = Mathf.Sin(2f * Mathf.PI * 84f * t) * 0.09f;
                var noise = ((float)random.NextDouble() * 2f - 1f) * 0.025f;
                samples[index] = (motor + noise) * envelope;
            }
            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
