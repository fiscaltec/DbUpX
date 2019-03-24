using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DbUpX
{
    /// <summary>
    /// Provides a number of handy utility methods for running SQL commands and queries.
    /// A very minimal clone of Dapper, except it uses a DbUp-style command factory, 
    /// <see cref="Func{IDbCommand}"/>, instead of <see cref="IDbConnection"/>.
    /// </summary>
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Sets up a command object with the specific SQL and parameters,
        /// where the parameters come from the properties of an anonymous object.
        /// </summary>
        /// <returns>The command.</returns>
        /// <param name="db">Db.</param>
        /// <param name="sql">Sql.</param>
        /// <param name="args">Arguments.</param>
        public static IDbCommand PrepareCommand(
            this Func<IDbCommand> db,
            string sql,
            object args = null)
        {
            var cmd = db();
            cmd.CommandText = sql;
            if (args != null)
            {
                foreach (var prop in args.GetType().GetProperties())
                {
                    var param = cmd.CreateParameter();
                    param.ParameterName = prop.Name;
                    param.Value = prop.GetValue(args);
                    cmd.Parameters.Add(param);
                }
            }
            return cmd;
        }

        /// <summary>
        /// Execute the specified SQL command.
        /// </summary>
        /// <returns>The result of ExecuteNonQuery, often the number of affected rows.</returns>
        /// <param name="db">Command factory</param>
        /// <param name="sql">SQL command</param>
        /// <param name="args">Object whose properties supply parameters used in the SQL</param>
        public static int Execute(this Func<IDbCommand> db, string sql, object args = null)
            => db.PrepareCommand(sql, args).ExecuteNonQuery();

        /// <summary>
        /// Execute the specified SQL scalar query.
        /// </summary>
        /// <returns>The result of ExecuteScalar.</returns>
        /// <param name="db">Command factory</param>
        /// <param name="sql">SQL scalar query (selecting a single row/column)</param>
        /// <param name="args">Object whose properties supply parameters used in the SQL</param>
        public static object ExecuteScalar(this Func<IDbCommand> db, string sql, object args = null)
            => db.PrepareCommand(sql, args).ExecuteScalar();

        /// <summary>
        /// Execute the specified SQL query and returns a collection of objects. For a query
        /// returning one column the type T is the datatype of that column. Otherwise it should
        /// be a type that can be constructed with the datatypes of the columns, for example a
        /// ValueType type whose items match the datatypes of the columns.
        /// </summary>
        /// <returns>The result of ExecuteScalar.</returns>
        /// <param name="db">Command factory</param>
        /// <param name="sql">SQL query</param>
        /// <param name="args">Object whose properties supply parameters used in the SQL</param>
        public static IReadOnlyCollection<T> Query<T>(this Func<IDbCommand> db, string sql, object args = null)
        {
            var results = new List<T>();

            using (var reader = db.PrepareCommand(sql, args).ExecuteReader())
            {
                if (reader.FieldCount == 1)
                {
                    while (reader.Read())
                    {
                        results.Add((T)reader.GetValue(0));
                    }
                }
                else
                {
                    var ctor = typeof(T).GetConstructor(
                        Enumerable.Range(0, reader.FieldCount)
                                  .Select(reader.GetFieldType)
                                  .ToArray());

                    while (reader.Read())
                    {
                        results.Add((T)ctor.Invoke(Enumerable.Range(0, reader.FieldCount)
                                                             .Select(reader.GetValue)
                                                             .ToArray()));
                    }
                }
            }

            return results;
        }
    }
}
