using UnityEngine;

namespace Core.Performance
{
    [DisallowMultipleComponent]
    public sealed class FPSLimiter : MonoBehaviour
    {
        public static FPSLimiter Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int _targetFPS = 60;
        [SerializeField] private bool _disableVSync = true;

        private void Awake()
        {
            InitializeSingleton();
            ApplyFPSSettings();
        }

        private void InitializeSingleton()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void ApplyFPSSettings()
        {
            // В профессиональной разработке vSync контролируется строго, 
            // так как он принудительно перебивает targetFrameRate
            if (_disableVSync)
            {
                QualitySettings.vSyncCount = 0;
            }

            Application.targetFrameRate = _targetFPS;
        }

        /// <summary>
        /// Позволяет изменять лимит FPS динамически из других скриптов (например, из меню настроек)
        /// </summary>
        public void UpdateTargetFPS(int newFPS)
        {
            _targetFPS = newFPS;
            ApplyFPSSettings();
        }
    }
}
