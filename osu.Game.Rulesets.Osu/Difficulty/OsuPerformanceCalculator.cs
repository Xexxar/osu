// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        public new OsuDifficultyAttributes Attributes => (OsuDifficultyAttributes)base.Attributes;

        private Mod[] mods;

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countOk;
        private int countMeh;
        private int countMiss;

        private const double combo_weight = 0.5;

        public OsuPerformanceCalculator(Ruleset ruleset, DifficultyAttributes attributes, ScoreInfo score)
            : base(ruleset, attributes, score)
        {
        }

        public override double Calculate(Dictionary<string, double> categoryRatings = null)
        {
            mods = Score.Mods;
            accuracy = Score.Accuracy;
            scoreMaxCombo = Score.MaxCombo;
            countGreat = Score.Statistics.GetOrDefault(HitResult.Great);
            countOk = Score.Statistics.GetOrDefault(HitResult.Ok);
            countMeh = Score.Statistics.GetOrDefault(HitResult.Meh);
            countMiss = Score.Statistics.GetOrDefault(HitResult.Miss);

            // Don't count scores made with supposedly unranked mods
            if (mods.Any(m => !m.Ranked))
                return 0;

            // Custom multipliers for NoFail and SpunOut.
            double multiplier = 1.12; // This is being adjusted to keep the final pp value scaled around what it used to be when changing things

            if (mods.Any(m => m is OsuModNoFail))
                multiplier *= 0.90;

            if (mods.Any(m => m is OsuModSpunOut))
                multiplier *= 1.0 - Math.Pow((double)Attributes.SpinnerCount / totalHits, 0.85);

            double aimValue = computeAimValue();
            double speedValue = computeSpeedValue();
            double accuracyValue = computeAccuracyValue();
            double totalValue =
                Math.Pow(
                    Math.Pow(aimValue, 1.1) +
                    Math.Pow(speedValue, 1.1) +
                    Math.Pow(accuracyValue, 1.1), 1.0 / 1.1
                ) * multiplier;

            if (categoryRatings != null)
            {
                categoryRatings.Add("Aim", aimValue);
                categoryRatings.Add("Speed", speedValue);
                categoryRatings.Add("Accuracy", accuracyValue);
                categoryRatings.Add("OD", Attributes.OverallDifficulty);
                categoryRatings.Add("AR", Attributes.ApproachRate);
                categoryRatings.Add("Max Combo", Attributes.MaxCombo);
            }

            return totalValue;
        }

        private double computeAimValue()
        {
          double aimComboStarRating = interpComboStarRating(Attributes.AimComboStarRatings, scoreMaxCombo, Attributes.MaxCombo);
          double aimMissCountStarRating = interpMissCountStarRating(Attributes.AimComboStarRatings.Last(), Attributes.AimMissCounts, countMiss);
          double rawAim = Math.Pow(aimComboStarRating, combo_weight) * Math.Pow(aimMissCountStarRating, 1 - combo_weight);

          if (mods.Any(m => m is OsuModTouchDevice))
              rawAim = Math.Pow(rawAim, 0.8);

          double aimValue = Math.Pow(5.0f * Math.Max(1.0f, rawAim / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

          // double lengthBonus = (1 + 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
          //                      (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0)) / 2;
          // aimValue *= lengthBonus;

          double approachRateFactor = 0.0;
          if (Attributes.ApproachRate > 10.33)
              approachRateFactor += 0.2 * (Attributes.ApproachRate - 10.33);
          else if (Attributes.ApproachRate < 8.0)
              approachRateFactor += 0.1 * (8.0 - Attributes.ApproachRate);

          aimValue *= 1.0 + Math.Min(approachRateFactor, approachRateFactor * (totalHits / 1000.0));

          // We want to give more reward for lower AR when it comes to aim and HD. This nerfs high AR and buffs lower AR.
          if (mods.Any(h => h is OsuModHidden))
              aimValue *= 1.0 + 0.04 * (12.0 - Attributes.ApproachRate);

          if (mods.Any(h => h is OsuModFlashlight))
          {
              // Apply object-based bonus for flashlight.
              aimValue *= 1.0 + 0.35 * Math.Min(1.0, totalHits / 200.0) +
                          (totalHits > 200
                              ? 0.3 * Math.Min(1.0, (totalHits - 200) / 300.0) +
                                (totalHits > 500 ? (totalHits - 500) / 1200.0 : 0.0)
                              : 0.0);
          }

          // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
          if (countMiss > 0)
              aimValue *= 0.97 * Math.Pow(1 - Math.Pow((double)countMiss / totalHits, 0.775), countMiss);

          // Scale the aim value with accuracy _slightly_
          aimValue *= 0.5 + accuracy / 2.0;
          // It is important to also consider accuracy difficulty when doing that
          aimValue *= 0.98 + Math.Pow(Attributes.OverallDifficulty, 2) / 2500;

          return aimValue;
        }

        private double computeSpeedValue()
        {

            double speedComboStarRating = interpComboStarRating(Attributes.SpeedComboStarRatings, scoreMaxCombo, Attributes.MaxCombo);
            double speedMissCountStarRating = interpMissCountStarRating(Attributes.SpeedComboStarRatings.Last(), Attributes.SpeedMissCounts, countMiss);
            double rawSpeed = Math.Pow(speedComboStarRating, combo_weight) * Math.Pow(speedMissCountStarRating, 1 - combo_weight);

            if (mods.Any(m => m is OsuModTouchDevice))
                rawSpeed = Math.Pow(rawSpeed, 1.25f);

            double speedValue = Math.Pow(5.0f * Math.Max(1.0f, rawSpeed / 0.0675f) - 4.0f, 3.0f) / 100000.0f;

            double lengthBonus = (1 + 0.95 + 0.4 * Math.Min(1.0, totalHits / 2000.0) +
                                 (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0)) / 2;
            speedValue *= lengthBonus;

            double approachRateFactor = 0.0;
            if (Attributes.ApproachRate > 10.33)
                approachRateFactor += 0.2 * (Attributes.ApproachRate - 10.33);

            speedValue *= 1.0 + Math.Min(approachRateFactor, approachRateFactor * (totalHits / 1000.0));

            if (mods.Any(m => m is OsuModHidden))
                speedValue *= 1.0 + 0.04 * (12.0 - Attributes.ApproachRate);

            // Penalize misses by assessing # of misses relative to the total # of objects. Default a 3% reduction for any # of misses.
            if (countMiss > 0)
                speedValue *= 0.97 * Math.Pow(1 - Math.Pow((double)countMiss / totalHits, 0.775), countMiss);

            // Scale the speed value with accuracy and OD
            speedValue *= (.95 + Math.Pow(Attributes.OverallDifficulty, 2) / 750) * Math.Pow(accuracy, (14.5 - Math.Max(Attributes.OverallDifficulty, 8)) / 2);
            // Scale the speed value with # of 50s to punish doubletapping.
            speedValue *= Math.Pow(0.98, countMeh < totalHits / 500.0 ? 0.5 * countMeh : countMeh - totalHits / 500.0 * 0.5);

            return speedValue;
        }

        private double computeAccuracyValue()
        {
            // This percentage only considers HitCircles of any value - in this part of the calculation we focus on hitting the timing hit window
            double betterAccuracyPercentage;
            int amountHitObjectsWithAccuracy = Attributes.HitCircleCount;

            if (amountHitObjectsWithAccuracy > 0)
                betterAccuracyPercentage = ((countGreat - (totalHits - amountHitObjectsWithAccuracy)) * 6 + countOk * 2 + countMeh) / (double)(amountHitObjectsWithAccuracy * 6);
            else
                betterAccuracyPercentage = 0;

            // It is possible to reach a negative accuracy with this formula. Cap it at zero - zero points
            if (betterAccuracyPercentage < 0)
                betterAccuracyPercentage = 0;

            // Lots of arbitrary values from testing.
            // Considering to use derivation from perfect accuracy in a probabilistic manner - assume normal distribution
            double accuracyValue = Math.Pow(1.52163, Attributes.OverallDifficulty) * Math.Pow(betterAccuracyPercentage, 24) * 2.83;

            // Bonus for many hitcircles - it's harder to keep good accuracy up for longer
            accuracyValue *= Math.Min(1.15, Math.Pow(amountHitObjectsWithAccuracy / 1000.0, 0.3));

            if (mods.Any(m => m is OsuModHidden))
                accuracyValue *= 1.08;
            if (mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02;

            return accuracyValue;
        }

        private double interpComboStarRating(IList<double> values, double scoreCombo, double mapCombo)
        {
            if (mapCombo == 0)
            {
                return values.Last();
            }

            double comboRatio = scoreCombo / mapCombo;
            double pos = Math.Min(comboRatio * (values.Count), values.Count);
            int i = (int)pos;

            if (i == values.Count)
            {
                return values.Last();
            }

            if (pos <= 0)
            {
                return 0;
            }

            double ub = values[i];
            double lb = i == 0 ? 0 : values[i - 1];

            double t = pos - i;
            double ret = lb * (1 - t) + ub * t;

            return ret;
        }

        // get star rating corresponding to miss count in miss count list
        private double missStarRating(double sr, int i) => sr * (1 - Math.Pow((i + 1), Attributes.MissStarRatingExponent) * Attributes.MissStarRatingIncrement);

        private double interpMissCountStarRating(double sr, IList<double> values, int missCount)
        {
            double increment = Attributes.MissStarRatingIncrement;
            double t;

            if (missCount == 0)
            {
                // zero misses, return SR
                return sr;
            }

            if (missCount < values[0])
            {
                t = missCount / values[0];
                return sr * (1 - t) + missStarRating(sr, 0) * t;
            }

            for (int i = 0; i < values.Count; ++i)
            {
                if (missCount == values[i])
                {
                    if (i < values.Count - 1 && missCount == values[i + 1])
                    {
                        // if there are duplicates, take the lowest SR that can achieve miss count
                        continue;
                    }

                    return missStarRating(sr, i);
                }

                if (i < values.Count - 1 && missCount < values[i + 1])
                {
                    t = (missCount - values[i]) / (values[i + 1] - values[i]);
                    return missStarRating(sr, i) * (1 - t) + missStarRating(sr, i + 1) * t;
                }
            }

            // more misses than max evaluated, interpolate to zero
            t = (missCount - values.Last()) / (Attributes.MaxCombo - values.Last());
            return missStarRating(sr, values.Count - 1) * (1 - t);
        }

        private int totalHits => countGreat + countOk + countMeh + countMiss;
        private int totalSuccessfulHits => countGreat + countOk + countMeh;
    }
}
