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
                    
                    // Check for null terminator after constructing each byte
                    if (b == 0)
                    {
                        // Found null terminator - remove it and stop
                        messageBytes.RemoveAt(messageBytes.Count - 1);
                        break;
                    }
                }
            }
            
            return Encoding.UTF8.GetString(messageBytes.ToArray());
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
        

    }
} 