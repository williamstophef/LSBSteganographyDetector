using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LSBSteganographyDetector.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SLImage = SixLabors.ImageSharp.Image;

namespace LSBSteganographyDetector.Services
{
    public class GeotagExtractor
    {
        // Mapbox configuration
        private const string MAPBOX_ACCESS_TOKEN = "pk.eyJ1Ijoid2lsbGlhbXNjaHJpc3RvcGhlcmYiLCJhIjoiY21jZG5lZTNlMGs3MzJpcHkwZDJ5MnJ1MCJ9.PqDnfN0P5WM6R7iho-VMuA";
        
        private readonly string[] _supportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" };

        public async Task<GeotagAnalysisResult> ExtractGeotagDataAsync(string imagePath)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new GeotagAnalysisResult
            {
                StartTime = DateTime.Now,
                SourcePath = imagePath,
                IsBatchAnalysis = false,
                TotalImages = 1
            };

            try
            {
                var locationData = await ExtractGpsFromImageAsync(imagePath);
                if (locationData != null)
                {
                    result.ImageLocations.Add(locationData);
                    result.GeotaggedImages = 1;
                    result.UniqueLocations = 1;
                }

                stopwatch.Stop();
                result.EndTime = DateTime.Now;
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.EndTime = DateTime.Now;
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                
                // Log error but return partial results
                System.Diagnostics.Debug.WriteLine($"Error extracting GPS data from {imagePath}: {ex.Message}");
                return result;
            }
        }

        public async Task<GeotagAnalysisResult> ExtractBatchGeotagDataAsync(string folderPath, IProgress<GeotagExtractionProgress>? progress = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new GeotagAnalysisResult
            {
                StartTime = DateTime.Now,
                SourcePath = folderPath,
                IsBatchAnalysis = true
            };

            try
            {
                // Get all supported image files
                var imageFiles = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(file => _supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToArray();

                result.TotalImages = imageFiles.Length;

                if (imageFiles.Length == 0)
                {
                    result.EndTime = DateTime.Now;
                    result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                    return result;
                }

                for (int i = 0; i < imageFiles.Length; i++)
                {
                    var imagePath = imageFiles[i];
                    
                    try
                    {
                        // Report progress
                        progress?.Report(new GeotagExtractionProgress
                        {
                            CurrentFile = Path.GetFileName(imagePath),
                            ProcessedCount = i,
                            TotalCount = imageFiles.Length,
                            GeotaggedCount = result.GeotaggedImages
                        });

                        var locationData = await ExtractGpsFromImageAsync(imagePath);
                        if (locationData != null)
                        {
                            result.ImageLocations.Add(locationData);
                            result.GeotaggedImages++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing
                        System.Diagnostics.Debug.WriteLine($"Error processing {imagePath}: {ex.Message}");
                        
                        progress?.Report(new GeotagExtractionProgress
                        {
                            CurrentFile = Path.GetFileName(imagePath),
                            ProcessedCount = i,
                            TotalCount = imageFiles.Length,
                            GeotaggedCount = result.GeotaggedImages,
                            ErrorMessage = $"Failed to process: {ex.Message}"
                        });
                    }
                }

                // Calculate unique locations (group by approximate coordinates)
                result.UniqueLocations = CalculateUniqueLocations(result.ImageLocations);

                // Final progress report
                progress?.Report(new GeotagExtractionProgress
                {
                    CurrentFile = "Analysis complete",
                    ProcessedCount = imageFiles.Length,
                    TotalCount = imageFiles.Length,
                    GeotaggedCount = result.GeotaggedImages
                });

                stopwatch.Stop();
                result.EndTime = DateTime.Now;
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.EndTime = DateTime.Now;
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                
                throw new InvalidOperationException($"Batch GPS extraction failed: {ex.Message}", ex);
            }
        }

        private async Task<ImageLocationData?> ExtractGpsFromImageAsync(string imagePath)
        {
            try
            {
                using var image = await SLImage.LoadAsync(imagePath);
                var fileInfo = new FileInfo(imagePath);
                
                // Extract GPS data from EXIF
                var gpsData = ExtractGpsFromExif(image);
                
                if (!gpsData.HasGpsData)
                {
                    return null; // No GPS data found
                }

                // Extract additional EXIF metadata
                var exifProfile = image.Metadata.ExifProfile;
                
                string? cameraModel = null;
                if (exifProfile?.TryGetValue(ExifTag.Model, out var modelValue) == true && modelValue.Value != null)
                {
                    cameraModel = modelValue.Value.ToString();
                }
                
                string? cameraMake = null;
                if (exifProfile?.TryGetValue(ExifTag.Make, out var makeValue) == true && makeValue.Value != null)
                {
                    cameraMake = makeValue.Value.ToString();
                }
                
                // Try to get image timestamp
                var timestamp = GetImageTimestamp(exifProfile) ?? fileInfo.CreationTime;

                var locationData = new ImageLocationData
                {
                    FileName = Path.GetFileName(imagePath),
                    FilePath = imagePath,
                    Latitude = gpsData.Latitude!.Value,
                    Longitude = gpsData.Longitude!.Value,
                    Altitude = gpsData.Altitude,
                    Timestamp = timestamp,
                    FileSizeBytes = fileInfo.Length,
                    CameraModel = cameraModel,
                    CameraMake = cameraMake,
                    Accuracy = gpsData.Accuracy,
                    GpsProcessingMethod = gpsData.ProcessingMethod,
                    GpsTimestamp = gpsData.GpsTimestamp
                };

                // Try to get location description (reverse geocoding would go here)
                // For now, we'll use coordinate-based description
                locationData.LocationDescription = GenerateLocationDescription(locationData.Latitude, locationData.Longitude);

                return locationData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract GPS from {imagePath}: {ex.Message}");
                return null;
            }
        }

        private ExifGpsData ExtractGpsFromExif(SixLabors.ImageSharp.Image image)
        {
            var gpsData = new ExifGpsData();
            var exifProfile = image.Metadata.ExifProfile;

            if (exifProfile == null)
                return gpsData;

            try
            {
                // Extract latitude
                string? gpsLatRef = null;
                Rational[]? gpsLat = null;
                
                if (exifProfile.TryGetValue(ExifTag.GPSLatitudeRef, out var latRefValue) && latRefValue.Value != null)
                {
                    gpsLatRef = latRefValue.Value.ToString();
                }
                    
                if (exifProfile.TryGetValue(ExifTag.GPSLatitude, out var latValue))
                {
                    gpsLat = latValue.Value as Rational[];
                    if (gpsLat != null)
                    {
                        for (int i = 0; i < gpsLat.Length; i++)
                        {
                            var rational = gpsLat[i];
                            var doubleVal = SafeRationalToDouble(rational);
                        }
                    }
                }
                
                if (gpsLat != null && gpsLatRef != null)
                {
                    var convertedLat = ConvertDmsToDecimalSafe(gpsLat, gpsLatRef);
                    gpsData.Latitude = convertedLat;
                }

                // Extract longitude
                string? gpsLngRef = null;
                Rational[]? gpsLng = null;
                
                if (exifProfile.TryGetValue(ExifTag.GPSLongitudeRef, out var lngRefValue) && lngRefValue.Value != null)
                {
                    gpsLngRef = lngRefValue.Value.ToString();
                }
                    
                if (exifProfile.TryGetValue(ExifTag.GPSLongitude, out var lngValue))
                {
                    gpsLng = lngValue.Value as Rational[];
                    if (gpsLng != null)
                    {
                        for (int i = 0; i < gpsLng.Length; i++)
                        {
                            var rational = gpsLng[i];
                            var doubleVal = SafeRationalToDouble(rational);
                        }
                    }
                }
                
                if (gpsLng != null && gpsLngRef != null)
                {
                    var convertedLng = ConvertDmsToDecimalSafe(gpsLng, gpsLngRef);
                    gpsData.Longitude = convertedLng;
                }

                // Extract altitude
                byte? gpsAltRef = null;
                Rational? gpsAlt = null;
                
                if (exifProfile.TryGetValue(ExifTag.GPSAltitudeRef, out var altRefValue))
                    gpsAltRef = altRefValue.Value as byte?;
                if (exifProfile.TryGetValue(ExifTag.GPSAltitude, out var altValue))
                    gpsAlt = altValue.Value as Rational?;
                
                if (gpsAlt != null)
                {
                    var altitudeValue = SafeRationalToDouble(gpsAlt.Value);
                    // GPS altitude reference: 0 = above sea level, 1 = below sea level
                    gpsData.Altitude = gpsAltRef == 1 ? -altitudeValue : altitudeValue;
                }

                // Extract GPS timestamp
                Rational[]? gpsTimeStamp = null;
                string? gpsDateStamp = null;
                
                if (exifProfile.TryGetValue(ExifTag.GPSTimestamp, out var timeValue))
                    gpsTimeStamp = timeValue.Value as Rational[];
                if (exifProfile.TryGetValue(ExifTag.GPSDateStamp, out var dateValue) && dateValue.Value != null)
                {
                    gpsDateStamp = dateValue.Value.ToString();
                }
                
                if (gpsTimeStamp != null && !string.IsNullOrEmpty(gpsDateStamp))
                {
                    gpsData.GpsTimestamp = ParseGpsDateTime(gpsDateStamp, gpsTimeStamp);
                }

                // Extract additional GPS metadata
                if (exifProfile.TryGetValue(ExifTag.GPSProcessingMethod, out var processingValue) && processingValue.Value != null)
                {
                    gpsData.ProcessingMethod = processingValue.Value.ToString();
                }

                // Extract map datum
                if (exifProfile.TryGetValue(ExifTag.GPSMapDatum, out var datumValue) && datumValue.Value != null)
                {
                    gpsData.MapDatum = datumValue.Value.ToString();
                }

                return gpsData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting GPS EXIF data: {ex.Message}");
                return gpsData;
            }
        }

        private double SafeRationalToDouble(Rational rational)
        {
            try
            {
                if (rational.Denominator == 0)
                {
                    return 0.0;
                }
                
                var result = (double)rational.Numerator / rational.Denominator;
                
                if (double.IsNaN(result) || double.IsInfinity(result))
                {
                    return 0.0;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error converting rational {rational.Numerator}/{rational.Denominator}: {ex.Message}");
                return 0.0;
            }
        }

        private double ConvertDmsToDecimalSafe(Rational[] dms, string reference)
        {
            try
            {
                if (dms == null || dms.Length != 3)
                {
                    return 0;
                }

                var degrees = SafeRationalToDouble(dms[0]);
                var minutes = SafeRationalToDouble(dms[1]);
                var seconds = SafeRationalToDouble(dms[2]);

                var decimal_degrees = degrees + (minutes / 60.0) + (seconds / 3600.0);

                // Apply direction reference
                if (reference == "S" || reference == "W")
                {
                    decimal_degrees = -decimal_degrees;
                }

                // Sanity check
                if (Math.Abs(decimal_degrees) > 180)
                {
                    return 0;
                }

                return decimal_degrees;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DMS conversion: {ex.Message}");
                return 0;
            }
        }

        private DateTime? GetImageTimestamp(ExifProfile? exifProfile)
        {
            if (exifProfile == null)
                return null;

            // Try different timestamp fields in order of preference
            var timestampTags = new[]
            {
                ExifTag.DateTimeOriginal,    // When the photo was taken
                ExifTag.DateTimeDigitized,   // When it was digitized
                ExifTag.DateTime             // When it was modified
            };

            foreach (var tag in timestampTags)
            {
                if (exifProfile.TryGetValue(tag, out var timestampValue) && timestampValue.Value != null)
                {
                    var timestamp = timestampValue.Value.ToString();
                        
                    if (!string.IsNullOrEmpty(timestamp))
                    {
                        if (DateTime.TryParseExact(timestamp, "yyyy:MM:dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var dateTime))
                        {
                            return dateTime;
                        }
                    }
                }
            }

            return null;
        }

        private DateTime? ParseGpsDateTime(string dateStamp, Rational[] timeStamp)
        {
            try
            {
                if (timeStamp.Length != 3)
                    return null;

                var hours = (int)timeStamp[0].ToDouble();
                var minutes = (int)timeStamp[1].ToDouble();
                var seconds = (int)timeStamp[2].ToDouble();

                // GPS date format is typically "YYYY:MM:DD"
                var dateParts = dateStamp.Split(':');
                if (dateParts.Length != 3)
                    return null;

                var year = int.Parse(dateParts[0]);
                var month = int.Parse(dateParts[1]);
                var day = int.Parse(dateParts[2]);

                return new DateTime(year, month, day, hours, minutes, seconds, DateTimeKind.Utc);
            }
            catch
            {
                return null;
            }
        }

        private string GenerateLocationDescription(double latitude, double longitude)
        {
            // Simple coordinate-based description
            // In a real application, you would use reverse geocoding API here
            var latDirection = latitude >= 0 ? "N" : "S";
            var lngDirection = longitude >= 0 ? "E" : "W";
            
            return $"{Math.Abs(latitude):F4}°{latDirection}, {Math.Abs(longitude):F4}°{lngDirection}";
        }

        private int CalculateUniqueLocations(List<ImageLocationData> locations)
        {
            if (!locations.Any())
                return 0;

            // Group locations that are within ~100 meters of each other
            const double proximityThreshold = 0.001; // Roughly 100 meters
            var clusters = new List<LocationCluster>();

            foreach (var location in locations)
            {
                var nearbyCluster = clusters.FirstOrDefault(c => 
                    Math.Abs(c.CenterLatitude - location.Latitude) < proximityThreshold &&
                    Math.Abs(c.CenterLongitude - location.Longitude) < proximityThreshold);

                if (nearbyCluster != null)
                {
                    nearbyCluster.Images.Add(location);
                    // Update cluster center (average)
                    nearbyCluster.CenterLatitude = nearbyCluster.Images.Average(i => i.Latitude);
                    nearbyCluster.CenterLongitude = nearbyCluster.Images.Average(i => i.Longitude);
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

            return clusters.Count;
        }
    }
} 