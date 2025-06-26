using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using LSBSteganographyDetector.Models;
using LSBSteganographyDetector.Services;

namespace LSBSteganographyDetector.Views
{
    public partial class GeotagAnalysisPage : ContentPage
    {
        private readonly GeotagExtractor _geotagExtractor;
        private string? _selectedPath;
        private bool _isBatchMode;
        private GeotagAnalysisResult? _analysisResult;

        public GeotagAnalysisPage()
        {
            InitializeComponent();
            _geotagExtractor = new GeotagExtractor();
        }

        private async void OnSelectImageClicked(object sender, EventArgs e)
        {
            try
            {
                var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.image" } },
                    { DevicePlatform.Android, new[] { "image/*" } },
                    { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.image" } }
                });

                var result = await FilePicker.PickAsync(new PickOptions
                {
                    FileTypes = fileTypes,
                    PickerTitle = "Select an image file for GPS analysis"
                });

                if (result != null)
                {
                    _selectedPath = result.FullPath;
                    _isBatchMode = false;
                    ShowSelectedPath(Path.GetFileName(result.FullPath), "1 image selected");
                    ShowAnalysisSection();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to select image: {ex.Message}", "OK");
            }
        }

        private async void OnSelectFolderClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FolderPicker.Default.PickAsync(default);
                if (result.IsSuccessful)
                {
                    _selectedPath = result.Folder.Path;
                    _isBatchMode = true;

                    // Count supported image files
                    var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" };
                    var imageFiles = Directory.GetFiles(_selectedPath, "*", SearchOption.TopDirectoryOnly)
                        .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .ToArray();

                    ShowSelectedPath(Path.GetFileName(_selectedPath), $"{imageFiles.Length} images found");
                    ShowAnalysisSection();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to select folder: {ex.Message}", "OK");
            }
        }

        private void ShowSelectedPath(string name, string description)
        {
            SelectedPathLabel.Text = name;
            ImageCountLabel.Text = description;
            PathInfoPanel.IsVisible = true;
        }

        private void ShowAnalysisSection()
        {
            AnalysisFrame.IsVisible = true;
        }

        private async void OnAnalyzeClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedPath))
            {
                await DisplayAlert("Error", "Please select an image or folder first", "OK");
                return;
            }

            try
            {
                // Show progress
                ProgressIndicator.IsRunning = true;
                ProgressPanel.IsVisible = true;
                AnalyzeButton.IsEnabled = false;

                // Perform analysis
                if (_isBatchMode)
                {
                    await AnalyzeBatchFolder();
                }
                else
                {
                    await AnalyzeSingleImage();
                }

                // Show results
                ShowResults();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Analysis Error", $"Failed to analyze GPS data: {ex.Message}", "OK");
            }
            finally
            {
                // Hide progress
                ProgressIndicator.IsRunning = false;
                ProgressPanel.IsVisible = false;
                AnalyzeButton.IsEnabled = true;
            }
        }

        private async Task AnalyzeSingleImage()
        {
            ProgressLabel.Text = "Extracting GPS metadata...";
            _analysisResult = await _geotagExtractor.ExtractGeotagDataAsync(_selectedPath!);
        }

        private async Task AnalyzeBatchFolder()
        {
            var progress = new Progress<GeotagExtractionProgress>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProgressLabel.Text = $"Processing {p.CurrentFile} ({p.ProcessedCount}/{p.TotalCount})";
                });
            });

            _analysisResult = await _geotagExtractor.ExtractBatchGeotagDataAsync(_selectedPath!, progress);
        }

        private void ShowResults()
        {
            if (_analysisResult == null) return;

            // Update statistics
            TotalImagesLabel.Text = _analysisResult.TotalImages.ToString();
            GeotaggedImagesLabel.Text = _analysisResult.GeotaggedImages.ToString();
            LocationsLabel.Text = _analysisResult.UniqueLocations.ToString();

            if (_analysisResult.ImageLocations != null && _analysisResult.ImageLocations.Any())
            {
                // Generate and load map
                LoadInteractiveMap();
            }

            // Show results panel
            ResultsFrame.IsVisible = true;
        }

        private void LoadInteractiveMap()
        {
            if (_analysisResult?.ImageLocations == null || !_analysisResult.ImageLocations.Any())
            {
                return;
            }

            try
            {
                // Configure WebView for better compatibility
                MapWebView.Navigated += OnMapNavigated;
                MapWebView.Navigating += OnMapNavigating;
                
                // Use the new Mapbox GL JS implementation
                var mapHtml = GenerateMapHtml(_analysisResult.ImageLocations);
                
                // Load the map
                MapWebView.Source = new HtmlWebViewSource
                {
                    Html = mapHtml
                };
                
                MapWebView.IsVisible = true;
                MapPlaceholder.IsVisible = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Map loading error: {ex.Message}");
                // Fallback to working map
                LoadWorkingMap();
            }
        }

        private void LoadWorkingMap()
        {
            try
            {
                // Use the main map implementation as fallback
                var mapHtml = GenerateMapHtml(_analysisResult.ImageLocations);
                
                // Load the map
                MapWebView.Source = new HtmlWebViewSource
                {
                    Html = mapHtml
                };
                
                MapWebView.IsVisible = true;
                MapPlaceholder.IsVisible = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Working map fallback error: {ex.Message}");
                ShowMapError();
            }
        }

        private void OnMapNavigating(object sender, WebNavigatingEventArgs e)
        {
            // Allow navigation to Mapbox resources
            if (e.Url.StartsWith("https://api.mapbox.com") || e.Url.StartsWith("https://unpkg.com"))
            {
                e.Cancel = false;
            }
        }

        private void OnMapNavigated(object sender, WebNavigatedEventArgs e)
        {
            if (e.Result != WebNavigationResult.Success)
            {
                ShowMapError();
            }
        }

        private void ShowMapError()
        {
            MapWebView.IsVisible = false;
            MapPlaceholder.IsVisible = true;
            
            // Update placeholder to show error
            var placeholder = MapPlaceholder.Children.OfType<Label>().Skip(1).FirstOrDefault();
            if (placeholder != null)
            {
                placeholder.Text = "Map loading failed. Check internet connection.";
                placeholder.TextColor = Colors.Red;
            }
        }

        private string GenerateMapHtml(List<ImageLocationData> locations, string mapStyle = "streets-v12")
        {
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
                        .coordinates-display {
                            position: absolute;
                            bottom: 20px;
                            left: 20px;
                            background: rgba(0,0,0,0.8);
                            padding: 10px;
                            border-radius: 8px;
                            backdrop-filter: blur(10px);
                            border: 1px solid rgba(255,255,255,0.2);
                            font-family: monospace;
                            font-size: 12px;
                            z-index: 1000;
                        }
                    </style>
                </head>
                <body>
                    <div id="map"></div>
                    <div class="map-info">
                        <div><strong>📍 Location Map</strong></div>
                        <div>Total Locations: <span id="locationCount">0</span></div>
                        <div>Center: <span id="centerCoords">0, 0</span></div>
                    </div>
                    <div class="map-controls">
                        <button onclick="fitAllMarkers()">Fit All</button>
                        <button onclick="clearSelection()">Clear</button>
                    </div>
                    <div class="coordinates-display">
                        <div>Mouse: <span id="mouseCoords">0, 0</span></div>
                        <div>Selected: <span id="selectedCoords">None</span></div>
                    </div>
                    <script>
                """);

            // Add JavaScript for Mapbox initialization
            sb.AppendLine($@"
                        mapboxgl.accessToken = '{GetMapboxToken()}';
                        const map = new mapboxgl.Map({{
                            container: 'map',
                            style: 'mapbox://styles/mapbox/{mapStyle}',
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
                        
                        let selectedMarker = null;
                        
                        // Update info
                        document.getElementById('locationCount').textContent = locations.length;
                        document.getElementById('centerCoords').textContent = '{centerLat:F4}, {centerLng:F4}';
                        
                        // Wait for map to load
                        map.on('load', () => {{
                            console.log('Map loaded successfully');
                            
                            // Add markers
                            locations.forEach(location => {{
                                // Create a DOM element for the marker
                                const el = document.createElement('div');
                                el.className = 'marker';
                                el.style.backgroundImage = 'url(https://docs.mapbox.com/mapbox-gl-js/assets/custom_marker.png)';
                                el.style.width = '25px';
                                el.style.height = '25px';
                                el.style.backgroundSize = '100%';
                                el.style.cursor = 'pointer';
                                el.innerHTML = '<span style=""position: absolute; top: -30px; left: 50%; transform: translateX(-50%); background: rgba(0,0,0,0.8); color: white; padding: 2px 6px; border-radius: 3px; font-size: 10px; white-space: nowrap;"">' + (location.id + 1) + '</span>';
                                
                                // Add marker to map
                                new mapboxgl.Marker(el)
                                    .setLngLat([location.lng, location.lat])
                                    .addTo(map);
                                
                                // Add click handler
                                el.addEventListener('click', () => {{
                                    showPopup(location, el);
                                    if (selectedMarker) selectedMarker.style.filter = '';
                                    el.style.filter = 'brightness(1.5) drop-shadow(0 0 10px #4ecdc4)';
                                    selectedMarker = el;
                                    document.getElementById('selectedCoords').textContent = location.lat.toFixed(4) + ', ' + location.lng.toFixed(4);
                                }});
                            }});
                        }});
                        
                        function showPopup(location, marker) {{
                            const popup = new mapboxgl.Popup({{ closeButton: false }})
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
                                `);
                            popup.addTo(map);
                        }}
                        
                        function fitAllMarkers() {{
                            if (locations.length === 0) return;
                            const bounds = new mapboxgl.LngLatBounds();
                            locations.forEach(location => {{
                                bounds.extend([location.lng, location.lat]);
                            }});
                            map.fitBounds(bounds, {{ padding: 50 }});
                        }}
                        
                        function clearSelection() {{
                            if (selectedMarker) {{
                                selectedMarker.style.filter = '';
                                selectedMarker = null;
                            }}
                            document.getElementById('selectedCoords').textContent = 'None';
                        }}
                        
                        // Mouse coordinate tracking
                        map.on('mousemove', (e) => {{
                            document.getElementById('mouseCoords').textContent = e.lngLat.lat.toFixed(4) + ', ' + e.lngLat.lng.toFixed(4);
                        }});
                        
                        // Error handling
                        map.on('error', (e) => {{
                            console.error('Mapbox error:', e);
                            document.getElementById('centerCoords').textContent = 'Error loading map';
                        }});
                    </script>
                </body>
                </html>
                """);

            return sb.ToString();
        }

        private string GetMapboxToken()
        {
            // Get the Mapbox token from the GeotagExtractor service
            // This is a simple way to access the token - in a production app you might want
            // to use dependency injection or configuration files
            return "pk.eyJ1Ijoid2lsbGlhbXNjaHJpc3RvcGhlcmYiLCJhIjoiY21jZG5lZTNlMGs3MzJpcHkwZDJ5MnJ1MCJ9.PqDnfN0P5WM6R7iho-VMuA";
        }

        private string GenerateFallbackMapHtml(List<ImageLocationData> locations)
        {
            var sb = new StringBuilder();
            
            // Calculate center point
            var centerLat = locations.Average(l => l.Latitude);
            var centerLng = locations.Average(l => l.Longitude);

            sb.AppendLine("""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8">
                    <title>Image Locations Map (Fallback)</title>
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
                    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
                    <style>
                        body { margin: 0; padding: 0; }
                        #map { height: 100vh; width: 100%; }
                        .custom-popup { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
                        .popup-title { font-weight: bold; font-size: 14px; margin-bottom: 5px; }
                        .popup-details { font-size: 12px; color: #666; }
                    </style>
                </head>
                <body>
                    <div id="map"></div>
                    <script>
                """);

            sb.AppendLine($@"
                        var map = L.map('map').setView([{centerLat:F6}, {centerLng:F6}], 10);
                        
                        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
                            attribution: '© OpenStreetMap contributors',
                            maxZoom: 19
                        }}).addTo(map);
                        
                        var markers = [];
                ");

            // Add markers for each location
            for (int i = 0; i < locations.Count; i++)
            {
                var loc = locations[i];
                var popupContent = $@"
                    <div class='custom-popup'>
                        <div class='popup-title'>📸 {loc.FileName}</div>
                        <div class='popup-details'>
                            📍 {loc.Latitude:F6}, {loc.Longitude:F6}<br>
                            🕒 {loc.Timestamp:yyyy-MM-dd HH:mm:ss}<br>
                            📏 {loc.FileSizeBytes / 1024.0:F1} KB
                            {(loc.Altitude.HasValue ? $@"<br>⛰️ {loc.Altitude:F0}m" : "")}
                            {(!string.IsNullOrEmpty(loc.CameraModel) ? $@"<br>📷 {loc.CameraModel}" : "")}
                        </div>
                    </div>";

                sb.AppendLine($@"
                        var marker{i} = L.marker([{loc.Latitude:F6}, {loc.Longitude:F6}])
                            .addTo(map)
                            .bindPopup(`{popupContent}`);
                        markers.push(marker{i});
                    ");
            }

            sb.AppendLine("""
                        
                        // Fit map to show all markers
                        if (markers.length > 1) {
                            var group = new L.featureGroup(markers);
                            map.fitBounds(group.getBounds().pad(0.1));
                        }
                    </script>
                </body>
                </html>
                """);

            return sb.ToString();
        }

        private async void OnExportReportClicked(object sender, EventArgs e)
        {
            if (_analysisResult == null)
            {
                await DisplayAlert("Error", "No analysis results to export", "OK");
                return;
            }

            try
            {
                var report = GenerateLocationReport();
                var fileName = $"GPS_Location_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = Path.Combine(FileSystem.Current.CacheDirectory, fileName);
                
                await File.WriteAllTextAsync(filePath, report);
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Export GPS Location Report",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Error", $"Failed to export report: {ex.Message}", "OK");
            }
        }

        private async void OnExportMapClicked(object sender, EventArgs e)
        {
            if (_analysisResult?.ImageLocations == null || !_analysisResult.ImageLocations.Any())
            {
                await DisplayAlert("No Data", "No GPS data available to export.", "OK");
                return;
            }

            try
            {
                var html = GenerateMapHtml(_analysisResult.ImageLocations);
                var fileName = $"LocationMap_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                
                await File.WriteAllTextAsync(filePath, html);
                await DisplayAlert("Export Complete", $"Map exported to:\n{filePath}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Error", $"Failed to export map: {ex.Message}", "OK");
            }
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
            sb.AppendLine($"Analysis Type: {(_isBatchMode ? "Batch Folder" : "Single Image")}");
            sb.AppendLine($"Source Path: {_selectedPath}");
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
                
                // Add coordinate summary for mapping
                sb.AppendLine();
                sb.AppendLine("COORDINATE SUMMARY (for external mapping):");
                sb.AppendLine("==========================================");
                foreach (var location in _analysisResult.ImageLocations)
                {
                    sb.AppendLine($"{location.FileName},{location.Latitude:F6},{location.Longitude:F6},{location.Timestamp:yyyy-MM-dd HH:mm:ss}");
                }
            }
            else
            {
                sb.AppendLine("📍 NO GPS DATA FOUND");
                sb.AppendLine("No images in the selected location contain GPS metadata.");
                sb.AppendLine("This could mean:");
                sb.AppendLine("• GPS was disabled when photos were taken");
                sb.AppendLine("• Location services were not available");
                sb.AppendLine("• EXIF metadata was stripped from the images");
                sb.AppendLine("• Images are from sources that don't embed GPS data");
            }
            
            sb.AppendLine();
            sb.AppendLine("=====================================");
            sb.AppendLine("Report generated by LSB Steganography Detector v1.0");
            sb.AppendLine("GPS Location Analysis Engine");
            
            return sb.ToString();
        }
    }
} 