using LSBSteganographyDetector.Models;

namespace LSBSteganographyDetector.Services.Geotag
{
    /// <summary>
    /// Interface for processing and analyzing location data
    /// </summary>
    public interface ILocationProcessor
    {
        /// <summary>
        /// Generate a human-readable description for coordinates
        /// </summary>
        /// <param name="latitude">Latitude coordinate</param>
        /// <param name="longitude">Longitude coordinate</param>
        /// <returns>Location description</returns>
        string GenerateLocationDescription(double latitude, double longitude);
        
        /// <summary>
        /// Calculate the number of unique locations from a list of images
        /// </summary>
        /// <param name="locations">List of image location data</param>
        /// <returns>Number of unique location clusters</returns>
        int CalculateUniqueLocations(List<ImageLocationData> locations);
        
        /// <summary>
        /// Cluster nearby locations together
        /// </summary>
        /// <param name="locations">List of image location data</param>
        /// <param name="proximityThreshold">Distance threshold for clustering (degrees)</param>
        /// <returns>List of location clusters</returns>
        List<LocationCluster> ClusterLocations(List<ImageLocationData> locations, double proximityThreshold = 0.001);
    }
} 