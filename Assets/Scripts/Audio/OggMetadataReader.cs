// File: OggMetadataReader.cs
// Utility class to parse OGG Vorbis comment headers and extract custom loop metadata tags.
using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class OggMetadataReader
{
    public struct LoopData
    {
        public bool HasLoop;
        public int LoopStart;
        public int LoopLength;
        public int LoopEnd => LoopStart + LoopLength;
    }

    /// <summary>
    /// Reads OGG metadata from raw byte array or file path to locate LOOPSTART and LOOPLENGTH tags.
    /// </summary>
    public static LoopData ReadLoopDataFromBytes(byte[] oggBytes)
    {
        LoopData data = new LoopData { HasLoop = false, LoopStart = 0, LoopLength = 0 };

        if (oggBytes == null || oggBytes.Length == 0) return data;

        try
        {
            string fileContent = Encoding.ASCII.GetString(oggBytes);

            int startIdx = fileContent.IndexOf("LOOPSTART=", StringComparison.OrdinalIgnoreCase);
            int lengthIdx = fileContent.IndexOf("LOOPLENGTH=", StringComparison.OrdinalIgnoreCase);

            if (startIdx != -1 && lengthIdx != -1)
            {
                string startVal = ExtractValue(fileContent, startIdx + "LOOPSTART=".Length);
                string lengthVal = ExtractValue(fileContent, lengthIdx + "LOOPLENGTH=".Length);

                if (int.TryParse(startVal, out int loopStart) && int.TryParse(lengthVal, out int loopLength))
                {
                    data.HasLoop = true;
                    data.LoopStart = loopStart;
                    data.LoopLength = loopLength;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OggMetadataReader] Failed to parse OGG metadata: {e.Message}");
        }

        return data;
    }

    private static string ExtractValue(string content, int startIndex)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = startIndex; i < content.Length; i++)
        {
            char c = content[i];
            if (char.IsDigit(c))
            {
                sb.Append(c);
            }
            else
            {
                break;
            }
        }
        return sb.ToString();
    }
}