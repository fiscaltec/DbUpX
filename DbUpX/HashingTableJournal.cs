using System;
using System.Data;
using System.Linq;
using DbUp.Engine;
using DbUp.Engine.Output;
using DbUp.Engine.Transactions;

namespace DbUpX
{
    /// <summary>
    /// Base class for <see cref="IJournal"/> implementations that store a hash
    /// of the script's contents. <see cref="SqlHashingJournal"/> is an
    /// implementation that supports SQL Server.
    /// </summary>
    public abstract class HashingTableJournal : IJournal
    {
        protected Func<IConnectionManager> Connections { get; private set; }
        protected Func<IUpgradeLog> Log { get; private set; }
        protected ISqlObjectParser SqlObjectParser { get; private set; }

        protected string UnquotedTableSchema { get; }
        protected string UnquotedTableName { get; }

        protected string QuotedTableSchema => SqlObjectParser.QuoteIdentifier(UnquotedTableSchema);
        protected string QuotedTableName => SqlObjectParser.QuoteIdentifier(UnquotedTableName);

        protected string FqSchemaTableName =>
            string.IsNullOrEmpty(UnquotedTableSchema)
                ? QuotedTableName
                : $"{QuotedTableSchema}.{QuotedTableName}";

        protected HashingTableJournal(
            Func<IConnectionManager> connections,
            Func<IUpgradeLog> logger,
            ISqlObjectParser sqlObjectParser,
            string schemaName,
            string tableName)
        {
            Connections = connections;
            Log = logger;
            SqlObjectParser = sqlObjectParser;

            UnquotedTableSchema = schemaName ?? "dbo";
            UnquotedTableName = tableName ?? "SchemaVersionHash";
        }

        protected abstract string CreateSchemaTableSql();
        protected abstract string GetJournalEntriesSql();
        protected abstract string GetInsertScriptSql();
        protected abstract string GetDeleteScriptSql();

        protected virtual string DoesTableExistSql()
        {
            return $"select 1 from INFORMATION_SCHEMA.TABLES where TABLE_NAME = '{UnquotedTableName}'" +
                (string.IsNullOrEmpty(UnquotedTableSchema)
                    ? string.Empty
                    : $"and TABLE_SCHEMA = '{UnquotedTableSchema}'");
        }

        public virtual string[] GetExecutedScripts()
        {
            if (DoesTableExist())
            {
                Log().WriteInformation("Fetching list of already executed scripts.");

                return Connections().ExecuteCommandsWithManagedConnection(db =>
                    db.Query<(string name, string hash)>(GetJournalEntriesSql())
                      .Select(i => new NameWithHash(i.name, i.hash).ToString())
                      .ToArray());
            }

            Log().WriteInformation("Journal table does not exist");
            return new string[0];
        }

        public virtual void StoreExecutedScript(SqlScript script, Func<IDbCommand> db)
        {
            var name = NameWithHash.FromScript(script);

            db.Execute(GetDeleteScriptSql(), new { scriptName = name.PlainName });

            db.Execute(GetInsertScriptSql(),
                new
                {
                    scriptName = name.PlainName,
                    contentsHash = name.ContentsHash
                });
        }

        public virtual void EnsureTableExistsAndIsLatestVersion(Func<IDbCommand> db)
        {
            if (DoesTableExist())
            {
                return;
            }

            db.Execute(CreateSchemaTableSql());
        }

        protected virtual bool DoesTableExist()
        {
            var sql = DoesTableExistSql();
            var result = Connections().ExecuteCommandsWithManagedConnection(
                db => db.ExecuteScalar(sql));
            return result != null;
        }
    }
}
