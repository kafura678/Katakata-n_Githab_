using UnityEngine;
using System.Collections.Generic;

public class enterInput : MonoBehaviour
{
    private List<float> enterValues = new List<float>();

    void Update()
    {
        float enterValue = Input.GetAxis("enter");
        if (enterValue != 0 && enterValue < 1f)
        {
            enterValues.Add(1f / enterValue);
        }
        else if (enterValues.Count > 0)
        {
            float acceleration = 0f;
            for (int i = 0; i < enterValues.Count - 1; i++)
            {
                acceleration += Mathf.Abs(enterValues[i + 1] - enterValues[i]);
            }
            acceleration /= enterValues.Count - 1;
            Debug.Log("Enter key released. Acceleration: " + acceleration);

            enterValues.Clear();
        }
    }
}
