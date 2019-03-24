using System;
using DbUp.Engine.Output;
using DbUp.Engine.Transactions;
using DbUp.SqlServer;

namespace DbUpX
{
    /// <summary>
    /// Implements <see cref="DbUp.Engine.IJournal"/> to store hashed script
    /// contents as well as the usual ScriptName and Applied date.
    /// </summary>
    public class SqlHashingJournal : HashingTableJournal
    {
        public SqlHashingJournal(
            Func<IConnectionManager> connections, 
            Func<IUpgradeLog> logger,
            string schemaName,
            string tableName)
            : base(connections, 
                   logger, 
                   new SqlServerObjectParser(), 
                   schemaName, 
                   tableName) { }

        protected override string CreateSchemaTableSql()
        {
            return
                $@"create table {FqSchemaTableName} (
                    [ScriptName] nvarchar(255) not null,
                    [ContentsHash] nvarchar(255) not null,
                    [Applied] datetime not null
                )";
        }

        protected override string GetDeleteScriptSql()
        {
            return $"delete from {FqSchemaTableName} where [ScriptName] = @scriptName";
        }

        protected override string GetInsertScriptSql()
        {
            return $@"insert into {FqSchemaTableName} ([ScriptName], [ContentsHash], [Applied]) 
                      values (@scriptName, @contentsHash, GETUTCDATE())";
        }

        protected override string GetJournalEntriesSql()
        {
            return $"select [ScriptName], [ContentsHash] from {FqSchemaTableName}";
        }
    }
}
