using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using LSBSteganographyDetector.Models;
using LSBSteganographyDetector.Services;
using LSBSteganographyDetector.Services.Geotag;
using System.Text;

namespace LSBSteganographyDetector.ViewModels
{
    public class GeotagAnalysisViewModel : BaseViewModel
    {
        private readonly IGeotagExtractor _geotagExtractor;
        private readonly IGeotagBatchProcessor _batchProcessor;
        
        // Properties
        private string? _selectedPath;
        private bool _isBatchMode;
        private bool _isAnalyzing;
        private bool _hasResults;
        private bool _showAnalysisSection;
        private string _progressText = string.Empty;
        private string _selectedPathDisplay = string.Empty;
        private string _imageCountText = string.Empty;
        private int _totalImages;
        private int _geotaggedImages;
        private int _uniqueLocations;
        private string _mapHtml = string.Empty;
        private bool _showMap;
        private GeotagAnalysisResult? _analysisResult;

        public GeotagAnalysisViewModel()
        {
            _geotagExtractor = new GeotagExtractorRefactored();
            _batchProcessor = new GeotagBatchProcessor(_geotagExtractor, new ImageFileHandler(), new LocationProcessor());
            
            // Initialize commands
            SelectImageCommand = new Command(async () => await SelectImageAsync());
            SelectFolderCommand = new Command(async () => await SelectFolderAsync());
            AnalyzeCommand = new Command(async () => await AnalyzeAsync(), () => !IsAnalyzing && !string.IsNullOrEmpty(SelectedPath));
            ExportReportCommand = new Command(async () => await ExportReportAsync(), () => HasResults);
            ExportMapCommand = new Command(async () => await ExportMapAsync(), () => HasResults && _analysisResult?.ImageLocations?.Any() == true);
        }

        #region Properties

        public string? SelectedPath
        {
            get => _selectedPath;
            private set
            {
                _selectedPath = value;
                OnPropertyChanged();
                ((Command)AnalyzeCommand).ChangeCanExecute();
            }
        }

        public bool IsBatchMode
        {
            get => _isBatchMode;
            private set
            {
                _isBatchMode = value;
                OnPropertyChanged();
            }
        }

        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            private set
            {
                _isAnalyzing = value;
                OnPropertyChanged();
                ((Command)AnalyzeCommand).ChangeCanExecute();
            }
        }

        public bool HasResults
        {
            get => _hasResults;
            private set
            {
                _hasResults = value;
                OnPropertyChanged();
                ((Command)ExportReportCommand).ChangeCanExecute();
                ((Command)ExportMapCommand).ChangeCanExecute();
            }
        }

        public bool ShowAnalysisSection
        {
            get => _showAnalysisSection;
            private set
            {
                _showAnalysisSection = value;
                OnPropertyChanged();
            }
        }

        public string ProgressText
        {
            get => _progressText;
            private set
            {
                _progressText = value;
                OnPropertyChanged();
            }
        }

        public string SelectedPathDisplay
        {
            get => _selectedPathDisplay;
            private set
            {
                _selectedPathDisplay = value;
                OnPropertyChanged();
            }
        }

        public string ImageCountText
        {
            get => _imageCountText;
            private set
            {
                _imageCountText = value;
                OnPropertyChanged();
            }
        }

        public int TotalImages
        {
            get => _totalImages;
            private set
            {
                _totalImages = value;
                OnPropertyChanged();
            }
        }

        public int GeotaggedImages
        {
            get => _geotaggedImages;
            private set
            {
                _geotaggedImages = value;
                OnPropertyChanged();
            }
        }

        public int UniqueLocations
        {
            get => _uniqueLocations;
            private set
            {
                _uniqueLocations = value;
                OnPropertyChanged();
            }
        }

        public string MapHtml
        {
            get => _mapHtml;
            private set
            {
                _mapHtml = value;
                OnPropertyChanged();
            }
        }

        public bool ShowMap
        {
            get => _showMap;
            private set
            {
                _showMap = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Commands

        public ICommand SelectImageCommand { get; }
        public ICommand SelectFolderCommand { get; }
        public ICommand AnalyzeCommand { get; }
        public ICommand ExportReportCommand { get; }
        public ICommand ExportMapCommand { get; }

        #endregion

        #region Methods

        private async Task SelectImageAsync()
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select an image file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" } }
                    })
                });

                if (result != null)
                {
                    SelectedPath = result.FullPath;
                    IsBatchMode = false;
                    UpdateSelectedPathDisplay(Path.GetFileName(result.FullPath), "Single image selected for GPS analysis");
                    ShowAnalysisSection = true;
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", $"Failed to select image: {ex.Message}", "OK");
                }
            }
        }

        private async Task SelectFolderAsync()
        {
            try
            {
                var result = await FolderPicker.Default.PickAsync(default);
                if (result.IsSuccessful && !string.IsNullOrEmpty(result.Folder?.Path))
                {
                    SelectedPath = result.Folder.Path;
                    IsBatchMode = true;
                    
                    // Count supported image files
                    var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" };
                    var imageFiles = Directory.GetFiles(result.Folder.Path)
                        .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .ToArray();
                    
                    UpdateSelectedPathDisplay(Path.GetFileName(result.Folder.Path), $"Folder with {imageFiles.Length} image files");
                    ShowAnalysisSection = true;
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", $"Failed to select folder: {ex.Message}", "OK");
                }
            }
        }

        private async Task AnalyzeAsync()
        {
            if (string.IsNullOrEmpty(SelectedPath))
                return;

            try
            {
                IsAnalyzing = true;
                HasResults = false;
                ShowMap = false;

                if (IsBatchMode)
                {
                    await AnalyzeBatchFolderAsync();
                }
                else
                {
                    await AnalyzeSingleImageAsync();
                }

                ShowResults();
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Analysis Error", $"Failed to analyze: {ex.Message}", "OK");
                }
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        private async Task AnalyzeSingleImageAsync()
        {
            ProgressText = "Extracting GPS metadata...";
            _analysisResult = await _geotagExtractor.ExtractGeotagDataAsync(SelectedPath!);
        }

        private async Task AnalyzeBatchFolderAsync()
        {
            var progress = new Progress<GeotagExtractionProgress>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProgressText = $"Processing {p.CurrentFile} ({p.ProcessedCount}/{p.TotalCount})";
                });
            });

            _analysisResult = await _batchProcessor.ExtractBatchGeotagDataAsync(SelectedPath!, progress);
        }

        private void ShowResults()
        {
            if (_analysisResult == null) return;

            // Update statistics
            TotalImages = _analysisResult.TotalImages;
            GeotaggedImages = _analysisResult.GeotaggedImages;
            UniqueLocations = _analysisResult.UniqueLocations;

            if (_analysisResult.ImageLocations != null && _analysisResult.ImageLocations.Any())
            {
                // Generate map HTML
                GenerateMapHtml();
            }

            HasResults = true;
        }

        private void GenerateMapHtml()
        {
            if (_analysisResult?.ImageLocations == null || !_analysisResult.ImageLocations.Any())
                return;

            var locations = _analysisResult.ImageLocations;
            var sb = new StringBuilder();
            
            // Calculate center point
            var centerLat = locations.Average(l => l.Latitude);
            var centerLng = locations.Average(l => l.Longitude);

            sb.AppendLine("""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8">
                    <title>Mapbox GL JS map</title>
                    <meta name="viewport" content="initial-scale=1,maximum-scale=1,user-scalable=no">
                    <link href="https://api.mapbox.com/mapbox-gl-js/v3.12.0/mapbox-gl.css" rel="stylesheet">
                    <script src="https://api.mapbox.com/mapbox-gl-js/v3.12.0/mapbox-gl.js"></script>
                    <style>
                        body { 
                            margin: 0; 
                            padding: 0; 
                            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                            background: #1a1a2e;
                            color: white;
                        }
                        #map { 
                            position: absolute; 
                            top: 0; 
                            bottom: 0; 
                            width: 100%; 
                        }
                        .map-info {
                            position: absolute;
                            top: 20px;
                            left: 20px;
                            background: rgba(0,0,0,0.8);
                            padding: 15px;
                            border-radius: 8px;
                            backdrop-filter: blur(10px);
                            border: 1px solid rgba(255,255,255,0.2);
                            z-index: 1000;
                        }
                        .map-controls {
                            position: absolute;
                            top: 20px;
                            right: 20px;
                            background: rgba(0,0,0,0.8);
                            padding: 10px;
                            border-radius: 8px;
                            backdrop-filter: blur(10px);
                            border: 1px solid rgba(255,255,255,0.2);
                            z-index: 1000;
                        }
                        .map-controls button {
                            background: #4ecdc4;
                            color: white;
                            border: none;
                            padding: 8px 12px;
                            border-radius: 4px;
                            cursor: pointer;
                            margin: 2px;
                            font-size: 12px;
                        }
                        .map-controls button:hover {
                            background: #45b7aa;
                        }
                    </style>
                </head>
                <body>
                    <div id="map"></div>
                    <div class="map-info">
                        <div><strong>📍 Location Map</strong></div>
                        <div>Total Locations: {{locationCount}}</div>
                        <div>Center: {{centerCoords}}</div>
                    </div>
                    <div class="map-controls">
                        <button onclick="fitAllMarkers()">Fit All</button>
                        <button onclick="clearSelection()">Clear</button>
                    </div>
                    <script>
                """);

            // Add JavaScript for Mapbox initialization
            sb.AppendLine($@"
                        mapboxgl.accessToken = 'pk.eyJ1Ijoid2lsbGlhbXNjaHJpc3RvcGhlcmYiLCJhIjoiY21jZG5lZTNlMGs3MzJpcHkwZDJ5MnJ1MCJ9.PqDnfN0P5WM6R7iho-VMuA';
                        const map = new mapboxgl.Map({{
                            container: 'map',
                            style: 'mapbox://styles/mapbox/streets-v12',
                            center: [{centerLng:F6}, {centerLat:F6}],
                            zoom: 10
                        }});
                        
                        const locations = [
                ");

            // Add location data
            for (int i = 0; i < locations.Count; i++)
            {
                var loc = locations[i];
                sb.AppendLine($@"
                            {{
                                id: {i},
                                name: '{loc.FileName}',
                                lat: {loc.Latitude:F6},
                                lng: {loc.Longitude:F6},
                                timestamp: '{loc.Timestamp:yyyy-MM-dd HH:mm:ss}',
                                size: '{loc.FileSizeBytes / 1024.0:F1} KB',
                                altitude: {(loc.Altitude.HasValue ? $"'{loc.Altitude:F0}m'" : "null")},
                                camera: {(string.IsNullOrEmpty(loc.CameraModel) ? "null" : $"'{loc.CameraModel}'")}
                            }},
                ");
            }

            sb.AppendLine($@"
                        ];
                        
                        document.querySelector('.map-info').innerHTML = `
                            <div><strong>📍 Location Map</strong></div>
                            <div>Total Locations: ${{locations.length}}</div>
                            <div>Center: {centerLat:F4}, {centerLng:F4}</div>
                        `;
                        
                        // Wait for map to load
                        map.on('load', () => {{
                            locations.forEach(location => {{
                                const el = document.createElement('div');
                                el.style.backgroundImage = 'url(https://docs.mapbox.com/mapbox-gl-js/assets/custom_marker.png)';
                                el.style.width = '25px';
                                el.style.height = '25px';
                                el.style.backgroundSize = '100%';
                                el.style.cursor = 'pointer';
                                
                                new mapboxgl.Marker(el)
                                    .setLngLat([location.lng, location.lat])
                                    .addTo(map);
                                
                                el.addEventListener('click', () => {{
                                    new mapboxgl.Popup({{ closeButton: false }})
                                        .setLngLat([location.lng, location.lat])
                                        .setHTML(`
                                            <div style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;"">
                                                <div style=""font-weight: bold; font-size: 14px; margin-bottom: 8px; color: #4ecdc4;"">📸 ${{location.name}}</div>
                                                <div style=""font-size: 12px; line-height: 1.4;"">
                                                    📍 ${{location.lat.toFixed(6)}}, ${{location.lng.toFixed(6)}}<br>
                                                    🕒 ${{location.timestamp}}<br>
                                                    📏 ${{location.size}}
                                                    ${{location.altitude ? '<br>⛰️ ' + location.altitude : ''}}
                                                    ${{location.camera ? '<br>📷 ' + location.camera : ''}}
                                                </div>
                                            </div>
                                        `)
                                        .addTo(map);
                                }});
                            }});
                        }});
                        
                        function fitAllMarkers() {{
                            if (locations.length === 0) return;
                            const bounds = new mapboxgl.LngLatBounds();
                            locations.forEach(location => {{
                                bounds.extend([location.lng, location.lat]);
                            }});
                            map.fitBounds(bounds, {{ padding: 50 }});
                        }}
                        
                        function clearSelection() {{
                            // Clear any existing popups
                            document.querySelectorAll('.mapboxgl-popup').forEach(popup => popup.remove());
                        }}
                    </script>
                </body>
                </html>
                """);

            MapHtml = sb.ToString();
            ShowMap = true;
        }

        private async Task ExportReportAsync()
        {
            if (_analysisResult == null)
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", "No analysis results to export", "OK");
                }
                return;
            }

            try
            {
                var report = GenerateLocationReport();
                var fileName = $"GPS_Location_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                
                await File.WriteAllTextAsync(filePath, report);
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Export Complete", $"Report exported to:\n{filePath}", "OK");
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Export Error", $"Failed to export report: {ex.Message}", "OK");
                }
            }
        }

        private async Task ExportMapAsync()
        {
            if (_analysisResult?.ImageLocations == null || !_analysisResult.ImageLocations.Any())
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("No Data", "No GPS data available to export.", "OK");
                }
                return;
            }

            try
            {
                var fileName = $"LocationMap_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                
                await File.WriteAllTextAsync(filePath, MapHtml);
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Export Complete", $"Map exported to:\n{filePath}", "OK");
                }
            }
            catch (Exception ex)
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert("Export Error", $"Failed to export map: {ex.Message}", "OK");
                }
            }
        }

        private void UpdateSelectedPathDisplay(string name, string description)
        {
            SelectedPathDisplay = name;
            ImageCountText = description;
        }

        private string GenerateLocationReport()
        {
            if (_analysisResult == null) return "";

            var sb = new StringBuilder();
            
            sb.AppendLine("=====================================");
            sb.AppendLine("    GPS LOCATION ANALYSIS REPORT");
            sb.AppendLine("=====================================");
            sb.AppendLine();
            sb.AppendLine($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Analysis Type: {(IsBatchMode ? "Batch Folder" : "Single Image")}");
            sb.AppendLine($"Source Path: {SelectedPath}");
            sb.AppendLine();
            
            sb.AppendLine("SUMMARY:");
            sb.AppendLine($"  Total Images Processed: {_analysisResult.TotalImages}");
            sb.AppendLine($"  Images with GPS Data: {_analysisResult.GeotaggedImages}");
            sb.AppendLine($"  Unique Locations: {_analysisResult.UniqueLocations}");
            
            if (_analysisResult.GeotaggedImages > 0)
            {
                var geotagPercentage = (double)_analysisResult.GeotaggedImages / _analysisResult.TotalImages * 100;
                sb.AppendLine($"  GPS Coverage: {geotagPercentage:F1}%");
            }
            
            sb.AppendLine();
            
            if (_analysisResult.ImageLocations.Any())
            {
                sb.AppendLine("DETAILED LOCATION DATA:");
                sb.AppendLine("=====================================");
                
                foreach (var location in _analysisResult.ImageLocations.OrderBy(l => l.Timestamp))
                {
                    sb.AppendLine();
                    sb.AppendLine($"📸 File: {location.FileName}");
                    sb.AppendLine($"   Path: {location.FilePath}");
                    sb.AppendLine($"   Size: {location.FileSizeBytes / 1024.0:F1} KB");
                    sb.AppendLine($"   📍 Coordinates: {location.Latitude:F6}, {location.Longitude:F6}");
                    sb.AppendLine($"   🕒 Timestamp: {location.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    
                    if (!string.IsNullOrEmpty(location.LocationDescription))
                    {
                        sb.AppendLine($"   🏠 Location: {location.LocationDescription}");
                    }
                    
                    if (location.Altitude.HasValue)
                    {
                        sb.AppendLine($"   ⛰️ Altitude: {location.Altitude:F1} meters");
                    }
                }
            }
            else
            {
                sb.AppendLine("📍 NO GPS DATA FOUND");
                sb.AppendLine("No images in the selected location contain GPS metadata.");
            }
            
            sb.AppendLine();
            sb.AppendLine("=====================================");
            sb.AppendLine("Report generated by LSB Steganography Detector v1.0");
            
            return sb.ToString();
        }

        #endregion
    }
} 