using System;
using System.Linq;
using DbUp.Engine;
using FluentAssertions;
using Xunit;

namespace DbUpX.Tests
{
    public class OrderByDependencyTests
    {
        private readonly string _prefix = "#requires";

        [Fact]
        public void DoesNotAffectIndependentScripts()
        {
            var unsorted = new[]
            {
                new SqlScript("y.sql", "contents of y"),
                new SqlScript("x.sql", "contents of x"),
                new SqlScript("z.sql", "contents of z")
            };

            var sorted = unsorted.OrderByDependency(_prefix).ToList();
            sorted.Should().ContainInOrder(sorted);
        }

        [Fact]
        public void SortsByDependencies()
        {
            var a = new SqlScript("a.sql", "contents of a #requires b");
            var b = new SqlScript("b.sql", "contents of b");
            var x = new SqlScript("x.sql", "-- #requires b \r\n contents of x");
            var y = new SqlScript("y.sql", "contents of y #requires a");
            var z = new SqlScript("z.sql", "contents of z");

            var unsorted = new[] { y, x, z, a, b };

            var sorted = unsorted.OrderByDependency(_prefix).ToList();
            sorted.Should().ContainInOrder(b, a, y, x, z);
        }

        [Fact]
        public void AllowsMultipleDependencies()
        {
            var a = new SqlScript("a.sql", "contents of a #requires b, c");
            var b = new SqlScript("b.sql", "contents of b");
            var c = new SqlScript("c.sql", "contents of c #requires x, y, z");
            var x = new SqlScript("x.sql", "-- #requires y \r\n contents of x");
            var y = new SqlScript("y.sql", "contents of y");
            var z = new SqlScript("z.sql", "contents of z");

            var unsorted = new[] { a, b, c, y, x, z };

            var sorted = unsorted.OrderByDependency(_prefix).ToList();
            sorted.Should().ContainInOrder(b, y, x, z, c, a);
        }

        [Fact]
        public void ComplainsAboutMissing()
        {
            var a = new SqlScript("bee.sql", "contents of bee");
            var b = new SqlScript("owl.sql", "contents of owl #requires be");

            var unsorted = new[] { a, b };

            Action ex = () => unsorted.OrderByDependency(_prefix);
            ex.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void AllowsNameToBeSuffix()
        {
            var bear = new SqlScript("bear.sql", "contents of bee");
            var chair = new SqlScript("chair.sql", "contents of chair #requires ear");

            var unsorted = new[] { chair, bear };

            var sorted = unsorted.OrderByDependency(_prefix).ToList();
            sorted.Should().ContainInOrder(bear, chair);
        }

        [Fact]
        public void ComplainsAboutAmbiguous()
        {
            var b = new SqlScript("bear.sql", "contents of bear");
            var d = new SqlScript("fear.sql", "contents of fear");
            var c = new SqlScript("um.sql", "contents of um #requires ear");

            var unsorted = new[] { c, b, d };

            Action ex = () => unsorted.OrderByDependency(_prefix);
            ex.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ComplainsAboutCycle()
        {
            var a = new SqlScript("a.sql", "contents of a #requires b");
            var b = new SqlScript("b.sql", "contents of b #requires c");
            var c = new SqlScript("c.sql", "contents of c #requires a");

            var unsorted = new[] { a, b, c };

            Action ex = () => unsorted.OrderByDependency(_prefix);
            ex.Should().Throw<InvalidOperationException>();
        }
    }
}
