using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ACE.Entity.Enum;
using ACE.Server.Entity;

namespace ACE.Server.Managers
{
    public static class ConfirmationManager
    {
        static ConcurrentDictionary<uint, Confirmation> confirmations = new ConcurrentDictionary<uint, Confirmation>();

        public static void AddConfirmation(Confirmation confirmation)
        {
            var isAlreadyInQueue = (from conf in confirmations
                                    where conf.Key == confirmation.ConfirmationID
                                    select conf).Count();

            if (isAlreadyInQueue == 0)
                confirmations.TryAdd(confirmation.ConfirmationID, confirmation);
        }

        public static void ProcessConfirmation(uint contextId, bool response)
        {
            if (!confirmations.Remove(contextId, out var confirm))
                return;

            switch (confirm.ConfirmationType)
            {
                case ConfirmationType.Fellowship:

                    var inviter = PlayerManager.GetOnlinePlayer(confirm.Source.Guid);
                    var invited = PlayerManager.GetOnlinePlayer(confirm.Target.Guid);

                    inviter.CompleteConfirmation(confirm.ConfirmationType, confirm.ConfirmationID);

                    if (response)
                        inviter.Fellowship.AddConfirmedMember(inviter, invited, response);

                    break;

                case ConfirmationType.SwearAllegiance:
                    break;

                case ConfirmationType.CraftInteraction:

                    confirm.Player.CompleteConfirmation(confirm.ConfirmationType, confirm.ConfirmationID);

                    if (response)
                        RecipeManager.HandleTinkering(confirm.Player, confirm.Source, confirm.Target, true);
                    break;

                default:
                    break;
            }
        }
    }
}
