using LSBSteganographyDetector.Models;
using LSBSteganographyDetector.Services.StatisticalTests;

namespace LSBSteganographyDetector.Services.RiskAssessment
{
    /// <summary>
    /// Weighted risk assessor that calculates overall risk based on test weights
    /// </summary>
    public class WeightedRiskAssessor : IRiskAssessor
    {
        private readonly Dictionary<string, double> _testWeights;

        public WeightedRiskAssessor()
        {
            // Initialize test weights based on reliability and effectiveness
            // Chi-Square and RS Analysis are the gold standard - heavily weighted
            _testWeights = new Dictionary<string, double>
            {
                ["Chi-Square Test"] = 3.0,        // Very high weight - most proven and reliable
                ["RS Analysis (Flipping Mask)"] = 3.0,  // Very high weight - classical method, highly reliable
                ["Sample Pair Analysis"] = 1.0,   // Moderate weight - solid foundation but secondary
                ["Python LSB Pattern"] = 1.0,     // Moderate weight - specific use case
                ["Entropy Analysis"] = 0.5,       // Low weight - can have false positives
                ["Histogram Analysis"] = 0.5      // Low weight - least reliable method
            };
        }

        public void AssessRisk(DetectionResult result, long fileSizeBytes)
        {
            // Calculate weighted score based on suspicious tests
            var suspiciousTests = result.TestResults.Values.Where(t => t.IsSuspicious).ToList();
            var totalWeight = 0.0;
            var weightedScore = 0.0;

            foreach (var test in result.TestResults.Values)
            {
                var weight = GetTestWeight(test.TestName);
                totalWeight += weight;
                
                if (test.IsSuspicious)
                {
                    // Apply full weight for suspicious tests
                    weightedScore += weight;
                }
                else
                {
                    // Apply partial weight based on how close to threshold
                    var proximityFactor = Math.Min(1.0, test.Score / test.Threshold);
                    weightedScore += weight * proximityFactor * 0.3; // Up to 30% for non-suspicious but elevated scores
                }
            }

            // Normalize the weighted score
            var normalizedScore = totalWeight > 0 ? weightedScore / totalWeight : 0;

            // Overall confidence calculation with conservative approach
            result.OverallConfidence = Math.Min(95, normalizedScore * 100); // Cap at 95%

            // Balanced risk level determination
            var chiSquareFlagging = suspiciousTests.Any(t => t.TestName == "Chi-Square Test");
            var rsAnalysisFlagging = suspiciousTests.Any(t => t.TestName == "RS Analysis (Flipping Mask)");
            var bothPremiumTestsFlagging = chiSquareFlagging && rsAnalysisFlagging;
            var anyPremiumTestFlagging = chiSquareFlagging || rsAnalysisFlagging;

            if (bothPremiumTestsFlagging && result.OverallConfidence > 80)
            {
                result.RiskLevel = "Very High";
                result.IsSuspicious = true;
            }
            else if (bothPremiumTestsFlagging && result.OverallConfidence > 65)
            {
                result.RiskLevel = "High";
                result.IsSuspicious = true;
            }
            else if (anyPremiumTestFlagging && result.OverallConfidence > 70)
            {
                result.RiskLevel = "High";
                result.IsSuspicious = true;
            }
            else if (suspiciousTests.Count >= 3 && result.OverallConfidence > 65)
            {
                result.RiskLevel = "High";
                result.IsSuspicious = true;
            }
            else if (suspiciousTests.Count >= 2 && result.OverallConfidence > 55)
            {
                result.RiskLevel = "Medium";
                result.IsSuspicious = false;
            }
            else if (suspiciousTests.Count >= 1 && result.OverallConfidence > 45)
            {
                result.RiskLevel = "Medium";
                result.IsSuspicious = false;
            }
            else
            {
                result.RiskLevel = "Low";
                result.IsSuspicious = false;
            }

            // Generate summary
            GenerateSummary(result, suspiciousTests, fileSizeBytes);
        }

        private double GetTestWeight(string testName)
        {
            return _testWeights.TryGetValue(testName, out var weight) ? weight : 1.0;
        }

        private void GenerateSummary(DetectionResult result, List<TestResult> suspiciousTests, long fileSizeBytes)
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
                
                foreach (var test in suspiciousTests.OrderByDescending(t => GetTestWeight(t.TestName)))
                {
                    summary.Add($"• {test.TestName}: {test.Interpretation}");
                }

                // Add recommendation based on risk level
                switch (result.RiskLevel)
                {
                    case "Very High":
                        summary.Add("\n⚠️ VERY HIGH RISK: Strong evidence of steganography detected.");
                        summary.Add("Recommend immediate investigation and content analysis.");
                        break;
                    case "High":
                        summary.Add("\n⚠️ HIGH RISK: Multiple indicators suggest hidden data.");
                        summary.Add("Manual review recommended.");
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