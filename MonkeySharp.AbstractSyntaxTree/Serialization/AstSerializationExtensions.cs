using System.IO;

namespace MonkeySharp.AbstractSyntaxTree.Serialization;

/// <summary>
/// Extension methods for convenient AST serialization and deserialization.
/// </summary>
internal static class AstSerializationExtensions
{
    /// <summary>
    /// Serializes the program node to a binary stream.
    /// </summary>
    /// <param name="program">The program node to serialize.</param>
    /// <param name="stream">The output stream.</param>
    public static void SerializeToBinary(this ProgramNode program, Stream stream)
    {
        AstBinarySerializer.Serialize(program, stream);
    }

    /// <summary>
    /// Serializes the program node to a byte array.
    /// </summary>
    /// <param name="program">The program node to serialize.</param>
    /// <returns>A byte array containing the serialized AST.</returns>
    public static byte[] SerializeToBinary(this ProgramNode program)
    {
        using var ms = new MemoryStream();
        AstBinarySerializer.Serialize(program, ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Serializes the program node to a file.
    /// </summary>
    /// <param name="program">The program node to serialize.</param>
    /// <param name="filePath">The file path to write to.</param>
    public static void SerializeToBinaryFile(this ProgramNode program, string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        AstBinarySerializer.Serialize(program, fs);
    }

    /// <summary>
    /// Deserializes a program node from a byte array.
    /// </summary>
    /// <param name="data">The byte array containing serialized AST data.</param>
    /// <returns>The deserialized program node.</returns>
    public static ProgramNode DeserializeFromBinary(byte[] data)
    {
        using var ms = new MemoryStream(data);
        return AstBinaryDeserializer.Deserialize(ms);
    }

    /// <summary>
    /// Deserializes a program node from a file.
    /// </summary>
    /// <param name="filePath">The file path to read from.</param>
    /// <returns>The deserialized program node.</returns>
    public static ProgramNode DeserializeFromBinaryFile(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return AstBinaryDeserializer.Deserialize(fs);
    }
}