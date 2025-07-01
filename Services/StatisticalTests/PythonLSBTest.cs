using LSBSteganographyDetector.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LSBSteganographyDetector.Services.StatisticalTests
{
    /// <summary>
    /// Python LSB Pattern Detection for steganography analysis
    /// Detects Python-style LSB embedding patterns in individual channels
    /// </summary>
    public class PythonLSBTest : IStatisticalTest
    {
        private const double THRESHOLD = 0.60; // Balanced threshold for detecting Python LSB patterns
        
        public string Name => "Python LSB Pattern";
        public double Weight => 1.0; // Moderate weight - specific use case

        public TestResult Execute(Image<Rgb24> image)
        {
            // Improved Python LSB detection - check each channel independently
            var lsbSequenceR = new List<bool>();
            var lsbSequenceG = new List<bool>();
            var lsbSequenceB = new List<bool>();
            
            // Extract LSBs in flatten order (like Python img.flatten())
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        lsbSequenceR.Add((pixel.R & 1) == 1);
                        lsbSequenceG.Add((pixel.G & 1) == 1);
                        lsbSequenceB.Add((pixel.B & 1) == 1);
                    }
                }
            });

            // Check each channel independently for repetition patterns
            var repR = DetectRepetitionPattern(lsbSequenceR);
            var repG = DetectRepetitionPattern(lsbSequenceG);
            var repB = DetectRepetitionPattern(lsbSequenceB);

            // Check for correlation between channels (Python might embed in all channels)
            var corrRG = CalculateBinaryCorrelation(lsbSequenceR, lsbSequenceG);
            var corrRB = CalculateBinaryCorrelation(lsbSequenceR, lsbSequenceB);
            var corrGB = CalculateBinaryCorrelation(lsbSequenceG, lsbSequenceB);

            // Find the strongest signals
            double maxRepetition = Math.Max(repR, Math.Max(repG, repB));
            double maxCorrelation = Math.Max(corrRG, Math.Max(corrRB, corrGB));

            // Combined score - prioritize repetition patterns over correlation
            var combinedScore = (maxRepetition * 0.7) + (maxCorrelation * 0.3);
            
            // Determine which channel(s) show strongest pattern
            string channelInfo = "";
            if (repR == maxRepetition && repR > 0.3)
                channelInfo += "Red ";
            if (repG == maxRepetition && repG > 0.3)
                channelInfo += "Green ";
            if (repB == maxRepetition && repB > 0.3)
                channelInfo += "Blue ";
            
            if (string.IsNullOrEmpty(channelInfo))
                channelInfo = "None";
            else
                channelInfo = channelInfo.Trim() + " channel(s)";

            bool isSuspicious = combinedScore > THRESHOLD;

            return new TestResult
            {
                TestName = Name,
                Score = combinedScore,
                Threshold = THRESHOLD,
                IsSuspicious = isSuspicious,
                Description = "Detects Python-style LSB embedding patterns in individual channels",
                Interpretation = isSuspicious
                    ? $"Python LSB pattern detected in {channelInfo} (repetition: {maxRepetition:F3}, correlation: {maxCorrelation:F3})"
                    : $"No Python LSB pattern detected (repetition: {maxRepetition:F3}, correlation: {maxCorrelation:F3})"
            };
        }

        private double CalculateBinaryCorrelation(List<bool> sequence1, List<bool> sequence2)
        {
            if (sequence1.Count == 0 || sequence2.Count == 0) return 0;
            
            int matches = 0;
            int totalComparisons = Math.Min(sequence1.Count, sequence2.Count);
            
            for (int i = 0; i < totalComparisons; i++)
            {
                if (sequence1[i] == sequence2[i])
                    matches++;
            }
            
            return totalComparisons > 0 ? (double)matches / totalComparisons : 0;
        }

        private double DetectRepetitionPattern(List<bool> bits)
        {
            if (bits.Count < 64) return 0; // Need sufficient data
            
            // Check for repeating 8-bit blocks (character patterns)
            var blockSize = 8;
            var blockCounts = new Dictionary<string, int>();
            var totalBlocks = 0;

            // Extract all possible 8-bit blocks
            for (int i = 0; i <= bits.Count - blockSize; i += blockSize)
            {
                if (i + blockSize <= bits.Count)
                {
                    var block = string.Join("", bits.Skip(i).Take(blockSize).Select(b => b ? "1" : "0"));
                    if (!blockCounts.ContainsKey(block))
                        blockCounts[block] = 0;
                    blockCounts[block]++;
                    totalBlocks++;
                }
            }

            if (totalBlocks == 0) return 0;

            // Calculate repetition score based on most frequent block
            int maxCount = blockCounts.Values.Max();
            double baseScore = (double)maxCount / totalBlocks;

            // Bonus for multiple repeated blocks (stronger pattern)
            var frequentBlocks = blockCounts.Values.Where(count => count > 1).Count();
            double repetitionBonus = Math.Min(0.3, frequentBlocks * 0.1); // Cap bonus at 30%

            // Check for sequential repetition patterns (ABABABAB...)
            double sequentialScore = CheckSequentialRepetition(bits, blockSize);

            // Combine scores
            return Math.Min(1.0, baseScore + repetitionBonus + sequentialScore);
        }

        private double CheckSequentialRepetition(List<bool> bits, int blockSize)
        {
            if (bits.Count < blockSize * 3) return 0; // Need at least 3 repetitions

            var maxSequentialScore = 0.0;

            // Test different block sizes (8, 16, 24, 32 bits)
            for (int testBlockSize = 8; testBlockSize <= 32 && testBlockSize <= bits.Count / 3; testBlockSize += 8)
            {
                var firstBlock = bits.Take(testBlockSize).ToArray();
                int consecutiveMatches = 0;
                int totalChecks = 0;

                // Check how many times this pattern repeats consecutively
                for (int start = testBlockSize; start <= bits.Count - testBlockSize; start += testBlockSize)
                {
                    var currentBlock = bits.Skip(start).Take(testBlockSize).ToArray();
                    
                    if (currentBlock.Length == testBlockSize)
                    {
                        var matches = firstBlock.Zip(currentBlock, (a, b) => a == b).Count(match => match);
                        var matchRatio = (double)matches / testBlockSize;
                        
                        if (matchRatio > 0.8) // 80% similarity
                            consecutiveMatches++;
                        else
                            break; // Pattern broken
                            
                        totalChecks++;
                    }
                }

                if (totalChecks > 0)
                {
                    var sequentialScore = (double)consecutiveMatches / Math.Max(totalChecks, 3);
                    maxSequentialScore = Math.Max(maxSequentialScore, sequentialScore);
                }
            }

            return Math.Min(0.4, maxSequentialScore); // Cap at 40% contribution
        }
    }
} 