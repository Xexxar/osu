// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : Skill
    {
        protected override double SkillMultiplier => 37.5;
        protected override double StrainDecayBase => 0.3;

        private const double min_speed_bonus = 75; // ~200BPM
        private const double max_speed_bonus = 45; // ~330BPM
        private const double speed_balancing_factor = 40;

        private const double stars_per_double = 1.10;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        public double calculateRealSR(IEnumerable<double> strains) //Scuffed way to override process until more time can be spent working on that.
        {
            double k = Math.Log(2) / (Math.Log(stars_per_double));
            double oneOverk = 1 / k;
            double SR = 0;

            foreach (double strain in strains)
            {
                SR = Math.Pow(Math.Pow(SR, k) + Math.Pow(strain, k), oneOverk);
            }

            return SR;
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;

            double deltaTime = Math.Max(max_speed_bonus, current.DeltaTime);

            double speedBonus = 0.0;
            if (deltaTime < min_speed_bonus)
                speedBonus = Math.Pow((min_speed_bonus - deltaTime) / speed_balancing_factor, 2) * 0.5;

            return (1 + speedBonus) / osuCurrent.StrainTime;
        }
    }
}
