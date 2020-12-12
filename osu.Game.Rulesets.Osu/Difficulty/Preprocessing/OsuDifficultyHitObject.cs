// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    public class OsuDifficultyHitObject : DifficultyHitObject
    {
        private const double normalized_diameter = 104;

        public new OsuHitObject BaseObject => (OsuHitObject)base.BaseObject;

        /// <summary>
        ///  Distance from the end position of the previous <see cref="OsuDifficultyHitObject"/> to the start position of this <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public Vector2 DistanceVector { get; private set; }

        /// <summary>
        /// Normalized distance from the end position of the previous <see cref="OsuDifficultyHitObject"/> to the start position of this <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public double JumpDistance { get; private set; }

        /// <summary>
        /// Normalized distance between the start and end position of the previous <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public double TravelDistance { get; private set; }

        /// <summary>
        /// The time given to go through the normalized distance between the start and end position of the previous <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public double TravelTime { get; private set; }

        /// <summary>
        /// The time given to go between the start and end position of the previous <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        public double TravelDuration { get; private set; }

        /// <summary>
        /// Angle the player has to take to hit this <see cref="OsuDifficultyHitObject"/>.
        /// Calculated as the angle between the circles (current-2, current-1, current).
        /// </summary>
        public double? Angle { get; private set; }

        /// <summary>
        /// Milliseconds elapsed since the start time of the previous <see cref="OsuDifficultyHitObject"/>, with a minimum of 50ms.
        /// </summary>
        public readonly double StrainTime;

        /// <summary>
        /// The rhythm required to hit this hit object.
        /// </summary>
        public readonly OsuDifficultyHitObjectRhythm Rhythm;

        /// <summary>
        /// The rhythm required to hit this hit object, using the previous previous sliderend instead of clickable object.
        /// </summary>
        public readonly OsuDifficultyHitObjectRhythm SliderRhythm;

        private readonly OsuHitObject lastLastObject;
        private readonly OsuHitObject lastObject;

        public OsuDifficultyHitObject(HitObject hitObject, HitObject lastLastObject, HitObject lastObject, double clockRate)
            : base(hitObject, lastObject, clockRate)
        {
            this.lastLastObject = (OsuHitObject)lastLastObject;
            this.lastObject = (OsuHitObject)lastObject;

            setDistances(clockRate);

            // Every strain interval is hard capped at the equivalent of 375 BPM streaming speed as a safety measure
            StrainTime = Math.Max(50, DeltaTime);
            TravelTime = Math.Max(50, TravelTime);

            if (lastLastObject != null) {
                Rhythm = getClosestRhythm(lastObject.StartTime, lastLastObject.StartTime, clockRate);
                if (lastObject is Slider) {
                    Slider sliderLastObject = (Slider)lastObject;
                    SliderRhythm = getClosestRhythm(lastObject.StartTime, sliderLastObject.EndTime, clockRate);
                }
                else if (lastLastObject is Slider) {
                    Slider sliderLastLastObject = (Slider)lastLastObject;
                    SliderRhythm = getClosestRhythm(lastObject.StartTime, sliderLastLastObject.EndTime, clockRate);
                }
            }
        }

        private void setDistances(double clockRate)
        {
            // We will scale distances by this factor, so we can assume a uniform CircleSize among beatmaps.
            float scalingFactor = (float)normalized_diameter / (2 * (float)BaseObject.Radius);
            // if (BaseObject.Radius < 30)
            // {
            //     float smallCircleBonus = (30 - (float)BaseObject.Radius) / 50;
            //     scalingFactor *= 1 + smallCircleBonus;
            // }

            if (lastObject is Slider lastSlider)
            {
                computeSliderCursorPosition(lastSlider);
                TravelDistance = lastSlider.LazyTravelDistance * scalingFactor;
                TravelTime = lastSlider.LazyTravelTime / clockRate;
                TravelDuration = lastSlider.Duration / clockRate;
            }

            Vector2 lastCursorPosition = getEndCursorPosition(lastObject);

            // Don't need to jump to reach spinners
            if (!(BaseObject is Spinner))
            {
                DistanceVector = (BaseObject.StackedPosition * scalingFactor - lastCursorPosition * scalingFactor);
                JumpDistance = DistanceVector.Length;
            }

            if (lastLastObject != null)
            {
                Vector2 lastLastCursorPosition = getEndCursorPosition(lastLastObject);

                Vector2 v1 = lastLastCursorPosition - lastObject.StackedPosition;
                Vector2 v2 = BaseObject.StackedPosition - lastCursorPosition;

                float dot = Vector2.Dot(v1, v2);
                float det = v1.X * v2.Y - v1.Y * v2.X;

                Angle = Math.Abs(Math.Atan2(det, dot));
            }
        }

        private void computeSliderCursorPosition(Slider slider)
        {
            if (slider.LazyEndPosition != null)
                return;
            slider.LazyEndPosition = slider.StackedPosition;

            float approxFollowCircleRadius = (float)(slider.Radius * 3);
            var computeVertex = new Action<double>(t =>
            {
                double progress = (t - slider.StartTime) / slider.SpanDuration;
                if (progress % 2 >= 1)
                    progress = 1 - progress % 1;
                else
                    progress = progress % 1;

                // ReSharper disable once PossibleInvalidOperationException (bugged in current r# version)
                var diff = slider.StackedPosition + slider.Path.PositionAt(progress) - slider.LazyEndPosition.Value;
                float dist = diff.Length;

                if (dist > approxFollowCircleRadius)
                {
                    // The cursor would be outside the follow circle, we need to move it
                    diff.Normalize(); // Obtain direction of diff
                    dist -= approxFollowCircleRadius;
                    slider.LazyEndPosition += diff * dist;
                    slider.LazyTravelDistance += dist;
                    slider.LazyTravelTime = t - slider.StartTime;
                }
                if (t != slider.TailCircle.StartTime)
                    slider.LazyTravelTime = t - slider.StartTime;
            });

            // Skip the head circle
            var scoringTimes = slider.NestedHitObjects.Skip(1).Select(t => t.StartTime);
            foreach (var time in scoringTimes)
                computeVertex(time);
        }

        private Vector2 getEndCursorPosition(OsuHitObject hitObject)
        {
            Vector2 pos = hitObject.StackedPosition;

            var slider = hitObject as Slider;
            if (slider != null)
            {
                computeSliderCursorPosition(slider);
                pos = slider.LazyEndPosition ?? pos;
            }

            return pos;
        }

        /// <summary>
        /// List of most common rhythm changes
        /// </summary>
        private static readonly OsuDifficultyHitObjectRhythm[] common_rhythms =
        {
            new OsuDifficultyHitObjectRhythm(1, 1, 0.0),
            new OsuDifficultyHitObjectRhythm(2, 1, 0.25),
            new OsuDifficultyHitObjectRhythm(1, 2, 0.35),
            new OsuDifficultyHitObjectRhythm(3, 1, 0.2),
            new OsuDifficultyHitObjectRhythm(1, 3, 0.35),
            new OsuDifficultyHitObjectRhythm(3, 2, 0.6),
            new OsuDifficultyHitObjectRhythm(2, 3, 0.5),
            new OsuDifficultyHitObjectRhythm(5, 4, 0.8),
            new OsuDifficultyHitObjectRhythm(4, 5, 0.7)
        };

        /// <summary>
        /// Returns the closest rhythm change from <see cref="common_rhythms"/> required to hit this object.
        /// </summary>
        /// <param name="lastObjectTime">The gameplay preceding this one.</param>
        /// <param name="lastLastObjectTime">The gameplay preceding <paramref name="lastObjectTime"/>.</param>
        /// <param name="clockRate">The rate of the gameplay clock.</param>
        private OsuDifficultyHitObjectRhythm getClosestRhythm(double lastObjectTime, double lastLastObjectTime, double clockRate)
        {
            double prevLength = (lastObjectTime - lastLastObjectTime) / clockRate;
            double ratio = DeltaTime / prevLength;

            if ((ratio < 1.0/4.0) || (ratio > 4.0)) {
                ratio = 1.0;  // Extreme ratio changes are counted as 0 strain.
            }

            return common_rhythms.OrderBy(x => Math.Abs(x.Ratio - ratio)).First();
        }
    }
}
