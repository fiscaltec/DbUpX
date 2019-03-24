using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using DbUp;
using DbUp.Engine;
using FluentAssertions;
using Xunit;

namespace DbUpX.Tests
{
    public class IntegrationTests
    {
        private static string MakeConnectionString(string database)
        {
            return new SqlConnectionStringBuilder
            {
                DataSource = "localhost",
                InitialCatalog = database,
                UserID = "SA",
                Password = "P@ssw0rd",
                MultipleActiveResultSets = true
            }
            .ConnectionString;
        }

        private const string DatabaseName = "IntegrationTests";

        private static readonly string ConnectionString = MakeConnectionString(DatabaseName);

        private static readonly string MasterConnectionString = MakeConnectionString("master");

        private static T WithDB<T>(string connectionString, Func<Func<IDbCommand>, T> op)
        {
            using (var db = new SqlConnection(connectionString))
            {
                db.Open();
                return op(db.CreateCommand);
            }
        }

        public IntegrationTests()
        {
            WithDB(MasterConnectionString, db => db.Execute($@"
                if DB_ID('{DatabaseName}') is not null
                begin
                    alter database {DatabaseName} set SINGLE_USER with rollback immediate;
                    drop database {DatabaseName};
                end"));

            EnsureDatabase.For.SqlDatabase(ConnectionString);
        }

        [Fact]
        public void RunsScripts()
        {
            var deployer = DeployChanges.To
                    .SqlDatabase(ConnectionString)
                    .JournalToSqlWithHashing()
                    .WithScripts(
                        new SqlScript("test1", @"
                            create table Frog (Eyes int)
                        "),
                        new SqlScript("test2", @"
                            insert into Frog (Eyes) values (2)
                        "))
                    .Build();

            deployer.PerformUpgrade().Successful.Should().BeTrue();

            WithDB(ConnectionString, db => (int)db.ExecuteScalar("select Eyes from Frog"))
                .Should().Be(2);

            WithDB(ConnectionString, db => db.Query<string>(
                @"select ScriptName from SchemaVersionHash
                  where len(ContentsHash) > 0
                  order by Applied"))
                .Should().ContainInOrder(new[] {
                    "test1", "test2"
                });
        }

        private static IDictionary<string, string> GetHashes()
        {
            return WithDB(ConnectionString, db => db.Query<(string name, string hash)>(
                @"select ScriptName, ContentsHash from SchemaVersionHash"))
                .ToDictionary(x => x.name, x => x.hash);
        }

        [Fact]
        public void UpgradesOneScript()
        {
            var deployer = DeployChanges.To
                    .SqlDatabase(ConnectionString)
                    .JournalToSqlWithHashing()
                    .WithScripts(
                        new SqlScript("test1", @"
                            create table Frog (Eyes int)
                        "),
                        new SqlScript("test2", @"
                            insert into Frog (Eyes) values (2)
                        "))
                    .LogToConsole()
                    .Build();

            deployer.PerformUpgrade().Successful.Should().BeTrue();

            var hashes1 = GetHashes();
            hashes1.Count().Should().Be(2);

            deployer = DeployChanges.To
                    .SqlDatabase(ConnectionString)
                    .WithScripts(
                        new SqlScript("test1", @"
                            create table Frog (Eyes int)
                        "),
                        new SqlScript("test2", @"
                            delete from Frog
                            insert into Frog (Eyes) values (3)
                        "))
                    .LogToConsole()
                    .JournalToSqlWithHashing()
                    .Build();

            deployer.PerformUpgrade().Successful.Should().BeTrue();

            WithDB(ConnectionString, db => (int)db.ExecuteScalar("select Eyes from Frog"))
                .Should().Be(3);

            var hashes2 = GetHashes();
            hashes2.Count().Should().Be(2);

            hashes2["test1"].Should().Be(hashes1["test1"]);
            hashes2["test2"].Should().NotBe(hashes1["test2"]);
        }
    }
}
