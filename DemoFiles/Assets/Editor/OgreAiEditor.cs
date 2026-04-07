using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(OgreBehaviors))]
public class OgreBehaviorsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default Inspector fields (jumpForce, spinSpeed, etc.)
        DrawDefaultInspector();

        // Get a reference to the script we are editing
        OgreBehaviors ogreScript = (OgreBehaviors)target;

        // Add some space for visual clarity
        EditorGUILayout.Space();

        // --- JUMP BUTTON (from previous request) ---
        if (GUILayout.Button("Activate Jump Sequence"))
        {
            ogreScript.StartJumpingSequence();
        }

        // --- NEW SPIN BUTTON ---
        if (GUILayout.Button("Activate Spin Sequence"))
        {
            // Call the public spin method in the OgreAI script
            ogreScript.StartSpinningSequence();
        }
        // -----------------------

        if (GUILayout.Button("NavShroom"))
        {
            // Call the public spin method in the OgreAI script
            ogreScript.StartShroomNavigation();
        }

        if (GUILayout.Button("NavCave"))
        {
            // Call the public spin method in the OgreAI script
            ogreScript.ReturnToCave();
        }

        if (GUILayout.Button("Chase Player"))
        {
            // Call the public spin method in the OgreAI script
            ogreScript.StartChasePlayer();
        }

        if (GUILayout.Button("Pick up rock and throw it"))
        {
            // Call the public spin method in the OgreAI script
            ogreScript.StartRockThrow();
        }


        //if (GUILayout.Button("Switch Cam"))
        //{
        //    GameObject mainCamera = GameObject.Find("Main Camera");
        //    GameObject playerCamera = GameObject.Find("PlayerCamera");

        //    if (!mainCamera.activeSelf)
        //    {
        //        playerCamera.SetActive(false);
        //        mainCamera.SetActive(true);
        //    } else
        //    {
        //        mainCamera.SetActive(false);
        //        playerCamera.SetActive(true);
        //    }
        //}
    }
}
