using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Challenge
{
    public int minCount;
    public int maxCount;
    public List<char> requiredKeys;
}

public static class ChallengeGenerator
{
    private static readonly string LetterPool = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// お題を生成
    /// - 下限：10〜20（両端含む）
    /// - 上限：下限 + (4〜6)
    /// - 必要キー：0〜3個（英字のみ）
    /// </summary>
    public static Challenge Create()
    {
        int min = UnityEngine.Random.Range(10, 21);     // 10〜20
        int max = min + UnityEngine.Random.Range(4, 7); // +4〜+6

        return new Challenge
        {
            minCount = min,
            maxCount = max,
            requiredKeys = PickUniqueChars(LetterPool, UnityEngine.Random.Range(0, 4)) // 0〜3
        };
    }

    private static List<char> PickUniqueChars(string pool, int count)
    {
        var result = new List<char>(count);
        if (count <= 0) return result;

        var used = new HashSet<int>();
        while (result.Count < count)
        {
            int idx = UnityEngine.Random.Range(0, pool.Length);
            if (used.Add(idx))
                result.Add(pool[idx]);
        }
        return result;
    }
}