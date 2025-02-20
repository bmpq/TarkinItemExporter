using EFT.InputSystem;
using EFT.UI;
using EFT.UI.Screens;
using EFT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Diz.Utils;

namespace gltfmod.UI
{
    internal class ProgressScreen : UIScreen
    {
        public static ProgressScreen Instance
        {
            get
            {
                if (_instance == null)
                    Instantiate();
                return _instance;
            }
        }
        private static ProgressScreen _instance;
        private static void Instantiate()
        {
            _instance = new GameObject("Screen Progress").AddComponent<ProgressScreen>();
            _instance.transform.SetParent(MonoBehaviourSingleton<PreloaderUI>.Instance.transform.GetChild(0));

            Canvas canvas = _instance.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999;

            RectTransform rect = _instance.GetOrAddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image bg = _instance.GetOrAddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);

            GameObject goTextStatus = new GameObject("Text status");
            goTextStatus.transform.SetParent(_instance.transform);
            _instance.textStatus = goTextStatus.AddComponent<TextMeshProUGUI>();
            _instance.textStatus.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _instance.textStatus.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _instance.textStatus.rectTransform.anchoredPosition = Vector2.zero;
            _instance.textStatus.horizontalAlignment = HorizontalAlignmentOptions.Center;
            _instance.textStatus.fontSize = 12;

            GameObject goProgressBar = new GameObject("Progress bar", typeof(RectTransform));
            _instance.progressBar = goProgressBar.AddComponent<UIProgressBar>();
            _instance.progressBar.transform.SetParent(_instance.transform, false);
            _instance.progressBar.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _instance.progressBar.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _instance.progressBar.RectTransform.anchoredPosition = new Vector2(0, -30f);
            _instance.progressBar.RectTransform.sizeDelta = new Vector2(300f, 10f);
        }

        void OnEnable()
        {
            UIEventSystem.Instance.Disable();
            Plugin.Log.LogEvent += OnLog;
            AssetStudio.ProgressLogger.OnProgress += OnProgress;
            Plugin.InputBlocked = true;
        }

        void OnDisable()
        {
            UIEventSystem.Instance.Enable();
            Plugin.Log.LogEvent -= OnLog;
            AssetStudio.ProgressLogger.OnProgress -= OnProgress;
            Plugin.InputBlocked = false;
        }

        private TMP_Text textStatus;
        private UIProgressBar progressBar;

        private void OnProgress(int value)
        {
            AsyncWorker.RunInMainTread(() => UpdateProgressBar(value));
        }

        private void OnLog(object sender, BepInEx.Logging.LogEventArgs e)
        {
            AsyncWorker.RunInMainTread(() => UpdateText(e.Data.ToString()));
        }

        private void UpdateProgressBar(int value)
        {
            progressBar.SetProgress((float)value / 100f);
        }

        private void UpdateText(string value)
        {
            textStatus.text = value;
        }

        public override InputNode.ETranslateResult TranslateCommand(ECommand command)
        {
            return GetDefaultBlockResult(command);
        }
    }
}
