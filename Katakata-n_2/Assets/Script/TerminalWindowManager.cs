using UnityEngine;

public class TerminalWindowManager : MonoBehaviour
{
    [Header("生成")]
    [SerializeField] private GameObject windowPrefab;   // TerminalWindow prefab（中にHackerTerminalLogを入れる）
    [SerializeField] private Transform windowParent;    // Canvas配下推奨（未設定なら自分のTransform）

    private GameObject currentWindow;
    private HackerTerminalLog currentLog;

    public bool HasWindow => currentWindow != null;

    public void Open()
    {
        if (windowPrefab == null) return;

        if (currentWindow != null)
        {
            // 既に開いているなら前面へ
            currentWindow.transform.SetAsLastSibling();
            return;
        }

        Transform parent = (windowParent != null) ? windowParent : transform;
        currentWindow = Instantiate(windowPrefab, parent);
        currentWindow.transform.SetAsLastSibling();

        // ウィンドウ内の HackerTerminalLog を取得（必須）
        currentLog = currentWindow.GetComponentInChildren<HackerTerminalLog>(true);
        if (currentLog != null)
            currentLog.ResetLog();
    }

    public void Close()
    {
        if (currentWindow == null) return;
        Destroy(currentWindow);
        currentWindow = null;
        currentLog = null;
    }

    public void ResetLog()
    {
        if (currentLog == null) return;
        currentLog.ResetLog();
    }

    public void AddCharacters(int count)
    {
        if (currentLog == null) return;
        currentLog.AddCharacters(count);
    }
}