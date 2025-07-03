using LSBSteganographyDetector.Models;
using LSBSteganographyDetector.Services.StatisticalTests;

namespace LSBSteganographyDetector.Services.RiskAssessment
{
    /// <summary>
    /// Weighted risk assessor that calculates overall risk based on test weights
    /// Enhanced to account for extreme deviations in test scores
    /// </summary>
    public class WeightedRiskAssessor : IRiskAssessor
    {
        private readonly Dictionary<string, double> _baseTestWeights;

        public WeightedRiskAssessor()
        {
            // Base weights for each test
            _baseTestWeights = new Dictionary<string, double>
            {
                ["Chi-Square Test"] = 4.0,        // Highest base weight - most proven and reliable
                ["RS Analysis (Flipping Mask)"] = 4.0,  // Highest base weight - classical method, highly reliable
                ["Sample Pair Analysis"] = 1.5,   // Moderate weight - solid foundation
                ["Python LSB Pattern"] = 1.5,     // Moderate weight - specific use case but effective
                ["Entropy Analysis"] = 0.6,       // Lower weight - can have false positives
                ["Histogram Analysis"] = 1.0      // Moderate weight - useful when combined with others
            };
        }

        public void AssessRisk(DetectionResult result, long fileSizeBytes)
        {
            // Calculate enhanced weighted score with magnitude bonuses
            var suspiciousTests = result.TestResults.Values.Where(t => t.IsSuspicious).ToList();
            var totalWeight = 0.0;
            var weightedScore = 0.0;

            foreach (var test in result.TestResults.Values)
            {
                var baseWeight = GetBaseTestWeight(test.TestName);
                var enhancedWeight = CalculateEnhancedWeight(test, baseWeight);
                totalWeight += enhancedWeight;
                
                if (test.IsSuspicious)
                {
                    // Apply enhanced weight for suspicious tests
                    weightedScore += enhancedWeight;
                }
                else
                {
                    // Apply partial weight based on how close to threshold
                    var proximityFactor = Math.Min(1.0, test.Score / test.Threshold);
                    weightedScore += enhancedWeight * proximityFactor * 0.25; // Up to 25% for non-suspicious but elevated scores
                }
            }

            // Normalize the weighted score
            var normalizedScore = totalWeight > 0 ? weightedScore / totalWeight : 0;

            // Enhanced confidence calculation
            result.OverallConfidence = Math.Min(98, normalizedScore * 100); // Cap at 98%

            // Enhanced risk level determination with lower thresholds for extreme cases
            var chiSquareFlagging = suspiciousTests.Any(t => t.TestName == "Chi-Square Test");
            var rsAnalysisFlagging = suspiciousTests.Any(t => t.TestName == "RS Analysis (Flipping Mask)");
            var samplePairFlagging = suspiciousTests.Any(t => t.TestName == "Sample Pair Analysis");
            var pythonLSBFlagging = suspiciousTests.Any(t => t.TestName == "Python LSB Pattern");
            var histogramFlagging = suspiciousTests.Any(t => t.TestName == "Histogram Analysis");
            var entropyFlagging = suspiciousTests.Any(t => t.TestName == "Entropy Analysis");

            var bothPremiumTestsFlagging = chiSquareFlagging && rsAnalysisFlagging;
            var anyPremiumTestFlagging = chiSquareFlagging || rsAnalysisFlagging;
            var reliableTestsFlagging = chiSquareFlagging || rsAnalysisFlagging || samplePairFlagging || pythonLSBFlagging;
            var onlyUnreliableTestsFlagging = suspiciousTests.Count > 0 && !reliableTestsFlagging;

            // Check for extreme Chi-Square deviations
            var chiSquareTest = result.TestResults.Values.FirstOrDefault(t => t.TestName == "Chi-Square Test");
            var hasExtremeChiSquare = chiSquareTest != null && chiSquareTest.IsSuspicious && 
                                     (chiSquareTest.Score / chiSquareTest.Threshold) > 100; // 100x threshold

            // Check for extreme RS Analysis deviations  
            var rsTest = result.TestResults.Values.FirstOrDefault(t => t.TestName == "RS Analysis (Flipping Mask)");
            var hasExtremeRS = rsTest != null && rsTest.IsSuspicious && 
                              (rsTest.Score / rsTest.Threshold) > 5; // 5x threshold

            if (hasExtremeChiSquare || hasExtremeRS)
            {
                // Extreme deviation detected - very high confidence in steganography
                result.RiskLevel = "Very High";
                result.IsSuspicious = true;
                result.OverallConfidence = Math.Max(result.OverallConfidence, 85);
            }
            else if (bothPremiumTestsFlagging && result.OverallConfidence > 70)
            {
                result.RiskLevel = "Very High";
                result.IsSuspicious = true;
            }
            else if (bothPremiumTestsFlagging && result.OverallConfidence > 55)
            {
                result.RiskLevel = "High";
                result.IsSuspicious = true;
            }
            else if (anyPremiumTestFlagging && result.OverallConfidence > 60)
            {
                result.RiskLevel = "High";
                result.IsSuspicious = true;
            }
            else if (suspiciousTests.Count >= 4 && reliableTestsFlagging && result.OverallConfidence > 55)
            {
                // 4+ tests flagging with at least one reliable test should be high risk
                result.RiskLevel = "High";
                result.IsSuspicious = true;
            }
            else if (suspiciousTests.Count >= 3 && reliableTestsFlagging && result.OverallConfidence > 50)
            {
                result.RiskLevel = "High";
                result.IsSuspicious = true;
            }
            else if (suspiciousTests.Count >= 3 && result.OverallConfidence > 65)
            {
                // 3+ tests but no reliable ones need higher confidence
                result.RiskLevel = "Medium";
                result.IsSuspicious = false;
            }
            else if (suspiciousTests.Count >= 2 && reliableTestsFlagging && result.OverallConfidence > 50)
            {
                // 2+ tests with reliable ones flagging
                result.RiskLevel = "Medium";
                result.IsSuspicious = false;
            }
            else if (suspiciousTests.Count >= 2 && result.OverallConfidence > 60)
            {
                // 2+ unreliable tests need higher confidence
                result.RiskLevel = "Medium";
                result.IsSuspicious = false;
            }
            else if (anyPremiumTestFlagging && result.OverallConfidence > 45)
            {
                // Single premium test flagging with decent confidence
                result.RiskLevel = "Medium";
                result.IsSuspicious = false;
            }
            else if (onlyUnreliableTestsFlagging && result.OverallConfidence > 70)
            {
                // Only unreliable tests (entropy/histogram) flagging - need very high confidence
                result.RiskLevel = "Medium";
                result.IsSuspicious = false;
            }
            else
            {
                result.RiskLevel = "Low";
                result.IsSuspicious = false;
            }

            // Generate enhanced summary
            GenerateEnhancedSummary(result, suspiciousTests, fileSizeBytes);
        }

        private double GetBaseTestWeight(string testName)
        {
            return _baseTestWeights.TryGetValue(testName, out var weight) ? weight : 1.0;
        }

        private double CalculateEnhancedWeight(TestResult test, double baseWeight)
        {
            if (!test.IsSuspicious || test.Threshold <= 0)
                return baseWeight;

            // Calculate magnitude of deviation
            var deviationRatio = test.Score / test.Threshold;

            // Apply magnitude bonus for extreme deviations
            double magnitudeBonus = 1.0;
            
            if (test.TestName == "Chi-Square Test")
            {
                // Chi-Square gets significant bonuses for extreme deviations
                if (deviationRatio > 1000) magnitudeBonus = 3.0;      // 1000x+ threshold
                else if (deviationRatio > 100) magnitudeBonus = 2.5;  // 100x+ threshold  
                else if (deviationRatio > 10) magnitudeBonus = 2.0;   // 10x+ threshold
                else if (deviationRatio > 3) magnitudeBonus = 1.5;    // 3x+ threshold
            }
            else if (test.TestName == "RS Analysis (Flipping Mask)")
            {
                // RS Analysis gets bonuses for extreme deviations
                if (deviationRatio > 10) magnitudeBonus = 2.5;        // 10x+ threshold
                else if (deviationRatio > 5) magnitudeBonus = 2.0;    // 5x+ threshold
                else if (deviationRatio > 2) magnitudeBonus = 1.5;    // 2x+ threshold
            }
            else
            {
                // Other tests get modest bonuses
                if (deviationRatio > 5) magnitudeBonus = 1.8;         // 5x+ threshold
                else if (deviationRatio > 2) magnitudeBonus = 1.4;    // 2x+ threshold
            }

            return baseWeight * magnitudeBonus;
        }

        private void GenerateEnhancedSummary(DetectionResult result, List<TestResult> suspiciousTests, long fileSizeBytes)
        {
            var summary = new List<string>();
            
            if (suspiciousTests.Count == 0)
            {
                summary.Add("No statistical anomalies detected.");
                summary.Add("Image appears to contain no hidden data.");
            }
            else
            {
                summary.Add($"Detected {suspiciousTests.Count} statistical anomal{(suspiciousTests.Count == 1 ? "y" : "ies")}:");
                
                // Sort by weight (enhanced weight for better prioritization)
                var prioritizedTests = suspiciousTests.OrderByDescending(t => 
                {
                    var baseWeight = GetBaseTestWeight(t.TestName);
                    return CalculateEnhancedWeight(t, baseWeight);
                }).ToList();

                foreach (var test in prioritizedTests)
                {
                    var deviationRatio = test.Threshold > 0 ? test.Score / test.Threshold : 1;
                    var severity = "";
                    
                    if (deviationRatio > 100) severity = " (EXTREME deviation)";
                    else if (deviationRatio > 10) severity = " (Very high deviation)";
                    else if (deviationRatio > 3) severity = " (High deviation)";
                    
                    summary.Add($"• {test.TestName}: {test.Interpretation}{severity}");
                }

                // Enhanced recommendations based on risk level
                switch (result.RiskLevel)
                {
                    case "Very High":
                        summary.Add("\n🚨 VERY HIGH RISK: Strong evidence of steganography detected.");
                        summary.Add("Immediate investigation recommended - likely contains hidden data.");
                        break;
                    case "High":
                        summary.Add("\n⚠️ HIGH RISK: Multiple indicators suggest hidden data.");
                        summary.Add("Manual review and deeper analysis recommended.");
                        break;
                    case "Medium":
                        summary.Add("\n⚠️ MEDIUM RISK: Some statistical irregularities detected.");
                        summary.Add("Consider additional analysis or monitoring.");
                        break;
                }
            }

            // Add file size context for small files
            var fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);
            if (fileSizeMB < 0.5 && suspiciousTests.Count > 0)
            {
                summary.Add($"\nNote: Small file size ({fileSizeMB:F1}MB) may affect test accuracy.");
            }

            result.Summary = string.Join("\n", summary);
        }
    }
} 