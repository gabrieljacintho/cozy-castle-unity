using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hanzzz.MeshSlicerFree
{

public class MeshSlicerTest : MonoBehaviour
{

    public Transform slicePlane;
    public GameObject sliceTarget;
    public Material intersectionMaterial;
    public float splitDistance;

    private MeshSlicer meshSlicer = new MeshSlicer();
    private SkinnedMeshSlicer skinnedMeshSlicer = new SkinnedMeshSlicer();
    private (GameObject, GameObject) result;

    public Text logText;
    private Stopwatch timer;
    private int _sliceCount;

#if UNITY_EDITOR
    [SerializeField] private string _meshSavePath = "Assets/Art/Models/GeneratedMeshes";
#endif


    private (Vector3,Vector3,Vector3) Get3PointsOnPlane(Plane p)
    {
        Vector3 xAxis;
        if(0f != p.normal.x)
        {
            xAxis = new Vector3(-p.normal.y/p.normal.x, 1f, 0f);
        }
        else if(0f != p.normal.y)
        {
            xAxis = new Vector3(0f, -p.normal.z/p.normal.y, 1f);
        }
        else
        {
            xAxis = new Vector3(1f, 0f, -p.normal.x/p.normal.z);
        }
        Vector3 yAxis = Vector3.Cross(p.normal, xAxis);
        return (-p.distance*p.normal, -p.distance*p.normal+xAxis, -p.distance*p.normal+yAxis);
    }

    private void PreSliceOperation()
    {
        result = (null, null);
        timer = Stopwatch.StartNew();
    }
    private void PostSliceOperation()
    {
        timer.Stop();
        string log = $"Slice Time: {timer.ElapsedMilliseconds}ms.";
        //logText.text = log;
        UnityEngine.Debug.Log(log);
        if(null == result.Item1)
        {
            UnityEngine.Debug.Log("Slice plane does not intersect slice target.");
            return;
        }

        _sliceCount++;
        result.Item1.name = $"{sliceTarget.name}_Slice{_sliceCount}_A";
        result.Item2.name = $"{sliceTarget.name}_Slice{_sliceCount}_B";

        result.Item1.transform.position += splitDistance * slicePlane.up;
        result.Item2.transform.position -= splitDistance * slicePlane.up;
        sliceTarget.SetActive(false);

#if UNITY_EDITOR
        SaveMeshAsset(result.Item1);
        SaveMeshAsset(result.Item2);
#endif
    }

#if UNITY_EDITOR
    private void SaveMeshAsset(GameObject go)
    {
        if (go == null)
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(_meshSavePath))
        {
            string[] folders = _meshSavePath.Split('/');
            string currentPath = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = $"{currentPath}/{folders[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = nextPath;
            }
        }

        if (go.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
        {
            Mesh meshToSave = Instantiate(meshFilter.sharedMesh);
            string meshPath = $"{_meshSavePath}/{go.name}.asset";
            meshPath = AssetDatabase.GenerateUniqueAssetPath(meshPath);
            AssetDatabase.CreateAsset(meshToSave, meshPath);
            meshFilter.sharedMesh = meshToSave;
            UnityEngine.Debug.Log($"Mesh saved: {meshPath}");
        }

        AssetDatabase.SaveAssets();
    }
#endif

    [Button]
    public void Slice()
    {
        PreSliceOperation();
        result = meshSlicer.Slice(sliceTarget, Get3PointsOnPlane(new Plane(slicePlane.up, slicePlane.position)), intersectionMaterial);
        PostSliceOperation();
    }
    public async void SliceAsync()
    {
        PreSliceOperation();
        result = await meshSlicer.SliceAsync(sliceTarget,Get3PointsOnPlane(new Plane(slicePlane.up, slicePlane.position)),intersectionMaterial);
        PostSliceOperation();
    }
    [Button]
    public void SliceSkinned()
    {
        PreSliceOperation();
        result = skinnedMeshSlicer.Slice(sliceTarget, 0, 1, Get3PointsOnPlane(new Plane(slicePlane.up, slicePlane.position)), intersectionMaterial);
        PostSliceOperation();
    }
    public async void SliceSkinnedAsync()
    {
        PreSliceOperation();
        result = await skinnedMeshSlicer.SliceAsync(sliceTarget, 0, 1, Get3PointsOnPlane(new Plane(slicePlane.up, slicePlane.position)), intersectionMaterial);
        PostSliceOperation();
    }
    
    [Button]
    public void Clear()
    {
        if(null != result.Item1)
        {
            DestroyImmediate(result.Item1);
            DestroyImmediate(result.Item2);
            result = (null, null);
        }
        meshSlicer = new MeshSlicer();
        skinnedMeshSlicer = new SkinnedMeshSlicer();
        sliceTarget.SetActive(true);
    }
}

}
