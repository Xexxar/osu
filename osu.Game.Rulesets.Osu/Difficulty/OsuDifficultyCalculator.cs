// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using MathNet.Numerics;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.0675;
        private const double star_rating_scale_factor = 1.485 * 3.0;
        private const double aim_star_factor = 1.1;
        private const double speed_star_factor = 2.0;
        private const double total_star_factor = 2.2;

        public OsuDifficultyCalculator(Ruleset ruleset, WorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        public double PointsTransformation(double skillRating) => Math.Pow(5.0f * Math.Max(1.0f, skillRating / difficulty_multiplier) - 4.0f, 3.0f) / 100000.0f;
        public double StarTransformation(double pointsRating) => difficulty_multiplier * (Math.Pow(100000.0f * pointsRating, 1.0f / 3.0f) + 4.0f) / 5.0f;

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = mods };

            // Todo: These int casts are temporary to achieve 1:1 results with osu!stable, and should be removed in the future
            double hitWindowGreat = (int)(beatmap.HitObjects.First().HitWindows.great / 2) / clockRate;
            double overralDifficulty = (80 - hitWindowGreat) / 6;
            double preempt = (int)BeatmapDifficulty.DifficultyRange(beatmap.BeatmapInfo.BaseDifficulty.ApproachRate, 1800, 1200, 450) / clockRate;

            int maxCombo = beatmap.HitObjects.Count;
            // Add the ticks + tail of the slider. 1 is subtracted because the head circle would be counted twice (once for the slider itself in the line above)
            maxCombo += beatmap.HitObjects.OfType<Slider>().Sum(s => s.NestedHitObjects.Count - 1);

            double totalHits = beatmap.HitObjects.Count;
            double circles = beatmap.HitObjects.Count(h => h is HitCircle);
            double sliders = beatmap.HitObjects.Count(h => h is Slider);

            var jumpAim = (OsuSkill)skills[0];
            var streamAim = (OsuSkill)skills[1];
            var stamina = (OsuSkill)skills[2];
            var speed = (OsuSkill)skills[3];
            var aimControl = (OsuSkill)skills[4];
            var fingerControl = (OsuSkill)skills[5];

            IList<double> jumpAimComboSr = jumpAim.ComboStarRatings;
            IList<double> jumpAimMissCounts = jumpAim.MissCounts;

            IList<double> streamAimComboSr = streamAim.ComboStarRatings;
            IList<double> streamAimMissCounts = streamAim.MissCounts;

            IList<double> staminaComboSr = stamina.ComboStarRatings;
            IList<double> staminaMissCounts = stamina.MissCounts;

            IList<double> speedComboSr = speed.ComboStarRatings;
            IList<double> speedMissCounts = speed.MissCounts;

            IList<double> aimControlComboSr = aimControl.ComboStarRatings;
            IList<double> aimControlMissCounts = aimControl.MissCounts;

            IList<double> fingerControlComboSr = fingerControl.ComboStarRatings;
            IList<double> fingerControlMissCounts = fingerControl.MissCounts;

            const double miss_sr_increment = OsuSkill.MISS_STAR_RATING_INCREMENT_MULTIPLIER;
            const double miss_sr_exponent = OsuSkill.MISS_STAR_RATING_INCREMENT_EXPONENT;

            double jumpAimRating = jumpAimComboSr.Last();
            double streamAimRating = streamAimComboSr.Last();
            double staminaRating = staminaComboSr.Last();
            double speedRating = speedComboSr.Last();
            double aimControlRating = aimControlComboSr.Last();
            double fingerControlRating = fingerControlComboSr.Last();
            double accuracyRating = calculateAccuracyRating(overralDifficulty, circles, sliders, totalHits);

            double totalAimRating = Math.Pow(
                Math.Pow(PointsTransformation(jumpAimRating), aim_star_factor) +
                Math.Pow(PointsTransformation(streamAimRating), aim_star_factor) +
                Math.Pow(PointsTransformation(aimControlRating), aim_star_factor), 1.0 / aim_star_factor);
            double totalSpeedRating = Math.Pow(
                Math.Pow(PointsTransformation(staminaRating), speed_star_factor) +
                Math.Pow(PointsTransformation(speedRating), speed_star_factor) +
                Math.Pow(PointsTransformation(fingerControlRating), speed_star_factor), 1.0 / speed_star_factor);
            double starRating = StarTransformation(star_rating_scale_factor * Math.Pow(
                Math.Pow(totalAimRating, total_star_factor) +
                Math.Pow(totalSpeedRating, total_star_factor) +
                Math.Pow(accuracyRating, total_star_factor), 1.0 / total_star_factor));

            string values = "Jump Aim: " + Math.Round(jumpAimRating, 2) +
            "\nStream Aim: " + Math.Round(streamAimRating, 2) +
            "\nStamina: " + Math.Round(staminaRating, 2) +
            "\nSpeed: " + Math.Round(speedRating, 2) +
            "\nAim Control: " + Math.Round(aimControlRating, 2) +
            "\nFinger Control: " + Math.Round(fingerControlRating, 2);

            using (StreamWriter outputFile = new StreamWriter(beatmap.BeatmapInfo.OnlineBeatmapID + "values.txt"))
                outputFile.WriteLine(values);

            return new OsuDifficultyAttributes
            {
                StarRating = starRating,
                AimRating = StarTransformation(totalAimRating),
                SpeedRating = StarTransformation(totalSpeedRating),
                Mods = mods,
                MissStarRatingIncrement = miss_sr_increment,
                MissStarRatingExponent = miss_sr_exponent,
                ApproachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5,
                OverallDifficulty = overralDifficulty,
                MaxCombo = maxCombo,
                countCircles = circles,
                countSliders = sliders,

                JumpAimStrain = jumpAimRating,
                JumpAimComboStarRatings = jumpAimComboSr,
                JumpAimMissCounts = jumpAimMissCounts,

                StreamAimStrain = streamAimRating,
                StreamAimComboStarRatings = streamAimComboSr,
                StreamAimMissCounts = streamAimMissCounts,

                StaminaStrain = staminaRating,
                StaminaComboStarRatings = staminaComboSr,
                StaminaMissCounts = staminaMissCounts,

                SpeedStrain = speedRating,
                SpeedComboStarRatings = speedComboSr,
                SpeedMissCounts = speedMissCounts,

                AimControlStrain = aimControlRating,
                AimControlComboStarRatings = aimControlComboSr,
                AimControlMissCounts = aimControlMissCounts,

                FingerControlStrain = fingerControlRating,
                FingerControlComboStarRatings = fingerControlComboSr,
                FingerControlMissCounts = fingerControlMissCounts,

                AccuracyStrain = StarTransformation(accuracyRating),
            };
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                var lastLast = i > 1 ? beatmap.HitObjects[i - 2] : null;
                var last = beatmap.HitObjects[i - 1];
                var current = beatmap.HitObjects[i];

                yield return new OsuDifficultyHitObject(current, lastLast, last, clockRate);
            }
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap) => new Skill[]
        {
            new JumpAim(),
            new StreamAim(),
            new Stamina(),
            new Speed(),
            new AimControl(),
            new FingerControl()
        };

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
        };

        private double calculateAccuracyRating(double OD, double circles, double sliders, double totalHits)
        {
            double sigmaCircle = 0;
            double sigmaSlider = 0;
            double sigmaTotal = 0;

            double zScore = 2.58f;
            double sqrt2 = Math.Sqrt(2.0f);
            double accMultiplier = 1200.0f;
            double accScale = 1.3f;

            // Slider sigma calculations
            if (sliders > 0)
            {
                double sliderConst = Math.Sqrt(2.0f / sliders) * zScore;
                double sliderProbability = (2.0f * 1.0f + Math.Pow(sliderConst, 2.0f) - sliderConst * Math.Sqrt(4.0f * 1.0f + Math.Pow(sliderConst, 2.0f) - 4.0f * Math.Pow(1.0f, 2.0f))) / (2.0f + 2.0f * Math.Pow(sliderConst, 2.0f));
                sigmaSlider = (199.5f - 10.0f * OD) / (sqrt2 * SpecialFunctions.ErfInv(sliderProbability));
            }

            // Circle sigma calculations
            if (circles > 0)
            {
                double circleConst = Math.Sqrt(2.0f / circles) * zScore;
                double circleProbability = (2.0f * 1.0f + Math.Pow(circleConst, 2.0f) - circleConst * Math.Sqrt(4.0f * 1.0f + Math.Pow(circleConst, 2.0f) - 4.0f * Math.Pow(1.0f, 2.0f))) / (2.0f + 2.0f * Math.Pow(circleConst, 2.0f));
                sigmaCircle = (79.5f - 6.0f * OD) / (sqrt2 * SpecialFunctions.ErfInv(circleProbability));
            }

            if (sigmaSlider == 0) return accMultiplier * Math.Pow(accScale, -sigmaCircle);
            if (sigmaCircle == 0) return accMultiplier * Math.Pow(accScale, -sigmaSlider);

            sigmaTotal = 1.0f / (1.0f / sigmaCircle + 1.0f / sigmaSlider);

            return accMultiplier * Math.Pow(accScale, -sigmaTotal);
        }
    }
}
