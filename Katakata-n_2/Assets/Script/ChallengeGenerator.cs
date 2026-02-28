using UnityEngine;

[System.Serializable]
public class Challenge
{
    [Header("最低入力数")]
    public int minInputCount = 5;
}

public static class ChallengeGenerator
{
    public static Challenge Create()
    {
        return new Challenge
        {
            minInputCount = Random.Range(3, 8)
        };
    }
}