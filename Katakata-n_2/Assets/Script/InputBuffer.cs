using System.Collections.Generic;
using UnityEngine;

public class InputBuffer : MonoBehaviour
{
    public bool AcceptInput = false;

    // 入力履歴（英字のみ、大文字に正規化）
    public readonly List<char> TypedChars = new List<char>(256);

    public int LetterCount => TypedChars.Count;

    public void ClearAll()
    {
        TypedChars.Clear();
    }

    public void Tick()
    {
        if (!AcceptInput) return;

        string s = Input.inputString;
        if (string.IsNullOrEmpty(s)) return;

        foreach (char ch in s)
        {
            // EnterはGameManager側で送信処理するので無視
            if (ch == '\n' || ch == '\r') continue;

            // Backspace：末尾を1文字削除
            if (ch == '\b')
            {
                if (TypedChars.Count > 0)
                    TypedChars.RemoveAt(TypedChars.Count - 1);
                continue;
            }

            // 英字のみ採用（それ以外は完全無視）
            if (IsLetterAscii(ch))
                TypedChars.Add(char.ToUpperInvariant(ch));
        }
    }

    public bool ContainsAllRequired(List<char> requiredKeys)
    {
        if (requiredKeys == null || requiredKeys.Count == 0) return true;

        var set = new HashSet<char>(TypedChars);
        foreach (var rk in requiredKeys)
        {
            char key = char.ToUpperInvariant(rk);
            if (!set.Contains(key)) return false;
        }
        return true;
    }

    private bool IsLetterAscii(char c)
    {
        return (c >= 'A' && c <= 'Z')
            || (c >= 'a' && c <= 'z');
    }
}