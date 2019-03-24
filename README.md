# DbUpX

Extensions to [DbUp](https://github.com/DbUp/DbUp) supporting easy filtering, ordering
and versioning:

- a journaling system that stores hashes of script contents, so we know if they need to
  rerun,
- a concept of "dependency comments" in scripts that let you more easily control the
  ordering of scripts,
- protection against code reorganisation affecting long script names,
- utilities for sorting and filtering scripts in helpful ways.

# Background

The philosophy of DbUp is that database upgrades should absolutely *not* be figured out 
automatically by tools that infers the necessary transitions to reach a target state.
They are only able to do this in the simplest of cases, while they do a poor job in the
majority of cases.

Instead, the transitions required should be explicitly stated. This way there is full 
control over how information is preserved, transformed or (where it makes sense) even
discarded by the transitions.

To that end DbUp establishes a pattern for embedding scripts and code that describe the
transitions, a simple engine for executing each script and a "journal" system for 
tracking whether scripts have already run, by name.

Other than that it is very unopinionated. In this library we add a few more extensions
that fill in some gaps.

## LINQ is the right way to manipulate sequences of objects

DbUp provides ways to generate a sequence of scripts, through script providers. It also
provides an engine that executes a sequence of scripts. Such a sequence is represented
by the type `IEnumerable<SqlScript>`.

You want to be able to control which of those scripts run, and in what order. The 
obvious way to do this is to use LINQ extension methods such as `Concat`,`Where`, 
`Select` and `OrderBy`. So this library provides ways to easily insert your own LINQ
filtering straight into the building of your upgrade engine, without breaking built-in 
behaviour or having to implement extra classes.

## Run once vs. run always

Some scripts define views and stored procs, and it's easier to just run them every time
you perform an upgrade.

On the other hand, if you have to upgrade a lot of databases, and you have a lot of 
such scripts, it can take a significant amount of time, which may mean downtime for
your users.

This library provides a journal table that stores a hash of the contents of each
script, so it can detect if a script has changed since it was run. If it has, it runs
again. This takes care of almost all use cases in an optimal way. If you want a script
to be "run once", then simply don't make changes to it.

## Brittle long script names

In DbUp scripts are gathered from embedded resources and script generator classes. They
wind up with long names that include the full path to the script within the namespace
system of the CLR.

This makes the journal contents brittle against code reorganisations. Instead it's
better to remove the namespace prefix. This library has an extension method
`WithPrefix` that takes care of this, while also serving as a useful way to select a
subset of your scripts.

# Usage

Build and run your upgrader in the usual way, but use `JournalToSqlWithHashing` to
get both the change-detecting form of journaling and also the ability to perform your
own custom sorting:

```csharp
var upgrader = DeployChanges.To.SqlDatabase(connectionString)
    .WithScriptsAndCodeEmbeddedInAssembly(typeof(MyAssembly).Assembly)
    .LogToConsole()        
    .JournalToSqlWithHashing(scripts => /* filter and sort the scripts here */)    
    .Build()
    .PerformUpgrade();
```

For example, you may have two sets of scripts:

 - The first is in the namespace `MyAssembly.Scripts.ByDate`, containing the
   transitions to be applied in chronological order, such as adding new tables,
   adding new columns to existing tables, etc. You name the scripts after the
   date/time when they were written, so to sort them chronologically you can just sort
   them lexicographically, by name.
 - The second is in the namespace `MyAssembly.Scripts.Dependent`. It includes scripts
   that define views, stored procs and custom data types. These sometimes depend on
   each other, but can be modified and then need to be re-executed, and hence ordering
   them correctly is more challenging.

Solution:
   
```csharp
var upgrader = DeployChanges.To.SqlDatabase(connectionString)
    .WithScriptsAndCodeEmbeddedInAssembly(typeof(MyAssembly).Assembly)
    .LogToConsole()        
    .JournalToSqlWithHashing(scripts =>
    
        scripts.WithPrefix("MyAssembly.Scripts.ByDate.")
               .OrderBy(s => s.Name).Concat(
               
        scripts.WithPrefix("MyAssembly.Scripts.Dependent.")
               .OrderByDependency("@requires"))
    )
    .Build()
    .PerformUpgrade();
```

DbUp's standard `WithScriptsAndCodeEmbeddedInAssembly` method is used to collect all 
the scripts regardless of their location in the code. This means the scripts all have
long names prefixed with their namespaces.

Then the custom filter expression passed to `JournalToSqlWithHashing` is able to
identify and deal with the two subsets of scripts appropriately. It uses `WithPrefix`
to find the `ByDate` scripts and orders them by name (i.e. by date) and then it finds
the `Dependent` scripts and uses DbUpX's `OrderByDependency` feature to make sure they
are ordered according to how they depend on one another.

The `WithPrefix` actually removes the prefix from each script's name, so what is
recorded in the journal table will not become incorrect if you move the scripts to
elsewhere in your namespace.

The `Dependent` scripts declare the scripts they depend on by including comments
such as:

```sql
-- @requires UserTable, ProductTable
```

See `OrderByDependencyTests.cs` for examples.

# What about C# scripts?

In DbUp it is possible to implement `IScript` instead of embedding a static SQL
script. As we cannot cache the contents of C# before we execute it, it seems like we
cannot support this use case.

However, as long as `IScript` is used correcty, there is no problem. The 
`ProvideScript` method is meant to generate and return a SQL script to be executed
later, and we can hash that script.

```cs
public class MyScript : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        // don't have any side-effects on database state here!

        return "dynamically generated SQL...";
    }
}
```

Do not execute any SQL commands inside `ProvideScript` that modify the state of the
database in any way. The method is called *before* the engine starts running scripts,
so if you modify the database here then you mess up the order in which effects occur.

You can *read* information from the database using the command factory provided,
and use that information to influence the script you generate and return.

Note that DbUpX provides some helpful extension methods that are like a minimal form
of the excellent [Dapper](https://github.com/StackExchange/Dapper) library, except
they operate on `Func<IDbCommand>` to better fit in with DbUp.

# Running the Tests

Some of the tests (`IntegrationTests.cs`) run on a real SQL Server database. You can
spin one up by installing [docker](https://www.docker.com/) and running the
`start-sql.sh` script. (On Windows you can rename it to `start-sql.bat` and it should
still be correct.)
