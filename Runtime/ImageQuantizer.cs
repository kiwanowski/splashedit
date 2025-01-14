using System.Collections.Generic;
using UnityEngine;

public class ImageQuantizer
{
    /// <summary>
    /// Quantizes a texture and outputs a 3D pixel array.
    /// </summary>
    /// <param name="image">The input texture.</param>
    /// <param name="maxColors">The maximum number of colors in the quantized image.</param>
    /// <param name="maxIterations">The maximum number of iterations for the k-means algorithm.</param>
    /// <returns>A tuple containing a 3D pixel array and the color lookup table.</returns>
    public static (float[,,], Vector3[,]) Quantize(Texture2D image, int maxColors, int maxIterations = 10)
    {
        int width = image.width;
        int height = image.height;

        List<Vector3> centroids = InitializeCentroids(image, maxColors);

        Color[] pixels = image.GetPixels();
        Vector3[] pixelColors = new Vector3[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixelColors[i] = new Vector3(pixels[i].r, pixels[i].g, pixels[i].b);
        }

        // Storage for pixel-to-centroid assignments
        int[] assignments = new int[pixelColors.Length];

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool centroidsChanged = false;

            // Step 1: Assign each pixel to the closest centroid
            for (int i = 0; i < pixelColors.Length; i++)
            {
                int closestCentroid = GetClosestCentroid(pixelColors[i], centroids);
                if (assignments[i] != closestCentroid)
                {
                    assignments[i] = closestCentroid;
                    centroidsChanged = true;
                }
            }

            // Step 2: Recalculate centroids
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

        float[,,] pixelArray = new float[width, height, 3];
        for (int i = 0; i < pixelColors.Length; i++)
        {
            int x = i % width;
            int y = i / width;

            Vector3 centroidColor = centroids[assignments[i]];
            pixelArray[x, y, 0] = centroidColor.x; // Red
            pixelArray[x, y, 1] = centroidColor.y; // Green
            pixelArray[x, y, 2] = centroidColor.z; // Blue
        }

        int actualColors = centroids.Count;
        Vector3[,] clut = new Vector3[actualColors, 1];
        for (int i = 0; i < actualColors; i++)
        {
            clut[i, 0] = centroids[i];
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
