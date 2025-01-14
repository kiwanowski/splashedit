using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class ImageQuantizer
{

    public static (ushort[], float[]) Quantize(Texture2D image, int bpp, int maxIterations = 10)
    {
        int width = image.width;
        int height = image.height;

        int maxColors = (int)Math.Pow(bpp, 2);

        List<Vector3> centroids = InitializeCentroids(image, maxColors);

        Color[] pixels = image.GetPixels();
        Vector3[] pixelColors = new Vector3[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixelColors[i] = new Vector3(pixels[i].r, pixels[i].g, pixels[i].b);
        }

        ushort[] assignments = new ushort[pixelColors.Length];

        // Perform k-means clustering
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool centroidsChanged = false;

            for (int i = 0; i < pixelColors.Length; i++)
            {
                ushort closestCentroid = (ushort)GetClosestCentroid(pixelColors[i], centroids);
                if (assignments[i] != closestCentroid)
                {
                    assignments[i] = closestCentroid;
                    centroidsChanged = true;
                }
            }

            Vector3[] newCentroids = new Vector3[centroids.Count];
            int[] centroidCounts = new int[centroids.Count];

            for (int i = 0; i < assignments.Length; i++)
            {
                int centroidIndex = assignments[i];
                newCentroids[centroidIndex] += pixelColors[i];
                centroidCounts[centroidIndex]++;
            }

            for (int i = 0; i < centroids.Count; i++)
            {
                if (centroidCounts[i] > 0)
                {
                    newCentroids[i] /= centroidCounts[i];
                }
                else
                {
                    newCentroids[i] = RandomizeCentroid(image);
                }
            }

            if (!centroidsChanged) break;

            centroids = new List<Vector3>(newCentroids);
        }

        int pixelSize = bpp == 4 ? 4 : bpp == 8 ? 2 : 1;  
        int adjustedWidth = width / pixelSize;  
        ushort[] pixelArray = new ushort[adjustedWidth * height];  

        ushort packIndex = 0;  
        int bitShift = 0;   

        for (int i = 0; i < pixelColors.Length; i++)
        {
            ushort centroidIndex = assignments[i]; 

            // For 4bpp, we need to pack 4 indices into a single integer
            if (bpp == 4)
            {
                pixelArray[packIndex] |= (ushort)(centroidIndex << (bitShift * 4));  // Shift by 4 bits for each index
                bitShift++;

                // Every 4 indices, move to the next position in the pixelArray
                if (bitShift == 4)
                {
                    bitShift = 0;
                    packIndex++;
                }
            }
            // For 8bpp, we need to pack 2 indices into a single integer
            else if (bpp == 8)
            {
                pixelArray[packIndex] |= (ushort)(centroidIndex << (bitShift * 8));  // Shift by 8 bits for each index
                bitShift++;

                // Every 2 indices, move to the next position in the pixelArray
                if (bitShift == 2)
                {
                    bitShift = 0;
                    packIndex++;
                }
            }
            // For 15bpp, just place each index directly (no packing)
            else
            {
                pixelArray[packIndex] = centroidIndex;
                packIndex++;
            }
        }

        // Create the CLUT as a 1D array of RGB values
        int actualColors = centroids.Count;
        float[] clut = new float[actualColors * 3];
        for (int i = 0; i < actualColors; i++)
        {
            clut[i * 3 + 0] = centroids[i].x; // Red
            clut[i * 3 + 1] = centroids[i].y; // Green
            clut[i * 3 + 2] = centroids[i].z; // Blue
        }

        return (pixelArray, clut);
    }

    private static List<Vector3> InitializeCentroids(Texture2D image, int maxColors)
    {
        List<Vector3> centroids = new List<Vector3>();
        Color[] pixels = image.GetPixels();
        HashSet<Vector3> uniqueColors = new HashSet<Vector3>();

        foreach (Color pixel in pixels)
        {
            Vector3 color = new Vector3(pixel.r, pixel.g, pixel.b);
            if (!uniqueColors.Contains(color))
            {
                uniqueColors.Add(color);
                centroids.Add(color);
                if (centroids.Count >= maxColors) break;
            }
        }

        return centroids;
    }

    private static Vector3 RandomizeCentroid(Texture2D image)
    {
        Color randomPixel = image.GetPixel(Random.Range(0, image.width), Random.Range(0, image.height));
        return new Vector3(randomPixel.r, randomPixel.g, randomPixel.b);
    }

    private static int GetClosestCentroid(Vector3 color, List<Vector3> centroids)
    {
        int closestCentroid = 0;
        float minDistanceSq = float.MaxValue;

        for (int i = 0; i < centroids.Count; i++)
        {
            float distanceSq = (color - centroids[i]).sqrMagnitude;
            if (distanceSq < minDistanceSq)
            {
                minDistanceSq = distanceSq;
                closestCentroid = i;
            }
        }

        return closestCentroid;
    }
}
