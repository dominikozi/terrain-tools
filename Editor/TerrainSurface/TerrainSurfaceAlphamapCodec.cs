using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Dominikozi.TerrainTools.Editor
{
    internal static class TerrainSurfaceAlphamapCodec
    {
        internal static byte[] Encode(float[,,] alphamaps)
        {
            int height = alphamaps.GetLength(0);
            int width = alphamaps.GetLength(1);
            int layers = alphamaps.GetLength(2);
            byte[] quantized = new byte[height * width * layers];
            int cursor = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int layer = 0; layer < layers; layer++)
                    {
                        quantized[cursor++] = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphamaps[y, x, layer]) * 255f);
                    }
                }
            }

            using MemoryStream output = new MemoryStream();
            using (GZipStream gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(quantized, 0, quantized.Length);
            }

            return output.ToArray();
        }

        internal static float[,,] Decode(byte[] compressed, int width, int height, int layers)
        {
            int expectedLength = checked(width * height * layers);
            byte[] quantized = new byte[expectedLength];
            using MemoryStream input = new MemoryStream(compressed, writable: false);
            using GZipStream gzip = new GZipStream(input, CompressionMode.Decompress);
            int cursor = 0;
            while (cursor < expectedLength)
            {
                int read = gzip.Read(quantized, cursor, expectedLength - cursor);
                if (read == 0)
                {
                    throw new InvalidDataException(
                        $"Alphamap payload ended at {cursor} bytes; expected {expectedLength} bytes.");
                }

                cursor += read;
            }

            float[,,] result = new float[height, width, layers];
            cursor = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    for (int layer = 0; layer < layers; layer++)
                    {
                        float weight = quantized[cursor++] / 255f;
                        result[y, x, layer] = weight;
                        sum += weight;
                    }

                    if (sum <= 0.000001f)
                    {
                        result[y, x, 0] = 1f;
                    }
                    else
                    {
                        for (int layer = 0; layer < layers; layer++)
                        {
                            result[y, x, layer] /= sum;
                        }
                    }
                }
            }

            return result;
        }

        internal static void WriteToFile(string path, float[,,] alphamaps)
        {
            File.WriteAllBytes(path, Encode(alphamaps));
        }

        internal static float[,,] ReadFromFile(string path, int width, int height, int layers)
        {
            return Decode(File.ReadAllBytes(path), width, height, layers);
        }
    }
}
