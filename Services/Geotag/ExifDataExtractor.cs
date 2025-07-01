using LSBSteganographyDetector.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SLImage = SixLabors.ImageSharp.Image;

namespace LSBSteganographyDetector.Services.Geotag
{
    /// <summary>
    /// Extracts EXIF metadata from images including GPS, camera info, and timestamps
    /// </summary>
    public class ExifDataExtractor : IExifDataExtractor
    {
        public ExifGpsData? ExtractGpsData(SLImage image)
        {
            var exifProfile = image.Metadata.ExifProfile;
            if (exifProfile == null)
                return null;

            var gpsData = new ExifGpsData();

            try
            {
                // Extract latitude
                if (TryExtractLatitude(exifProfile, out var latitude))
                {
                    gpsData.Latitude = latitude;
                }

                // Extract longitude
                if (TryExtractLongitude(exifProfile, out var longitude))
                {
                    gpsData.Longitude = longitude;
                }

                // Extract altitude
                if (TryExtractAltitude(exifProfile, out var altitude))
                {
                    gpsData.Altitude = altitude;
                }

                // Extract GPS timestamp
                if (TryExtractGpsTimestamp(exifProfile, out var gpsTimestamp))
                {
                    gpsData.GpsTimestamp = gpsTimestamp;
                }

                // Extract additional GPS metadata
                ExtractAdditionalGpsMetadata(exifProfile, gpsData);

                return gpsData.HasGpsData ? gpsData : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public ExifCameraInfo ExtractCameraInfo(SLImage image)
        {
            var cameraInfo = new ExifCameraInfo();
            var exifProfile = image.Metadata.ExifProfile;

            if (exifProfile == null)
                return cameraInfo;

            try
            {
                // Extract camera make and model
                if (exifProfile.TryGetValue(ExifTag.Make, out var makeValue) && makeValue.Value != null)
                {
                    cameraInfo.CameraMake = makeValue.Value.ToString()?.Trim();
                }

                if (exifProfile.TryGetValue(ExifTag.Model, out var modelValue) && modelValue.Value != null)
                {
                    cameraInfo.CameraModel = modelValue.Value.ToString()?.Trim();
                }

                // Extract software
                if (exifProfile.TryGetValue(ExifTag.Software, out var softwareValue) && softwareValue.Value != null)
                {
                    cameraInfo.Software = softwareValue.Value.ToString()?.Trim();
                }

                // Extract focal length
                if (exifProfile.TryGetValue(ExifTag.FocalLength, out var focalValue) && focalValue.Value is Rational focal)
                {
                    cameraInfo.FocalLength = SafeRationalToDouble(focal);
                }

                // Extract ISO
                if (exifProfile.TryGetValue(ExifTag.ISOSpeedRatings, out var isoValue) && isoValue.Value != null)
                {
                    cameraInfo.IsoSpeedRating = isoValue.Value.ToString();
                }

                // Extract exposure time
                if (exifProfile.TryGetValue(ExifTag.ExposureTime, out var exposureValue) && exposureValue.Value is Rational exposure)
                {
                    cameraInfo.ExposureTime = $"1/{(int)(1.0 / SafeRationalToDouble(exposure))}";
                }

                // Extract F-number
                if (exifProfile.TryGetValue(ExifTag.FNumber, out var fnumberValue) && fnumberValue.Value is Rational fnumber)
                {
                    cameraInfo.FNumber = $"f/{SafeRationalToDouble(fnumber):F1}";
                }

                // Extract flash
                if (exifProfile.TryGetValue(ExifTag.Flash, out var flashValue) && flashValue.Value != null)
                {
                    cameraInfo.Flash = flashValue.Value.ToString();
                }

                // Extract orientation
                if (exifProfile.TryGetValue(ExifTag.Orientation, out var orientationValue) && orientationValue.Value is ushort orientation)
                {
                    cameraInfo.Orientation = orientation;
                }

                return cameraInfo;
            }
            catch (Exception)
            {
                return cameraInfo;
            }
        }

        public DateTime? ExtractTimestamp(SLImage image)
        {
            var exifProfile = image.Metadata.ExifProfile;
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
                    var timestamp = timestampValue.Value.ToString() ?? string.Empty;
                        
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

        private bool TryExtractLatitude(ExifProfile exifProfile, out double latitude)
        {
            latitude = 0;
            
            string? gpsLatRef = null;
            Rational[]? gpsLat = null;
            
            if (exifProfile.TryGetValue(ExifTag.GPSLatitudeRef, out var latRefValue) && latRefValue.Value != null)
            {
                gpsLatRef = latRefValue.Value.ToString() ?? string.Empty;
            }
                
            if (exifProfile.TryGetValue(ExifTag.GPSLatitude, out var latValue))
            {
                gpsLat = latValue.Value as Rational[];
            }
            
            if (gpsLat != null && gpsLatRef != null)
            {
                latitude = ConvertDmsToDecimalSafe(gpsLat, gpsLatRef);
                return latitude != 0;
            }

            return false;
        }

        private bool TryExtractLongitude(ExifProfile exifProfile, out double longitude)
        {
            longitude = 0;
            
            string? gpsLngRef = null;
            Rational[]? gpsLng = null;
            
            if (exifProfile.TryGetValue(ExifTag.GPSLongitudeRef, out var lngRefValue) && lngRefValue.Value != null)
            {
                gpsLngRef = lngRefValue.Value.ToString() ?? string.Empty;
            }
                
            if (exifProfile.TryGetValue(ExifTag.GPSLongitude, out var lngValue))
            {
                gpsLng = lngValue.Value as Rational[];
            }
            
            if (gpsLng != null && gpsLngRef != null)
            {
                longitude = ConvertDmsToDecimalSafe(gpsLng, gpsLngRef);
                return longitude != 0;
            }

            return false;
        }

        private bool TryExtractAltitude(ExifProfile exifProfile, out double altitude)
        {
            altitude = 0;
            
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
                altitude = gpsAltRef == 1 ? -altitudeValue : altitudeValue;
                return true;
            }

            return false;
        }

        private bool TryExtractGpsTimestamp(ExifProfile exifProfile, out DateTime gpsTimestamp)
        {
            gpsTimestamp = default;
            
            Rational[]? gpsTimeStamp = null;
            string? gpsDateStamp = null;
            
            if (exifProfile.TryGetValue(ExifTag.GPSTimestamp, out var timeValue))
                gpsTimeStamp = timeValue.Value as Rational[];
            if (exifProfile.TryGetValue(ExifTag.GPSDateStamp, out var dateValue) && dateValue.Value != null)
            {
                gpsDateStamp = dateValue.Value.ToString() ?? string.Empty;
            }
            
            if (gpsTimeStamp != null && !string.IsNullOrEmpty(gpsDateStamp))
            {
                var parsedDateTime = ParseGpsDateTime(gpsDateStamp, gpsTimeStamp);
                if (parsedDateTime.HasValue)
                {
                    gpsTimestamp = parsedDateTime.Value;
                    return true;
                }
            }

            return false;
        }

        private void ExtractAdditionalGpsMetadata(ExifProfile exifProfile, ExifGpsData gpsData)
        {
            // Extract GPS processing method
            if (exifProfile.TryGetValue(ExifTag.GPSProcessingMethod, out var processingValue) && processingValue.Value != null)
            {
                gpsData.ProcessingMethod = processingValue.Value.ToString() ?? string.Empty;
            }

            // Extract map datum
            if (exifProfile.TryGetValue(ExifTag.GPSMapDatum, out var datumValue) && datumValue.Value != null)
            {
                gpsData.MapDatum = datumValue.Value.ToString() ?? string.Empty;
            }

            // Extract GPS accuracy (DOP - Dilution of Precision)
            if (exifProfile.TryGetValue(ExifTag.GPSDOP, out var dopValue) && dopValue.Value is Rational dop)
            {
                gpsData.Accuracy = SafeRationalToDouble(dop);
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
            catch (Exception)
            {
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
            catch (Exception)
            {
                return 0;
            }
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
    }
} 