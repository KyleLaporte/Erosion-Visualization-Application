using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoiseMapGenerator : MonoBehaviour
{
    public int seed = 0;
    public bool randomizeSeed;
    public int numOctaves = 7;
    [Range(0, 1)]
    public float persistance = 0.4f;
    public float lacunarity = 2f;
    public float initialFrequency = 2f;
    float[] noiseMap1D;
    public float maxHeight = 0f;
    public float minHeight = 0f;
    private bool firstIteration = true;

    public Vector2 offset = Vector2.zero;

    // Generate 3D float array containing noise map
    public float[] BuildNoiseMap(int mapSize)
    {
        // Generate random seed if selected
        if (randomizeSeed)
        {
            seed = Random.Range(-10000, 10000);
        }

        // Set offsets for use in sample locations of perlin noise based on seed offsets
        System.Random pseudoRandom = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[numOctaves];
        for(int i = 0; i < numOctaves; i++)
        {
            float offsetX = pseudoRandom.Next(-100000, 100000) + offset.x;
            float offsetY = pseudoRandom.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        // Populate 2D array with perlin values
        float[,] noiseMap = new float[mapSize, mapSize];
        for (int y = 0; y < mapSize;  y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                // Establish initial conditions for the first octave
                float amplitude = 1;
                float frequency = initialFrequency;
                float noiseHeight = 0;

                // Loop through octaves
                for (int i = 0; i < numOctaves; i++)
                {
                    // Use sample values between 0 and 1 as Perlin Noise repeats on whole numbers; factor in frequency to affect later octaves, and factor in seeded offsets
                    float sampleX = (x / (float)mapSize * frequency) + octaveOffsets[i].x;
                    float sampleY = (y / (float)mapSize * frequency) + octaveOffsets[i].y;

                    // Get Perlin Noise value at the appropriate location
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);

                    // Factor in amplitude's affect on height in the same style as frequency
                    noiseHeight += perlinValue * amplitude;

                    // Scale amplitude and frequency according to persistance and lacunarity for future octave iterations
                    amplitude *= persistance;
                    frequency *= lacunarity;
                }

                // Track max and min for normalization
                if (firstIteration)
                {
                    maxHeight = noiseHeight;
                    minHeight = noiseHeight;
                    firstIteration = false;
                }
                if(noiseHeight > maxHeight)
                {
                    maxHeight = noiseHeight;
                }
                if(noiseHeight < minHeight)
                {
                    minHeight = noiseHeight;
                }

                // Store octave-affected height value into noise map
                noiseMap[x, y] = noiseHeight;
            }
        }
        // Convert 2D noise map into 1D noise map; not sure which I wanted so it's setup to do both (I ran with the 1D array, so that's what I'm using here)
        noiseMap1D = NoiseMap1D(noiseMap, mapSize, maxHeight, minHeight);
        return noiseMap1D;


    }
    float[] NoiseMap1D(float[,] noiseMap, int mapSize, float maxHeight, float minHeight)
    {
        float[] noiseMap1D = new float[mapSize * mapSize];
        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                noiseMap1D[y * mapSize + x] = ((noiseMap[x, y] - minHeight) / (maxHeight - minHeight));
            }
        }

        return noiseMap1D;
    }
}
