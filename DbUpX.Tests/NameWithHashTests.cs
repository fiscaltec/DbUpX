using DbUp.Engine;
using FluentAssertions;
using Xunit;

namespace DbUpX.Tests
{
    public class NameWithHashTests
    {
        [Fact]
        public void CombinesParts()
        {
            new NameWithHash("name", "hash").ToString().Should().Be("name#hash");
        }

        [Fact]
        public void SplitsParts()
        {
            NameWithHash.TryParse("name#hash", out var parsed).Should().BeTrue();
            parsed.PlainName.Should().Be("name");
            parsed.ContentsHash.Should().Be("hash");
        }

        [Fact]
        public void RejectsNonHashed()
        {
            NameWithHash.TryParse("random", out var parsed).Should().BeFalse();
            parsed.Should().BeNull();
        }

        [Fact]
        public void AddsHash()
        {
            var hashed = NameWithHash.FromScript(new SqlScript("name", "contents"));

            hashed.PlainName.Should().Be("name");
            hashed.ContentsHash.Should().Be(NameWithHash.GenerateHash("contents"));
        }

        [Fact]
        public void CorrectsExistingHash()
        {
            var hashed = NameWithHash.FromScript(new SqlScript("name#blah", "contents"));

            hashed.PlainName.Should().Be("name");
            hashed.ContentsHash.Should().Be(NameWithHash.GenerateHash("contents"));
        }
    }
}
