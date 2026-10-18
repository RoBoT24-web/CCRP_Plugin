using CCRPPlugin;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CCRP_Plugin.Services
{
    public static class MaxSkillsService
    {
        // Index order matches the in-game skill arrays: offense, defense, support.
        private static readonly Type[] SpecialityEnums = { typeof(EPlayerOffense), typeof(EPlayerDefense), typeof(EPlayerSupport) };

        public static void MaxSkills(UnturnedPlayer player)
        {
            MaxSkillsSettings settings = Main.Instance?.Configuration?.Instance?.MaxSkills;
            if (settings == null || !settings.Enabled || player == null || player.Player == null)
            {
                return;
            }

            HashSet<string> ignored = new HashSet<string>(
                (settings.IgnoredSkills ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase);

            PlayerSkills skills = player.Player.skills;
            for (int speciality = 0; speciality < skills.skills.Length && speciality < SpecialityEnums.Length; speciality++)
            {
                Skill[] group = skills.skills[speciality];
                for (int index = 0; index < group.Length; index++)
                {
                    string name = Enum.GetName(SpecialityEnums[speciality], index);
                    if (name != null && ignored.Contains(name))
                    {
                        continue;
                    }

                    if (group[index].level < group[index].max)
                    {
                        skills.ServerSetSkillLevel(speciality, index, group[index].max);
                    }
                }
            }
        }
    }
}
