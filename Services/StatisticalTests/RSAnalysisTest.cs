using LSBSteganographyDetector.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LSBSteganographyDetector.Services.StatisticalTests
{
    /// <summary>
    /// RS (Regular/Singular) Analysis for LSB steganography detection
    /// Classical implementation based on Fridrich's methodology
    /// </summary>
    public class RSAnalysisTest : IStatisticalTest
    {
        private const double THRESHOLD = 0.12; // Balanced threshold - detects steganography with reasonable sensitivity
        
        public string Name => "RS Analysis (Flipping Mask)";
        public double Weight => 3.0; // Very high weight - classical method, highly reliable

        public TestResult Execute(Image<Rgb24> image)
        {
            // Extract pixel values from red channel (most commonly used for LSB)
            var pixels = new List<byte>();
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        pixels.Add(row[x].R);
                    }
                }
            });

            if (pixels.Count < 4)
            {
                return new TestResult
                {
                    TestName = Name,
                    Score = 0,
                    Threshold = THRESHOLD,
                    IsSuspicious = false,
                    Description = "Classical RS analysis using dual flipping masks",
                    Interpretation = "Insufficient data for RS analysis"
                };
            }

            // Use both positive and negative masks as per classical RS analysis
            var positiveMask = new int[] { 0, 1, 1, 0 };
            var negativeMask = new int[] { 1, 0, 0, 1 };

            var posResult = CalculateRSStatistics(pixels, positiveMask);
            var negResult = CalculateRSStatistics(pixels, negativeMask);

            // Calculate RS discrimination
            var rsDiscrimination = CalculateDiscrimination(posResult, negResult);
            
            // Estimate message length using classical RS equation
            var estimatedPayload = EstimateMessageLength(posResult, negResult, pixels.Count);

            // Balanced detection - flag if either condition shows strong evidence
            bool isSuspicious = rsDiscrimination > THRESHOLD || estimatedPayload > 0.03; // Either condition sufficient

            return new TestResult
            {
                TestName = Name,
                Score = rsDiscrimination,
                Threshold = THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Classical RS analysis using dual flipping masks",
                Interpretation = isSuspicious
                    ? $"RS analysis detects steganography: estimated {estimatedPayload:P1} payload, discrimination {rsDiscrimination:F4}"
                    : $"RS analysis shows normal patterns: estimated {estimatedPayload:P1} payload, discrimination {rsDiscrimination:F4}"
            };
        }

        private RSStatistics CalculateRSStatistics(List<byte> pixels, int[] mask)
        {
            var stats = new RSStatistics();
            const int groupSize = 4;

            for (int i = 0; i <= pixels.Count - groupSize; i += groupSize)
            {
                var group = pixels.Skip(i).Take(groupSize).ToArray();
                
                // Calculate variation function for original group
                var originalVariation = CalculateVariationFunction(group);
                
                // Apply flipping mask to create modified group
                var flippedGroup = new byte[groupSize];
                for (int j = 0; j < groupSize; j++)
                {
                    if (mask[j] == 1)
                    {
                        // Flip LSB
                        flippedGroup[j] = (byte)(group[j] ^ 1);
                    }
                    else
                    {
                        flippedGroup[j] = group[j];
                    }
                }
                
                // Calculate variation function for flipped group
                var flippedVariation = CalculateVariationFunction(flippedGroup);
                
                // Classify groups based on variation change
                if (flippedVariation > originalVariation)
                {
                    stats.RegularGroups++;
                }
                else if (flippedVariation < originalVariation)
                {
                    stats.SingularGroups++;
                }
                else
                {
                    stats.UnusableGroups++;
                }

                stats.TotalGroups++;
            }

            return stats;
        }

        private double CalculateVariationFunction(byte[] group)
        {
            // Variation function: sum of absolute differences between adjacent pixels
            // This measures local smoothness - steganography tends to increase variation
            double variation = 0;
            for (int i = 0; i < group.Length - 1; i++)
            {
                variation += Math.Abs(group[i] - group[i + 1]);
            }
            return variation;
        }

        private double CalculateDiscrimination(RSStatistics posStats, RSStatistics negStats)
        {
            if (posStats.TotalGroups == 0 || negStats.TotalGroups == 0)
                return 0;

            // Calculate relative frequencies
            var posRegularRatio = (double)posStats.RegularGroups / posStats.TotalGroups;
            var posSingularRatio = (double)posStats.SingularGroups / posStats.TotalGroups;
            
            var negRegularRatio = (double)negStats.RegularGroups / negStats.TotalGroups;
            var negSingularRatio = (double)negStats.SingularGroups / negStats.TotalGroups;

            // RS discrimination: difference between positive and negative mask responses
            var regularDiff = Math.Abs(posRegularRatio - negRegularRatio);
            var singularDiff = Math.Abs(posSingularRatio - negSingularRatio);
            
            // Return the maximum difference as discrimination measure
            return Math.Max(regularDiff, singularDiff);
        }

        private double EstimateMessageLength(RSStatistics posStats, RSStatistics negStats, int totalPixels)
        {
            if (posStats.TotalGroups == 0 || negStats.TotalGroups == 0)
                return 0;

            // Classical RS message length estimation
            var rPos = (double)posStats.RegularGroups / posStats.TotalGroups;
            var sPos = (double)posStats.SingularGroups / posStats.TotalGroups;
            var rNeg = (double)negStats.RegularGroups / negStats.TotalGroups;
            var sNeg = (double)negStats.SingularGroups / negStats.TotalGroups;

            // Check for expected relationship: R_M > R_{-M} and S_M < S_{-M} for steganography
            var deltaR = rPos - rNeg;
            var deltaS = sPos - sNeg;

            // Simple linear approximation for message length
            // In clean images: deltaR ≈ 0, deltaS ≈ 0
            // In stego images: deltaR > 0, deltaS < 0
            var lengthIndicator = Math.Max(deltaR, -deltaS);
            
            // Convert to percentage (empirically tuned)
            var estimatedLength = Math.Max(0, lengthIndicator * 2); // Scale factor
            
            return Math.Min(estimatedLength, 1.0); // Cap at 100%
        }
    }

    /// <summary>
    /// Helper class for RS Analysis statistics
    /// </summary>
    internal class RSStatistics
    {
        public int RegularGroups { get; set; }
        public int SingularGroups { get; set; }
        public int UnusableGroups { get; set; }
        public int TotalGroups { get; set; }
    }
} 