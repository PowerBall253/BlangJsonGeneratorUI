// Taken from BlangParser by proteh
// https://github.com/dcealopez/BlangParser

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BlangParser;

/// <summary>
/// BlangFile class
/// </summary>
public class BlangFile
{
    /// <summary>
    /// Unknown data
    /// </summary>
    public long UnknownData;

    /// <summary>
    /// The strings in this file
    /// </summary>
    public List<BlangString> Strings;

    /// <summary>
    /// BlangFile default constructor
    /// </summary>
    public BlangFile()
    {
        Strings = new List<BlangString>();
    }

    /// <summary>
    /// Parses the given Blang file into a BlangFile object
    /// </summary>
    /// <param name="path">path to the Blang file</param>
    /// <returns>parsed Blang file in a BlangFile object</returns>
    public static BlangFile Parse(string path)
    {
        var blangFile = new BlangFile();

        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        var blangStrings = new List<BlangString>();

        // Check if blang format is new or old
        fileStream.Seek(0xC, SeekOrigin.Begin);
        var strBytes = binaryReader.ReadBytes(5);
        fileStream.Seek(0x0, SeekOrigin.Begin);

        if (!Encoding.UTF8.GetString(strBytes).ToLower().Equals("#str_"))
        {
            // Read unknown data (big-endian 64 bit integer)
            byte[] unknownDataBytes = binaryReader.ReadBytes(8);
            Array.Reverse(unknownDataBytes, 0, unknownDataBytes.Length);
            blangFile.UnknownData = BitConverter.ToInt64(unknownDataBytes, 0);
        }

        // Read the amount of strings (big-endian 32 bit integer)
        byte[] stringAmountBytes = binaryReader.ReadBytes(4);
        Array.Reverse(stringAmountBytes, 0, stringAmountBytes.Length);
        int stringAmount = BitConverter.ToInt32(stringAmountBytes, 0);

        // Check if blang strings contain unknown data
        bool skipUnknown = false;

        long currentPosition = fileStream.Position;
        fileStream.Seek(0x4, SeekOrigin.Current); // Skip first string's hash
        fileStream.Seek(binaryReader.ReadUInt32(), SeekOrigin.Current); // Skip first string's identifier
        fileStream.Seek(binaryReader.ReadUInt32(), SeekOrigin.Current); // Skip first string's text
        fileStream.Seek(0x8, SeekOrigin.Current); // Skip 8 bytes

        if (Encoding.UTF8.GetString(binaryReader.ReadBytes(5)).ToLower().Equals("#str_"))
        {
            // There's only 8 bytes between last text and next identifier (hash and length)
            // So there are no unknown bytes
            skipUnknown = true;
        }

        // Seek back to before first string
        fileStream.Seek(currentPosition, SeekOrigin.Begin);

        // Parse each string
        for (int i = 0; i < stringAmount; i++)
        {
            // Read string hash
            uint hash = binaryReader.ReadUInt32();

            // Read string identifier
            int identifierBytes = binaryReader.ReadInt32();
            string identifier = Encoding.UTF8.GetString(binaryReader.ReadBytes(identifierBytes));

            // Read string
            int textBytes = binaryReader.ReadInt32();
            string text = Encoding.UTF8.GetString(binaryReader.ReadBytes(textBytes));

            // Read unknown data
            string unknown = "";

            if (!skipUnknown)
            {
                int unknownBytes = binaryReader.ReadInt32();
                unknown = Encoding.UTF8.GetString(binaryReader.ReadBytes(unknownBytes));
            }

            blangStrings.Add(new BlangString(hash, identifier, identifier, text, text, unknown, false));
        }

        blangFile.Strings = blangStrings;
        return blangFile;
    }

    /// <summary>
    /// Writes the current BlangFile object to the specified path
    /// </summary>
    /// <param name="path"></param>
    public void WriteTo(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var binaryWriter = new BinaryWriter(fileStream);

        // Delete invalid strings first
        // Strings must have a valid identifier
        for (int i = Strings.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrEmpty(Strings[i].Identifier) || string.IsNullOrWhiteSpace(Strings[i].Identifier))
            {
                Strings.RemoveAt(i);
            }
        }

        // Write unknown data in big-endian
        byte[] unknownDataBytes = BitConverter.GetBytes(UnknownData);
        Array.Reverse(unknownDataBytes);
        binaryWriter.Write(unknownDataBytes);

        // Write string amount in big-endian
        byte[] stringsAmount = BitConverter.GetBytes(Strings.Count);
        Array.Reverse(stringsAmount);
        binaryWriter.Write(stringsAmount);

        // Write each string
        foreach (var blangString in Strings)
        {
            // Calculate the hash of the identifier string (FNV1A32)
            var identifierBytes = Encoding.UTF8.GetBytes(blangString.Identifier.ToLowerInvariant());
            uint fnvPrime = 0x01000193;
            blangString.Hash = 0x811C9DC5;

            for (int i = 0; i < identifierBytes.Length; i++)
            {
                unchecked
                {
                    blangString.Hash ^= identifierBytes[i];
                    blangString.Hash *= fnvPrime;
                }
            }

            // Convert to little endian
            byte[] hashBytes = BitConverter.GetBytes(blangString.Hash);
            Array.Reverse(hashBytes);
            blangString.Hash = BitConverter.ToUInt32(hashBytes, 0);

            // Write the hash (little-endian)
            binaryWriter.Write(blangString.Hash);

            // Write identifier (don't convert to lower-case this time)
            identifierBytes = Encoding.UTF8.GetBytes(blangString.Identifier);
            binaryWriter.Write(identifierBytes.Length);
            binaryWriter.Write(identifierBytes);

            // Write text
            // Null or empty strings are permitted
            if (string.IsNullOrEmpty(blangString.Text) || string.IsNullOrWhiteSpace(blangString.Text))
            {
                blangString.Text = "";
            }

            // Remove carriage returns
            blangString.Text = blangString.Text.Replace("\r", "");

            var textBytes = Encoding.UTF8.GetBytes(blangString.Text);
            binaryWriter.Write(textBytes.Length);
            binaryWriter.Write(textBytes);

            // Write unknown data
            if (string.IsNullOrEmpty(blangString.Unknown) || string.IsNullOrWhiteSpace(blangString.Unknown))
            {
                binaryWriter.Write(new byte[4]);
            }
            else
            {
                var unknownBytes = Encoding.UTF8.GetBytes(blangString.Unknown);
                binaryWriter.Write(unknownBytes.Length);
                binaryWriter.Write(unknownBytes);
            }
        }
    }

    /// <summary>
    /// Parses the given Blang file in memory into a BlangFile object
    /// </summary>
    /// <param name="memoryStream">stream containing the Blang file</param>
    /// <returns>parsed Blang file in a BlangFile object</returns>
    public static BlangFile ParseFromMemory(MemoryStream memoryStream)
    {
        var blangFile = new BlangFile();

        using var binaryReader = new BinaryReader(memoryStream);

        var blangStrings = new List<BlangString>();

        // Check if blang format is new or old
        memoryStream.Seek(0xC, SeekOrigin.Begin);
        var strBytes = binaryReader.ReadBytes(5);
        memoryStream.Seek(0x0, SeekOrigin.Begin);

        if (!Encoding.UTF8.GetString(strBytes).ToLower().Equals("#str_"))
        {
            // Read unknown data (big-endian 64 bit integer)
            byte[] unknownDataBytes = binaryReader.ReadBytes(8);
            Array.Reverse(unknownDataBytes, 0, unknownDataBytes.Length);
            blangFile.UnknownData = BitConverter.ToInt64(unknownDataBytes, 0);
        }

        // Read the amount of strings (big-endian 32 bit integer)
        byte[] stringAmountBytes = binaryReader.ReadBytes(4);
        Array.Reverse(stringAmountBytes, 0, stringAmountBytes.Length);
        int stringAmount = BitConverter.ToInt32(stringAmountBytes, 0);

        // Check if blang strings contain unknown data
        bool skipUnknown = false;

        long currentPosition = memoryStream.Position;
        memoryStream.Seek(0x4, SeekOrigin.Current); // Skip first string's hash
        memoryStream.Seek(binaryReader.ReadUInt32(), SeekOrigin.Current); // Skip first string's identifier
        memoryStream.Seek(binaryReader.ReadUInt32(), SeekOrigin.Current); // Skip first string's text
        memoryStream.Seek(0x8, SeekOrigin.Current); // Skip 8 bytes

        if (Encoding.UTF8.GetString(binaryReader.ReadBytes(5)).ToLower().Equals("#str_"))
        {
            // There's only 8 bytes between last text and next identifier (hash and length)
            // So there are no unknown bytes
            skipUnknown = true;
        }

        // Seek back to before first string
        memoryStream.Seek(currentPosition, SeekOrigin.Begin);

        // Parse each string
        for (int i = 0; i < stringAmount; i++)
        {
            // Read string hash
            uint hash = binaryReader.ReadUInt32();

            // Read string identifier
            int identifierBytes = binaryReader.ReadInt32();
            string identifier = Encoding.UTF8.GetString(binaryReader.ReadBytes(identifierBytes));

            // Read string
            int textBytes = binaryReader.ReadInt32();
            string text = Encoding.UTF8.GetString(binaryReader.ReadBytes(textBytes));

            // Read unknown data
            string unknown = "";

            if (!skipUnknown)
            {
                int unknownBytes = binaryReader.ReadInt32();
                unknown = Encoding.UTF8.GetString(binaryReader.ReadBytes(unknownBytes));
            }

            blangStrings.Add(new BlangString(hash, identifier, identifier, text, text, unknown, false));
        }

        blangFile.Strings = blangStrings;
        return blangFile;
    }

    /// <summary>
    /// Writes the current BlangFile object into a memory stream
    /// </summary>
    /// <returns>memory stream containing a .blang file</returns>
    public MemoryStream WriteToStream()
    {
        var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        // Delete invalid strings first
        // Strings must have a valid identifier
        for (int i = Strings.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrEmpty(Strings[i].Identifier) || string.IsNullOrWhiteSpace(Strings[i].Identifier))
            {
                Strings.RemoveAt(i);
            }
        }

        // Write unknown data in big-endian
        byte[] unknownDataBytes = BitConverter.GetBytes(UnknownData);
        Array.Reverse(unknownDataBytes);
        binaryWriter.Write(unknownDataBytes);

        // Write string amount in big-endian
        byte[] stringsAmount = BitConverter.GetBytes(Strings.Count);
        Array.Reverse(stringsAmount);
        binaryWriter.Write(stringsAmount);

        // Write each string
        foreach (var blangString in Strings)
        {
            // Calculate the hash of the identifier string (FNV1A32)
            var identifierBytes = Encoding.UTF8.GetBytes(blangString.Identifier.ToLowerInvariant());
            uint fnvPrime = 0x01000193;
            blangString.Hash = 0x811C9DC5;

            for (int i = 0; i < identifierBytes.Length; i++)
            {
                unchecked
                {
                    blangString.Hash ^= identifierBytes[i];
                    blangString.Hash *= fnvPrime;
                }
            }

            // Convert to little endian
            byte[] hashBytes = BitConverter.GetBytes(blangString.Hash);
            Array.Reverse(hashBytes);
            blangString.Hash = BitConverter.ToUInt32(hashBytes, 0);

            // Write the hash (little-endian)
            binaryWriter.Write(blangString.Hash);

            // Write identifier (don't convert to lower-case this time)
            identifierBytes = Encoding.UTF8.GetBytes(blangString.Identifier);
            binaryWriter.Write(identifierBytes.Length);
            binaryWriter.Write(identifierBytes);

            // Write text
            // Null or empty strings are permitted
            if (string.IsNullOrEmpty(blangString.Text) || string.IsNullOrWhiteSpace(blangString.Text))
            {
                blangString.Text = "";
            }

            // Remove carriage returns
            blangString.Text = blangString.Text.Replace("\r", "");

            var textBytes = Encoding.UTF8.GetBytes(blangString.Text);
            binaryWriter.Write(textBytes.Length);
            binaryWriter.Write(textBytes);

            // Write unknown data
            if (string.IsNullOrEmpty(blangString.Unknown) || string.IsNullOrWhiteSpace(blangString.Unknown))
            {
                binaryWriter.Write(new byte[4]);
            }
            else
            {
                var unknownBytes = Encoding.UTF8.GetBytes(blangString.Unknown);
                binaryWriter.Write(unknownBytes.Length);
                binaryWriter.Write(unknownBytes);
            }
        }

        return memoryStream;
    }
}
