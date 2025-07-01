using LSBSteganographyDetector.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LSBSteganographyDetector.Services.StatisticalTests
{
    /// <summary>
    /// Interface for all statistical LSB detection tests following the Strategy pattern
    /// </summary>
    public interface IStatisticalTest
    {
        /// <summary>
        /// Name of the test for identification and reporting
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Execute the statistical test on the provided image
        /// </summary>
        /// <param name="image">Image to analyze</param>
        /// <returns>Test result with score, threshold, and interpretation</returns>
        TestResult Execute(Image<Rgb24> image);
        
        /// <summary>
        /// Weight of this test in overall risk calculation (0.0 to 1.0+)
        /// Higher weight means more reliable for steganography detection
        /// </summary>
        double Weight { get; }
    }
} 