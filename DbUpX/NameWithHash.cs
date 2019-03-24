using System;
using System.Security.Cryptography;
using System.Text;
using DbUp.Engine;

namespace DbUpX
{
    /// <summary>
    /// Defines a format for script names, "name#hash". This allows the DbUp engine
    /// to pass through the hash of the script's contents. It's necessary because
    /// <see cref="IJournal.GetExecutedScripts"/> only returns an array of strings.
    /// </summary>
    public sealed class NameWithHash
    {
        public string PlainName { get; }
        public string ContentsHash { get; }

        public NameWithHash(string plainName, string contentsHash)
        {
            PlainName = plainName;
            ContentsHash = contentsHash;
        }

        /// <summary>
        /// Attempts to split the supplied string into name and hash parts.
        /// The parse will only succeed if the string contains a #.
        /// </summary>
        /// <returns><c>true</c>, if parse was successful, <c>false</c> otherwise.</returns>
        /// <param name="combinedName">Combined name or other string.</param>
        /// <param name="result">Parsed result.</param>
        public static bool TryParse(string combinedName, out NameWithHash result)
        {
            var pos = combinedName.IndexOf('#');
            if (pos == -1)
            {
                result = null;
                return false;
            }

            result = new NameWithHash(
                combinedName.Substring(0, pos),
                combinedName.Substring(pos + 1));

            return true;
        }

        /// <summary>
        /// Requires the specified string to be in name#hash format.
        /// </summary>
        /// <returns>The parsed parts of the string.</returns>
        /// <param name="combinedName">Combined name in name#hash format.</param>
        public static NameWithHash Parse(string combinedName)
        {
            if (TryParse(combinedName, out var result))
            {
                return result;
            }
             
            throw new ArgumentException(
                "Could not find expected # in name", 
                nameof(combinedName));
        }

        /// <summary>
        /// Given a script, returns the parsed name. If the name was already
        /// in name#hash format, checks that the hash was consistent with
        /// the script contents.
        /// </summary>
        /// <returns>The script.</returns>
        /// <param name="script">Script.</param>
        public static NameWithHash FromScript(SqlScript script)
        {
            var name = TryParse(script.Name, out var result) ? result.PlainName : script.Name;
            return new NameWithHash(name, GenerateHash(script.Contents));
        }

        /// <summary>
        /// Returns the combined string of the name and hash separate by a #.
        /// </summary>
        /// <returns>The combined string.</returns>
        public override string ToString()
        {
            return $"{PlainName}#{ContentsHash}";
        }

        /// <summary>
        /// Returns the SHA256 hash of the supplied content
        /// </summary>
        /// <returns>The hash.</returns>
        /// <param name="content">Content.</param>
        public static string GenerateHash(string content)
        {
            using (var algorithm = SHA256.Create())
            {
                return Convert.ToBase64String(
                    algorithm.ComputeHash(
                        Encoding.UTF8.GetBytes(content)));
            }
        }
    }
}
