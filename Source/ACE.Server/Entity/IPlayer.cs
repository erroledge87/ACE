using System;

using ACE.Entity;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.Entity
{
    /// <summary>
    /// This interface is used by Player and OfflinePlayer.
    /// It allows us to maintain two separate lists for online players (Player) and offline players (OfflinePlayer) in PlayerManager and return generic IPlayer results.
    /// </summary>
    public interface IPlayer
    {
        ObjectGuid Guid { get; }


        bool? GetProperty(PropertyBool property);
        uint? GetProperty(PropertyDataId property);
        double? GetProperty(PropertyFloat property);
        uint? GetProperty(PropertyInstanceId property);
        int? GetProperty(PropertyInt property);
        long? GetProperty(PropertyInt64 property);
        string GetProperty(PropertyString property);

        void SetProperty(PropertyBool property, bool value);
        void SetProperty(PropertyDataId property, uint value);
        void SetProperty(PropertyFloat property, double value);
        void SetProperty(PropertyInstanceId property, uint value);
        void SetProperty(PropertyInt property, int value);
        void SetProperty(PropertyInt64 property, long value);
        void SetProperty(PropertyString property, string value);

        void RemoveProperty(PropertyBool property);
        void RemoveProperty(PropertyDataId property);
        void RemoveProperty(PropertyFloat property);
        void RemoveProperty(PropertyInstanceId property);
        void RemoveProperty(PropertyInt property);
        void RemoveProperty(PropertyInt64 property);
        void RemoveProperty(PropertyString property);


        string Name { get; }

        int? Level { get; }

        int? Heritage { get; }

        int? Gender { get; }


        uint? Monarch { get; set; }

        uint? Patron { get; set; }

        ulong AllegianceXPCached { get; set; }

        ulong AllegianceXPGenerated { get; set; }


        uint GetCurrentLoyalty();

        uint GetCurrentLeadership();


        Allegiance Allegiance { get; set; }

        AllegianceNode AllegianceNode { get; set; }
    }
}
