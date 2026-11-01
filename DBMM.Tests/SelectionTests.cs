using EXM.DBMM;
using Xunit;

namespace DBMM.Tests;

public class SelectionTests {
    [Fact]
    public void EditingFilteredSelectionPreservesHiddenValues() {
        var source = new Dictionary<string, List<string>> { ["A"] = new() { "A_VAR", "B_VAR" } };
        var editor = new VariableSelectionEditor(source);
        Assert.Equal(new[] { "A_VAR" }, editor.Configured("A", "a_"));
        editor.Add("A", new[] { "NEW_VAR" });
        editor.Remove("A", new[] { "A_VAR" });
        Assert.Equal(new[] { "B_VAR", "NEW_VAR" }, editor.Configured("A"));
        Assert.Equal(2, source["A"].Count); // Cancel still leaves the original untouched.
    }

    [Fact]
    public void ShorteningOrReplacingSearchRestoresMatchesFromSource() {
        var editor = new VariableSelectionEditor(new() { ["A"] = new() { "ALPHA", "ALPINE", "BETA" } });
        Assert.Single(editor.Configured("A", "ALPH"));
        Assert.Equal(2, editor.Configured("A", "AL").Length);
        Assert.Equal(new[] { "BETA" }, editor.Configured("A", "BETA"));
        var all = new[] { "ALPHA", "ALPINE", "BETA", "DELTA", "DENT" };
        Assert.Single(editor.Available("A", all, "DELT"));
        Assert.Equal(2, editor.Available("A", all, "DE").Length);
        Assert.Equal(new[] { "DENT" }, editor.Available("A", all, "DENT"));
    }

    [Fact]
    public void BrowsingDoesNotCreateEmptySelections() {
        var editor = new VariableSelectionEditor(new());
        Assert.Empty(editor.Configured("A"));
        Assert.Empty(editor.Snapshot());
        editor.Clear("A");
        Assert.True(editor.Snapshot().ContainsKey("A"));
        Assert.Empty(editor.Snapshot()["A"]);
    }
}
