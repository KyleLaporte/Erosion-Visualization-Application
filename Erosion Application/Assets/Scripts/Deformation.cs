using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Deformation : MonoBehaviour
{
    // Deformation details
    public float radius = 200f; // unit of vertices
    // public float deformationStrength = 20000f;
    public float force = 15f;
    public float smoothingFactor = 6f;
    public float maximumDepression;
    private bool meshChanged = false;
    public float flattenStrength = 5f;

    // General Mesh parameters
    private Mesh mesh;
    private Vector3[] modifiedVertices;
    private MeshCollider meshCollider;
    private bool updateCollider;
    private MeshGenerator meshGenerator;

    // Mesh parameters used for colouring
    private float modifiedMaxHeight;
    private float modifiedMinHeight;
    private float heightFactor;

    // Colour parameters
    public Gradient gradient;
    private Color[] coloursHyp;
    private Color[] coloursGrey;

    // Button mechanisms
    public enum TerrainMod { Raise, Lower, Flatten, None };
    public TerrainMod terrainMod;
    public Slider radiusSlider;
    public Slider intensitySlider;
    public Slider flattenSlider;
    private bool usingSlider = false;
    public GameObject intensitySliderHolder;
    public GameObject flattenSliderHolder;
    public Button raiseToggle;
    public Button lowerToggle;
    public Button flattenToggle;
    public Color toggleOnColour;

    // Raycast
    public bool raycastHittingTerrain = false;

    // Toggles
    public Toggle toggleRaise;
    public Toggle toggleLower;
    public Toggle toggleFlatten;
    public Toggle toggleNone;

    private void Start()
    {
        meshGenerator = FindObjectOfType<MeshGenerator>();
        terrainMod = TerrainMod.None;
    }

    void Update()
    {
        if (toggleRaise.isOn)
        {
            terrainMod = TerrainMod.Raise;
        }
        if (toggleLower.isOn)
        {
            terrainMod = TerrainMod.Lower;
        }
        if (toggleFlatten.isOn)
        {
            terrainMod = TerrainMod.Flatten;
        }
        if (toggleNone.isOn)
        {
            terrainMod = TerrainMod.None;
        }
        // Get rid of gradient legends
        if(terrainMod != TerrainMod.None)
        {
            meshGenerator.LossGrad.gameObject.SetActive(false);
            meshGenerator.GainGrad.gameObject.SetActive(false);
        }

        // Colour check for toggle buttons
        if(terrainMod == TerrainMod.Raise)
        {
            raiseToggle.image.color = toggleOnColour;
            lowerToggle.image.color = Color.white;
            flattenToggle.image.color = Color.white;
        }
        else if(terrainMod == TerrainMod.Lower)
        {
            raiseToggle.image.color = Color.white;
            lowerToggle.image.color = toggleOnColour;
            flattenToggle.image.color = Color.white;
        }
        else if (terrainMod == TerrainMod.Flatten)
        {
            raiseToggle.image.color = Color.white;
            lowerToggle.image.color = Color.white;
            flattenToggle.image.color = toggleOnColour;
        }
        else
        {
            raiseToggle.image.color = Color.white;
            lowerToggle.image.color = Color.white;
            flattenToggle.image.color = Color.white;
        }

        // Slider check
        if(terrainMod != TerrainMod.Flatten)
        {
            flattenSliderHolder.SetActive(false);
            intensitySliderHolder.SetActive(true);
        }
        else
        {
            flattenSliderHolder.SetActive(true);
            intensitySliderHolder.SetActive(false);
        }

        // Check if one of the editing modes is on
        if(terrainMod != TerrainMod.None && usingSlider == false)
        {
            // Shoot raycast
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // If raycast hits object
            if (Physics.Raycast(ray, out hit, Mathf.Infinity) && !EventSystem.current.IsPointerOverGameObject())
            {
                raycastHittingTerrain = true;
                for (int i = 0; i < modifiedVertices.Length; i++)
                {
                    Vector3 distance = modifiedVertices[i] - hit.point;
                    //float force = deformationStrength / (1f + hit.point.sqrMagnitude); 

                    if (distance.sqrMagnitude < radius)
                    {
                        if (Input.GetMouseButton(0) && terrainMod == TerrainMod.Raise)
                        {
                            if(meshChanged == false)
                            {
                                meshChanged = true;
                            }
                            modifiedVertices[i] = modifiedVertices[i] + (Vector3.up * force) / smoothingFactor * ((radius - distance.sqrMagnitude) / radius);
                            if(modifiedVertices[i].y / heightFactor > modifiedMaxHeight)
                            {
                                modifiedMaxHeight = modifiedVertices[i].y / heightFactor;
                            }
                        }
                        else if (Input.GetMouseButton(0) && terrainMod == TerrainMod.Lower)
                        {
                            if (meshChanged == false)
                            {
                                meshChanged = true;
                            }
                            modifiedVertices[i] = modifiedVertices[i] - (Vector3.up * force) / smoothingFactor * ((radius - distance.sqrMagnitude) / radius);
                            if(modifiedVertices[i].y / heightFactor < modifiedMinHeight)
                            {
                                modifiedMinHeight = modifiedVertices[i].y / heightFactor;
                            }
                        }
                        else if (Input.GetMouseButton(0) && terrainMod == TerrainMod.Flatten)
                        {
                            if(meshChanged == false)
                            {
                                meshChanged = true;
                            }
                            modifiedVertices[i].y = flattenStrength;
                            if (modifiedVertices[i].y / heightFactor > modifiedMaxHeight)
                            {
                                modifiedMaxHeight = modifiedVertices[i].y / heightFactor;
                            }
                            if (modifiedVertices[i].y / heightFactor < modifiedMinHeight)
                            {
                                modifiedMinHeight = modifiedVertices[i].y / heightFactor;
                            }
                        }
                    }
                }
            }
            else
            {
                raycastHittingTerrain = false;
            }
            // Update mesh collider once user stops touching / clicking the screen
            if (Input.GetMouseButton(0) && meshChanged)
            {
                updateCollider = false;
            }
            else if (Input.GetMouseButtonUp(0) && meshChanged)
            {
                updateCollider = true;
                meshGenerator.NewSavedMesh(mesh);
                meshChanged = false;
            }
            RecalculateMesh(updateCollider);
        }
    }

    void RecalculateMesh(bool updateCollider)
    {
        // Set new vertices to mesh collider
        mesh.vertices = modifiedVertices;
        mesh.RecalculateNormals();
        // Recolour based on new heights
        for (int i = 0; i < modifiedVertices.Length; i++)
        {
            // Perfectly flat case
            if (modifiedMaxHeight == modifiedMinHeight)
            {
                coloursHyp[i] = meshGenerator.hypsometricGradient.Evaluate(modifiedVertices[i].y);
                coloursGrey[i] = meshGenerator.greyscaleGradient.Evaluate(modifiedVertices[i].y);
            }
            // Recolour regularly
            else
            {
                coloursHyp[i] = meshGenerator.hypsometricGradient.Evaluate(((modifiedVertices[i].y / heightFactor) - (modifiedMinHeight)) / ((modifiedMaxHeight) - (modifiedMinHeight)));
                coloursGrey[i] = meshGenerator.greyscaleGradient.Evaluate(((modifiedVertices[i].y / heightFactor) - (modifiedMinHeight)) / ((modifiedMaxHeight) - (modifiedMinHeight)));
            }
        }
        meshGenerator.hypsometricColours = coloursHyp;
        meshGenerator.greyscaleColours = coloursGrey;
        if(meshGenerator.colourMode == MeshGenerator.ColourMode.Hypsometric)
        {
            mesh.colors = coloursHyp;
        }
        else if(meshGenerator.colourMode == MeshGenerator.ColourMode.Greyscale)
        {
            mesh.colors = coloursGrey;
        }

        // Update mesh collider
        if (updateCollider)
        {
            meshCollider.sharedMesh = mesh;
        }
    }

    // Receive newly generated mesh from MeshGenerator class
    public void NewMeshGenerated(Mesh newMesh, float heightFactor, float[] mapMaxMin)
    {
        // Get vertices of the newly generated mesh to be used in deformation
        mesh = newMesh;
        modifiedVertices = mesh.vertices;
        int coloursLength = mesh.colors.Length;
        coloursHyp = new Color[coloursLength];
        coloursGrey = new Color[coloursLength];
        this.heightFactor = heightFactor;
        modifiedMaxHeight = mapMaxMin[0];
        modifiedMinHeight = mapMaxMin[1];
        meshCollider = FindObjectOfType<MeshCollider>();
    }

    // Handle the buttons present in the Terrain Editor sub-menu
    public void ButtonRaise()
    {
        terrainMod = TerrainMod.Raise;
    }
    public void ButtonRaiseToggle()
    {
        if(terrainMod == TerrainMod.Raise)
        {
            terrainMod = TerrainMod.None;
        }
        else
        {
            terrainMod = TerrainMod.Raise;
        }
    }

    public void ButtonLower()
    {
        terrainMod = TerrainMod.Lower;
    }
    public void ButtonLowerToggle()
    {
        if (terrainMod == TerrainMod.Lower)
        {
            terrainMod = TerrainMod.None;
        }
        else
        {
            terrainMod = TerrainMod.Lower;
        }
    }

    public void ButtonFlattenToggle()
    {
        if (terrainMod == TerrainMod.Flatten)
        {
            terrainMod = TerrainMod.None;
        }
        else
        {
            terrainMod = TerrainMod.Flatten;
        }

    }
    public void ButtonSaveEdits()
    {
        terrainMod = TerrainMod.None;
        toggleNone.isOn = true;
        meshGenerator.NewSavedMesh(mesh);
    }
    public void ButtonBack()
    {
        terrainMod = TerrainMod.None;
        toggleNone.isOn = true;
        // MeshGenerator meshGenerator = FindObjectOfType<MeshGenerator>();
        // meshGenerator.GenerateTerrain();
    }

    public void SliderBrushRadius()
    {
        radius = radiusSlider.value;
    }

    public void SliderBrushIntensity()
    {
        force = intensitySlider.value;
    }
    public void SliderFlattenHeight()
    {
        flattenStrength = flattenSlider.value;
    }
    public void SliderUsing()
    {
        if (usingSlider == false)
        {
            usingSlider = true;
        }
        else
        {
            usingSlider = false;
        }
    }
}
