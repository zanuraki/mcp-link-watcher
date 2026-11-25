namespace Zanuraki.Mcp.LinkWatcher.Services;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public sealed class FrontmatterService
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    /// <summary>Splits a markdown document with a leading <c>---</c> YAML block into its deserialized frontmatter and the remaining body.</summary>
    public (T Frontmatter, string Body) Parse<T>(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        if (lines[0] != "---")
        {
            throw new FormatException("Markdown document does not start with a frontmatter block.");
        }

        var closing = Array.IndexOf(lines, "---", 1);
        if (closing < 0)
        {
            throw new FormatException("Frontmatter block is not closed.");
        }

        var yaml = string.Join('\n', lines[1..closing]);
        var body = string.Join('\n', lines[(closing + 1)..]);
        return (_deserializer.Deserialize<T>(yaml), body);
    }

    public string Serialize<T>(T frontmatter, string body) =>
        $"---\n{_serializer.Serialize(frontmatter)}---\n{body}";
}
