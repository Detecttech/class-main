using System;
using UnityEngine;

namespace QuizBattle.Characters
{
    /// Runtime loader for the 4 v1 character definitions — Resources.LoadAll keeps this
    /// working unmodified on-device (Android) and in the Editor alike.
    public static class CharacterCatalogLoader
    {
        private static CharacterDefinitionSO[] _cached;

        public static CharacterDefinitionSO[] LoadAll()
        {
            if (_cached == null || _cached.Length == 0)
            {
                _cached = Resources.LoadAll<CharacterDefinitionSO>("Characters");
                Array.Sort(_cached, (a, b) => string.CompareOrdinal(a.characterId, b.characterId));
            }
            return _cached;
        }
    }
}
