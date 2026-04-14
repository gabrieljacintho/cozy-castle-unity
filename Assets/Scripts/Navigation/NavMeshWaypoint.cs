using UnityEngine;

namespace GabrielBertasso.Navigation
{
    public class NavMeshWaypoint : MonoBehaviour
    {
        [SerializeField, Tooltip("Animator bool parameter name to set true while the agent is stopped at this point.")]
        private string _animatorBoolParameterName;

        public string AnimatorBoolParameterName
        {
            get
            {
                return _animatorBoolParameterName;
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_animatorBoolParameterName))
            {
                return;
            }

            _animatorBoolParameterName = _animatorBoolParameterName.Trim();
        }
    }
}
