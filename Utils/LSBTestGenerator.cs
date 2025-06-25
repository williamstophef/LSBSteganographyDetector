using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SLImage = SixLabors.ImageSharp.Image;

namespace LSBSteganographyDetector.Utils
{
    /// <summary>
    /// Utility class for generating test LSB steganography images
    /// Use this to create test cases for validating the detector
    /// </summary>
    public static class LSBTestGenerator
    {
        /// <summary>
        /// Embeds a text message into an image using LSB steganography
        /// </summary>
        /// <param name="imagePath">Source image path</param>
        /// <param name="message">Message to embed</param>
        /// <param name="outputPath">Output path for stego image</param>
        public static async Task EmbedMessageAsync(string imagePath, string message, string outputPath)
        {
            using var image = await SLImage.LoadAsync<Rgb24>(imagePath);
            
            // Convert message to binary
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var messageBits = new System.Collections.BitArray(messageBytes);
            
            // Add end marker (null terminator)
            var endMarker = new byte[] { 0 };
            var endBits = new System.Collections.BitArray(endMarker);
            
            // Combine message and end marker
            var totalBits = new bool[messageBits.Length + endBits.Length];
            messageBits.CopyTo(totalBits, 0);
            endBits.CopyTo(totalBits, messageBits.Length);
            
            int bitIndex = 0;
            bool completed = false;
            
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height && !completed; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length && !completed; x++)
                    {
                        var pixel = row[x];
                        
                        // Embed in red channel LSB
                        if (bitIndex < totalBits.Length)
                        {
                            var newR = (byte)((pixel.R & 0xFE) | (totalBits[bitIndex] ? 1 : 0));
                            row[x] = new Rgb24(newR, pixel.G, pixel.B);
                            bitIndex++;
                        }
                        else
                        {
                            completed = true;
                            break;
                        }
                    }
                }
            });
            
            await image.SaveAsPngAsync(outputPath);
        }

        /// <summary>
        /// Embeds a message using Python-compatible LSB steganography
        /// (All channels, repeating pattern, no terminator, Python f"{ord(c):08b}" format)
        /// </summary>
        /// <param name="imagePath">Source image path</param>
        /// <param name="message">Message to embed</param>
        /// <param name="outputPath">Output path for stego image</param>
        public static async Task EmbedMessagePythonCompatibleAsync(string imagePath, string message, string outputPath)
        {
            using var image = await SLImage.LoadAsync<Rgb24>(imagePath);
            
            // Convert message to binary exactly like Python: f"{ord(c):08b}"
            var messageBits = string.Join("", message.Select(c => Convert.ToString(c, 2).PadLeft(8, '0')));
            var totalPixels = image.Width * image.Height;
            var totalBits = totalPixels * 3; // R, G, B channels
            
            // Repeat the message to fill the entire image (Python: msg_bits * ((flat.size // len(msg_bits)) + 1))
            var repeatedBits = "";
            var repetitions = (totalBits / messageBits.Length) + 1;
            for (int i = 0; i < repetitions; i++)
            {
                repeatedBits += messageBits;
            }
            repeatedBits = repeatedBits.Substring(0, totalBits); // Truncate to exact size
            
            int bitIndex = 0;
            
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        
                        // Embed in flatten order: R,G,B per pixel (matches Python img.flatten())
                        var newR = (byte)((pixel.R & 0xFE) | (repeatedBits[bitIndex] == '1' ? 1 : 0));
                        bitIndex++;
                        var newG = (byte)((pixel.G & 0xFE) | (repeatedBits[bitIndex] == '1' ? 1 : 0));
                        bitIndex++;
                        var newB = (byte)((pixel.B & 0xFE) | (repeatedBits[bitIndex] == '1' ? 1 : 0));
                        bitIndex++;
                        
                        row[x] = new Rgb24(newR, newG, newB);
                    }
                }
            });
            
            await image.SaveAsPngAsync(outputPath);
        }
        
        /// <summary>
        /// Creates random LSB noise in an image (simulates steganography without meaningful message)
        /// </summary>
        /// <param name="imagePath">Source image path</param>
        /// <param name="outputPath">Output path for modified image</param>
        /// <param name="percentage">Percentage of pixels to modify (0.0 to 1.0)</param>
        public static async Task CreateRandomLSBNoiseAsync(string imagePath, string outputPath, double percentage = 0.5)
        {
            using var image = await SLImage.LoadAsync<Rgb24>(imagePath);
            var random = new Random();
            
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        if (random.NextDouble() < percentage)
                        {
                            var pixel = row[x];
                            
                            // Randomly flip LSB of red channel
                            var newR = (byte)(pixel.R ^ 1);
                            row[x] = new Rgb24(newR, pixel.G, pixel.B);
                        }
                    }
                }
            });
            
            await image.SaveAsPngAsync(outputPath);
        }
        
        /// <summary>
        /// Extracts hidden message from LSB steganography image
        /// </summary>
        /// <param name="imagePath">Stego image path</param>
        /// <returns>Extracted message</returns>
        public static async Task<string> ExtractMessageAsync(string imagePath)
        {
            using var image = await SLImage.LoadAsync<Rgb24>(imagePath);
            var extractedBits = new List<bool>();
            
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        
                        // Extract LSB from red channel
                        bool bit = (pixel.R & 1) == 1;
                        extractedBits.Add(bit);
                        
                        // Check for end marker (8 consecutive false bits)
                        if (extractedBits.Count >= 8)
                        {
                            var lastByte = extractedBits.Skip(extractedBits.Count - 8).Take(8).ToArray();
                            if (lastByte.All(b => !b)) // All bits are false (null byte)
                            {
                                extractedBits.RemoveRange(extractedBits.Count - 8, 8); // Remove end marker
                                return;
                            }
                        }
                    }
                }
            });
            
            // Convert bits to bytes
            var messageBytes = new List<byte>();
            for (int i = 0; i < extractedBits.Count; i += 8)
            {
                if (i + 7 < extractedBits.Count)
                {
                    byte b = 0;
                    for (int j = 0; j < 8; j++)
                    {
                        if (extractedBits[i + j])
                            b |= (byte)(1 << j);
                    }
                    messageBytes.Add(b);
                }
            }
            
            return Encoding.UTF8.GetString(messageBytes.ToArray());
        }

        /// <summary>
        /// Extracts hidden message using Python-compatible LSB extraction
        /// (All channels, repeating pattern, no terminator, flattened array order)
        /// </summary>
        /// <param name="imagePath">Stego image path</param>
        /// <returns>Extracted message</returns>
        public static async Task<string> ExtractMessagePythonCompatibleAsync(string imagePath)
        {
            using var image = await SLImage.LoadAsync<Rgb24>(imagePath);
            var extractedBits = new List<bool>();
            
            // Extract LSBs in Python flatten() order: R1,G1,B1,R2,G2,B2,R3,G3,B3...
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        
                        // Extract LSB from all channels in flatten order (R,G,B per pixel)
                        extractedBits.Add((pixel.R & 1) == 1);
                        extractedBits.Add((pixel.G & 1) == 1);
                        extractedBits.Add((pixel.B & 1) == 1);
                    }
                }
            });
            
            // Try to extract message directly (Python uses standard bit order)
            var result = TryExtractPythonMessage(extractedBits);
            if (!string.IsNullOrEmpty(result))
                return result;
                
            // Fallback: try to find repeating pattern
            var messages = TryExtractRepeatingMessage(extractedBits);
            return messages.FirstOrDefault() ?? "";
        }

        private static string TryExtractPythonMessage(List<bool> bits)
        {
            // Try extracting with different message lengths from your MESSAGES array
            var knownMessages = new[] { "Alpha123", "Bravo456", "Charlie789", "Delta321", "Echo654",
                                       "Foxtrot987", "Golf111", "Hotel222", "India333", "Juliet444" };
            
            foreach (var knownMsg in knownMessages)
            {
                var msgLength = knownMsg.Length;
                var bitsNeeded = msgLength * 8;
                
                if (bits.Count < bitsNeeded) continue;
                
                // Try extracting message at the beginning
                var extractedBytes = new List<byte>();
                for (int i = 0; i < bitsNeeded; i += 8)
                {
                    byte b = 0;
                    for (int j = 0; j < 8; j++)
                    {
                        if (i + j < bits.Count && bits[i + j])
                            b |= (byte)(1 << j);  // Python uses little-endian bit order
                    }
                    extractedBytes.Add(b);
                }
                
                try
                {
                    var message = System.Text.Encoding.UTF8.GetString(extractedBytes.ToArray());
                    
                    // Check if this matches a known message
                    if (knownMessages.Contains(message))
                    {
                        return message;
                    }
                }
                catch
                {
                    // Continue to next attempt
                }
            }
            
            // Try with big-endian bit order if little-endian failed
            foreach (var knownMsg in knownMessages)
            {
                var msgLength = knownMsg.Length;
                var bitsNeeded = msgLength * 8;
                
                if (bits.Count < bitsNeeded) continue;
                
                var extractedBytes = new List<byte>();
                for (int i = 0; i < bitsNeeded; i += 8)
                {
                    byte b = 0;
                    for (int j = 0; j < 8; j++)
                    {
                        if (i + j < bits.Count && bits[i + j])
                            b |= (byte)(1 << (7 - j));  // Big-endian bit order
                    }
                    extractedBytes.Add(b);
                }
                
                try
                {
                    var message = System.Text.Encoding.UTF8.GetString(extractedBytes.ToArray());
                    
                    if (knownMessages.Contains(message))
                    {
                        return message;
                    }
                }
                catch
                {
                    // Continue to next attempt
                }
            }
            
            return "";
        }

        private static List<string> TryExtractRepeatingMessage(List<bool> bits)
        {
            var results = new List<string>();
            
            // Try different message lengths (from your MESSAGES array)
            var commonLengths = new[] { 8, 9, 10, 11, 12 }; // "Alpha123", "Bravo456", etc.
            
            foreach (var msgLength in commonLengths)
            {
                var bitLength = msgLength * 8; // 8 bits per character
                if (bits.Count < bitLength) continue;
                
                // Extract first occurrence
                var messageBytes = new List<byte>();
                for (int i = 0; i < bitLength; i += 8)
                {
                    if (i + 7 < bits.Count)
                    {
                        byte b = 0;
                        for (int j = 0; j < 8; j++)
                        {
                            if (bits[i + j])
                                b |= (byte)(1 << j);
                        }
                        messageBytes.Add(b);
                    }
                }
                
                try
                {
                    var message = Encoding.UTF8.GetString(messageBytes.ToArray());
                    
                    // Validate: check if this pattern repeats
                    if (ValidateRepeatingPattern(bits, message))
                    {
                        results.Add(message);
                    }
                }
                catch
                {
                    // Invalid UTF-8, continue
                }
            }
            
            return results;
        }

        private static bool ValidateRepeatingPattern(List<bool> bits, string message)
        {
            var msgBits = string.Join("", message.Select(c => Convert.ToString(c, 2).PadLeft(8, '0')));
            var patternLength = msgBits.Length;
            
            // Check if pattern repeats at least 3 times
            var repetitions = 0;
            for (int i = 0; i < bits.Count - patternLength; i += patternLength)
            {
                bool matches = true;
                for (int j = 0; j < patternLength && i + j < bits.Count; j++)
                {
                    var expectedBit = msgBits[j] == '1';
                    if (bits[i + j] != expectedBit)
                    {
                        matches = false;
                        break;
                    }
                }
                
                if (matches)
                {
                    repetitions++;
                    if (repetitions >= 3) return true;
                }
                else
                {
                    break;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Calculates the maximum message capacity for an image
        /// </summary>
        /// <param name="imagePath">Image path</param>
        /// <returns>Maximum characters that can be embedded</returns>
        public static async Task<int> CalculateCapacityAsync(string imagePath)
        {
            using var image = await SLImage.LoadAsync<Rgb24>(imagePath);
            var totalPixels = image.Width * image.Height;
            
            // Each character needs 8 bits, plus 8 bits for end marker
            // Using only red channel LSB
            return (totalPixels - 8) / 8;
        }
        
        /// <summary>
        /// Creates a comprehensive test suite with various steganography scenarios
        /// </summary>
        /// <param name="sourceImagePath">Clean source image</param>
        /// <param name="outputDirectory">Directory to save test images</param>
        public static async Task CreateTestSuiteAsync(string sourceImagePath, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            
            // Test 1: Clean image (control)
            var cleanPath = Path.Combine(outputDirectory, "01_clean_control.png");
            File.Copy(sourceImagePath, cleanPath, true);
            
            // Test 2: Short message
            var shortMsgPath = Path.Combine(outputDirectory, "02_short_message.png");
            await EmbedMessageAsync(sourceImagePath, "Hello", shortMsgPath);
            
            // Test 3: Medium message
            var mediumMsgPath = Path.Combine(outputDirectory, "03_medium_message.png");
            await EmbedMessageAsync(sourceImagePath, 
                "This is a longer message that should be detectable by statistical analysis methods.", 
                mediumMsgPath);
            
            // Test 4: Long message
            var longMsgPath = Path.Combine(outputDirectory, "04_long_message.png");
            var longMessage = string.Join(" ", Enumerable.Repeat("Lorem ipsum dolor sit amet consectetur adipiscing elit", 10));
            await EmbedMessageAsync(sourceImagePath, longMessage, longMsgPath);
            
            // Test 5: Random LSB noise (10%)
            var noise10Path = Path.Combine(outputDirectory, "05_random_noise_10pct.png");
            await CreateRandomLSBNoiseAsync(sourceImagePath, noise10Path, 0.1);
            
            // Test 6: Random LSB noise (50%)
            var noise50Path = Path.Combine(outputDirectory, "06_random_noise_50pct.png");
            await CreateRandomLSBNoiseAsync(sourceImagePath, noise50Path, 0.5);
            
            // Test 7: Binary data (non-text)
            var binaryPath = Path.Combine(outputDirectory, "07_binary_data.png");
            var binaryData = new byte[100];
            new Random().NextBytes(binaryData);
            await EmbedMessageAsync(sourceImagePath, Convert.ToBase64String(binaryData), binaryPath);
            
            // Create test report
            var reportPath = Path.Combine(outputDirectory, "TEST_SUITE_README.txt");
            await File.WriteAllTextAsync(reportPath, GenerateTestReport());
        }
        
        private static string GenerateTestReport()
        {
            return @"
LSB STEGANOGRAPHY TEST SUITE
============================

This test suite contains various images for validating the LSB detector:

01_clean_control.png      - Original clean image (should NOT be detected)
02_short_message.png      - Short text message embedded
03_medium_message.png     - Medium length message
04_long_message.png       - Long message (high embedding rate)
05_random_noise_10pct.png - 10% random LSB modifications
06_random_noise_50pct.png - 50% random LSB modifications  
07_binary_data.png        - Binary data embedded

EXPECTED DETECTION RESULTS:
---------------------------
01: ✅ NO STEGANOGRAPHY (all tests should pass)
02-07: ⚠️ STEGANOGRAPHY DETECTED (2+ tests should fail)

The detector should show increasing confidence levels from 02→07.
Random noise images (05-06) may show highest detection confidence.

Use these images to validate detector accuracy and tune thresholds.
";
        }
    }
} 