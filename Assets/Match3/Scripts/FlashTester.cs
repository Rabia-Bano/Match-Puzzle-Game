using UnityEngine;

public class FlashTester : MonoBehaviour
{
    void Start()
    {
        Debug.Log("FlashTester Start() called!");   // ye sabse pehle print honi chahiye

        if (JuiceManager.Instance != null)
        {
            JuiceManager.Instance.FlashScreen(Color.red, 0.5f);
            Debug.Log("Flash triggered!");
        }
        else
        {
            Debug.LogError("JuiceManager.Instance is NULL!");
        }
    }
}