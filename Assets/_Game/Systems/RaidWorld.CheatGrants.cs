using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        string[] cheatFishSpecies;
        public IReadOnlyList<string> CheatFishSpecies
        {
            get
            {
                if(cheatFishSpecies==null || cheatFishSpecies.Length==0)
                    cheatFishSpecies=FindObjectsByType<FishActor>(FindObjectsInactive.Include)
                        .Where(fish=>fish.gameObject.scene==gameObject.scene && !string.IsNullOrEmpty(fish.SpeciesId))
                        .Select(fish=>fish.SpeciesId).Distinct(StringComparer.Ordinal).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
                return cheatFishSpecies;
            }
        }
        public int CheatFishRoom => (IsServerInitialized?team:view)?.ReservoirRoom(progression)??0;

        internal bool GrantFromCheatPanel(string id,int amount,bool fish,out int granted,out string reason)
        {
            granted=0;
            if(!IsServerInitialized || team==null){reason="Мир ещё не готов.";return false;}
            if(saveFailed){reason="Сохранение команды недоступно.";return false;}
            bool success;
            if(fish)
            {
                // Prefer the local depth variant. Its size, food and yields are set by the scene.
                var specimen=FishActor.ServerSchool.Where(f=>f && f.gameObject.scene==gameObject.scene && f.SpeciesId==id)
                    .OrderByDescending(f=>f.depthTier==CurrentDepth).ThenBy(f=>f.depthTier).ThenBy(f=>f.ObjectId).FirstOrDefault();
                if(!specimen){reason="Вид рыбы недоступен.";return false;}
                success=team.TryGrantFish(specimen.ToCargo(1),amount,progression,SaveTransaction,out granted,out reason);
            }
            else success=team.TryGrantResource(id,amount,SaveTransaction,out granted,out reason);
            if(success)Publish();
            return success;
        }
    }
}
