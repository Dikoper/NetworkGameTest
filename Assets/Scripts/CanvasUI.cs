using UnityEngine;

    public class CanvasUI : MonoBehaviour
    {
        public RectTransform mainPanel;

        public RectTransform playersPanel;

        // static instance that can be referenced from static methods below.
        static CanvasUI instance;

        void Awake()
        {
            instance = this;
        }

        public static void SetActive(bool active)
        {
            instance.mainPanel.gameObject.SetActive(active);
        }

        public static RectTransform GetPlayersPanel() => instance.playersPanel;
    }
