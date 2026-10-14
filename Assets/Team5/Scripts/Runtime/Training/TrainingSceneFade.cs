using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.qPCR
{
    public sealed class TrainingSceneFade : MonoBehaviour
    {
        [SerializeField] private CanvasGroup blackCover;
        private Coroutine fadeRoutine;
        public bool IsLoading { get; private set; }
        public event Action<string> LoadFailed;
        private void Start() { if (!IsLoading) fadeRoutine = StartCoroutine(Fade(1f, 0f)); }
        public void Load(string sceneName) => TryLoad(sceneName);
        public bool TryLoad(string sceneName)
        {
            if (IsLoading) return false;
            if (!isActiveAndEnabled || string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
                Fail("The laboratory is unavailable. Check that LobbyScene and DemoScene are included in the build, then try again.");
                return false;
            }
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            IsLoading = true;
            fadeRoutine = StartCoroutine(LoadRoutine(sceneName));
            return true;
        }
        private IEnumerator LoadRoutine(string sceneName)
        {
            yield return Fade(blackCover == null ? 0f : blackCover.alpha, 1f);
            AsyncOperation operation = null;
            string failure = null;
            try { operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (Exception) { failure = "The laboratory could not load. Please check the scene setup and try again."; }
            if (operation == null)
            {
                yield return Fade(1f, 0f);
                Fail(failure ?? "The laboratory could not load. Please try again.");
                yield break;
            }
            while (!operation.isDone) yield return null;
        }
        private void Fail(string message)
        {
            IsLoading = false;
            if (blackCover != null) blackCover.alpha = 0f;
            LoadFailed?.Invoke(message);
        }
        private IEnumerator Fade(float from, float to)
        {
            var time = 0f;
            while (time < .3f)
            {
                time += Time.unscaledDeltaTime;
                if (blackCover != null) blackCover.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(time / .3f));
                yield return null;
            }
            if (blackCover != null) blackCover.alpha = to;
        }
    }
}
