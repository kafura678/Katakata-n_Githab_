public interface IInterference
{
    string DisplayName { get; }

    // 実行中か
    bool IsRunning { get; }

    // ★この妨害中は他操作を無効化するか
    bool BlocksInput { get; }

    void Begin();
    void End();
    void Tick();
}