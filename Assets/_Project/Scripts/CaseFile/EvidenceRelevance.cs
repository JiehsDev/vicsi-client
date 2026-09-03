// Assets/_Project/Scripts/CaseFile/EvidenceRelevance.cs

/// <summary>
/// How much an evidence item actually bears on the correct conclusion. This is
/// AUTHORING METADATA for scoring - it is deliberately not a forensic-type taxonomy
/// (biological / trace / impression / testimonial), which remains a separate
/// deferred piece of work.
///
/// Nothing enforces this at interaction time and nothing should: a Distractor is a
/// real EvidenceProp that tents, photographs and collects exactly like any other
/// item. Gating on relevance would tell the student which items matter, which is
/// precisely the judgment the scenario is trying to measure. Relevance is read
/// afterwards, by scoring, against what the player actually did.
///
/// Values are explicit for the same reason EvidenceStatus's are: these are
/// persisted as ints in EvidenceDefinition asset YAML.
/// </summary>
public enum EvidenceRelevance
{
    /// <summary>Directly establishes the correct conclusion; missing it should cost the most.</summary>
    Critical = 0,

    /// <summary>Genuinely probative but not decisive on its own - supports or narrows a theory.</summary>
    Relevant = 1,

    /// <summary>Real evidence, correctly collectible, but does not discriminate between theories.</summary>
    Neutral = 2,

    /// <summary>Deliberately misleading - plausible-looking and designed to support a wrong theory.</summary>
    Distractor = 3
}
