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
    
    private const float minRadius = 0.1f;
    [Min(minRadius)]
    public float radius = 1f;
    
    [Min(0)]
    public int spawnCount = 10;
    
    public GameObject myPrefab;
    private List<GameObject> selectedPrefabs = new List<GameObject>();

    SerializedObject so;
    SerializedProperty propRadius;
    SerializedProperty propSpawnCount;
    SerializedProperty propseletedPrefabs;

    private GameObject[] prefabs;
    public struct PrefabData
    {
        public GameObject prefab;
        public float height;
        public Vector2 pointInDisc;
        public float randAngleDeg;

        public void SetRandValues()
        {
            height = 0.0f;
            randAngleDeg = Random.value * 360f;
            prefab = null;
        }
        
    }
    public struct SpawnData
    {
        public Pose hitPose;
        public GameObject prefab;
    }
    
    public List<SpawnData> pointsToSpawnPrefabs = new List<SpawnData>();
    private PrefabData[] prefabsInsideCircle;
   

    private void OnEnable()
    {
        //Create serialized object
        so = new SerializedObject(this);
        propRadius = so.FindProperty("radius");
        propSpawnCount = so.FindProperty("spawnCount");
        
        // Retrieve the saved radius value from EditorPrefs. If no saved value exists,
        // use the default value of 1.0f.
        radius = EditorPrefs.GetFloat("TOOLS_PLACER_RADIUS", 1f);
        
        // Retrieve the saved spawn count value from EditorPrefs. If no saved value exists,
        // use the default value of 10.
        spawnCount = EditorPrefs.GetInt("TOOLS_PLACER_SPAWN_COUNT", 10);
        
        GenerateSpawnPoints();

        SceneView.duringSceneGui += DuringSceneGUI;
        Undo.undoRedoPerformed += UndoRedoPerformed;
        
        // Load prefabs placer
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] {"Assets/Prefabs"});
        IEnumerable<string> paths = guids.Select(AssetDatabase.GUIDToAssetPath);

        prefabs = paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
    }

    private void UndoRedoPerformed()
    {
        GenerateSpawnPoints();
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
            GenerateSpawnPoints();
            Repaint();
        }
    }

    void DuringSceneGUI(SceneView sceneView)
    {
        DrawPrefabsIconsOnViewScreen();
        HandleInput();
        
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        
        Ray rayFromMouse = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        
        // If ray from mouse hits a surface
        if (Physics.Raycast(rayFromMouse, out RaycastHit hit))
        {
            Transform camTf = sceneView.camera.transform;
            
            // Calculate Tangent Space of hit point from mouse
            Vector3 hitNormal = hit.normal;
            Vector3 hitTangent = Vector3.Cross(hitNormal, camTf.up).normalized;
            Vector3 hitBiTangent = Vector3.Cross(hitNormal, hitTangent);
            Vector3 hitPos = hit.point;
            
            // Draws circle that where the prefabs will be spawned 
            Handles.color = Color.red;
            Handles.DrawLine(hitPos, hitPos + hitNormal);
            Handles.DrawWireDisc(hitPos, hitNormal, radius);

            foreach (PrefabData p in prefabsInsideCircle)
            {
                // Calculate the position in world space based on the tangent and bitangent vectors,
                // along with the collision point's normal, taking into account a scale factor
                Vector3 worldPos = hitPos + (p.pointInDisc.x * hitTangent + p.pointInDisc.y * hitBiTangent) * radius + hit.normal * 2.0f;
                
                // If point converted from unit circle to world hits a surface
                if (Physics.Raycast(worldPos, -hitNormal, out RaycastHit hit2, 6.0f))
                {   
                    // Check if there is something above
                    if (!Physics.Raycast(hit2.point, hit2.normal, p.height))
                    {
                        // Calculate the rotation for spawning the prefab by combining the normal-based rotation and a
                        // random rotation.
                        Quaternion rot = Quaternion.LookRotation(Vector3.Cross(hit2.normal, camTf.up), hit2.normal);
                        Quaternion randomRot = Quaternion.Euler(0,p.randAngleDeg,0f);
                        rot *= randomRot;
                        
                        // Create a SpawnData object with the calculated hit pose and the prefab to spawn.
                        SpawnData spawnPoint;
                        spawnPoint.hitPose = new Pose(hit2.point, rot);
                        spawnPoint.prefab = p.prefab;
                        
                        // Add the spawn point to the list of points to spawn prefabs
                        pointsToSpawnPrefabs.Add(spawnPoint);
                        
                        DrawPreviewPrefabMeshes(spawnPoint.hitPose.position, spawnPoint.hitPose.rotation, p.prefab);
                    }
                    // If there is something above, paint prefab with Red material
                    else
                    {
                        DrawPreviewPrefabMeshesRed(hit2.point, Quaternion.identity, p.prefab);
                    }
                }
            }
            
            bool spaceIsPressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space;
            
            if (spaceIsPressed)
            {
                TrySpawnPoints();
                GenerateSpawnPoints();
            }
            
            pointsToSpawnPrefabs.Clear();
        }
    }
    
    void  HandleInput()
    {
        // If mouse moves
        if (Event.current.type == EventType.MouseMove)
        {
            //repaint scene view
            SceneView.currentDrawingSceneView.Repaint();
        }

        bool isHoldingAlt = (Event.current.modifiers & EventModifiers.Alt) != 0;
        
        // If placer is active, and alt is not pressed, and there is a scroll event
        // change radius of placer
        if (Event.current.type == EventType.ScrollWheel && !isHoldingAlt)    
        {
            float signScroll = Mathf.Sign(Event.current.delta.y);
            
            so.Update();
            propRadius.floatValue *= 1f + signScroll * 0.05f;
            
            //clamp with minimum radius possible
            propRadius.floatValue = Mathf.Clamp(propRadius.floatValue, minRadius, float.MaxValue);
            
            so.ApplyModifiedProperties();
            
            Repaint();
            Event.current.Use(); //consumes event
        }
    }
    void  DrawPrefabsIconsOnViewScreen()
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
                GenerateSpawnPoints();
            }

            GUI.Toggle(rectToggle, selectedPrefabs.Contains(prefab), icon);
            rectButton.y += rectButton.height + 3;
            rectToggle.y += rectButton.height + 3;
        }
        
        Handles.EndGUI();
    }
    void  DrawPreviewPrefabMeshes(Vector3 position, Quaternion rotation, GameObject prefab)
    {
        if (prefab != null)
        {
            // Get all MeshFilters in the prefab and its children
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>();
            
            // Create the parent transformation matrix using the provided position and rotation
            Matrix4x4 parentMatrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            
            foreach (MeshFilter filter in filters)
            {
                // Get the local-to-world transformation matrix of the filter's transform
                Matrix4x4 localMatrix = filter.transform.localToWorldMatrix;
                
                // Combine the parent matrix with the local matrix to get the final transformation matrix
                Matrix4x4 combinedMatrix = parentMatrix * localMatrix;
                
                // Get the mesh and material of the current MeshFilter
                Mesh mesh = filter.sharedMesh;
                Material mat = filter.GetComponent<MeshRenderer>().sharedMaterial;
                
                // Repaint the SceneView to ensure proper preview rendering
                SceneView.RepaintAll();
                
                // Set the material pass and draw the mesh using the combined matrix and current camera
                mat.SetPass(0);
                Graphics.DrawMesh(mesh, combinedMatrix, mat, 0, Camera.current);

            }
        }
    }
    void  DrawPreviewPrefabMeshesRed(Vector3 position, Quaternion rotation, GameObject prefab)
    {
        if (prefab != null)
        {
            
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>();

            Matrix4x4 parentMatrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            
            foreach (MeshFilter filter in filters)
            {
                // Get the local-to-world transformation matrix of the filter's transform
                Matrix4x4 localMatrix = filter.transform.localToWorldMatrix;
                
                // Combine the parent matrix with the local matrix to get the final transformation matrix
                Matrix4x4 combinedMatrix = parentMatrix * localMatrix;
                
                // Create Red material to indicate that the prefab won't be spawn
                Mesh mesh = filter.sharedMesh;
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = Color.red;
                
                // Repaint the SceneView to ensure proper preview rendering
                SceneView.RepaintAll();
                
                // Set the material pass and draw the mesh using the combined matrix and current camera
                mat.SetPass(0);
                Graphics.DrawMesh(mesh,combinedMatrix, mat, 0, Camera.current);

            }
        }
    }
    float CalculateObjectHeight(GameObject prefab)
    {
        if (prefab != null)
        {
            // Get all MeshFilters in the prefab and its children
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>();

            float maxHeight = 0f;

            // Find the maximum height among the vertices of the meshes
            foreach (MeshFilter filter in filters)
            {
                Transform filterTf = filter.transform;
                Mesh mesh = filter.sharedMesh;
                
                // Get the local-to-world transformation matrix of the filter's transform
                Matrix4x4 localMatrix = filterTf.localToWorldMatrix;
                
                // Calculate the height based on the position and scale of the filter's transform and the bounds of the
                // mesh
                float height = filterTf.position.y + (mesh.bounds.max.y * localMatrix.lossyScale.y);
                
                // Update the maxHeight if the current height is greater
                if (height > maxHeight)
                {
                     maxHeight = height;
                }
            }
            
            return maxHeight;
        }

        return 0;
    }
    void  TrySpawnPoints()
    {
        foreach (SpawnData hitPoint in pointsToSpawnPrefabs)
        {
            if (hitPoint.prefab != null)
            {
                // Instantiate the prefab as a GameObject
                GameObject newObject = (GameObject) PrefabUtility.InstantiatePrefab(hitPoint.prefab);
                
                // Get the position and rotation from the hitPoint
                Vector3 position = hitPoint.hitPose.position;
                Quaternion rotation = hitPoint.hitPose.rotation;

                // Set the rotation and position of the newObject
                newObject.transform.rotation = rotation;
                newObject.transform.position = position + hitPoint.prefab.transform.position.y * newObject.transform.up;

                // Register the instantiated object for Undo/Redo functionality
                Undo.RegisterCreatedObjectUndo ( newObject, "Created prefab by placer Tool");
            }
        }
    }
    void  GenerateSpawnPoints()
    {
        //Initialize array of points inside circle
        prefabsInsideCircle = new PrefabData[spawnCount];
        
        int selectPrebabsLength = selectedPrefabs.Count;
        
        //Defines minimum distance between prefabs
        float distanceBetweenPrefabs = (Mathf.Sqrt(Mathf.PI/spawnCount)/ 2)* 2f * 0.30f;
        
        int maxTriesToSpawn = 10;
        
        for (int i = 0; i < spawnCount; i++)
        {
            // Generate a random point inside the unit circle
            prefabsInsideCircle[i].SetRandValues();
            prefabsInsideCircle[i].pointInDisc = Random.insideUnitCircle;
            
            // Set a prefab to be placed at this point in the unit circle
            if (selectPrebabsLength > 0)
            {
                // Select a random prefab from the available options
                prefabsInsideCircle[i].prefab = selectedPrefabs[Random.Range(0, selectPrebabsLength)];
                prefabsInsideCircle[i].height = CalculateObjectHeight(prefabsInsideCircle[i].prefab);
            }
            
            for(int j = i - 1; j >= 0 ; j--)
            {
                int triesCount = 0;
                float minimumDistance = Vector2.Distance(prefabsInsideCircle[i].pointInDisc, prefabsInsideCircle[j].pointInDisc);
                
                // Try to find a place inside the circle with enough distance from other prefabs
                while ( minimumDistance < distanceBetweenPrefabs && triesCount < maxTriesToSpawn)
                {
                    // Generate a new random point
                    prefabsInsideCircle[i].pointInDisc = Random.insideUnitCircle;
                    triesCount++;
                }
            }
        }
    }
}

