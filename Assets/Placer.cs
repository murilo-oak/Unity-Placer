using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor;

using Matrix4x4 = UnityEngine.Matrix4x4;
using Quaternion = UnityEngine.Quaternion;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

[CanEditMultipleObjects]
public class Placer : EditorWindow
{
    [MenuItem("Tools/Placer")]
    public static void OpenWindow() => GetWindow<Placer>();
    
    public float radius = 1f;
    
    [Min(0)]
    public int spawnCount = 10;
    public GameObject myPrefab;
    private List<GameObject> selectedPrefabs = new List<GameObject>();

    SerializedObject so;
    SerializedProperty propRadius;
    SerializedProperty propSpawnCount;
    SerializedProperty propseletedPrefabs;

    public struct RandomData
    {
        public Vector2 pointInDisc;
        public float randAngleDeg;
        public GameObject randomPrefab;

        public void SetRandValues()
        {
            randAngleDeg = Random.value * 360f;
            randomPrefab = null;
        }
        
    }

    public struct HitData
    {
        public Pose hitPose;
        public GameObject prefab;
    }
    
    public List<HitData> hitPoints = new List<HitData>();
    private RandomData[] randomPoints;
    private GameObject[] prefabs;

    private void OnEnable()
    {
        so = new SerializedObject(this);
        propRadius = so.FindProperty("radius");
        propSpawnCount = so.FindProperty("spawnCount");

        radius = EditorPrefs.GetFloat("TOOLS_PLACER_RADIUS", 1f);
        spawnCount = EditorPrefs.GetInt("TOOLS_PLACER_SPAWN_COUNT", 10);
        
        GenerateRandomPoints();
        
        
        SceneView.duringSceneGui += DuringSceneGUI;
        Undo.undoRedoPerformed += UndoRedoPerformed;

        
        //load prefabs placer
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] {"Assets/Prefabs"});
        IEnumerable<string> paths = guids.Select(AssetDatabase.GUIDToAssetPath);

        prefabs = paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
    }

    private void UndoRedoPerformed()
    {
        GenerateRandomPoints();
        Repaint();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        Undo.undoRedoPerformed -= UndoRedoPerformed;
        EditorPrefs.SetFloat("TOOLS_PLACER_RADIUS", radius);
        EditorPrefs.SetInt("TOOLS_PLACER_SPAWN_COUNT", spawnCount);
    }

    private void OnGUI()
    {
        so.Update();
        EditorGUILayout.PropertyField(propRadius);
        EditorGUILayout.PropertyField(propSpawnCount);

        if (so.ApplyModifiedProperties()) {
            GenerateRandomPoints();
            Repaint();
        }
    }

    void DuringSceneGUI(SceneView sceneView)
    {
        DrawPrefabsIconsOnViewScreen();
        
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

        if (Event.current.type == EventType.MouseMove)
        {
            SceneView.currentDrawingSceneView.Repaint();
        }

        bool isHoldingAlt = (Event.current.modifiers & EventModifiers.Alt) != 0;
        
        if (Event.current.type == EventType.ScrollWheel && !isHoldingAlt)    
        {
            float signScroll = Mathf.Sign(Event.current.delta.y);
            
            so.Update();
            propRadius.floatValue *= 1f + signScroll * 0.05f;
            so.ApplyModifiedProperties();
            
            Repaint();
            Event.current.Use(); //consumes event
        }

        if (Physics.Raycast(ray, out RaycastHit hit))
        {

            Transform camTf = sceneView.camera.transform;

            Vector3 hitNormal = hit.normal;
            Vector3 hitTangent = Vector3.Cross(hitNormal, camTf.up).normalized;
            Vector3 hitBiTangent = Vector3.Cross(hitNormal, hitTangent);
            Vector3 hitPos = hit.point;

            Handles.color = Color.red;
            Handles.DrawLine(hitPos, hitPos + hitNormal);
            Handles.DrawWireDisc(hitPos, hitNormal, radius);

            foreach (RandomData p in randomPoints)
            {
                Handles.color = Color.black;
                
                
                Vector3 worldPos = hitPos + (p.pointInDisc.x * hitTangent + p.pointInDisc.y * hitBiTangent) * radius + hit.normal * 2.0f;
                
                if (Physics.Raycast(worldPos, -hitNormal, out RaycastHit hit2, 6.0f))
                {
                    
                    float height = CalculateObjectHeight(p.randomPrefab);

                    if (!Physics.Raycast(hit2.point, hit2.normal, height))
                    {
                        Quaternion rot = Quaternion.LookRotation(Vector3.Cross(hit2.normal, camTf.up), hit2.normal);
                        Quaternion randomRot = Quaternion.Euler(0,p.randAngleDeg,0f);
                        rot *= randomRot;

                        
                        HitData hitPoint;
                        hitPoint.hitPose = new Pose(hit2.point, rot);
                        hitPoint.prefab = p.randomPrefab;
                        
                        hitPoints.Add(hitPoint);
                        
                        DrawPreviewPrefabMeshes(hitPoint.hitPose.position, hitPoint.hitPose.rotation, p.randomPrefab, hit2.normal);
                    }
                    else
                    {
                        Handles.color = Color.magenta;
                        DrawPreviewPrefabMeshesRed(hit2.point, Quaternion.identity, p.randomPrefab, hit2.normal);
                        Handles.DrawLine(hit2.point, hit2.point + hit2.normal * height);
                    }
                }
            }
            
            bool spaceIsPressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space;
            if (spaceIsPressed )
            {
                TrySpawnPoints();
                GenerateRandomPoints();
            }
            hitPoints.Clear();
        }
    }

    void DrawPrefabsIconsOnViewScreen()
    {
        Handles.BeginGUI();
        
        //defines size of frame
        Rect rectButton = new Rect(8, 16, 64, 64);
        Rect rectToggle = new Rect(8, 16, 60, 60);

        foreach (GameObject prefab in prefabs)
        {
            Texture2D icon = AssetPreview.GetAssetPreview(prefab);

            if (GUI.Button(rectButton, GUIContent.none))
            {
                if (!selectedPrefabs.Contains(prefab))
                {
                    selectedPrefabs.Add(prefab);
                    Repaint();
                }
                else
                {
                    selectedPrefabs.Remove(prefab);
                    Repaint();
                }
                GenerateRandomPoints();
            }

            GUI.Toggle(rectToggle, selectedPrefabs.Contains(prefab), icon);
            rectButton.y += rectButton.height + 3;
            rectToggle.y += rectButton.height + 3;
        }
        
        Handles.EndGUI();
    }
    void DrawPreviewPrefabMeshes(Vector3 position, Quaternion rotation, GameObject prefab, Vector3 normal)
    {
        if (prefab != null)
        {
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>();
            ;
            Matrix4x4 parentMatrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            
            foreach (MeshFilter filter in filters)
            {
                Matrix4x4 localMatrix = filter.transform.localToWorldMatrix;
                Matrix4x4 aux = parentMatrix * localMatrix;
                
                Mesh mesh = filter.sharedMesh;
                Material mat = filter.GetComponent<MeshRenderer>().sharedMaterial;
                
                SceneView.RepaintAll();

                mat.SetPass(0);
                Graphics.DrawMesh(mesh,aux, mat, 0, Camera.current);

            }
        }
    }
    
    void DrawPreviewPrefabMeshesRed(Vector3 position, Quaternion rotation, GameObject prefab, Vector3 normal)
    {
        if (prefab != null)
        {
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>();
            ;
            Matrix4x4 parentMatrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            
            foreach (MeshFilter filter in filters)
            {
                Matrix4x4 localMatrix = filter.transform.localToWorldMatrix;
                Matrix4x4 aux = parentMatrix * localMatrix;
                
                Mesh mesh = filter.sharedMesh;
                Material mat = new Material(Shader.Find("Standard"));

                mat.color = Color.red;
                
                SceneView.RepaintAll();

                mat.SetPass(0);
                Graphics.DrawMesh(mesh,aux, mat, 0, Camera.current);

            }
        }
    }
    
    
    float CalculateObjectHeight(GameObject prefab)
    {
        if (prefab != null)
        {
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>();

            float maxHeight = 0f;

            //find max height
            foreach (MeshFilter filter in filters)
            {
                Transform filterTf = filter.transform;
                Mesh mesh = filter.sharedMesh;
                
                Matrix4x4 localMatrix = filterTf.localToWorldMatrix;

                float height = filterTf.position.y + (mesh.bounds.max.y * localMatrix.lossyScale.y);
                
                 if (height > maxHeight)
                 {
                     maxHeight = height;
                 }
            }
            return maxHeight;
        }

        return 0;
    }
    
    void TrySpawnPoints()
    {
        foreach (HitData hitPoint in hitPoints)
        {
            if (hitPoint.prefab != null)
            {
                GameObject newObject = (GameObject) PrefabUtility.InstantiatePrefab(hitPoint.prefab);
                Vector3 position = hitPoint.hitPose.position;
                Quaternion rotation = hitPoint.hitPose.rotation;

                //Set rotation and then position (order matters)
                newObject.transform.rotation = rotation;
                newObject.transform.position = position + hitPoint.prefab.transform.position.y * newObject.transform.up;

                //Control-Z instantiated objects
                Undo.RegisterCreatedObjectUndo ( newObject, "Created prefab by placer Tool");
            }
        }
    }

    void GenerateRandomPoints()
    {
        randomPoints = new RandomData[spawnCount];
        int selectPrebabsLength = selectedPrefabs.Count;
        float minDistance = (Mathf.Sqrt(Mathf.PI/spawnCount)/ 2)* 2f * 0.30f;
        int maxIter = 10;
        
        for (int i = 0; i < spawnCount; i++)
        {
            randomPoints[i].SetRandValues();
            randomPoints[i].pointInDisc = Random.insideUnitCircle;
            
            for(int j = i - 1; j >= 0 ; j--)
            {
                int count = 0;
                
                float distance = Vector2.Distance(randomPoints[i].pointInDisc, randomPoints[j].pointInDisc);

                while ( distance < minDistance && count<maxIter)
                {
                    randomPoints[i].pointInDisc = Random.insideUnitCircle;
                    count++;
                }
            }
            
            if (selectPrebabsLength > 0)
            {
                randomPoints[i].randomPrefab = selectedPrefabs[Random.Range(0, selectPrebabsLength)];
            }
            
        }
    }
}

