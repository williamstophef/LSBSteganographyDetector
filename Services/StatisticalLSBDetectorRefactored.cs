using System.Diagnostics;
using LSBSteganographyDetector.Models;
using LSBSteganographyDetector.Services.StatisticalTests;
using LSBSteganographyDetector.Services.RiskAssessment;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SLImage = SixLabors.ImageSharp.Image;

namespace LSBSteganographyDetector.Services
{
    /// <summary>
    /// Refactored Statistical LSB Detector following Single Responsibility Principle
    /// Orchestrates individual statistical tests and risk assessment
    /// </summary>
    public class StatisticalLSBDetectorRefactored : IStatisticalLSBDetector
    {
        private readonly IReadOnlyList<IStatisticalTest> _tests;
        private readonly IRiskAssessor _riskAssessor;

        public StatisticalLSBDetectorRefactored(
            IReadOnlyList<IStatisticalTest> tests,
            IRiskAssessor riskAssessor)
        {
            _tests = tests ?? throw new ArgumentNullException(nameof(tests));
            _riskAssessor = riskAssessor ?? throw new ArgumentNullException(nameof(riskAssessor));
        }

        /// <summary>
        /// Constructor with default test suite
        /// </summary>
        public StatisticalLSBDetectorRefactored() : this(
            new List<IStatisticalTest>
            {
                new ChiSquareTest(),
                new SamplePairTest(),
                new RSAnalysisTest(),
                new EntropyTest(),
                new HistogramTest(),
                new PythonLSBTest()
            },
            new WeightedRiskAssessor())
        {
        }

        public async Task<DetectionResult> DetectLSBAsync(string imagePath)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var image = await SLImage.LoadAsync<Rgb24>(imagePath);
                var fileInfo = new FileInfo(imagePath);
                
                var result = new DetectionResult();
                
                // Run all statistical tests in parallel for better performance
                var testTasks = _tests.Select(test => Task.Run(() => 
                    new { TestName = test.Name, Result = test.Execute(image) }
                )).ToArray();

                var testResults = await Task.WhenAll(testTasks);

                // Collect results
                foreach (var testResult in testResults)
                {
                    result.TestResults[testResult.TestName] = testResult.Result;
                }

                // Calculate overall assessment using risk assessor
                _riskAssessor.AssessRisk(result, fileInfo.Length);
                
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new DetectionResult
                {
                    IsSuspicious = false,
                    OverallConfidence = 0,
                    Summary = $"Error analyzing image: {ex.Message}",
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }
    }
} 