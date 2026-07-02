using System.Collections.Generic;
using Hwatu.Core;
using NUnit.Framework;

namespace Hwatu.View.Tests
{
    public class CardArtDatabaseTests
    {
        [Test]
        public void StandardDeckCards_have_base_sprite_entries()
        {
            var db = CardArtDatabase.Instance;
            Assert.IsNotNull(db, "Resources/CardArtDatabase.asset must exist.");

            var keys = new HashSet<string>();
            var missing = new List<string>();

            foreach (var card in CardFactory.CreateStandardDeck())
            {
                var key = CardView.ArtIdOf(card);
                if (!string.IsNullOrEmpty(key))
                    keys.Add(key);

                if (string.IsNullOrEmpty(key) ||
                    !db.TryGetBase(key, out var sprite) ||
                    sprite == null)
                {
                    missing.Add($"{card.Id}:{card.DebugName}->{key ?? "(null)"}");
                }
            }

            Assert.AreEqual(48, keys.Count, "The standard deck should resolve to 48 unique art ids.");
            Assert.That(missing, Is.Empty, "Missing card art: " + string.Join(", ", missing));
        }
    }
}
