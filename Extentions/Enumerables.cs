using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Next.Extention
{
    public static class Enumerables
    {
        /// <summary>
        /// Converts an IEnumerable&lt;dynamic&gt; (Dapper result set) into a DataTable.
        ///
        /// COLUMN RULES
        /// - Columns are inferred from the first row's keys
        /// - All columns use typeof(object) for maximum compatibility
        ///
        /// SAFETY
        /// - Returns an empty DataTable if rows is null or empty
        /// - Null values are converted to DBNull.Value
        ///
        /// INTENDED USE
        /// - WinForms DataGridView binding
        /// - UI-facing service/controller layers
        ///
        /// NOT INTENDED FOR
        /// - Core data-access libraries
        /// - Business/domain logic
        /// </summary>
        /// <param name="rows">Dynamic query results (typically from Dapper)</param>
        /// <param name="tableName">Optional DataTable name (default: "Result")</param>
        public static DataTable ToDataTable(this IEnumerable<dynamic> rows,
            string tableName = "Result")
        {
            var dt = new DataTable(tableName);

            if (rows == null)
                return dt;

            var list = rows as IList<dynamic> ?? rows.ToList();
            if (list.Count == 0)
                return dt;

            // Dapper returns ExpandoObject → IDictionary<string, object>
            var firstRow = (IDictionary<string, object>)list[0];

            // Build schema
            foreach (var key in firstRow.Keys)
            {
                dt.Columns.Add(key, typeof(object));
            }

            // Populate rows
            foreach (var row in list)
            {
                var dr = dt.NewRow();
                var dict = (IDictionary<string, object>)row;

                foreach (var kvp in dict)
                {
                    dr[kvp.Key] = kvp.Value ?? DBNull.Value;
                }

                dt.Rows.Add(dr);
            }

            return dt;
        }

        /// <summary>
        /// Converts an IEnumerable&lt;IDictionary&lt;string, object&gt;&gt; into a DataTable.
        ///
        /// This overload exists for explicit dictionary-based pipelines or
        /// non-dynamic callers.
        /// </summary>
        /// <param name="rows">Dictionary-based row data</param>
        /// <param name="tableName">Optional DataTable name (default: "Result")</param>
        public static DataTable ToDataTable(this IEnumerable<IDictionary<string,
            object>> rows,
            string tableName = "Result")
        {
            var dt = new DataTable(tableName);

            if (rows == null)
                return dt;

            var list = rows as IList<IDictionary<string, object>> ?? rows.ToList();
            if (list.Count == 0)
                return dt;

            var firstRow = list[0];

            foreach (var key in firstRow.Keys)
            {
                dt.Columns.Add(key, typeof(object));
            }

            foreach (var row in list)
            {
                var dr = dt.NewRow();
                foreach (var kvp in row)
                {
                    dr[kvp.Key] = kvp.Value ?? DBNull.Value;
                }
                dt.Rows.Add(dr);
            }

            return dt;
        }

        /// <summary>
        /// Returns the source sequence if not null; otherwise returns an empty sequence.
        /// Prevents null-reference exceptions in method chains.
        /// </summary>
        /// <example>
        /// <code>
        /// GetItems()                  // might return null
        ///     .OrEmpty()
        ///     .Where(x => x.IsActive)
        ///     .ForEach(x => Console.WriteLine(x.Name));
        /// </code>
        /// </example>
        public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T> source)
            => source ?? Enumerable.Empty<T>();

        /// <summary>
        /// Executes an action for each element, providing the 0-based index.
        /// Skips execution if source is null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if action is null.</exception>
        /// <example>
        /// <code>
        /// new[] { "Alice", "Bob" }.ForEach((name, i) => Console.WriteLine($"{i+1}: {name}"));
        /// </code>
        /// </example>
        public static void ForEach<T>(this IEnumerable<T> source, Action<T, int> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (source == null) return;

            int index = 0;
            foreach (var item in source)
            {
                action(item, index++);
            }
        }

        /// <summary>
        /// Joins non-null/non-empty/whitespace strings, trimming each item.
        /// Returns empty string if source is null or contains no valid items.
        /// </summary>
        /// <example>
        /// <code>
        /// new[] { "C#", "  ", null, "WinForms" }.JoinNotEmpty(" | ");
        /// // → "C# | WinForms"
        /// </code>
        /// </example>
        public static string JoinNotEmpty(this IEnumerable<string> items, string separator = ", ")
        {
            if (items == null) return string.Empty;

            return string.Join(separator,
                items.Where(s => !string.IsNullOrWhiteSpace(s))
                     .Select(s => s!.Trim()));
        }

        /// <summary>
        /// Splits the sequence into chunks of max size. Lazy enumeration.
        /// Useful for batch inserts, API calls, exports.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">If chunkSize ≤ 0.</exception>
        /// <example>
        /// <code>
        /// Enumerable.Range(1, 10).Chunk(3)
        ///     .Select(batch => string.Join(", ", batch));
        /// // → "1, 2, 3"  "4, 5, 6"  "7, 8, 9"  "10"
        /// </code>
        /// </example>
        public static IEnumerable<IReadOnlyList<T>> Chunk<T>(this IEnumerable<T> source, int chunkSize)
        {
            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Must be positive.");

            if (source == null) yield break;

            using var enumerator = source.GetEnumerator();

            while (true)
            {
                var chunk = new List<T>(chunkSize);
                for (int i = 0; i < chunkSize && enumerator.MoveNext(); i++)
                {
                    chunk.Add(enumerator.Current);
                }

                if (chunk.Count == 0) yield break;

                yield return chunk.AsReadOnly();  // immutable view
            }
        }

        /// <summary>
        /// Returns distinct elements based on a key selector (preserves first occurrence order).
        /// </summary>
        /// <exception cref="ArgumentNullException">If source or keySelector is null.</exception>
        /// <example>
        /// <code>
        /// orders.DistinctBy(o => o.OrderId);
        /// </code>
        /// </example>
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            var seen = new HashSet<TKey>(comparer);

            foreach (var element in source)
            {
                var key = keySelector(element);
                if (seen.Add(key))
                    yield return element;
            }
        }

        /// <summary>
        /// Materializes to List with suggested capacity (performance hint).
        /// </summary>
        /// <exception cref="ArgumentNullException">If source is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If estimatedCapacity < 0.</exception>
        public static List<T> ToListWithCapacity<T>(this IEnumerable<T> source, int estimatedCapacity)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (estimatedCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(estimatedCapacity), "Cannot be negative.");

            var list = new List<T>(estimatedCapacity);
            foreach (var item in source)
                list.Add(item);

            return list;
        }

        /// <summary>
        /// First element or specified default value if empty.
        /// </summary>
        /// <example>
        /// <code>
        /// emptyList.FirstOrDefault(-1);  // → -1
        /// </code>
        /// </example>
        public static T FirstOrDefault<T>(this IEnumerable<T> source, T defaultValue = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            using var e = source.GetEnumerator();
            return e.MoveNext() ? e.Current : defaultValue;
        }

        /// <summary>
        /// Materializes once and returns both items and count (avoids double enumeration).
        /// </summary>
        /// <exception cref="ArgumentNullException">If source is null.</exception>
        public static (IReadOnlyList<T> Items, int Count) ToListAndCount<T>(this IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var list = source.ToList();
            return (list.AsReadOnly(), list.Count);
        }

        /// <summary>
        /// Returns every Nth element from the sequence, starting from index 0 (first element).
        /// Useful for sampling, pagination previews, or processing in steps.
        /// </summary>
        /// <typeparam name="T">The type of elements.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="step">How many elements to skip between returned ones (must be ≥ 1).</param>
        /// <returns>Elements at positions 0, step, 2×step, etc.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If step ≤ 0.</exception>
        /// <exception cref="ArgumentNullException">If source is null.</exception>
        /// <example>
        /// <code>
        /// var numbers = Enumerable.Range(1, 20);
        /// var everyThird = numbers.EveryNth(3);  // → 1, 4, 7, 10, 13, 16, 19
        /// </code>
        /// </example>
        public static IEnumerable<T> EveryNth<T>(this IEnumerable<T> source, int step)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step), "Must be positive.");

            int index = 0;
            foreach (var item in source)
            {
                if (index % step == 0)
                    yield return item;

                index++;
            }
        }

        /// <summary>
        /// Splits the sequence into pages (for UI pagination, report generation, etc.).
        /// Returns one page at a time (lazy).
        /// </summary>
        /// <typeparam name="T">The type of elements.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="pageSize">Number of items per page (must be > 0).</param>
        /// <returns>Enumerable of pages, each as IReadOnlyList&lt;T&gt;.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If pageSize ≤ 0.</exception>
        /// <example>
        /// <code>
        /// var items = Enumerable.Range(1, 25);
        /// foreach (var page in items.Page(10))
        /// {
        ///     Console.WriteLine($"Page: {string.Join(", ", page)}");
        /// }
        /// // → Page: 1..10   Page: 11..20   Page: 21..25
        /// </code>
        /// </example>
        public static IEnumerable<IReadOnlyList<T>> Page<T>(this IEnumerable<T> source, int pageSize)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize), "Must be positive.");

            return source.Chunk(pageSize);  // Reuse Chunk from earlier!
        }

        /// <summary>
        /// Returns the sequence with an index attached to each element (0-based).
        /// Handy for displaying row numbers in grids, logs, etc.
        /// </summary>
        /// <typeparam name="T">The type of elements.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <returns>Sequence of (Index, Item) tuples.</returns>
        /// <example>
        /// <code>
        /// var fruits = new[] { "Apple", "Banana", "Cherry" };
        /// foreach (var (idx, fruit) in fruits.WithIndex())
        /// {
        ///     Console.WriteLine($"{idx + 1}. {fruit}");
        /// }
        /// </code>
        /// </example>
        public static IEnumerable<(int Index, T Item)> WithIndex<T>(this IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            int index = 0;
            foreach (var item in source)
            {
                yield return (index++, item);
            }
        }

        /// <summary>
        /// Checks if the sequence has any duplicates (based on default equality).
        /// Fast path: stops at first duplicate found.
        /// </summary>
        /// <typeparam name="T">The type of elements.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <returns>true if duplicates exist, false otherwise.</returns>
        /// <example>
        /// <code>
        /// var ids = new[] { 1, 2, 3, 2, 4 };
        /// bool hasDup = ids.HasDuplicates();  // → true
        /// </code>
        /// </example>
        public static bool HasDuplicates<T>(this IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var seen = new HashSet<T>();
            foreach (var item in source)
            {
                if (!seen.Add(item))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns distinct elements preserving original order (alias for DistinctBy with identity selector).
        /// </summary>
        /// <typeparam name="T">The type of elements.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <returns>Distinct elements in first-occurrence order.</returns>
        /// <example>
        /// <code>
        /// var duplicates = new[] { "a", "b", "a", "c", "b" };
        /// var unique = duplicates.DistinctPreservingOrder();  // → "a", "b", "c"
        /// </code>
        /// </example>
        public static IEnumerable<T> DistinctPreservingOrder<T>(this IEnumerable<T> source)
        {
            return source.DistinctBy(x => x);  // Reuse DistinctBy
        }

        /// <summary>
        /// Splits the sequence into two groups based on a predicate (true/false).
        /// Single-pass, memory efficient for small-to-medium collections.
        /// </summary>
        /// <typeparam name="T">The type of elements.</typeparam>
        /// <param name="source">The source sequence.</param>
        /// <param name="predicate">Condition to split on.</param>
        /// <returns>Tuple of (Matches, NonMatches) as lists.</returns>
        /// <exception cref="ArgumentNullException">If source or predicate is null.</exception>
        /// <example>
        /// <code>
        /// var numbers = Enumerable.Range(1, 10);
        /// var (evens, odds) = numbers.SplitBy(x => x % 2 == 0);
        /// // evens: 2,4,6,8,10    odds: 1,3,5,7,9
        /// </code>
        /// </example>
        public static (IReadOnlyList<T> Matches, IReadOnlyList<T> NonMatches) SplitBy<T>(
            this IEnumerable<T> source,
            Func<T, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            var matches = new List<T>();
            var nonMatches = new List<T>();

            foreach (var item in source)
            {
                (predicate(item) ? matches : nonMatches).Add(item);
            }

            return (matches.AsReadOnly(), nonMatches.AsReadOnly());
        }

        /// <summary>
        /// Materializes the async sequence into a <see cref="List{T}"/> asynchronously.
        /// Similar to <c>ToList()</c> for sync sequences.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="source">The async sequence.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes with the materialized list.</returns>
        /// <exception cref="ArgumentNullException">If source is null.</exception>
        /// <example>
        /// <code>
        /// var allUsers = await dbContext.Users
        ///     .AsAsyncEnumerable()
        ///     .ToListAsync();
        /// </code>
        /// </example>
        public static async Task<List<T>> ToListAsync<T>(
            this IAsyncEnumerable<T> source,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var list = new List<T>();
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                list.Add(item);
            }
            return list;
        }

        /// <summary>
        /// Materializes the async sequence into a <see cref="List{T}"/> with pre-allocated capacity.
        /// Useful when you have a size hint (e.g., from API metadata).
        /// </summary>
        public static async Task<List<T>> ToListWithCapacityAsync<T>(
            this IAsyncEnumerable<T> source,
            int estimatedCapacity,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (estimatedCapacity < 0) throw new ArgumentOutOfRangeException(nameof(estimatedCapacity));

            var list = new List<T>(estimatedCapacity);
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                list.Add(item);
            }
            return list;
        }

        /// <summary>
        /// Executes an async action for each element in the sequence.
        /// Processes items sequentially (one at a time).
        /// </summary>
        /// <example>
        /// <code>
        /// await items.ForEachAsync(async item =>
        /// {
        ///     await ProcessItemAsync(item);
        /// });
        /// </code>
        /// </example>
        public static async Task ForEachAsync<T>(
            this IAsyncEnumerable<T> source,
            Func<T, Task> asyncAction,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (asyncAction == null) throw new ArgumentNullException(nameof(asyncAction));

            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                await asyncAction(item);
            }
        }

        /// <summary>
        /// Executes an async action for each element, with index.
        /// </summary>
        public static async Task ForEachAsync<T>(
            this IAsyncEnumerable<T> source,
            Func<T, int, Task> asyncAction,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (asyncAction == null) throw new ArgumentNullException(nameof(asyncAction));

            int index = 0;
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                await asyncAction(item, index++);
            }
        }

        /// <summary>
        /// Batches/chunks the async stream (similar to Chunk for sync).
        /// </summary>
        /// <example>
        /// <code>
        /// await foreach (var batch in largeStream.ChunkAsync(100))
        /// {
        ///     await db.BulkInsertAsync(batch);
        /// }
        /// </code>
        /// </example>
        public static async IAsyncEnumerable<IReadOnlyList<T>> ChunkAsync<T>(
            this IAsyncEnumerable<T> source,
            int chunkSize,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));

            var chunk = new List<T>(chunkSize);

            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                chunk.Add(item);
                if (chunk.Count == chunkSize)
                {
                    yield return chunk.AsReadOnly();
                    chunk = new List<T>(chunkSize);
                }
            }

            if (chunk.Count > 0)
                yield return chunk.AsReadOnly();
        }

        /// <summary>
        /// Converts sync <see cref="IEnumerable{T}"/> to <see cref="IAsyncEnumerable{T}"/>.
        /// Useful when mixing sync & async code.
        /// </summary>
        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
            this IEnumerable<T> source,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            foreach (var item in source)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
        }

        /// <summary>
        /// Takes elements until the first one that doesn't satisfy the predicate.
        /// Stops early (useful for ordered streams).
        /// </summary>
        public static async IAsyncEnumerable<T> TakeWhileAsync<T>(
            this IAsyncEnumerable<T> source,
            Func<T, bool> predicate,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                if (!predicate(item)) yield break;
                yield return item;
            }
        }
    }
}
