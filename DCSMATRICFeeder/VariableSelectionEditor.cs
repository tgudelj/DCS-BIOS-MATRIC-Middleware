namespace EXM.DBMM;

// Editing and searching never use a filtered view as the source of saved selections.
internal sealed class VariableSelectionEditor {
    private readonly Dictionary<string, List<string>> _selections;
    public VariableSelectionEditor(Dictionary<string, List<string>> selections) =>
        _selections = selections.ToDictionary(p => p.Key, p => p.Value.Distinct(StringComparer.Ordinal).ToList());
    public Dictionary<string, List<string>> Snapshot() => _selections.ToDictionary(p => p.Key, p => p.Value.ToList());
    public string[] Configured(string aircraft, string search = "") =>
        (_selections.GetValueOrDefault(aircraft) ?? new()).Where(v => Matches(v, search)).OrderBy(v => v, StringComparer.Ordinal).ToArray();
    public string[] Available(string aircraft, IEnumerable<string> source, string search = "") =>
        source.Except(Configured(aircraft), StringComparer.Ordinal).Where(v => Matches(v, search))
            .Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToArray();
    private static bool Matches(string value, string search) => value.Contains(search, StringComparison.OrdinalIgnoreCase);
    public void Add(string aircraft, IEnumerable<string> variables) =>
        _selections[aircraft] = Configured(aircraft).Concat(variables).Distinct(StringComparer.Ordinal).ToList();
    public void Remove(string aircraft, IEnumerable<string> variables) =>
        _selections[aircraft] = Configured(aircraft).Except(variables, StringComparer.Ordinal).ToList();
    public void Clear(string aircraft) => _selections[aircraft] = new();
}
