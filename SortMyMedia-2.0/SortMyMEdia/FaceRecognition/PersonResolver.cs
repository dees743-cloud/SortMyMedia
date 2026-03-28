using System;
using System.Collections.Generic;
using System.Linq;

namespace SortMyMedia.FaceRecognition
{
    public enum PersonResolutionKind
    {
        Match,
        NewPerson,
        Ambiguous
    }

    public sealed class PersonResolutionResult
    {
        public PersonResolutionResult(PersonResolutionKind kind, PersonProfile? match, IReadOnlyList<(PersonProfile person, float distance)> candidates)
        {
            Kind = kind;
            Match = match;
            Candidates = candidates;
        }

        public PersonResolutionKind Kind { get; }
        public PersonProfile? Match { get; }
        public IReadOnlyList<(PersonProfile person, float distance)> Candidates { get; }
    }

    public sealed class PersonResolver
    {
        private readonly float strictThreshold;
        private readonly float looseThreshold;
        private readonly int maxCandidates;

        public PersonResolver(float strictThreshold, float looseThreshold, int maxCandidates)
        {
            this.strictThreshold = strictThreshold;
            this.looseThreshold = Math.Max(looseThreshold, strictThreshold + 0.01f);
            this.maxCandidates = Math.Max(1, maxCandidates);
        }

        public PersonResolutionResult Resolve(float[] embedding, PersonRegistry registry)
        {
            if (registry.Persons.Count == 0)
                return new PersonResolutionResult(PersonResolutionKind.NewPerson, null, Array.Empty<(PersonProfile person, float distance)>());

            var normalized = FaceComparer.NormalizeCopy(embedding);
            var scored = registry.Persons
                .Where(p => p.Centroid.Length == normalized.Length)
                .Select(p => (person: p, distance: FaceComparer.CosineDistance(normalized, p.Centroid)))
                .OrderBy(x => x.distance)
                .ToList();

            if (scored.Count == 0)
                return new PersonResolutionResult(PersonResolutionKind.NewPerson, null, Array.Empty<(PersonProfile person, float distance)>());

            var best = scored[0];
            if (best.distance <= strictThreshold)
                return new PersonResolutionResult(PersonResolutionKind.Match, best.person, Array.Empty<(PersonProfile person, float distance)>());

            if (best.distance > looseThreshold)
                return new PersonResolutionResult(PersonResolutionKind.NewPerson, null, Array.Empty<(PersonProfile person, float distance)>());

            var candidates = scored
                .Where(x => x.distance <= looseThreshold)
                .Take(maxCandidates)
                .ToList();

            return new PersonResolutionResult(PersonResolutionKind.Ambiguous, null, candidates);
        }
    }
}
