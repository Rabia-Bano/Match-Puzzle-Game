// ============================================================
//  OrientationLock.cs  —  NEW (no GameObject needed)
//
//  Game ko HAMESHA vertical (Portrait) rakhta hai, chahe player
//  apna phone ghuma de. RuntimeInitializeOnLoadMethod ki wajah se
//  ye app start hote hi, pehle scene se bhi pehle, khud chal jata
//  hai — kisi scene mein attach karne ki zaroorat NAHI.
//
//  Player Settings mein bhi Portrait set karein (guide dekhein) —
//  ye script ek extra safety hai, taake koi scene ya plugin
//  orientation badal bhi de to wapas Portrait ho jaye.
// ============================================================

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
