using UnityEngine;

public class ErrorWindowUI : MonoBehaviour
{
    // 必要ならここにText参照を持たせて、毎回違う文面を出せる

    public void Close()
    {
        Destroy(gameObject);
    }
}