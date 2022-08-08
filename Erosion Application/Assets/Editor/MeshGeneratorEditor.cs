using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor (typeof(MeshGenerator))]
public class MeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Apply to things with MeshGenerator component
        MeshGenerator meshGenerator = (MeshGenerator)target;

        // Draw inspector as normal
        DrawDefaultInspector();

        // Call GenerateTerrain when button is clicked
        if (GUILayout.Button("Generate Terrain"))
        {
            meshGenerator.GenerateNoisemap();
            meshGenerator.GenerateTerrain();
        }

        // Call GenerateErodedTerrain when button is clicked
        if (GUILayout.Button("Generate Eroded Terrain"))
        {
            meshGenerator.GenerateErodedTerrain();
        }
    }
}
