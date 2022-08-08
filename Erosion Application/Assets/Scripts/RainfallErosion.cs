using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RainfallErosion : MonoBehaviour
{
    // Erosion settings
    [Range(2, 8)]
    public int erosionRadius = 2;
    [Range(0, 1)]
    public float inertia = 0.05f; // 0 means the water will immediately change direction to move downhill, and 1 means the water will never change direction
    public float sedimentCapacityFactor = 4; // coefficient used to represent how much sediment a drop can carry
    public float minSedimentCapacity = 0.01f; // prevents carry capacity from zeroing out on flat terrain
    [Range(0, 1)]
    public float erodeSpeed = 0.3f;
    [Range(0, 1)]
    public float depositSpeed = 0.3f;
    [Range(0, 1)]
    public float evaporateSpeed = 0.01f;
    public float gravity = 4;
    public int maxDropletLifetime = 30;
    public float initialWaterVolume = 1;
    public float initialSpeed = 1;

    // Initialize brush arrays
    int[][] erosionBrushIndices;
    float[][] erosionBrushWeights;

    // Initialize seed parameters
    public int seed;
    System.Random pseudoRandom;
    
    // Setup current variable trackers
    int currentSeed;
    int currentErosionRadius;
    int currentMapSize;

    void Setup(int mapSize, bool resetSeed)
    {
        // Generate new seed if requested or needed
        if(resetSeed || pseudoRandom == null || currentSeed != seed)
        {
            pseudoRandom = new System.Random(seed);
            currentSeed = seed;
        }

        // Initialize the erosion brush indices and update current variable trackers if needed
        if(erosionBrushIndices == null || currentErosionRadius != erosionRadius || currentMapSize != mapSize)
        {
            SetupBrushIndices(mapSize, erosionRadius);
            currentErosionRadius = erosionRadius;
            currentMapSize = mapSize;
        }
    }


    public void Erode(float[] noiseMap, int mapSize, int numIterations = 1, bool resetSeed = false)
    {
        Setup(mapSize, resetSeed);
        
        // Erode for the specified number of iterations
        for (int iteration = 0; iteration < numIterations; iteration++)
        {
            // Create a water droplet at a random point within the bounds of the map
            float posX = pseudoRandom.Next(0, mapSize - 1);
            float posY = pseudoRandom.Next(0, mapSize - 1);
            float dirX = 0;
            float dirY = 0;
            float speed = initialSpeed;
            float water = initialWaterVolume;
            float sediment = 0;

            // While the droplet exists
            for(int lifetime = 0; lifetime < maxDropletLifetime; lifetime++)
            {
                int nodeX = (int)posX;
                int nodeY = (int)posY;
                int dropletIndex = nodeY * mapSize + nodeX;

                // Calculate droplet's offset inside the cell, where (0,0) is the NW node and (1,1) is the SE node (vertex)
                float cellOffsetX = posX - nodeX;
                float cellOffsetY = posY - nodeY;

                // Calculate droplet's height and direction of flow using bilinear interpolation of surrounding heights
                HeightAndGradient heightAndGradient = CalculateHeightAndGradient(noiseMap, mapSize, posX, posY);

                // Update the droplet's direction and position, moving 1 position at minimum regardless of speed (don't make holes)
                dirX = (dirX * inertia - heightAndGradient.gradientX * (1 - inertia));
                dirY = (dirY * inertia - heightAndGradient.gradientY * (1 - inertia));

                // Normalize the direction
                float len = Mathf.Sqrt(dirX * dirX + dirY * dirY);
                if(len != 0)
                {
                    dirX = dirX / len;
                    dirY = dirY / len;
                }
                posX += dirX;
                posY += dirY;

                // Don't simulate the droplet if it's been determine that it either isn't moving or has moved off the edge of the map
                if((dirX == 0&& dirY == 0) || posX < 0 || posX >= mapSize - 1 || posY < 0 || posY >= mapSize - 1)
                {
                    break;
                }

                // Find the droplet's new height and calculate the change in height
                float newHeight = CalculateHeightAndGradient(noiseMap, mapSize, posX, posY).height;
                float deltaHeight = newHeight - heightAndGradient.height;

                // Calculate the droplet's sediment capacity (higher moving fast down a slope with lots of water in it)
                float sedimentCapacity = Mathf.Max(-deltaHeight * speed * water * sedimentCapacityFactor, minSedimentCapacity);

                // If carrying more sediment than capacity, or if flowing uphill
                if(sediment>sedimentCapacity || deltaHeight > 0)
                {
                    // If moving uphill, try to fill up to the current height; otherwise deposite a fraction of the excess sediment
                    float amountToDeposit = (deltaHeight > 0) ? Mathf.Min(deltaHeight, sedimentCapacityFactor) : (sedimentCapacityFactor - sedimentCapacityFactor) * depositSpeed;
                    sediment -= amountToDeposit;

                    // Add the sediment to the four nodes of the current sell using bilinear interpolation. Deposition isn't distributed over a radius like erosion is to allow it to fill small pits
                    noiseMap[dropletIndex] += amountToDeposit * (1 - cellOffsetX) * (1 - cellOffsetY);
                    noiseMap[dropletIndex + 1] += amountToDeposit * cellOffsetX * (1 - cellOffsetY);
                    noiseMap[dropletIndex + mapSize] += amountToDeposit * (1 - cellOffsetX) * cellOffsetY;
                    noiseMap[dropletIndex + mapSize + 1] += amountToDeposit * cellOffsetX * cellOffsetY;
                }

                // If noy carrying more sediment than capacity or if flowing downhill
                else
                {
                    // Erode a fraction of the droplet's current capacity; clamp erosion to the change in height so it doesn't dig holes in terrain behind droplet
                    float amountToErode = Mathf.Min((sedimentCapacity - sediment) * erodeSpeed, -deltaHeight);

                    // Use erosion brush to erode from all nodes inside the droplet's erosion radius; go through every point in circle around droplet
                    for(int brushPointIndex = 0; brushPointIndex < erosionBrushIndices[dropletIndex].Length; brushPointIndex++)
                    {

                        int nodeIndex = erosionBrushIndices[dropletIndex][brushPointIndex];
                        float weightedErodeAmount = amountToErode * erosionBrushWeights[dropletIndex][brushPointIndex];
                        float deltaSediment = (noiseMap[nodeIndex] < weightedErodeAmount) ? noiseMap[nodeIndex] : weightedErodeAmount;
                        noiseMap[nodeIndex] -= deltaSediment;
                        sediment += deltaSediment;
                    }
                }

                // Update droplet's speed and water content
                speed = Mathf.Sqrt(speed * speed + deltaHeight * gravity);
                water *= (1 - evaporateSpeed);
            }
        }
    }

    HeightAndGradient CalculateHeightAndGradient(float[] nodes, int mapSize, float posX, float posY)
    {
        int coordX = (int)posX;
        int coordY = (int)posY;

        // Calculate droplet's offset inside the cell, again with (0,0) being NW and (1,) being SE
        float x = posX - coordX;
        float y = posY - coordY;

        // Calculate heights of the four nodes in the droplet's cell
        int nodeIndexNW = coordY * mapSize + coordX;
        float heightNW = nodes[nodeIndexNW];
        float heightNE = nodes[nodeIndexNW + 1];
        float heightSW = nodes[nodeIndexNW + mapSize];
        float heightSE = nodes[nodeIndexNW + mapSize + 1];

        // Calculate droplet's direction of flow with bilinear interpolation of height difference along the edges
        float gradientX = (heightNE - heightNW) * (1 - y) + (heightSE - heightSW) * y;
        float gradientY = (heightSW - heightNW) * (1 - x) + (heightSE - heightNE) * x;

        // Calculate height with bilinear interpolation of the heights of the nodes of the cell
        float height = heightNW * (1 - x) * (1 - y) + heightNE * x * (1 - y) + heightSW * (1 - x) * y + heightSE * x * y;

        // Assign and return struct
        HeightAndGradient calculatedHeightAndGradient = new HeightAndGradient();
        calculatedHeightAndGradient.height = height;
        calculatedHeightAndGradient.gradientX = gradientX;
        calculatedHeightAndGradient.gradientY = gradientY;

        return calculatedHeightAndGradient;
    }

    void SetupBrushIndices(int mapSize, int radius)
    {
        // Setup brush parameters; first array contains all cells in the map, second array is empty
        erosionBrushIndices = new int[mapSize * mapSize][];
        erosionBrushWeights = new float[mapSize * mapSize][];

        // Setup offset and weight arrays
        int[] xOffsets = new int[radius * radius * 4];
        int[] yOffsets = new int[radius * radius * 4];
        float[] weights = new float[radius * radius * 4];

        float weightSum = 0;
        int addIndex = 0;

        // For every cell in the map
        for(int i = 0; i < erosionBrushIndices.GetLength(0); i++)
        {
            // Establish the x and y coordinates of the current index i
            int centreX = i % mapSize;
            int centreY = i / mapSize;
            
            // If point is less than radius or greater than mapsize - radius (ie. if point lies in an outlining box on the map, with width radius)
            if(centreY <= radius || centreY >= mapSize - radius || centreX <= radius + 1 || centreX >= mapSize - radius)
            {
                // reset weightSum and addIndex each iteration
                weightSum = 0;
                addIndex = 0;

                // Iterate over -radius -> radius box
                // Iterate from negative radius to positive radius wrt y
                for(int y = -radius; y <= radius; y++)
                {
                    // Iterate from negative radius to positive radius wrt x
                    for (int x = -radius; x <= radius; x++)
                    {
                        // Grab hypoteneuse length squared where x and y are side lengths
                        float sqrDistance = x * x + y * y;

                        // If hypoteneuse length squared is less than radius squared (ie. only get hypoteneuses that will be inside a circle with radius "radius")
                        if (sqrDistance < radius * radius)
                        {
                            // Add current X and Y components of triangle who's hypotenuse is on or less than the circle to the current map coordinates; convert the circle that has been drawn on the -r to r grid to a circle drawn on the map
                            int coordX = centreX + x;
                            int coordY = centreY + y;

                            // Triggers for all parts of circle which are in the bounds of the map coordinates
                            if(coordX >= 0 && coordX < mapSize && coordY >= 0 && coordY < mapSize)
                            {
                                // make weight higher at the centre of the circle
                                float weight = 1 - Mathf.Sqrt(sqrDistance) / radius;

                                // accumulate weight
                                weightSum += weight;
                                
                                // add weight of coordinate pair to weight index
                                weights[addIndex] = weight;

                                // Record the amount of shift required to take circle coordinate to map coordinate
                                xOffsets[addIndex] = x;
                                yOffsets[addIndex] = y;

                                // Iterate counter index
                                addIndex++;
                            }
                        }
                    }
                }
            }

            // If inside the central area of the map, or after adjusting the circle points if there was a spillover via the above if statement
            int numEntries = addIndex;
            // At map point i, pre-allocate an array of appropriate size for the number of circle points
            erosionBrushIndices[i] = new int[numEntries];
            erosionBrushWeights[i] = new float[numEntries];

            // For each entry
            for(int j = 0; j < numEntries; j++)
            {
                // Location of circle point in map space, turned into a 1D index coordinate rather than a 2D one
                erosionBrushIndices[i][j] = (yOffsets[j] + centreY) * mapSize + xOffsets[j] + centreX;
                // What portion of the raindrop hits this point
                erosionBrushWeights[i][j] = weights[j] / weightSum;
            }
        }
    }

    struct HeightAndGradient
    {
        public float height;
        public float gradientX;
        public float gradientY;
    }

}
