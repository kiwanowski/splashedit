using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Codice.CM.Common;
using DataStructures.ViliWonka.KDTree;
using UnityEngine;



namespace PSXSplash.RuntimeCode
{

    public class ImageQuantizer
    {
        private int _maxColors;
        private Vector3[,] _pixels;
        private Vector3[] _centroids;
        private KDTree kdTree;
        private int[,] _assignments;
        private List<Vector3> _uniqueColors;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public Vector3[] Palette
        {
            get => _centroids;
        }

        public int[,] Pixels
        {
            get => _assignments;
        }


        public void Quantize(Texture2D texture2D, int maxColors)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Color[] pixels = texture2D.GetPixels();

            Width = texture2D.width;
            Height = texture2D.height;

            _pixels = new Vector3[Width, Height];

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Color pixel = pixels[x + y * Width];
                    Vector3 pixelAsVector = new Vector3(pixel.r, pixel.g, pixel.b);
                    _pixels[x, y] = pixelAsVector;
                }
            }

            _maxColors = maxColors;
            _centroids = new Vector3[_maxColors];
            _uniqueColors = new List<Vector3>();


            FillRandomCentroids();

            bool hasChanged;
            _assignments = new int[Width, Height];

            do
            {
                hasChanged = false;
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        Vector3 color = _pixels[x, y];
                        int newAssignment = GetNearestCentroid(color);
                        
                        if (_assignments[x, y] != newAssignment)
                        {
                            _assignments[x, y] = newAssignment;
                            hasChanged = true;
                        }
                    }
                }
                RecalculateCentroids();
            } while (hasChanged);

            stopwatch.Stop();

            UnityEngine.Debug.Log($"Quantization completed in {stopwatch.ElapsedMilliseconds} ms");

        }

        private void FillRandomCentroids()
        {

            List<Vector3> uniqueColors = new List<Vector3>();
            foreach (Vector3 pixel in _pixels)
            {
                if (!uniqueColors.Contains(pixel))
                {
                    _uniqueColors.Add(pixel);
                }
            }

            for (int i = 0; i < _maxColors; i++)
            {
                Vector3 color = _uniqueColors[UnityEngine.Random.Range(0, _uniqueColors.Count - 1)];
                _centroids[i] = color;
            }

            kdTree = new KDTree(_centroids);

        }

        private int GetNearestCentroid(Vector3 color)
        {
            KDQuery query = new KDQuery();
            List<int> resultIndices = new List<int>();
            query.ClosestPoint(kdTree, color, resultIndices);
            return resultIndices[0];
        }

        private void RecalculateCentroids()
        {
            Vector3[] newCentroids = new Vector3[_maxColors];


            for(int i = 0; i < _maxColors; i++) 
            {
                List<Vector3> clusterColors = new List<Vector3>();
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        {
                            if (_assignments[x, y] == i)
                            {
                                clusterColors.Add(_pixels[x, y]);
                            }
                        }
                    }
                }

                Vector3 newCentroid;

                try
                {
                    newCentroid = AverageColor(clusterColors);
                }
                catch (InvalidOperationException)
                {
                    System.Random random = new System.Random();
                    newCentroid = _uniqueColors[random.Next(0, _uniqueColors.Count - 1)];
                }

                newCentroids[i] = newCentroid;
            }

            _centroids = newCentroids;

            kdTree = new KDTree(_centroids);

        }

        private Vector3 AverageColor(List<Vector3> colors)
        {
            float r = colors.Average(c => c.x);
            float g = colors.Average(c => c.y);
            float b = colors.Average(c => c.z);
            return new Vector3(r, g, b);
        }
    }
}
