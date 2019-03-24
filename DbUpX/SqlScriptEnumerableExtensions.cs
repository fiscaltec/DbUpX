using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DbUp.Engine;

namespace DbUpX
{
    /// <summary>
    /// Extension methods for manipulating sequences of scripts. Useful in conjunction
    /// with <see cref="BuilderExtensions.WithFilter(DbUp.Builder.UpgradeEngineBuilder, Func{IEnumerable{SqlScript}, IEnumerable{SqlScript}})"/>.
    /// </summary>
    public static class SqlScriptDependencyExtensions
    {
        /// <summary>
        /// Filters the scripts to only those whose name begins with one of the specified
        /// <paramref name="prefixes"/>. The returns scripts have the prefix removed from
        /// their names.
        /// </summary>
        /// <returns>The prefix.</returns>
        /// <param name="scripts">Unfiltered scripts.</param>
        /// <param name="prefixes">Prefixes to check for in script names.</param>
        public static IEnumerable<SqlScript> WithPrefix(
            this IEnumerable<SqlScript> scripts,
            params string[] prefixes)
        {
            return from script in scripts
                   let prefix = prefixes.FirstOrDefault(p =>
                        script.Name.StartsWith(p, StringComparison.InvariantCultureIgnoreCase))
                   where prefix != null
                   select new SqlScript(script.Name.Substring(prefix.Length), script.Contents);
        }

        /// <summary>
        /// Given a set of scripts, returns new scripts with the same contents but
        /// names that are suitably suffixed with the script's hashed contents. This
        /// makes them compatible with <see cref="SqlHashingJournal"/>.
        /// </summary>
        /// <returns>The names.</returns>
        /// <param name="scripts">Scripts.</param>
        public static IEnumerable<SqlScript> HashNames(this IEnumerable<SqlScript> scripts)
        {
            return scripts.Select(s => new SqlScript(NameWithHash.FromScript(s).ToString(),
                                                     s.Contents));
        }

        /// <summary>
        /// Sorts the scripts by dependency comments. A script can depend on other
        /// scripts by listing their comma-separated names after a special comment
        /// prefix. Depended-on scripts will sort before the scripts that depend on
        /// them. 
        /// </summary>
        /// <returns>The scripts ordered by dependency.</returns>
        /// <param name="unsorted">Unsorted scripts.</param>
        /// <param name="commentPrefix">Comment prefix to look for in script contents.</param>
        public static IEnumerable<SqlScript> OrderByDependency(
            this IEnumerable<SqlScript> unsorted,
            string commentPrefix)
        {
            var sorted = new List<SqlScript>();
            var within = new HashSet<SqlScript>();

            bool MatchNames(string whole, string part)
            {
                whole = Path.GetFileNameWithoutExtension(whole) ?? whole;
                return whole.EndsWith(part.Trim(), StringComparison.InvariantCultureIgnoreCase);
            }

            SqlScript ResolveScriptName(string name, IEnumerable<SqlScript> scripts)
            {
                var matches = scripts.Where(s => MatchNames(s.Name, name)).ToList();
                if (matches.Count == 1)
                {
                    return matches[0];
                }

                if (matches.Count == 0)
                {
                    throw new InvalidOperationException($"The required script name '{name}' could not be found");
                }

                var possibles = string.Join(", ", matches.Select(s => $"'{Path.GetFileNameWithoutExtension(s.Name)}'"));
                throw new InvalidOperationException($"The required script name '{name}' is ambiguous, could be {possibles}");
            }

            IEnumerable<SqlScript> GetRequirements(SqlScript script, IEnumerable<SqlScript> scripts)
            {
                using (var reader = new StringReader(script.Contents))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var prefixPos = line.IndexOf(commentPrefix, StringComparison.InvariantCultureIgnoreCase);
                        if (prefixPos != -1)
                        {
                            return line.Substring(prefixPos + commentPrefix.Length)
                                       .Split(',')
                                       .Select(name => ResolveScriptName(name, scripts));
                        }
                    }
                }

                return Enumerable.Empty<SqlScript>();
            }

            void RecursivelyAdd(SqlScript script)
            {
                if (sorted.Contains(script))
                {
                    return;
                }

                if (!within.Add(script))
                {
                    var names = string.Join(", ", within.Select(s => s.Name));
                    throw new InvalidOperationException($"Cyclic dependency between {names}");
                }

                foreach (var required in GetRequirements(script, unsorted))
                {
                    RecursivelyAdd(required);
                }

                sorted.Add(script);
                within.Remove(script);
            }

            foreach (var script in unsorted)
            {
                RecursivelyAdd(script);
            }

            return sorted;
        }
    }
}
