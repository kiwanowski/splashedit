using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;



namespace PSXSplash.RuntimeCode
{
    public class ImageQuantizer
    {
        private int _maxColors;
        private Color[] _pixels;
        private Color[] _centroids;
        private int[] _assignments;
        private List<Color> _uniqueColors;

        public Color[] Palette
        {
            get => _centroids;
        }

        public int[] Pixels
        {
            get => _assignments;
        }


        public void Quantize(Texture2D texture2D, int maxColors)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            _pixels = texture2D.GetPixels();
            _maxColors = maxColors;
            _centroids = new Color[_maxColors];
            _uniqueColors = new List<Color>();


            FillRandomCentroids();

            bool hasChanged;
            _assignments = new int[_pixels.Count()];

            do
            {
                hasChanged = false;
                Parallel.For(0, _pixels.Count(), i =>
                {
                    int newAssignment = GetNearestCentroid(_pixels[i]);

                    if (_assignments[i] != newAssignment)
                    {
                        lock (_assignments)
                        {
                            _assignments[i] = newAssignment;
                        }

                        lock (this)
                        {
                            hasChanged = true;
                        }
                    }
                });

                RecalculateCentroids();
            } while (hasChanged);

            stopwatch.Stop();

            UnityEngine.Debug.Log($"Quantization completed in {stopwatch.ElapsedMilliseconds} ms");

        }

        private void FillRandomCentroids()
        {
            foreach (Color pixel in _pixels)
            {
                if (!_uniqueColors.Contains(pixel))
                {
                    _uniqueColors.Add(pixel);
                }
            }

            for (int i = 0; i < _maxColors; i++)
            {
                _centroids[i] = _uniqueColors[UnityEngine.Random.Range(0, _uniqueColors.Count - 1)];
            }

        }

        private double CalculateColorDistance(Color color1, Color color2)
        {
            float rDiff = color1.r - color2.r;
            float gDiff = color1.g - color2.g;
            float bDiff = color1.b - color2.b;

            return Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
        }

        private int GetNearestCentroid(Color color)
        {
            double minDistance = double.MaxValue;
            int closestCentroidIndex = 0;

            for (int i = 0; i < _maxColors; i++)
            {
                double distance = CalculateColorDistance(_centroids[i], color);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestCentroidIndex = i;
                }
            }

            return closestCentroidIndex;
        }

        private void RecalculateCentroids()
        {
            Color[] newCentroids = new Color[_maxColors];


            Parallel.For(0, _maxColors, i =>
            {
                List<Color> clusterColors = new List<Color>();
                for (int j = 0; j < _pixels.Length; j++)
                {
                    if (_assignments[j] == i)
                    {
                        clusterColors.Add(_pixels[j]);
                    }
                }

                Color newCentroid;

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
            });

            _centroids = newCentroids;
        }

        private Color AverageColor(List<Color> colors)
        {
            float r = colors.Average(c => c.r);
            float g = colors.Average(c => c.g);
            float b = colors.Average(c => c.b);
            float a = colors.Average(c => c.a);
            return new Color(r, g, b, a);
        }
    }
}
