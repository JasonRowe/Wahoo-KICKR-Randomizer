using System;
using System.IO;
using BikeFitness.Shared.Scoring;
using BikeFitness.Shared.SecondRider;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BikeFitnessApp.Tests.Scoring
{
    /// <summary>
    /// L0 tests for the POC's XP persistence. Every test asserts the quarantine as well as the behaviour:
    /// state lives under the POC scratch directory and nowhere else.
    /// </summary>
    [TestClass]
    public class XpStoreTests
    {
        [TestMethod]
        public void RoundTrip_PreservesTotalXp()
        {
            string path = Scratch("roundtrip");

            try
            {
                XpStore store = XpStore.Load(path);
                store.Add(450);
                store.Save();

                Assert.AreEqual(450, XpStore.Load(path).TotalXp);
            }
            finally
            {
                Delete(path);
            }
        }

        [TestMethod]
        public void CorruptFile_YieldsFreshStore_NoThrow()
        {
            string path = Scratch("corrupt");

            try
            {
                File.WriteAllText(path, "{ not json at all");

                XpStore store = XpStore.Load(path);

                Assert.AreEqual(0, store.TotalXp);
                Assert.AreEqual(0, store.PendingUnlocks().Count);
            }
            finally
            {
                Delete(path);
            }
        }

        [TestMethod]
        public void MissingFile_YieldsFreshStore()
        {
            string path = Scratch("missing");

            XpStore store = XpStore.Load(path);

            Assert.AreEqual(0, store.TotalXp);
            Assert.IsFalse(File.Exists(path), "loading must not create the file");
        }

        [TestMethod]
        public void WritesOnlyUnderScratchPath()
        {
            XpStore store = XpStore.Load();

            Assert.IsTrue(
                store.ScratchPath.StartsWith(Path.GetTempPath(), StringComparison.Ordinal),
                $"scratch path escaped the temp directory: {store.ScratchPath}");
            Assert.IsTrue(store.ScratchPath.Contains(PocScratch.FolderName, StringComparison.Ordinal));
            Assert.IsTrue(PocScratch.IsInScratch(store.ScratchPath));
        }

        [TestMethod]
        public void Save_OutsideScratch_Throws()
        {
            string outside = Path.Combine(Path.GetTempPath(), $"not-poc-xp-{Guid.NewGuid():N}.json");

            try
            {
                XpStore store = XpStore.Load(outside);
                store.Add(100);

                Assert.ThrowsExactly<InvalidOperationException>(() => store.Save());
                Assert.IsFalse(File.Exists(outside), "a refused save must not create the file");
            }
            finally
            {
                Delete(outside);
            }
        }

        [TestMethod]
        public void PendingUnlocks_FireOnce_ThenAreMarkedSeen()
        {
            string path = Scratch("unlocks");

            try
            {
                XpStore store = XpStore.Load(path);
                store.Add(150);

                var first = store.PendingUnlocks();
                Assert.AreEqual(1, first.Count, "150 XP must unlock exactly the 100 XP biome");
                Assert.AreEqual("biome_mountain", first[0].Id);

                Assert.AreEqual(0, store.PendingUnlocks().Count, "an unlock must not fire twice");

                store.Save();
                Assert.AreEqual(0, XpStore.Load(path).PendingUnlocks().Count, "seen unlocks must persist");
            }
            finally
            {
                Delete(path);
            }
        }

        [TestMethod]
        public void PendingUnlocks_ReturnInThresholdOrder()
        {
            string path = Scratch("order");

            try
            {
                XpStore store = XpStore.Load(path);
                store.Add(600);

                var pending = store.PendingUnlocks();

                Assert.IsTrue(pending.Count >= 3, "600 XP must unlock several entries");
                for (int i = 1; i < pending.Count; i++)
                {
                    Assert.IsTrue(pending[i].XpRequired >= pending[i - 1].XpRequired, "unlocks must arrive cheapest first");
                }
            }
            finally
            {
                Delete(path);
            }
        }

        [TestMethod]
        public void DoubleLoad_DoesNotDuplicateAwards()
        {
            string path = Scratch("double");

            try
            {
                XpStore writer = XpStore.Load(path);
                writer.Add(500);
                writer.Save();

                int first = XpStore.Load(path).TotalXp;
                int second = XpStore.Load(path).TotalXp;

                Assert.AreEqual(500, first);
                Assert.AreEqual(first, second, "loading twice must not re-award XP");
            }
            finally
            {
                Delete(path);
            }
        }

        [TestMethod]
        public void Add_IgnoresNonPositiveValues()
        {
            string path = Scratch("nonpositive");

            try
            {
                XpStore store = XpStore.Load(path);
                store.Add(200);
                store.Add(0);
                store.Add(-50);

                Assert.AreEqual(200, store.TotalXp);
            }
            finally
            {
                Delete(path);
            }
        }

        [TestMethod]
        public void Reset_DeletesOnlyTheScratchFile()
        {
            string path = Scratch("reset");
            string bystander = Path.Combine(Path.GetTempPath(), $"bystander-{Guid.NewGuid():N}.json");

            try
            {
                File.WriteAllText(bystander, "keep me");

                XpStore store = XpStore.Load(path);
                store.Add(500);
                store.Save();
                store.Reset();

                Assert.AreEqual(0, store.TotalXp);
                Assert.IsFalse(File.Exists(path), "reset must delete the POC scratch file");
                Assert.IsTrue(File.Exists(bystander), "reset must not touch anything else");
                Assert.AreEqual(0, XpStore.Load(path).TotalXp);
            }
            finally
            {
                Delete(path);
                Delete(bystander);
            }
        }

        [TestMethod]
        public void Catalog_NamesMatchShippedAssets_AndMarksCosmetics()
        {
            var ids = new System.Collections.Generic.List<string>();
            int biomeCount = 0;
            int cosmeticCount = 0;

            foreach (Unlock unlock in UnlockCatalog.Defaults)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(unlock.Id), "every unlock needs an id");
                Assert.IsTrue(unlock.XpRequired > 0, $"{unlock.Id} needs a positive threshold");
                ids.Add(unlock.Id);

                if (unlock.Cosmetic) cosmeticCount++;
                else
                {
                    biomeCount++;
                    Assert.IsTrue(
                        unlock.AssetName.StartsWith("biome_", StringComparison.Ordinal),
                        $"{unlock.Id} should reference a shipped biome asset");
                }
            }

            Assert.AreEqual(4, biomeCount, "the four shipped biomes must all appear");
            Assert.AreEqual(3, cosmeticCount, "three placeholder cosmetics, clearly marked");
            Assert.AreEqual(ids.Count, new System.Collections.Generic.HashSet<string>(ids).Count, "ids must be unique");
        }

        private static string Scratch(string tag)
        {
            PocScratch.EnsureDirectory();
            return PocScratch.FilePath($"xp-test-{tag}-{Guid.NewGuid():N}.json");
        }

        private static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
