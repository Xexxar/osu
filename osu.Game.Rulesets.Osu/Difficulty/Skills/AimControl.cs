// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// 
    /// </summary>
    public class AimControl : OsuSkill
    {
        private double StrainDecay = 0.25;
        protected override double SkillMultiplier => 8000;
        protected override double StrainDecayBase => StrainDecay;
        protected override double StarMultiplierPerRepeat => 1.1;
        private const double distThresh = 150;

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            StrainDecay = 0.25;

            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            if (osuCurrent.BaseObject is Slider && osuCurrent.TravelTime < osuCurrent.StrainTime) StrainDecay = Math.Min(osuCurrent.TravelTime, osuCurrent.StrainTime - 30.0) / osuCurrent.StrainTime *
                (1.0 - Math.Pow(1.0 - StrainDecay, Math.Pow(1.0 + osuCurrent.TravelDistance / Math.Max(osuCurrent.TravelTime, 30.0), 3.0))) +
                Math.Max(30.0, osuCurrent.StrainTime - osuCurrent.TravelTime) / osuCurrent.StrainTime * StrainDecay;

            test.Add(Tuple.Create(current.BaseObject.StartTime, 0.0));

            double strain = 0;
            double velScale = 0;
            double sliderVel = 1.0 + osuCurrent.TravelDistance / osuCurrent.TravelTime;

            if (Previous.Count > 0 && osuCurrent.Angle != null)
            {
                var osuPrevious = (OsuDifficultyHitObject)Previous[0];
                double awkVal = 0;

                double maxTime = Math.Max(osuCurrent.StrainTime, osuPrevious.StrainTime);
                double minTime = Math.Min(osuCurrent.StrainTime, osuPrevious.StrainTime);

                double currDistance = osuCurrent.JumpDistance + osuCurrent.TravelDistance;
                double prevDistance = osuPrevious.JumpDistance + osuPrevious.TravelDistance;

                double currVel = currDistance / osuCurrent.StrainTime;
                double prevVel = prevDistance / osuPrevious.StrainTime;

                double diffDist = Math.Abs(currDistance - prevDistance);
                double maxDist = Math.Max(Math.Max(currDistance, prevDistance), distThresh);
                double minDist = Math.Max(Math.Min(currDistance, prevDistance), distThresh);

                velScale = Math.Min(currVel, prevVel);

                awkVal = diffDist / maxDist;
                strain = awkVal / maxTime;

                test.Add(Tuple.Create(current.BaseObject.StartTime, awkVal));
            }
            return strain * velScale * sliderVel;
        }
    }
}
