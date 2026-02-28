using System.Collections.Generic;
using UnityEngine;

public static class Judge
{
    /// <summary>
    /// 成功条件：入力数が最低入力数以上
    /// </summary>
    public static bool IsSuccess(Challenge challenge, InputBuffer input)
    {
        if (challenge == null || input == null) return false;
        return input.LetterCount >= challenge.minInputCount;
    }

    /// <summary>
    /// 複数お題（同時打ち）用：全て満たせば成功
    /// </summary>
    public static bool IsSuccessAll(IEnumerable<Challenge> challenges, InputBuffer input)
    {
        if (challenges == null || input == null) return false;

        int n = input.LetterCount;
        foreach (var c in challenges)
        {
            if (c == null) return false;
            if (n < c.minInputCount) return false;
        }
        return true;
    }

    /// <summary>
    /// 選択中すべてを満たすために必要な最低入力数（= max(minInputCount)）
    /// </summary>
    public static int RequiredMinForAll(IEnumerable<Challenge> challenges)
    {
        if (challenges == null) return 0;

        int req = 0;
        foreach (var c in challenges)
        {
            if (c == null) continue;
            req = Mathf.Max(req, c.minInputCount);
        }
        return req;
    }

    /// <summary>
    /// 入力数倍率：最低入力数を超えた分だけ伸びる（上限あり）
    /// </summary>
    public static float CalcInputMultiplier(int inputCount, int requiredMin, float bonusPerExtraInput, float maxMultiplier)
    {
        int extra = Mathf.Max(0, inputCount - Mathf.Max(0, requiredMin));
        float mul = 1f + extra * Mathf.Max(0f, bonusPerExtraInput);
        return Mathf.Clamp(mul, 1f, Mathf.Max(1f, maxMultiplier));
    }
}