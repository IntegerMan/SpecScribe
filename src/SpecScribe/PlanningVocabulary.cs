namespace SpecScribe;

/// <summary>Framework-native names for the two normalized planning levels. Internal models retain their
/// stable Epic/Story names; renderers use this vocabulary so a framework is described in its own terms.</summary>
public sealed record PlanningVocabulary(
    string PrimarySingular,
    string PrimaryPlural,
    string SecondarySingular,
    string SecondaryPlural)
{
    public static readonly PlanningVocabulary Default = new("Epic", "Epics", "Story", "Stories");
    public static readonly PlanningVocabulary GsdCore = new("Phase", "Phases", "Plan", "Plans");
}