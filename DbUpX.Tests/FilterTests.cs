using System;
using System.Collections.Generic;
using System.Linq;
using DbUp.Builder;
using DbUp.Engine;
using DbUp.Engine.Transactions;
using DbUp.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace DbUpX.Tests
{
    public class FilterTests
    {
        private static (List<SqlScript> scripts,
                        Mock<IScriptExecutor> executor,
                        Mock<IConnectionManager> connection)
                            RunTest(UpgradeEngineBuilder builder)
        {
            var executor = new Mock<IScriptExecutor>();
            var connections = new Mock<IConnectionManager>();

            builder.Configure(c =>
            {
                c.ScriptExecutor = executor.Object;
                c.ConnectionManager = connections.Object;
            });

            var scripts = new List<SqlScript>();

            executor.Setup(x => x.Execute(It.IsAny<SqlScript>()))
                .Callback(new Action<SqlScript>(s => scripts.Add(s)));

            executor.Setup(x => x.Execute(It.IsAny<SqlScript>(), It.IsAny<IDictionary<string, string>>()))
                .Callback(new Action<SqlScript, IDictionary<string, string>>((s, a) => scripts.Add(s)));

            builder.Build().PerformUpgrade();

            return (scripts, executor, connections);
        }

        [Fact]
        public void FilterControlsOrderAndWithPrefixRemovesPrefix()
        {
            SqlScript[] Make(string prefix)
            {
                return new[]
                {
                    new SqlScript($"{prefix}.a{prefix}", $":{prefix}.a"),
                    new SqlScript($"{prefix}.b{prefix}", $":{prefix}.b"),
                    new SqlScript($"{prefix}.c{prefix}", $":{prefix}.c")
                };
            }

            var x = Make("x");
            var y = Make("y");

            var (executed, _, _) = RunTest(
                new UpgradeEngineBuilder()
                    .WithScripts(x)
                    .WithScripts(y)
                    .JournalTo(new NullJournal())
                    .WithFilter(scripts =>
                        scripts.WithPrefix("y.").Concat(
                        scripts.WithPrefix("x."))));

            executed.Select(s => s.Name).Should().ContainInOrder(
                "ay", "by", "cy", "ax", "bx", "cx");
        }
    }
}
