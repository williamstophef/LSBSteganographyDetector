using LSBSteganographyDetector.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LSBSteganographyDetector.Services.StatisticalTests
{
    /// <summary>
    /// Entropy Analysis for LSB steganography detection
    /// Measures randomness in LSB plane
    /// </summary>
    public class EntropyTest : IStatisticalTest
    {
        private const double THRESHOLD = 0.995; // Balanced threshold - detects unusually high LSB randomness
        
        public string Name => "Entropy Analysis";
        public double Weight => 0.5; // Low weight - can have false positives

        public TestResult Execute(Image<Rgb24> image)
        {
            // Calculate entropy of LSB plane
            var lsbValues = new List<byte>();
            
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        lsbValues.Add((byte)(pixel.R & 1));
                        lsbValues.Add((byte)(pixel.G & 1));
                        lsbValues.Add((byte)(pixel.B & 1));
                    }
                }
            });

            var entropy = CalculateEntropy(lsbValues);
            bool isSuspicious = entropy > THRESHOLD;

            return new TestResult
            {
                TestName = Name,
                Score = entropy,
                Threshold = THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Measures randomness in LSB plane",
                Interpretation = isSuspicious
                    ? "LSB plane has unusually high entropy (too random)"
                    : "LSB plane entropy is within normal range"
            };
        }

        private double CalculateEntropy(List<byte> values)
        {
            if (values.Count == 0) return 0;
            
            var frequencies = values.GroupBy(v => v)
                                  .ToDictionary(g => g.Key, g => (double)g.Count() / values.Count);
            
            return -frequencies.Values.Sum(p => p * Math.Log2(p));
        }
    }
} 