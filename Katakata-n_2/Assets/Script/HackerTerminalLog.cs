using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class HackerTerminalLog : MonoBehaviour
{
    [Header("表示")]
    [SerializeField] private Text targetText;

    [Header("設定")]
    [SerializeField] private int maxLines = 15;
    [SerializeField] private string prompt = "> ";

    private readonly Queue<string> lines = new Queue<string>();
    private readonly StringBuilder currentLine = new StringBuilder();

    private string targetLine = "";
    private int charIndex = 0;

    private static readonly string[] SourceLines =
    {
        "CipherBloom.RotateSalt(phase:3).Echo();",
        "EntropyGauge.Rebalance(seed:\"violet\");",
        "KeyLoom.Fold(\"K-∆\").Unfold(\"K-∇\");",
        "HashGarden.Sprout(\"ghost-seed\");",
        "VectorRune.Align(diagonal:true);",
        "NonceHarbor.Cast(\"n:??\").Anchor();",
        "ProofMint.Stamp(\"p≠np?\");",
        "var shard = new CipherShard(\"AZ-??\");",
        "PacketGlyph.Decode(\"□■□\").Then(Drift);",
        "VaultSong.Hum().MuteNoise();",
        "[trace] entropy=12.3% glyph=Δ",
        "[warn ] keyspace drift: soft-mirror",
        "[info ] compiling runes... done",
        "[ping ] node://phantom-lab shy",
        "MirageKey.Blur().ReturnVoid();"
    };

    void Awake()
    {
        ResetLog();
    }

    // ==============================
    // 外部から呼ぶ
    // ==============================
    public void AddCharacters(int count)
    {
        for (int i = 0; i < count; i++)
        {
            AddOneCharacter();
        }
    }

    public void ResetLog()
    {
        lines.Clear();
        currentLine.Clear();
        StartNewLine();
        Refresh();
    }

    // ==============================
    // 内部処理
    // ==============================
    private void AddOneCharacter()
    {
        if (charIndex >= targetLine.Length)
        {
            FinalizeLine();
            StartNewLine();
            Refresh();
            return;
        }

        currentLine.Append(targetLine[charIndex]);
        charIndex++;
        Refresh();
    }

    private void FinalizeLine()
    {
        lines.Enqueue(currentLine.ToString());
        currentLine.Clear();

        while (lines.Count > Mathf.Max(1, maxLines))
            lines.Dequeue();
    }

    private void StartNewLine()
    {
        targetLine = SourceLines[Random.Range(0, SourceLines.Length)];
        charIndex = 0;
    }

    private void Refresh()
    {
        if (targetText == null) return;

        var sb = new StringBuilder();

        foreach (var l in lines)
        {
            sb.Append(prompt);
            sb.AppendLine(l);
        }

        sb.Append(prompt);
        sb.Append(currentLine.ToString());

        targetText.text = sb.ToString();
    }
}