using System;
using System.Collections.Generic;
using System.Linq;
using DbUp.Builder;
using DbUp.Engine;
using DbUp.Support;

namespace DbUpX
{
    public static class BuilderExtensions
    {
        private class DelegatedFilter : IScriptFilter
        {
            private readonly Func<IEnumerable<SqlScript>, IEnumerable<SqlScript>> _sort;

            public DelegatedFilter(Func<IEnumerable<SqlScript>, IEnumerable<SqlScript>> sort)
            {
                _sort = sort;
            }

            public IEnumerable<SqlScript> Filter(
                IEnumerable<SqlScript> sorted,
                HashSet<string> executedScriptNames,
                ScriptNameComparer comparer)
            {
                return _sort(sorted).Where(s => !executedScriptNames.Contains(s.Name));
            }
        }

        /// <summary>
        /// Applies a filter to the upgrade pipeline, allowing the list of scripts
        /// to be sorted and filtered immediately before execution.
        /// 
        /// Scripts that have previously been executed will not run again.
        /// </summary>
        /// <returns>The filtered scripts.</returns>
        /// <param name="builder">Builder.</param>
        /// <param name="filter">A delegate that takes the input set of scripts
        /// and returns them sorted and filtered.</param>
        public static UpgradeEngineBuilder WithFilter(
            this UpgradeEngineBuilder builder,
            Func<IEnumerable<SqlScript>, IEnumerable<SqlScript>> filter)
        {
            builder.Configure(config => config.ScriptFilter = new DelegatedFilter(filter));
            return builder;
        }

        /// <summary>
        /// Configures hashing script contents and saving them to the journal table.
        /// This means that if a script is not changed, it won't be re-run, but if it is
        /// changed then it will. This avoids the need to treat "run always" and 
        /// "run once" scripts differently
        /// 
        /// A filter is also installed that ensures script names include the hash.
        /// You can optionally provide your own additional filtering.
        /// </summary>
        /// <returns>The to sql with hashing.</returns>
        /// <param name="builder">Builder.</param>
        /// <param name="filter">Filter.</param>
        /// <param name="schemaName">Schema name.</param>
        /// <param name="tableName">Table name.</param>
        public static UpgradeEngineBuilder JournalToSqlWithHashing(
            this UpgradeEngineBuilder builder,
            Func<IEnumerable<SqlScript>, IEnumerable<SqlScript>> filter = null,
            string schemaName = null,
            string tableName = null)
        {
            builder.Configure(config =>
            {
                config.Journal = new SqlHashingJournal(
                    () => config.ConnectionManager,
                    () => config.Log,
                    schemaName,
                    tableName);

                config.ScriptFilter = new DelegatedFilter(
                    scripts => (filter != null 
                                    ? filter(scripts) 
                                    : scripts)
                                        .HashNames());
            });

            return builder;
        }
    }
}
