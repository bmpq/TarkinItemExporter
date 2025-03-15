using UnityEngine;
using UnityEngine.UI;

namespace TarkinItemExporter.UI
{
    public class UIProgressBar : MonoBehaviour
    {
        public RectTransform RectTransform => transform as RectTransform;
        private RectTransform rectFill;

        public void Start()
        {
            Image backgroundImage = gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0, 0, 0, 0.6f);

            GameObject fillGO = new GameObject("ProgressBarFill");
            fillGO.transform.SetParent(transform, false);
            rectFill = fillGO.AddComponent<RectTransform>();
            fillGO.AddComponent<Image>().color = new Color(1, 1, 1, 0.9f);
            rectFill.sizeDelta = Vector2.zero; // hide at start
        }

        public void SetProgress(float progress)
        {
            if (rectFill == null)
                return;

            rectFill.anchorMin = new Vector2(0, 0);
            rectFill.anchorMax = new Vector2(progress, 1);
            rectFill.offsetMin = Vector2.zero;
            rectFill.offsetMax = Vector2.zero;
        }
    }
}
