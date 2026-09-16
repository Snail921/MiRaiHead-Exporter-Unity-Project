#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class MaterialReplacer : MonoBehaviour
{
    [SerializeField]
    GameObject targetModel;
    [SerializeField]
    List<Renderer> renderers = new List<Renderer>();
    [SerializeField]
    List<Material> materialList = new List<Material>();

    // Start is called before the first frame update
    void Start()
    {
        //ReplaceMaterials();
    }

    public void ReplaceMaterials()
    {
        List<GameObject> objs = GetAllChildren.GetAll(targetModel);
        objs.Add(targetModel);
        foreach(GameObject go in objs)
        {
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Debug.Log($"<color=red>{go.name}</color>");
                Material[] oldMaterials = rend.sharedMaterials;
                List<Material> newMaterials = new List<Material>();
                for (int i = 0; i < oldMaterials.Length; i++)
                {
                    newMaterials.Add(oldMaterials[i]);
                    foreach(Material mat in materialList)
                    {
                        if(oldMaterials[i].name.Contains(mat.name))
                        {
                            newMaterials[i] = mat;
                            Debug.Log("Found");
                        }
                    }
                }

                rend.materials = newMaterials.ToArray();
            }
        }
    }
}

[CustomEditor(typeof(MaterialReplacer))]
public class MaterialReplacerEditor : Editor
{
    MaterialReplacer obj;

    public void OnEnable()
    {
        obj = (MaterialReplacer)target;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Replace"))
        {
            obj.ReplaceMaterials();
        }
    }
}

#endif