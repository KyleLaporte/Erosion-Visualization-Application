using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MeshGenerator : MonoBehaviour
{
    // Setup mesh parameters, grabbed from the inspector
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    private Material[] materials;

    // public GameObject ColourmapButton;
    [Range(0, 256)]
    public int mapSize = 256;
    public float meshScale = 140f;
    public float heightFactor = 45f;
    public int numErosionIterations = 50000;
    private Mesh mesh;

    // Setup arrays for vertices, triangles, and uvs
    private Vector3[] vertices;
    private Vector2[] uvs;
    private int[] triangles;
    private float[] currentHeightmap;
    private float[] referenceHeightmap;
    private float[] flatHeightmap;

    // Colours
    private Color[] iterativeChangeColoursHyp;
    private Color[] iterativeChangeColoursGrey;
    private Color[] totalChangeColoursHyp;
    private Color[] totalChangeColoursGrey;
    public Color[] hypsometricColours;
    public Color[] greyscaleColours;
    public Material gridMaterial;
    public Material invisibleMaterial;
    public Gradient greyscaleGradient;
    public Gradient lossGradient;
    public Gradient gainGradient;
    public Gradient hypsometricGradient;
    private bool gridVisible = false;
    public enum ColourMode { Greyscale, Hypsometric };
    public ColourMode colourMode = ColourMode.Hypsometric;

    // Undo / Redo
    LinkedList<float[]> changeList = new LinkedList<float[]>();
    LinkedListNode<float[]> current;
    public Button undoButton;
    public Button redoButton;

    // Saving
    public Button saveButton;
    public Button savePopup;
    public bool savePopupOn;

    // Gradient legend
    public Image GainGrad;
    public Image LossGrad;
    public Image ColourGrad;
    public Image GreyscaleGrad;

    public Text legendMinHeight;
    public Text legendMaxHeight;
    public Text legendMinLoss;
    public Text legendMaxLoss;
    public Text legendMinGain;
    public Text legendMaxGain;

    private string totalMinLoss;
    private string totalMaxLoss;
    private string totalMinGain;
    private string totalMaxGain;
    private string iterativeMinLoss;
    private string iterativeMaxLoss;
    private string iterativeMinGain;
    private string iterativeMaxGain;

    // Detection Limit
    public Slider detectionSlider;
    public float detectionLimit = 0f;

    // Plane
    public float angle = 0f;
    public Slider angleSlider;

    private void Awake()
    {
        GenerateNoisemap();
        GenerateTerrain();
        GenerateFlat();
        ButtonShowGrid();
        legendMinLoss.text = "0";
        legendMinGain.text = "0";
    }

    private void Update()
    {
        // Heightmap legend max and min values
        if(currentHeightmap != null)
        {
            // This is scaling up and down by 100 to display a two digit number, because Mathf.Round natively only rounds to whole integer values
            // DO NOT use this when doing actual math, as this can mess with your significant digits. 
            // This is just displaying something to the number of decimals I want on the screen, not being used for math.
            legendMaxHeight.text = (Mathf.Round(HeightmapMaxMin(currentHeightmap)[0] * 100) / 100).ToString();
            legendMinHeight.text = (Mathf.Round(HeightmapMaxMin(currentHeightmap)[1] * 100) / 100).ToString();
        }

        // Check if undo and / or redo are usable
        if(current.Previous == null && undoButton.IsInteractable())
        {
            undoButton.interactable = false;
        }
        else if (current.Previous != null && !undoButton.IsInteractable())
        {
            undoButton.interactable = true;
        }
        if (current.Next == null && redoButton.IsInteractable())
        {
            redoButton.interactable = false;
        }
        else if (current.Next != null && !redoButton.IsInteractable())
        {
            redoButton.interactable = true;
        }

        // Check if we should popup the Saved! button
        if (savePopupOn)
        {
            savePopup.gameObject.SetActive(true);
            saveButton.gameObject.SetActive(false);
        }
        else if(savePopupOn == false)
        {
            savePopup.gameObject.SetActive(false);
            saveButton.gameObject.SetActive(true);
        }
    }

    public void GenerateErodedTerrain()
    {
        // Take the current mesh and make two copies of its heightmap, one to be eroded and one to compare change between the two
        float[] beforeErodedHeightmap = HeightmapFromMesh(vertices, heightFactor);
        float[] erodedHeightmap = HeightmapFromMesh(vertices, heightFactor);

        // Generate erosion on one of the heightmaps
        RainfallErosion erosion = FindObjectOfType<RainfallErosion>();
        erosion.Erode(erodedHeightmap, mapSize, numErosionIterations, true);

        // User might want to know that effectively no erosion happened in those cases, eg. intensity factor for "how much erosion"

        // Generate newly eroded mesh, coloured based on the degree of change
        BuildVertices(mapSize, erodedHeightmap, meshScale, heightFactor);
        BuildTriangles(mapSize);

        hypsometricColours = BuildHeightColourmap(erodedHeightmap, hypsometricGradient);
        greyscaleColours = BuildHeightColourmap(erodedHeightmap, greyscaleGradient);

        Color[][] changeColourHolder = BuildChangeColourmap(beforeErodedHeightmap, erodedHeightmap, detectionLimit, heightFactor, gainGradient, lossGradient);
        iterativeChangeColoursHyp = changeColourHolder[0];
        iterativeChangeColoursGrey = changeColourHolder[1];
        changeColourHolder = BuildChangeColourmapT(referenceHeightmap, erodedHeightmap, detectionLimit, heightFactor, gainGradient, lossGradient);
        totalChangeColoursHyp = changeColourHolder[0];
        totalChangeColoursGrey = changeColourHolder[1];
        currentHeightmap = erodedHeightmap;

        SaveHeightmapToList();
        BuildMesh();
    }

    public void GenerateFlat()
    {
        Deformation deformation = FindObjectOfType<Deformation>();
        flatHeightmap = new float[currentHeightmap.Length];
        for(int i = 0; i < currentHeightmap.Length; i++)
        {
            flatHeightmap[i] = deformation.flattenStrength / deformation.heightFactor;
        }
    }

    public void TestShowIterativeChange()
    {
        if(current.Previous.Value != null && current.Value != null)
        {
            Color[][] changeColourHolder = BuildChangeColourmap(current.Previous.Value, current.Value, detectionLimit, heightFactor, gainGradient, lossGradient);
            iterativeChangeColoursHyp = changeColourHolder[0];
            iterativeChangeColoursGrey = changeColourHolder[1];
        }
    }

    public void TestShowTotalChange()
    {
        if (current.Value != null)
        {
            Color[][] changeColourHolder = BuildChangeColourmap(referenceHeightmap, current.Value, detectionLimit, heightFactor, gainGradient, lossGradient);
            totalChangeColoursHyp = changeColourHolder[0];
            totalChangeColoursGrey = changeColourHolder[1];
        }
    }

    public void GenerateNoisemap()
    {
        NoiseMapGenerator noise = FindObjectOfType<NoiseMapGenerator>();
        float[] noiseMap = noise.BuildNoiseMap(mapSize);
        currentHeightmap = noiseMap;
        referenceHeightmap = noiseMap;
        SaveHeightmapToList();
    }
       
    public void GenerateTerrain()
    {
        // ColourmapButton.GetComponent<Button>().interactable = false;
        BuildVertices(mapSize, currentHeightmap, meshScale, heightFactor);
        BuildTriangles(mapSize);

        hypsometricColours = BuildHeightColourmap(currentHeightmap, hypsometricGradient);
        greyscaleColours = BuildHeightColourmap(currentHeightmap, greyscaleGradient);

        BuildMesh();
        savePopupOn = false;
    }

    public void BuildMesh()
    {
        Mesh generatedMesh = new Mesh();

        // Assign vertices and triangles to mesh
        generatedMesh.vertices = vertices;
        generatedMesh.uv = uvs;
        generatedMesh.triangles = triangles;
        generatedMesh.RecalculateNormals();

        // Display the mesh
        meshFilter.sharedMesh = generatedMesh;
        if(colourMode == ColourMode.Hypsometric)
        {
            generatedMesh.colors = hypsometricColours;
        }
        else
        {
            generatedMesh.colors = greyscaleColours;
        }

        // Don't show legend gradients, as we're not in a change mode
        LossGrad.gameObject.SetActive(false);
        GainGrad.gameObject.SetActive(false);

        // Update mesh collider
        MeshCollider meshCollider = FindObjectOfType<MeshCollider>();
        meshCollider.sharedMesh = generatedMesh;

        // Send heigh factor, max/min, and mesh object to deformation script to update its parameters with those of the new mesh
        float[] heightmap = HeightmapFromMesh(generatedMesh.vertices, heightFactor);
        float[] mapMaxMin = HeightmapMaxMin(heightmap);

        Deformation deformation = FindObjectOfType<Deformation>();
        deformation.NewMeshGenerated(generatedMesh, heightFactor, mapMaxMin);
        mesh = generatedMesh;
    }
 
    private void BuildVertices(int mapSize, float[] noiseMap, float meshScale, float heightFactor)
    {
        // Setup array of appropriate size to store all vertices
        vertices = new Vector3[(mapSize) * (mapSize)];

        // Setup array to build uv map
        uvs = new Vector2[(mapSize) * (mapSize)];
        
        // Iterate through all coordinates
        for (int x = 0; x < mapSize; x++)
        {
            for (int y = 0; y < mapSize; y++)
            {
                // Handle vertices being in 1D
                int vertIndex = y * mapSize + x;

                // Find how far the current vertex is across the mesh as a value between 0 and 1
                Vector2 percent = new Vector2(x / (float)(mapSize - 1), y / (float)(mapSize - 1));

                // Convert 0 to 1 percentages to -1 to 1 Cartesian positions, and multiply them by some scaling factor to size the map
                Vector3 cartesianPos = new Vector3(percent.x * 2 - 1, 0, percent.y * 2 - 1) * meshScale;
                cartesianPos = cartesianPos + (Vector3.up * noiseMap[vertIndex] * heightFactor);
                
                vertices[vertIndex] = cartesianPos;
                // vertices[vertIndex] = new Vector3(y, meshHeight, x);

                uvs[vertIndex] = new Vector2(y / (float)mapSize, x / (float)mapSize);
            }
        }
    }

    // This gets hella messy with actual data in a GIS, gradients etc. Pick triangle that's closer to being a contour line
    private void BuildTriangles(int mapSize)
    {
        // Setup array of appropriate size to store all triangles
        triangles = new int[(mapSize-1) * (mapSize-1) * 6];
        
        // Iterate through each point, building triangles in pairs based on an input point
        int vertIndex = 0;
        int trisIndex = 0;
        // Stop one row and column short of the vertices array, as they won't make more triangles
        for (int x = 0; x < mapSize-1; x++)
        {
            for (int y = 0; y < mapSize-1; y++)
            {
                // Build triangles in clockwise order for directionality
                triangles[trisIndex + 0] = vertIndex + mapSize;
                triangles[trisIndex + 1] = vertIndex + mapSize + 1;
                triangles[trisIndex + 2] = vertIndex;
                triangles[trisIndex + 3] = vertIndex + mapSize + 1;
                triangles[trisIndex + 4] = vertIndex + 1;
                triangles[trisIndex + 5] = vertIndex;

                vertIndex++;
                trisIndex += 6;
            }
            vertIndex++;
        }
    }

    // Make colourmap from height map
    public Color[] BuildHeightColourmap(float[] heightmap, Gradient gradient)
    {
        float[] maxMin = HeightmapMaxMin(heightmap);
        float maxHeight = maxMin[0];
        float minHeight = maxMin[1];
        Color[] colours = new Color[heightmap.Length];

        for (int i = 0; i < heightmap.Length; i++)
        {

            // Perfectly flat case
            if(maxHeight == minHeight)
            {
                colours[i] = gradient.Evaluate(heightmap[i]);
            }
            // Apply colour at location
            else
            {
                colours[i] = gradient.Evaluate((heightmap[i] - (minHeight)) / ((maxHeight) - (minHeight)));
            }
        }
        return colours;
    }

    public Color[][] BuildChangeColourmap(float[] beforeErosion, float[] afterErosion, float detectionLimit, float heightFactor, Gradient gainGradient, Gradient lossGradient)
    {
        Color[][] returnArray = new Color[2][];
        Color[] coloursHyp = new Color[beforeErosion.Length];
        Color[] coloursGrey = new Color[beforeErosion.Length];
        float maxLoss = 0f;
        float maxGain = 0f;
        float displayedMinLoss = 0f;
        float displayedMinGain = 0f;
        float trueDetectionLimit = detectionLimit / heightFactor;
        float[] lossMap = new float[beforeErosion.Length];
        float[] normalizedLossMap = new float[beforeErosion.Length];
        float[] normalizedGainMap = new float[beforeErosion.Length];
        float[] gainMap = new float[beforeErosion.Length];
        bool firstRunLoss = true;
        bool firstRunGain = true;

        // Get non-normalized change values, and find the max and min
        for (int i = 0; i < beforeErosion.Length; i++)
        {
            float heightChange = afterErosion[i] - beforeErosion[i];
            // Loss
            if (heightChange < 0)
            {
                lossMap[i] = Mathf.Abs(heightChange);
                gainMap[i] = 0;
                if (Mathf.Abs(heightChange) > maxLoss)
                {
                    maxLoss = Mathf.Abs(heightChange);
                }
            }
            // Gain
            else if (heightChange > 0)
            {
                lossMap[i] = 0;
                gainMap[i] = heightChange;
                if (heightChange > maxGain)
                {
                    maxGain = heightChange;
                }
            }
        }
        // Normalize change values
        for (int i = 0; i < beforeErosion.Length; i++)
        {
            normalizedLossMap[i] = (lossMap[i] / maxLoss);
            normalizedGainMap[i] = (gainMap[i] / maxGain);
        }

        // Apply colours if loss or gain is greater than the specified detection limit
        for (int i = 0; i < beforeErosion.Length; i++)
        {
            if (lossMap[i] > trueDetectionLimit)
            {
                // Minimum for legend display
                if (firstRunLoss)
                {
                    displayedMinLoss = lossMap[i];
                    firstRunLoss = false;
                }
                if (lossMap[i] < displayedMinLoss)
                {
                    displayedMinLoss = lossMap[i];
                }

                // Color fix
                coloursHyp[i] = lossGradient.Evaluate(normalizedLossMap[i]);
                coloursGrey[i] = lossGradient.Evaluate(normalizedLossMap[i]);
            }

            else if (gainMap[i] > trueDetectionLimit)
            {
                // Minimum for legend display
                if (firstRunGain)
                {
                    displayedMinGain = gainMap[i];
                    firstRunGain = false;
                }
                if (gainMap[i] < displayedMinGain)
                {
                    displayedMinGain = gainMap[i];
                }

                // Color fix
                coloursHyp[i] = gainGradient.Evaluate(normalizedGainMap[i]);
                coloursGrey[i] = gainGradient.Evaluate(normalizedGainMap[i]);
            }
            else
            {
                coloursHyp[i] = hypsometricColours[i];
                coloursGrey[i] = greyscaleColours[i];
            }
        }
        // Return legend values
        iterativeMinLoss = (Mathf.Round(displayedMinLoss * 100) / 100).ToString();
        iterativeMaxLoss = (Mathf.Round(maxLoss * 100) / 100).ToString();
        iterativeMinGain = (Mathf.Round(displayedMinGain * 100) / 100).ToString();
        iterativeMaxGain = (Mathf.Round(maxGain * 100) / 100).ToString();

        // ColourmapButton.GetComponent<Button>().interactable = true;
        returnArray[0] = coloursHyp;
        returnArray[1] = coloursGrey;
        return returnArray;
    }

    // Returns float array where array[0] is the max and array[1] is the min of the input heightmap
    public float[] HeightmapMaxMin(float[] heightmap)
    {
        float[] heightmapMaxMin = new float[2];
        float maxHeight = 0;
        float minHeight = 0;
        bool firstRun = true;
        for(int i = 0; i < heightmap.Length; i++)
        {
            if (firstRun)
            {
                maxHeight = heightmap[i];
                minHeight = heightmap[i];
                firstRun = false;
            }
            if(heightmap[i] > maxHeight)
            {
                maxHeight = heightmap[i];
            }
            if(heightmap[i] < minHeight)
            {
                minHeight = heightmap[i];
            }
        }
        heightmapMaxMin[0] = maxHeight;
        heightmapMaxMin[1] = minHeight;
        return heightmapMaxMin;
    }

    // This works because the mesh is the same size as the heightmaps
    private float[] HeightmapFromMesh(Vector3[] vertices, float heightFactor)
    {
        // Get mesh parameters of current mesh from Deformation script
        Deformation deformation = FindObjectOfType<Deformation>();
        Mesh mesh = deformation.GetComponent<MeshFilter>().sharedMesh;
        vertices = mesh.vertices;

        // Create float array to store heightmap
        float[] newHeightmap = new float[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {                
            // Undo the factor scaling when moving from map to mesh
            newHeightmap[i] = vertices[i].y / heightFactor;
        }
        return newHeightmap;
    }

    public void NewSavedMesh(Mesh mesh)
    {
        currentHeightmap = HeightmapFromMesh(mesh.vertices, heightFactor);
        SaveHeightmapToList();
        savePopupOn = false;
        LossGrad.gameObject.SetActive(false);
        GainGrad.gameObject.SetActive(false);
    }

    void SaveHeightmapToList()
    {
        // If we're not on the last node and are making difference changes, we need to delete the other states first
        if (current != null && current.Next != null)
        {
            // Remove the last entry until our current node is the last node
            while (current != changeList.Last)
            {
                changeList.RemoveLast();
            }
        }

        // Add heightmap to last position
        changeList.AddLast(currentHeightmap);
        current = changeList.Last;

        //////////////////////////////////////////////////////
        // Show save icon; swap this out to be an animation
        //savePopup.gameObject.SetActive(true);
    }

    public void ButtonRegenerateFull()
    {
        float[] savedHeightmapHolder = referenceHeightmap;
        GenerateNoisemap();
        GenerateTerrain();
        referenceHeightmap = savedHeightmapHolder;
    }

    public void ButtonRegenerateSaved()
    {
        Deformation deformation = FindObjectOfType<Deformation>();
        deformation.terrainMod = Deformation.TerrainMod.None;
        deformation.toggleNone.isOn = true;
        currentHeightmap = referenceHeightmap;
        GenerateTerrain();
    }

    public void ButtonGenerateFlat()
    {
        GenerateFlat();
        Deformation deformation = FindObjectOfType<Deformation>();
        deformation.terrainMod = Deformation.TerrainMod.None;
        deformation.toggleNone.isOn = true;
        currentHeightmap = flatHeightmap;
        SaveHeightmapToList();
        GenerateTerrain();
    }

    public void ButtonIterativeChangeColours()
    {
        if (current != null && current.Previous != null && current.Previous.Value != null && current.Value != null)
        {
            TestShowIterativeChange();
            if (colourMode == ColourMode.Greyscale)
            {
                mesh.colors = iterativeChangeColoursGrey;
            }
            else if (colourMode == ColourMode.Hypsometric)
            {
                mesh.colors = iterativeChangeColoursHyp;
            }
        }

        LossGrad.gameObject.SetActive(true);
        GainGrad.gameObject.SetActive(true);

        //legendMinLoss.text = iterativeMinLoss;
        legendMaxLoss.text = iterativeMaxLoss;
        //legendMinGain.text = iterativeMinGain;
        legendMaxGain.text = iterativeMaxGain;

        Deformation deformation = FindObjectOfType<Deformation>();
        deformation.terrainMod = Deformation.TerrainMod.None;
        deformation.toggleNone.isOn = true;
    }

    public void ButtonTotalChangeColours()
    {
        if (current != null && current.Value != null)
        {
            TestShowTotalChange();
            if (colourMode == ColourMode.Greyscale)
            {
                mesh.colors = totalChangeColoursGrey;
            }
            else if (colourMode == ColourMode.Hypsometric)
            {
                mesh.colors = totalChangeColoursHyp;
            }
        }

        LossGrad.gameObject.SetActive(true);
        GainGrad.gameObject.SetActive(true);

        //legendMinLoss.text = totalMinLoss;
        legendMaxLoss.text = totalMaxLoss;
        //legendMinGain.text = totalMinGain;
        legendMaxGain.text = totalMaxGain;

        Deformation deformation = FindObjectOfType<Deformation>();
        deformation.terrainMod = Deformation.TerrainMod.None;
        deformation.toggleNone.isOn = true;
    }

    public void ButtonHeightColours()
    {
        mesh.colors = hypsometricColours;
        colourMode = ColourMode.Hypsometric;
        LossGrad.gameObject.SetActive(false);
        GainGrad.gameObject.SetActive(false);
        GreyscaleGrad.gameObject.SetActive(false);
        ColourGrad.gameObject.SetActive(true);
        // Deformation deformation = FindObjectOfType<Deformation>();
        // deformation.terrainMod = Deformation.TerrainMod.None;
    }

    public void ButtonGreyscaleColours()
    {
        mesh.colors = greyscaleColours;
        colourMode = ColourMode.Greyscale;
        LossGrad.gameObject.SetActive(false);
        GainGrad.gameObject.SetActive(false);
        ColourGrad.gameObject.SetActive(false);
        GreyscaleGrad.gameObject.SetActive(true);
        // Deformation deformation = FindObjectOfType<Deformation>();
        // deformation.terrainMod = Deformation.TerrainMod.None;
    }


    public void ButtonNewSavedMesh()
    {
        referenceHeightmap = current.Value; //HeightmapFromMesh(mesh.vertices, heightFactor);
        savePopupOn = true;
    }

    public void ButtonShowGrid()
    {
        materials = meshRenderer.materials;
        // Turn grid off
        if (gridVisible == true)
        {
            materials[0] = invisibleMaterial;
            gridVisible = false;
        }

        // Turn grid on
        else if (gridVisible == false)
        {
            materials[0] = gridMaterial;
            gridVisible = true;
        }
        meshRenderer.materials = materials;
    }

    public void ButtonUndo()
    {
        Deformation deformation = FindObjectOfType<Deformation>();
        deformation.terrainMod = Deformation.TerrainMod.None;
        deformation.toggleNone.isOn = true;
        // If we have no previous state we can't go back to it
        if (current.Previous == null)
        {
            Debug.Log("Don't let button be clickable");
            return;
        }
        // Otherwise go back to our previous state
        currentHeightmap = current.Previous.Value;
        current = current.Previous;
        GenerateTerrain();
    }

    public void ButtonRedo()
    {
        Deformation deformation = FindObjectOfType<Deformation>();
        deformation.terrainMod = Deformation.TerrainMod.None;
        deformation.toggleNone.isOn = true;
        // If we have no next state we can't go back to it
        if (current.Next == null)
        {
            Debug.Log("Don't let button be clickable");
            return;
        }
        // Otherwise go back to our next state
        currentHeightmap = current.Next.Value;
        current = current.Next;
        GenerateTerrain();
    }




    // TILTING PLANE - UNDOING NOT SUPPORTED

    // Set saved heightmap to a flat plane
    public void BuildPlane()
    {
        for(int i = 0; i < currentHeightmap.Length; i++)
        {
            currentHeightmap[i] = 1;
        }
    }

    public void RotatePlane()
    {
        float[] heightmap = new float[currentHeightmap.Length];
        float minValue = 0;
        float maxValue = 0;
        bool firstRun = true;
        float distanceFromCentre;
        float magnitude;
        float direction;
        float newHeight;
        float midIndex = mapSize / 2 - 0.5f;

        // Rotate plane some angle
        for (int i = 0; i < heightmap.Length; i++)
        {
            distanceFromCentre = midIndex - (i % 256);
            magnitude = Mathf.Abs(distanceFromCentre);
            direction = Mathf.Sign(distanceFromCentre);
            newHeight = magnitude * Mathf.Tan(Mathf.Deg2Rad*(angle));
            // If left of centre line
            if(direction < 0)
            {
                if(angle > 0 && angle < 90)
                {
                    heightmap[i] = newHeight;
                }
                else if(angle < 0 && angle > -90)
                {
                    heightmap[i] = -1 * newHeight;
                }
            }
            if(direction > 0)
            {
                if(angle > 0 && angle < 90)
                {
                    heightmap[i] = -1 * newHeight;
                }
                else if (angle < 0 && angle > -90)
                {
                    heightmap[i] = newHeight;
                }
            }
            if (firstRun)
            {
                minValue = heightmap[i];
                maxValue = heightmap[i];
                firstRun = false;
            }
            else
            {
                if(heightmap[i] < minValue)
                {
                    minValue = heightmap[i];
                }
                if(heightmap[i] > maxValue)
                {
                    maxValue = heightmap[i];
                }
            }
        }

        // Normalize
        if(minValue < 0)
        {
            for(int i = 0; i < heightmap.Length; i++)
            {
                heightmap[i] = (heightmap[i] + Mathf.Abs(minValue)) / heightFactor;
            }
        }
        currentHeightmap = heightmap;
        GenerateTerrain();
    }

    public void ButtonPlane()
    {
        Deformation deformation = FindObjectOfType<Deformation>();
        deformation.terrainMod = Deformation.TerrainMod.None;
        deformation.toggleNone.isOn = true;
        BuildPlane();
        GenerateTerrain();
    }

    public void SliderAngle()
    {
        angle = angleSlider.value;
    }

    public Color[][] BuildChangeColourmapT(float[] beforeErosion, float[] afterErosion, float detectionLimit, float heightFactor, Gradient gainGradient, Gradient lossGradient)
    {
        Color[][] returnArray = new Color[2][];
        Color[] coloursHyp = new Color[beforeErosion.Length];
        Color[] coloursGrey = new Color[beforeErosion.Length];
        float maxLoss = 0f;
        float maxGain = 0f;
        float displayedMinLoss = 0f;
        float displayedMinGain = 0f;
        float trueDetectionLimit = detectionLimit / heightFactor;
        float[] lossMap = new float[beforeErosion.Length];
        float[] normalizedLossMap = new float[beforeErosion.Length];
        float[] normalizedGainMap = new float[beforeErosion.Length];
        float[] gainMap = new float[beforeErosion.Length];
        bool firstRunLoss = true;
        bool firstRunGain = true;

        // Get non-normalized change values, and find the max and min
        for (int i = 0; i < beforeErosion.Length; i++)
        {
            float heightChange = afterErosion[i] - beforeErosion[i];
            // Loss
            if (heightChange < 0)
            {
                lossMap[i] = Mathf.Abs(heightChange);
                gainMap[i] = 0;
                if (Mathf.Abs(heightChange) > maxLoss)
                {
                    maxLoss = Mathf.Abs(heightChange);
                }
            }
            // Gain
            else if (heightChange > 0)
            {
                lossMap[i] = 0;
                gainMap[i] = heightChange;
                if (heightChange > maxGain)
                {
                    maxGain = heightChange;
                }
            }
        }
        // Normalize change values
        for (int i = 0; i < beforeErosion.Length; i++)
        {
            normalizedLossMap[i] = (lossMap[i] / maxLoss);
            normalizedGainMap[i] = (gainMap[i] / maxGain);
        }

        // Apply colours if loss or gain is greater than the specified detection limit
        for (int i = 0; i < beforeErosion.Length; i++)
        {
            if (lossMap[i] > trueDetectionLimit)
            {
                // Minimum for legend display
                if (firstRunLoss)
                {
                    displayedMinLoss = lossMap[i];
                    firstRunLoss = false;
                }
                if(lossMap[i] < displayedMinLoss)
                {
                    displayedMinLoss = lossMap[i];
                }

                // Color fix
                coloursHyp[i] = lossGradient.Evaluate(normalizedLossMap[i]);
                coloursGrey[i] = lossGradient.Evaluate(normalizedLossMap[i]);
            }

            else if (gainMap[i] > trueDetectionLimit)
            {
                // Minimum for legend display
                if (firstRunGain)
                {
                    displayedMinGain = gainMap[i];
                    firstRunGain = false;
                }
                if (gainMap[i] < displayedMinGain)
                {
                    displayedMinGain = gainMap[i];
                }
                
                // Color fix
                coloursHyp[i] = gainGradient.Evaluate(normalizedGainMap[i]);
                coloursGrey[i] = gainGradient.Evaluate(normalizedGainMap[i]);
            }
            else
            {
                coloursHyp[i] = hypsometricColours[i];
                coloursGrey[i] = greyscaleColours[i];
            }
        }
        // Return legend values
        totalMinLoss = (Mathf.Round(displayedMinLoss * 100) / 100).ToString();
        totalMaxLoss= (Mathf.Round(maxLoss * 100) / 100).ToString();
        totalMinGain = (Mathf.Round(displayedMinGain * 100) / 100).ToString();
        totalMaxGain = (Mathf.Round(maxGain * 100) / 100).ToString();

        // ColourmapButton.GetComponent<Button>().interactable = true;
        returnArray[0] = coloursHyp;
        returnArray[1] = coloursGrey;
        return returnArray;
    }
}
