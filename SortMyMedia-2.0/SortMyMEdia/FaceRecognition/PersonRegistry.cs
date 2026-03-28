using System;
using System.Collections.Generic;
using System.Linq;

namespace SortMyMedia.FaceRecognition
{
    public sealed class PersonRegistry
    {
        private readonly List<PersonProfile> persons = new();
        private int nextId = 1;
        private int nextAutoNameIndex = 1;

        public bool AutoNamingEnabled { get; set; }

        public IReadOnlyList<PersonProfile> Persons => persons;

        public PersonProfile CreatePerson(string? requestedName)
        {
            string finalName = string.IsNullOrWhiteSpace(requestedName)
                ? $"Person_{nextAutoNameIndex++:00}"
                : requestedName.Trim();

            var profile = new PersonProfile(nextId++, finalName);
            persons.Add(profile);
            return profile;
        }

        public PersonProfile GetOrCreatePerson(string? requestedName)
        {
            if (!string.IsNullOrWhiteSpace(requestedName))
            {
                PersonProfile? existing = FindByName(requestedName.Trim());
                if (existing != null)
                    return existing;
            }

            return CreatePerson(requestedName);
        }

        public void AddEmbedding(PersonProfile person, float[] embedding, float quality)
        {
            person.AddEmbedding(embedding, quality);
        }

        public PersonProfile? FindByName(string name)
        {
            return persons.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<string> GetKnownNames()
        {
            return persons
                .Select(p => p.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();
        }
    }
}
