using UnityEngine;

namespace ShinySSRR {

[ExecuteAlways]
    public class ShinyTansparentSupport : MonoBehaviour {

        [HideInInspector]
        public Renderer theRenderer;

        public void OnEnable () {
            if (theRenderer == null) {
                theRenderer = GetComponent<Renderer>();
            }
            ShinySSRR.RegisterTransparentSupport(this);
        }

        public void OnDisable () {
            ShinySSRR.UnregisterTransparentSupport(this);
        }

    }

}