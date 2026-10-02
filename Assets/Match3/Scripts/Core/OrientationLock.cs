using UnityEngine;

public static class OrientationLock
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LockPortrait()
    {
        Screen.autorotateToPortrait           = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft      = false;
        Screen.autorotateToLandscapeRight     = false;
        Screen.orientation                    = ScreenOrientation.Portrait;
    }
}
