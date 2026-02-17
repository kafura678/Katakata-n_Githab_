public static class Judge
{
    public static bool IsSuccess(Challenge challenge, InputBuffer input)
    {
        int n = input.LetterCount;

        if (n < challenge.minCount || n > challenge.maxCount) return false;
        if (!input.ContainsAllRequired(challenge.requiredKeys)) return false;

        return true;
    }
}