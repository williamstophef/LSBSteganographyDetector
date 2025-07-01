using LSBSteganographyDetector.Models;

namespace LSBSteganographyDetector.Services.RiskAssessment
{
    /// <summary>
    /// Interface for assessing overall risk based on statistical test results
    /// </summary>
    public interface IRiskAssessor
    {
        /// <summary>
        /// Calculate overall risk assessment based on test results and file info
        /// </summary>
        /// <param name="result">Detection result to assess</param>
        /// <param name="fileSizeBytes">File size in bytes for context</param>
        void AssessRisk(DetectionResult result, long fileSizeBytes);
    }
} 