using LSBSteganographyDetector.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LSBSteganographyDetector.Services.StatisticalTests
{
    /// <summary>
    /// Sample Pair Analysis for LSB steganography detection
    /// Analyzes correlation between adjacent pixel LSBs
    /// </summary>
    public class SamplePairTest : IStatisticalTest
    {
        private const double THRESHOLD = 0.2; // Balanced threshold for detecting correlation anomalies
        
        public string Name => "Sample Pair Analysis";
        public double Weight => 1.0; // Moderate weight - solid foundation but secondary

        public TestResult Execute(Image<Rgb24> image)
        {
            var horizontalPairs = new List<(int, int)>();
            var verticalPairs = new List<(int, int)>();
            
            // Collect horizontal and vertical adjacent pairs
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        var lsb = pixel.R & 1;
                        
                        // Horizontal pairs
                        if (x < row.Length - 1)
                        {
                            var nextPixel = row[x + 1];
                            horizontalPairs.Add((lsb, nextPixel.R & 1));
                        }
                        
                        // Vertical pairs (if not last row)
                        if (y < accessor.Height - 1)
                        {
                            // We'll collect this in the next row iteration
                            // For now, store the current pixel's LSB for comparison
                        }
                    }
                }
            });
            
            // Add vertical pairs
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height - 1; y++)
                {
                    var currentRow = accessor.GetRowSpan(y);
                    var nextRow = accessor.GetRowSpan(y + 1);
                    for (int x = 0; x < currentRow.Length; x++)
                    {
                        verticalPairs.Add((currentRow[x].R & 1, nextRow[x].R & 1));
                    }
                }
            });

            // Combine all pairs
            var allPairs = horizontalPairs.Concat(verticalPairs).ToList();
            
            // Calculate sample pair statistics
            var sameCount = allPairs.Count(p => p.Item1 == p.Item2);
            var totalPairs = allPairs.Count;
            var samePairRatio = (double)sameCount / totalPairs;
            
            // For natural images, should be around 0.5
            // Steganography often creates correlation
            var deviation = Math.Abs(samePairRatio - 0.5);
            bool isSuspicious = deviation > THRESHOLD;

            return new TestResult
            {
                TestName = Name,
                Score = deviation,
                Threshold = THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Analyzes correlation between adjacent pixel LSBs",
                Interpretation = isSuspicious
                    ? $"Unusual correlation in adjacent pixels (ratio: {samePairRatio:F3})"
                    : $"Normal correlation in adjacent pixels (ratio: {samePairRatio:F3})"
            };
        }
    }
} 