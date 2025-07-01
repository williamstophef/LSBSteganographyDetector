using LSBSteganographyDetector.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LSBSteganographyDetector.Services.StatisticalTests
{
    /// <summary>
    /// Chi-Square Test for LSB randomness detection
    /// Tests if LSB distribution deviates from expected randomness
    /// </summary>
    public class ChiSquareTest : IStatisticalTest
    {
        private const double THRESHOLD = 18.0; // Balanced threshold - catches steganography while minimizing false positives
        
        public string Name => "Chi-Square Test";
        public double Weight => 3.0; // Very high weight - most proven and reliable

        public TestResult Execute(Image<Rgb24> image)
        {
            var lsbCounts = new int[2]; // 0s and 1s in LSB
            var totalSamples = 0;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        
                        // Check LSB of each color channel
                        lsbCounts[pixel.R & 1]++;
                        lsbCounts[pixel.G & 1]++;
                        lsbCounts[pixel.B & 1]++;
                        totalSamples += 3;
                    }
                }
            });

            // Expected frequency (should be roughly equal for natural images)
            double expected = totalSamples / 2.0;
            
            // Calculate chi-square statistic
            double chiSquare = 0;
            foreach (var count in lsbCounts)
            {
                double diff = count - expected;
                chiSquare += (diff * diff) / expected;
            }

            bool isSuspicious = chiSquare > THRESHOLD;

            return new TestResult
            {
                TestName = Name,
                Score = chiSquare,
                Threshold = THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Tests if LSB distribution deviates from expected randomness",
                Interpretation = isSuspicious 
                    ? "LSB distribution is significantly non-random, suggesting steganography"
                    : "LSB distribution appears random and natural"
            };
        }
    }
} 