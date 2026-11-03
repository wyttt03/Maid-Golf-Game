using UnityEngine;
using UnityEngine.InputSystem;

public class LayoutSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class Layout
    {
        public string name;
        public GameObject root;
        public Transform teeSpawn;
    }

    public ShotController shot;
    public Layout[] layouts;
    public int startIndex = 0;

    public int CurrentIndex { get; private set; } = -1;

    void Start()
    {
        SetLayout(startIndex);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Debug keys for testing layouts.
        if (kb.digit1Key.wasPressedThisFrame) SetLayout(0);
        if (kb.digit2Key.wasPressedThisFrame) SetLayout(1);
        if (kb.digit3Key.wasPressedThisFrame) SetLayout(2);
    }

    public void SetLayout(int index)
    {
        if (index < 0 || index >= layouts.Length) return;

        // Disable all first so re-enabling resets animators (cup goes back down).
        foreach (var l in layouts)
            if (l.root) l.root.SetActive(false);

        layouts[index].root.SetActive(true);
        CurrentIndex = index;
        shot.StartTurnAt(layouts[index].teeSpawn.position);
    }
}