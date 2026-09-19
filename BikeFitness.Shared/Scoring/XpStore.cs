using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using BikeFitness.Shared.SecondRider;

namespace BikeFitness.Shared.Scoring
{
    /// <summary>Something the XP total can unlock. <see cref="AssetName"/> matches the shipped file name.</summary>
    public sealed class Unlock
    {
        public string Id { get; set; } = "";

        public string Label { get; set; } = "";

        public int XpRequired { get; set; }

        /// <summary>True for placeholder cosmetics, so nobody mistakes the POC for art work.</summary>
        public bool Cosmetic { get; set; }

        public string AssetName { get; set; } = "";
    }

    /// <summary>
    /// The POC unlock list. Everything here already exists as an asset — the four biomes the app ships — so
    /// the POC needs no new art, and the eventual wiring is a lookup rather than a rename.
    /// </summary>
    public static class UnlockCatalog
    {
        public static IReadOnlyList<Unlock> Defaults { get; } = new List<Unlock>
        {
            new Unlock { Id = "biome_mountain", Label = "Mountain biome", XpRequired = 100, AssetName = "biome_mountain.png" },
            new Unlock { Id = "jersey_colour", Label = "Jersey colour (placeholder)", XpRequired = 250, Cosmetic = true, AssetName = "cosmetic_jersey.png" },
            new Unlock { Id = "biome_desert", Label = "Desert biome", XpRequired = 500, AssetName = "biome_desert.png" },
            new Unlock { Id = "wheel_colour", Label = "Wheel colour (placeholder)", XpRequired = 800, Cosmetic = true, AssetName = "cosmetic_wheel.png" },
            new Unlock { Id = "biome_ocean", Label = "Ocean biome", XpRequired = 1500, AssetName = "biome_ocean.png" },
            new Unlock { Id = "trail_effect", Label = "Trail effect (placeholder)", XpRequired = 2000, Cosmetic = true, AssetName = "cosmetic_trail.png" },
            new Unlock { Id = "biome_plain", Label = "Plain biome", XpRequired = 3000, AssetName = "biome_plain.png" },
        };
    }

    /// <summary>
    /// POC XP persistence. Deliberately tiny and deliberately quarantined: state lives in the POC scratch
    /// directory (never <c>AppSettings</c>, the app data folder, or a saved ride) and the file is stamped
    /// <c>"poc": true</c> so it can never be mistaken for real progression.
    /// </summary>
    public sealed class XpStore
    {
        public const string DefaultFileName = "xp.poc.json";

        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private XpStore(string scratchPath, int totalXp, IEnumerable<string> seen)
        {
            ScratchPath = scratchPath;
            TotalXp = Math.Max(0, totalXp);
            foreach (string id in seen) _seen.Add(id);
        }

        /// <summary>Path of the scratch file this store reads and writes.</summary>
        public string ScratchPath { get; }

        public int TotalXp { get; private set; }

        /// <summary>Ids already awarded, so an unlock popup fires once.</summary>
        public IReadOnlyCollection<string> SeenIds => _seen;

        /// <summary>Always true — this store is POC state, and says so in the file.</summary>
        public bool Poc => true;

        /// <summary>
        /// Loads the scratch store. A missing, empty or corrupt file yields a fresh store instead of throwing,
        /// because a POC must never be able to block starting a ride.
        /// </summary>
        public static XpStore Load(string? path = null)
        {
            string scratchPath = string.IsNullOrWhiteSpace(path) ? PocScratch.FilePath(DefaultFileName) : path!;

            if (!File.Exists(scratchPath)) return new XpStore(scratchPath, 0, Array.Empty<string>());

            try
            {
                XpFile? file = JsonSerializer.Deserialize<XpFile>(File.ReadAllText(scratchPath), ReadOptions);
                if (file == null) return new XpStore(scratchPath, 0, Array.Empty<string>());

                return new XpStore(scratchPath, file.TotalXp, file.Seen ?? new List<string>());
            }
            catch (Exception)
            {
                return new XpStore(scratchPath, 0, Array.Empty<string>());
            }
        }

        /// <summary>Adds XP. Non-positive or non-finite values are ignored, so the total only ever grows.</summary>
        public void Add(int xp)
        {
            if (xp <= 0) return;
            TotalXp += xp;
        }

        /// <summary>Writes the store. Refuses any path outside the POC scratch directory.</summary>
        public void Save()
        {
            if (!PocScratch.IsInScratch(ScratchPath))
            {
                throw new InvalidOperationException(
                    $"POC state may only be written under '{PocScratch.DirectoryPath}'.");
            }

            PocScratch.EnsureDirectory();
            var file = new XpFile { TotalXp = TotalXp, Poc = true, Seen = _seen.ToList() };
            File.WriteAllText(ScratchPath, JsonSerializer.Serialize(file, WriteOptions));
        }

        /// <summary>
        /// Unlocks earned but not yet shown, lowest threshold first. Reading marks them seen, so calling this
        /// twice never fires the same popup twice; <see cref="Save"/> persists that.
        /// </summary>
        public IReadOnlyList<Unlock> PendingUnlocks()
        {
            var pending = new List<Unlock>();

            foreach (Unlock unlock in UnlockCatalog.Defaults.OrderBy(u => u.XpRequired))
            {
                if (unlock.XpRequired > TotalXp) continue;
                if (_seen.Contains(unlock.Id)) continue;

                _seen.Add(unlock.Id);
                pending.Add(unlock);
            }

            return pending;
        }

        /// <summary>Deletes the scratch file (harness "Reset XP"). Never touches anything else.</summary>
        public void Reset()
        {
            TotalXp = 0;
            _seen.Clear();

            if (File.Exists(ScratchPath) && PocScratch.IsInScratch(ScratchPath))
            {
                File.Delete(ScratchPath);
            }
        }

        private sealed class XpFile
        {
            public int TotalXp { get; set; }
            public bool Poc { get; set; }
            public List<string>? Seen { get; set; }
        }
    }
}
