namespace Zanuraki.Mcp.LinkWatcher.Test.Services;

using Shouldly;
using Zanuraki.Mcp.LinkWatcher.Services;

public abstract class TheFrontmatterServiceClass
{
    public class TheParseMethod : TheFrontmatterServiceClass
    {
        [Test]
        public void ShouldSplitFrontmatterFromBody()
        {
            var markdown = "---\nname: first\ntags:\n- a\n- b\n---\n## Body\ntext\n";

            var (frontmatter, body) = new FrontmatterService().Parse<SampleFrontmatter>(markdown);

            frontmatter.Name.ShouldBe("first");
            frontmatter.Tags.ShouldBe(["a", "b"]);
            body.ShouldBe("## Body\ntext\n");
        }
    }

    public class TheSerializeMethod : TheFrontmatterServiceClass
    {
        [Test]
        public void ShouldWriteFrontmatterFollowedByBodyAndOmitNullFields()
        {
            var markdown = new FrontmatterService().Serialize(
                new SampleFrontmatter { Name = "first", Tags = null },
                "## Body\ntext\n");

            markdown.ShouldBe("---\nname: first\n---\n## Body\ntext\n");
        }
    }

    public sealed class SampleFrontmatter
    {
        public string Name { get; set; } = string.Empty;

        public List<string>? Tags { get; set; }
    }
}
