using LSBSteganographyDetector.Models;

namespace LSBSteganographyDetector.Services.Geotag
{
    /// <summary>
    /// Processes and analyzes geographical location data from images
    /// </summary>
    public class LocationProcessor : ILocationProcessor
    {
        public string GenerateLocationDescription(double latitude, double longitude)
        {
            // Simple coordinate-based description
            // In a real application, you would use reverse geocoding API here
            var latDirection = latitude >= 0 ? "N" : "S";
            var lngDirection = longitude >= 0 ? "E" : "W";
            
            return $"{Math.Abs(latitude):F4}°{latDirection}, {Math.Abs(longitude):F4}°{lngDirection}";
        }

        public int CalculateUniqueLocations(List<ImageLocationData> locations)
        {
            if (!locations.Any())
                return 0;

            // Group locations that are within ~100 meters of each other
            const double proximityThreshold = 0.001; // Roughly 100 meters
            var clusters = ClusterLocations(locations, proximityThreshold);
            return clusters.Count;
        }

        public List<LocationCluster> ClusterLocations(List<ImageLocationData> locations, double proximityThreshold = 0.001)
        {
            var clusters = new List<LocationCluster>();

            foreach (var location in locations)
            {
                var nearbyCluster = clusters.FirstOrDefault(c => 
                    CalculateDistance(c.CenterLatitude, c.CenterLongitude, location.Latitude, location.Longitude) <= proximityThreshold);

                if (nearbyCluster != null)
                {
                    nearbyCluster.Images.Add(location);
                    // Update cluster center (weighted average)
                    UpdateClusterCenter(nearbyCluster);
                }
                else
                {
                    clusters.Add(new LocationCluster
                    {
                        CenterLatitude = location.Latitude,
                        CenterLongitude = location.Longitude,
                        Images = new List<ImageLocationData> { location }
                    });
                }
            }

            return clusters;
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Simple Euclidean distance for small distances
            // For more accurate results, you could use Haversine formula
            var deltaLat = lat2 - lat1;
            var deltaLon = lon2 - lon1;
            return Math.Sqrt(deltaLat * deltaLat + deltaLon * deltaLon);
        }

        private void UpdateClusterCenter(LocationCluster cluster)
        {
            if (cluster.Images.Any())
            {
                cluster.CenterLatitude = cluster.Images.Average(i => i.Latitude);
                cluster.CenterLongitude = cluster.Images.Average(i => i.Longitude);
            }
        }
    }
} 