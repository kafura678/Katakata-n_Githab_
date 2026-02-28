using UnityEngine;
using UnityEngine.UI;

public class TerminalWindow : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private Text textTerminal;

    [Header("閉じる（任意）")]
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    public void SetText(string s)
    {
        if (textTerminal == null) return;
        textTerminal.text = s;
    }

    public void Append(string s)
    {
        if (textTerminal == null) return;
        textTerminal.text += s;
    }

    public void Clear()
    {
        if (textTerminal == null) return;
        textTerminal.text = "";
    }

    public void Close()
    {
        Destroy(gameObject);
    }
}