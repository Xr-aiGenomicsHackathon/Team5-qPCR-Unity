using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.qPCR
{
    public sealed class TrainingSceneFade : MonoBehaviour
    {
        [SerializeField] private CanvasGroup blackCover;
        public bool IsLoading { get; private set; }
        private IEnumerator Start() { yield return Fade(1f, 0f); }
        public void Load(string sceneName) { if (!IsLoading) StartCoroutine(LoadRoutine(sceneName)); }
        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;
            yield return Fade(blackCover == null ? 0 : blackCover.alpha, 1f);
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null) { IsLoading = false; yield return Fade(1f, 0f); yield break; }
            while (!operation.isDone) yield return null;
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
