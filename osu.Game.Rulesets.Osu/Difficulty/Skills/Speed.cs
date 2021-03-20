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
        protected override double SkillMultiplier => 16;
        protected override double StrainDecayBase => 0.3;

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

            double strain = 0;

            if (Previous.Count > 1)
            {
                // though we are on current, we want to use current as reference for previous,
                // this is kind of retarded, but real devs can make this less retarded probably
                // for what its worth, difficulty calculation is a function of Previous[0]
                var osuPrevObj = (OsuDifficultyHitObject)Previous[1];
                var osuCurrObj = (OsuDifficultyHitObject)Previous[0];
                var osuNextObj = (OsuDifficultyHitObject)current;

                double ms = (osuPrevObj.StrainTime + osuCurrObj.StrainTime) / 2;


                // if (ms < 75)
                //     strain = Math.Pow(1 / (ms - 20), 1);
                // else
                    strain = 1 / (ms - 20);
            }

            return strain;
        }
    }
}
