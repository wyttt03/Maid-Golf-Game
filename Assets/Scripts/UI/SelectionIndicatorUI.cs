using UnityEngine;
using UnityEngine.UI;

public sealed class SelectionIndicatorUI : MonoBehaviour
{
    [System.Serializable]
    public class CharacterIndicator
    {
        public int characterIndex;
        public RectTransform indicator;
    }

    [Header("核心引用")]
    [SerializeField] MatchManager match;

    [Header("按钮引用 (按顺序：Attack, Defense, Reflect)")]
    [SerializeField] GameObject[] p1Buttons = new GameObject[3];
    [SerializeField] GameObject[] p2Buttons = new GameObject[3];

    [Header("角色标识池")]
    [SerializeField] CharacterIndicator[] characterIndicators;

    [Header("偏移微调（P1向右偏，P2向左偏）")]
    [SerializeField] Vector2 p1Offset = new Vector2(60f, 0f);  // P1 在按钮的右侧，填正数
    [SerializeField] Vector2 p2Offset = new Vector2(-60f, 0f); // P2 在按钮的左侧，填负数
    [SerializeField] float p2RotationZ = 0f; // 如果 P2 的图标需要左右镜像翻转，填 180

    void Update()
    {
        if (!match) return;

        // 涵盖选技能和揭晓阶段
        bool isSelecting = match.Phase == MatchPhase.ActionSelect ||
                           match.Phase == MatchPhase.Reveal;

        // 判断是否按下了按键
        bool p1HasLocked = (match.Phase == MatchPhase.ActionSelect) ? match.Action1Locked : true;
        bool p2HasLocked = (match.Phase == MatchPhase.ActionSelect) ? match.Action2Locked : true;

        UpdatePlayerIndicator(match.P1, p1Buttons, p1Offset, 0f, isSelecting && p1HasLocked);
        UpdatePlayerIndicator(match.P2, p2Buttons, p2Offset, p2RotationZ, isSelecting && p2HasLocked);
    }

    void UpdatePlayerIndicator(MatchManager.PlayerState player, GameObject[] buttons, Vector2 offset, float rotationZ, bool show)
    {
        int myCharID = -1;
        if (player.cls != null && match.classes != null)
        {
            for (int i = 0; i < match.classes.Length; i++)
            {
                if (match.classes[i] == player.cls) { myCharID = i; break; }
            }
        }

        int actionIndex = (int)player.action;

        RectTransform currentIndicator = null;
        foreach (var ci in characterIndicators)
        {
            if (ci.indicator == null) continue;
            if (ci.characterIndex == myCharID) currentIndicator = ci.indicator;
            else ci.indicator.gameObject.SetActive(false);
        }

        if (currentIndicator == null) return;

        if (show && actionIndex >= 0 && actionIndex < buttons.Length && buttons[actionIndex] != null)
        {
            currentIndicator.gameObject.SetActive(true);

            RectTransform targetBtnRect = buttons[actionIndex].GetComponent<RectTransform>();
            if (targetBtnRect != null)
            {
                // 获取按钮在屏幕上的真实位置
                Vector3 btnPos = targetBtnRect.position;

                // 因为 Indicator 的父级是 Canvas，所以这里加上偏移量
                // offset.x 就是纯粹的像素位移，不受按钮自身缩放影响
                currentIndicator.position = btnPos + new Vector3(offset.x * (Screen.width / 1920f), offset.y * (Screen.height / 1080f), 0);

                // 应用旋转
                currentIndicator.localRotation = Quaternion.Euler(0, 0, rotationZ);
            }
        }
        else
        {
            currentIndicator.gameObject.SetActive(false);
        }
    }
}