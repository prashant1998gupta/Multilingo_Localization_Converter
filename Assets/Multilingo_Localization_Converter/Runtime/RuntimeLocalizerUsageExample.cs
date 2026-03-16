// Assets/Scripts/Localization/RuntimeLocalizerUsageExample.cs
using UnityEngine;
using System.Threading.Tasks;

namespace Multilingo.Localization
{

    public class RuntimeLocalizerUsageExample : MonoBehaviour
    {
        async void Start()
        {
            if (LocalizationManager.Instance == null)
            {
                Debug.LogWarning("LocalizationManager not present in scene.");
                return;
            }

            // Example 1: immediate score text (formatted)
            string score = await LocalizationManager.Instance.GetStringAsync("UI", "ScoreMessage", 1200);
            Debug.Log($"ScoreMessage -> {score}");

            // Example 2: dynamic key created at runtime (if table has that key)
            string dyn = await LocalizationManager.Instance.GetStringAsync("DynamicTexts", "item_pickup_sword");
            Debug.Log($"item_pickup_sword -> {dyn}");

            // Example 3: change locale by canonical code (ko-KR, zh-CN, etc.)
            LocalizationManager.Instance.SetLocaleByCode("ko-KR");

            // Optional: pre-warm common keys for performance
            _ = LocalizationManager.Instance.PrewarmKeys("UI", "Play", "Settings", "Exit");
        }
    }
}
