using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Entity.Enum;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.Server.Entity
{
    /// <summary>
    /// A spell component with a power level between 1-10,
    /// that determines the windup motion
    /// </summary>
    public enum Scarab
    {
        Lead     = 1,
        Iron     = 2,
        Copper   = 3,
        Silver   = 4,
        Gold     = 5,
        Pyreal   = 6,
        Diamond  = 110,
        Platinum = 112,
        Dark     = 192,
        Mana     = 193
    }

    /// <summary>
    /// The components required to cast a spell
    /// </summary>
    public class SpellFormula
    {
        /// <summary>
        /// A mapping of scarabs => their spell levels
        /// If the first component in a spell is a scarab,
        /// the client uses this to determine the spell level,
        /// for things like the spellbook filters.
        /// </summary>
        public static Dictionary<Scarab, uint> ScarabLevel = new Dictionary<Scarab, uint>()
        {
            { Scarab.Lead,     1 },
            { Scarab.Iron,     2 },
            { Scarab.Copper,   3 },
            { Scarab.Silver,   4 },
            { Scarab.Gold,     5 },
            { Scarab.Pyreal,   6 },
            { Scarab.Diamond,  6 },
            { Scarab.Platinum, 7 },
            { Scarab.Dark,     7 },
            { Scarab.Mana,     8 }
        };

        /// <summary>
        /// A mapping of scarabs => their power levels
        /// </summary>
        public static Dictionary<Scarab, uint> ScarabPower = new Dictionary<Scarab, uint>()
        {
            { Scarab.Lead,     1 },
            { Scarab.Iron,     2 },
            { Scarab.Copper,   3 },
            { Scarab.Silver,   4 },
            { Scarab.Gold,     5 },
            { Scarab.Pyreal,   6 },
            { Scarab.Diamond,  7 },
            { Scarab.Platinum, 8 },
            { Scarab.Dark,     9 },
            { Scarab.Mana,    10 }
        };

        /// <summary>
        /// Returns the spell level for a scarab
        /// </summary>
        public static uint GetLevel(Scarab scarab)
        {
            return ScarabLevel[scarab];
        }

        /// <summary>
        /// Returns the power level for a scarab
        /// </summary>
        public static uint GetPower(Scarab scarab)
        {
            return ScarabPower[scarab];
        }

        /// <summary>
        /// The maximum spell level in retail
        /// </summary>
        public static uint MaxSpellLevel = 8;

        /// <summary>
        /// A mapping of spell levels => minimum power
        /// </summary>
        public static Dictionary<uint, uint> MinPower = new Dictionary<uint, uint>()
        {
            { 1,   1 },
            { 2,  50 },
            { 3, 100 },
            { 4, 150 },
            { 5, 200 },
            { 6, 250 },
            { 7, 300 },
            { 8, 350 }
        };

        /// <summary>
        /// Returns TRUE if this spell component is a scarab
        /// </summary>
        /// <param name="componentID">The ID from the spell components table</param>
        public static bool IsScarab(uint componentID)
        {
            return Enum.IsDefined(typeof(Scarab), (int)componentID);
        }

        /// <summary>
        /// The spell for this formula
        /// </summary>
        public Spell Spell;

        /// <summary>
        /// The spell component IDs
        /// from the spell components table in portal.dat (0x0E00000F)
        /// </summary>
        public List<uint> Components;

        /// <summary>
        /// The spell components for the individual player
        /// uses a hashing algorithm based on player name
        /// </summary>
        public List<uint> PlayerFormula;

        /// <summary>
        /// Constructs a SpellFormula from a list of components
        /// </summary>
        /// <param name="spell">The spell for this formula</param>
        /// <param name="components">The list of components required to cast the spell</param>
        public SpellFormula(Spell spell, List<uint> components)
        {
            Spell = spell;
            Components = components;
        }

        /// <summary>
        /// Returns a list of scarabs in the spell formula
        /// </summary>
        public List<Scarab> Scarabs
        {
            get
            {
                var scarabs = new List<Scarab>();

                foreach (var component in Components)
                    if (IsScarab(component))
                        scarabs.Add((Scarab)component);

                return scarabs;
            }
        }

        /// <summary>
        /// Uses the client spell level formula, which is used for things like spell filtering
        /// a 'rough heuristic' based on the first component of the spell, which is expected to be a scarab
        /// </summary>
        public uint Level
        {
            get
            {
                if (Components == null || Components.Count == 0)
                    return 0;

                var firstComp = Components[0];
                if (!IsScarab(firstComp))
                    return 0;

                return ScarabLevel[(Scarab)firstComp];
            }
        }

        /// <summary>
        /// The spell table from the portal.dat
        /// </summary>
        public static SpellTable SpellTable { get => DatManager.PortalDat.SpellTable; }

        /// <summary>
        /// The spell components table from the portal.dat
        /// </summary>
        public static SpellComponentsTable SpellComponentsTable { get => DatManager.PortalDat.SpellComponentsTable; }

        /// <summary>
        /// Builds the pseudo-randomized spell formula
        /// based on account name
        /// </summary>
        public List<uint> GetPlayerFormula(string account)
        {
            PlayerFormula = SpellTable.GetSpellFormula(SpellTable, Spell.Id, account);
            return PlayerFormula;
        }

        /// <summary>
        /// Returns the windup gesture from all the scarabs
        /// </summary>
        public List<MotionCommand> WindupGestures
        {
            get
            {
                var windupGestures = new List<MotionCommand>();

                foreach (var scarab in Scarabs)
                {
                    SpellComponentsTable.SpellComponents.TryGetValue((uint)scarab, out var component);
                    if (component == null)
                    {
                        Console.WriteLine($"SpellFormula.WindupGestures error: spell ID {Spell.Id} contains scarab {scarab} not found in components table, skipping");
                        continue;
                    }
                    windupGestures.Add((MotionCommand)component.Gesture);
                }
                return windupGestures;
            }
        }

        /// <summary>
        /// Returns the spell casting gesture, after the initial windup(s) are completed
        /// Based on the talisman (assumed to be the last spell component)
        /// </summary>
        public MotionCommand CastGesture
        {
            get
            {
                if (PlayerFormula == null || PlayerFormula.Count == 0)
                    return MotionCommand.Invalid;

                // ensure talisman
                SpellComponentsTable.SpellComponents.TryGetValue(PlayerFormula.Last(), out var talisman);
                if (talisman == null || talisman.Type != (uint)SpellComponentsTable.Type.Talisman)
                {
                    Console.WriteLine($"SpellFormula.CastGesture error: spell ID {Spell.Id} last component not talisman!");
                    return MotionCommand.Invalid;
                }
                return (MotionCommand)talisman.Gesture;
            }
        }

        /// <summary>
        /// A mapping of scarabs => PlayScript scales
        /// This determines the scale of the glowing blue/purple ball of energy during the windup motion
        /// </summary>
        public static Dictionary<Scarab, float> ScarabScale = new Dictionary<Scarab, float>()
        {
            { Scarab.Lead,     0.05f },
            { Scarab.Iron,     0.2f },
            { Scarab.Copper,   0.4f },
            { Scarab.Silver,   0.5f },
            { Scarab.Gold,     0.6f },
            { Scarab.Pyreal,   1.0f },
            { Scarab.Diamond,  0.4f },  // verify onward
            { Scarab.Platinum, 0.6f },
            { Scarab.Dark,     1.0f },
            { Scarab.Mana,     0.6f }
        };

        public Scarab FirstScarab { get => Scarabs.First(); }

        /// <summary>
        /// Returns a simple scale for the spell formula,
        /// based on the first scarab
        /// </summary>
        public float Scale { get => ScarabScale[FirstScarab]; }

        /// <summary>
        /// Returns the total casting time,
        /// based on windup + cast gestures
        /// </summary>
        public float GetCastTime(uint motionTableID, float speed)
        {
            var windupMotion = WindupGestures.First();
            var castMotion = CastGesture;

            var motionTable = DatManager.PortalDat.ReadFromDat<MotionTable>(motionTableID);

            var windupTime = 0.0f;
            //var windupTime = motionTable.GetAnimationLength(MotionStance.Magic, windupMotion) / speed;
            foreach (var motion in WindupGestures)
                windupTime += motionTable.GetAnimationLength(MotionStance.Magic, motion) / speed;

            var castTime = motionTable.GetAnimationLength(MotionStance.Magic, castMotion) / speed;

            // FastCast = no windup motion
            if (Spell.Flags.HasFlag(SpellFlags.FastCast))
                return castTime;

            return windupTime + castTime;
        }
    }
}