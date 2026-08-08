namespace SpecScribe;

/// <summary>First-wins indexing for the numbered planning entities a user authors by hand — epics keyed by
/// <c>Number</c>, acceptance criteria keyed by <c>Number</c>, per-epic progress rows keyed by <c>Number</c>.
///
/// <para><b>Why this exists.</b> Eleven call sites built these lookups and ten of them used a bare
/// <c>ToDictionary(e =&gt; e.Number)</c>, which throws <see cref="ArgumentException"/> the moment a repository's
/// <c>epics.md</c> declares the same epic number twice — a typo in a hand-authored planning file, not a
/// programming error. The eleventh (<c>RequirementsTemplater</c>) had already worked around it with
/// <c>GroupBy(...).ToDictionary(g =&gt; g.Key, g =&gt; g.First())</c>. A codebase that disagrees with itself about
/// what a duplicate epic number means is the single-source-of-truth violation; the crash is only its symptom.
/// Story 17.1 settled it on the tolerant policy — SpecScribe documents whatever a repository actually contains,
/// so a duplicated number must render, not abort the run.</para>
///
/// <para>First-wins matches the behaviour <c>RequirementsTemplater</c> already shipped, and reads the file the
/// way a person does: the first declaration of "Epic 7" is the one that counts. <see cref="Dictionary{TKey,
/// TValue}.TryAdd"/> gives that directly, without <c>GroupBy</c>'s intermediate groupings.</para></summary>
internal static class NumberIndex
{
    /// <summary>Indexes <paramref name="source"/> by <paramref name="key"/>, keeping the FIRST item for any
    /// repeated key instead of throwing.</summary>
    public static Dictionary<TKey, TSource> ByFirst<TSource, TKey>(
        this IEnumerable<TSource> source, Func<TSource, TKey> key)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, TSource>();
        foreach (var item in source) result.TryAdd(key(item), item);
        return result;
    }

    /// <summary>Indexes <paramref name="source"/> by <paramref name="key"/> projecting each item through
    /// <paramref name="value"/>, keeping the FIRST item for any repeated key instead of throwing.</summary>
    public static Dictionary<TKey, TValue> ByFirst<TSource, TKey, TValue>(
        this IEnumerable<TSource> source, Func<TSource, TKey> key, Func<TSource, TValue> value)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>();
        foreach (var item in source) result.TryAdd(key(item), value(item));
        return result;
    }
}
