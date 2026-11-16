using UnityEngine;
using UnityEngine.UI;

public sealed class PowerBarUI : MonoBehaviour
{
    [System.Serializable]
    public class Bar
    {
        public Image trackFill;          // 进度条填充
        public RectTransform bowCurrent; // 黑蝴蝶结 (当前)
        public RectTransform bowLast;    // 白蝴蝶结 (历史)
    }

    [SerializeField] MatchManager match;
    [SerializeField] Slider powerSlider;
    [SerializeField] Bar player1;
    [SerializeField] Bar player2;

    // 记录历史力度，-1 代表从来没有打过球
    float last1 = -1f, last2 = -1f;

    // 用于实时记录当前拉到的最大值（蓄力）
    float currentMaxValue = 0f;
    MatchManager.PlayerState currentlyChargingPlayer = null;

    void Update()
    {
        if (!match || !powerSlider) return;

        // 1. 获取滑条百分比 (0~1)
        float range = Mathf.Max(.0001f, powerSlider.maxValue - powerSlider.minValue);
        float sliderValue = Mathf.Clamp01((powerSlider.value - powerSlider.minValue) / range);

        // 2. 获取游戏状态
        bool isGolfPhase = match.Phase == MatchPhase.Golf || match.Phase == MatchPhase.Tutorial;
        var currentShooter = match.CurrentShooter;

        // --- 核心逻辑：检测是否蓄力以及何时击球 ---
        bool isSomeoneCharging = isGolfPhase && sliderValue > 0.01f;

        if (isSomeoneCharging)
        {
            // 如果当前有人在蓄力，记录是谁，并且更新最大力度
            currentlyChargingPlayer = currentShooter;
            if (sliderValue > currentMaxValue)
            {
                currentMaxValue = sliderValue;
            }
        }
        else
        {
            // 如果滑条回零（或不再是击球阶段），但刚才有人在蓄力，说明“击球完成”
            if (currentlyChargingPlayer != null)
            {
                // 将刚才拉到的最大力度保存为该玩家的历史记录
                if (currentlyChargingPlayer == match.P1) last1 = currentMaxValue;
                else if (currentlyChargingPlayer == match.P2) last2 = currentMaxValue;

                // 重置状态，准备下一次击球
                currentlyChargingPlayer = null;
                currentMaxValue = 0f;
            }
        }

        // --- 应用到 UI ---
        bool p1Active = isGolfPhase && currentShooter == match.P1;
        bool p2Active = isGolfPhase && currentShooter == match.P2;

        Apply(player1, p1Active, sliderValue, last1);
        Apply(player2, p2Active, sliderValue, last2);
    }

    private void Apply(Bar bar, bool isActive, float currentValue, float lastValue)
    {
        if (bar == null || !bar.trackFill) return;

        // 1. 处理进度条填充
        // 如果当前玩家在动，显示当前值；如果不活跃，但有历史值，显示历史值；否则空
        bar.trackFill.fillAmount = isActive ? currentValue : (lastValue >= 0f ? lastValue : 0f);

        // 2. 处理黑蝴蝶结 (永远显示！)
        if (bar.bowCurrent)
        {
            // 强制开启，永远不隐藏
            bar.bowCurrent.gameObject.SetActive(true);

            // 如果不是当前玩家，或者没在蓄力，黑蝴蝶结停在底部；否则跟随当前力度
            float blackPos = isActive ? currentValue : 0f;
            bar.bowCurrent.anchorMin = new Vector2(0.5f, blackPos);
            bar.bowCurrent.anchorMax = new Vector2(0.5f, blackPos);
            bar.bowCurrent.anchoredPosition = Vector2.zero;
        }

        // 3. 处理白蝴蝶结 (有历史记录才显示)
        if (bar.bowLast)
        {
            bool hasHistory = lastValue >= 0f;
            bar.bowLast.gameObject.SetActive(hasHistory);

            if (hasHistory)
            {
                bar.bowLast.anchorMin = new Vector2(0.5f, lastValue);
                bar.bowLast.anchorMax = new Vector2(0.5f, lastValue);
                bar.bowLast.anchoredPosition = Vector2.zero;
            }
        }
    }
}