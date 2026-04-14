using UnityEngine;

namespace GabrielBertasso.GameManagement
{
    public class GameManager : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Initialize()
        {
            Application.targetFrameRate = 60;
        }
    }
}
