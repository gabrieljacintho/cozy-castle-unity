using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GabrielBertasso.Visual
{
    public class LightProximityManager : MonoBehaviour
    {
        private static readonly List<LightProximityManager> s_allLights = new List<LightProximityManager>();
        private static UnityEngine.Camera s_mainCamera;
        private static readonly int MaxActiveLights = 8;
        private static float s_nextUpdateTime;
        private static readonly float UpdateInterval = 0.1f;

        private Light _light;
        private float _viewScore;

        private void Awake()
        {
            _light = GetComponent<Light>();
            
            if (_light == null)
            {
                Debug.LogError($"LightProximityManager on {gameObject.name} requires a Light component.", this);
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            if (_light != null && !s_allLights.Contains(this))
            {
                s_allLights.Add(this);
            }
        }

        private void OnDisable()
        {
            s_allLights.Remove(this);
        }

        private void Update()
        {
            if (Time.time < s_nextUpdateTime)
            {
                return;
            }

            if (s_mainCamera == null)
            {
                s_mainCamera = UnityEngine.Camera.main;
                if (s_mainCamera == null)
                {
                    return;
                }
            }

            s_nextUpdateTime = Time.time + UpdateInterval;
            UpdateLightActivation();
        }

        private void UpdateLightActivation()
        {
            Vector3 cameraPosition = s_mainCamera.transform.position;
            Vector3 cameraForward = s_mainCamera.transform.forward;

            foreach (var lightManager in s_allLights)
            {
                if (lightManager != null && lightManager._light != null && lightManager.gameObject.activeInHierarchy)
                {
                    Vector3 directionToLight = (lightManager.transform.position - cameraPosition).normalized;
                    float dotProduct = Vector3.Dot(cameraForward, directionToLight);
                    float distance = Vector3.Distance(cameraPosition, lightManager.transform.position);
                    
                    lightManager._viewScore = dotProduct / (1f + distance * 0.1f);
                }
            }

            var sortedLights = s_allLights
                .Where(l => l != null && l._light != null && l.gameObject.activeInHierarchy)
                .OrderByDescending(l => l._viewScore)
                .ToList();

            for (int i = 0; i < sortedLights.Count; i++)
            {
                sortedLights[i]._light.enabled = i < MaxActiveLights;
            }
        }
    }
}
