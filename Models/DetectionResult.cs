using System;
using System.Collections.Generic;

namespace LSBSteganographyDetector.Models
{
    public class DetectionResult
    {
        public bool IsSuspicious { get; set; }
        public double OverallConfidence { get; set; }
        public string RiskLevel { get; set; } = "Low";
        public Dictionary<string, TestResult> TestResults { get; set; } = new();
        public string Summary { get; set; } = "";
        public long ProcessingTimeMs { get; set; }
    }

    public class BatchDetectionResult
    {
        public string ImagePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public DetectionResult Result { get; set; } = new();
        public long FileSizeBytes { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.Now;
    }

    public class BatchProcessingSummary
    {
        public int TotalImages { get; set; }
        public int ProcessedImages { get; set; }
        public int HighRiskImages { get; set; }
        public int MediumRiskImages { get; set; }
        public int LowRiskImages { get; set; }
        public List<BatchDetectionResult> HighRiskResults { get; set; } = new();
        public List<BatchDetectionResult> MediumRiskResults { get; set; } = new();
        public List<BatchDetectionResult> LowRiskResults { get; set; } = new();
        public List<BatchDetectionResult> AllResults { get; set; } = new();
        public long TotalProcessingTimeMs { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class BatchProcessingProgress
    {
        public string CurrentFile { get; set; } = "";
        public int ProcessedCount { get; set; }
        public int TotalCount { get; set; }
        public int HighRiskCount { get; set; }
        public string CurrentFileRisk { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public double ProgressPercentage => TotalCount > 0 ? (double)ProcessedCount / TotalCount * 100 : 0;
    }

    public class TestResult
    {
        public string TestName { get; set; } = "";
        public double Score { get; set; }
        public double Threshold { get; set; }
        public bool IsSuspicious { get; set; }
        public string Description { get; set; } = "";
        public string Interpretation { get; set; } = "";
    }

    public class ImageStats
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public long TotalPixels { get; set; }
        public double FileSize { get; set; }
        public string Format { get; set; } = "";
        public double BitsPerPixel { get; set; }
    }
} 