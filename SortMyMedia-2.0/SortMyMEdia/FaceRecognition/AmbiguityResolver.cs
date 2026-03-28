using System;
using System.Collections.Generic;

namespace SortMyMedia.FaceRecognition
{
    public sealed class AmbiguityResolver
    {
        private readonly IPersonInteractionService interactionService;

        public AmbiguityResolver(IPersonInteractionService interactionService)
        {
            this.interactionService = interactionService;
        }

        public PersonProfile Resolve(
            PersonResolutionResult resolution,
            IReadOnlyList<string> representativeThumbnails,
            Func<PersonProfile> createNewPerson,
            Action<string>? log = null)
        {
            foreach (var candidate in resolution.Candidates)
            {
                bool confirmed = interactionService.ConfirmCandidate(candidate.person.Name, representativeThumbnails);
                if (confirmed)
                {
                    log?.Invoke($"Resolved ambiguous cluster as {candidate.person.Name} (distance {candidate.distance:F3})");
                    return candidate.person;
                }
            }

            return createNewPerson();
        }
    }
}
