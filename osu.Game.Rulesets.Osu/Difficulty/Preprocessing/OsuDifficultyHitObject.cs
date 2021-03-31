// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing
{
    public class OsuDifficultyHitObject : DifficultyHitObject
    {
        private const double normalized_radius = 0.5;

        protected new OsuHitObject BaseObject => (OsuHitObject)base.BaseObject;

        /// <summary>
        ///  Normalized vector distance from the end position of the previous <see cref="OsuDifficultyHitObject"/> to the start position of this <see cref="OsuDifficultyHitObject"/>.
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
        /// Milliseconds elapsed since the start time of the previous <see cref="OsuDifficultyHitObject"/>, with a minimum of 50ms.
        /// </summary>
        public readonly double StrainTime;

        /// <summary>
        /// Milliseconds elapsed since the start time of the previous <see cref="OsuDifficultyHitObject"/>, with a minimum of 50ms.
        /// </summary>
        public double TravelTime;

        /// <summary>
        /// Angle the player has to take to hit this <see cref="OsuDifficultyHitObject"/>.
        /// Calculated as the angle between the circles (current-2, current-1, current).
        /// </summary>
        public double? Angle { get; private set; }

        /// <summary>
        /// Measure of angle leniency to be given when calculating the flow values of the next <see cref="OsuDifficultyHitObject"/> (scale of [0, 1]).
        /// </summary>
        public double AngleLeniency { get; private set; }

        /// <summary>
        /// Measure of expected aim flowiness based on time and distance from the previous <see cref="OsuDifficultyHitObject"/> (scale of [0, 1]).
        /// </summary>
        public double BaseFlow { get; private set; }

        /// <summary>
        /// Measure of expected aim flowiness based on <see cref="BaseFlow"/> and pattern context made up of the previous <see cref="OsuDifficultyHitObject"/>s (scale of [0, 1]).
        /// </summary>
        public double Flow { get; private set; }

        private readonly OsuHitObject lastLastObject;
        private readonly OsuHitObject lastObject;
        private readonly List<OsuDifficultyHitObject> previous;

        public OsuDifficultyHitObject(HitObject hitObject, HitObject lastLastObject, HitObject lastObject, List<OsuDifficultyHitObject> previous, double clockRate)
            : base(hitObject, lastObject, clockRate)
        {
            this.lastLastObject = (OsuHitObject)lastLastObject;
            this.lastObject = (OsuHitObject)lastObject;
            this.previous = previous;

            setDistances(clockRate);

            // Every strain interval is hard capped at the equivalent of 375 BPM streaming speed as a safety measure
            StrainTime = Math.Max(50, DeltaTime);

            setFlowValues();
        }

        private void setDistances(double clockRate)
        {
            // We will scale distances by this factor, so we can assume a uniform CircleSize among beatmaps.
            float scalingFactor = (float)normalized_radius / (float)BaseObject.Radius;

            if (BaseObject.Radius < 30)
            {
                float smallCircleBonus = Math.Min(30 - (float)BaseObject.Radius, 5) / 50;
                scalingFactor *= 1 + smallCircleBonus;
            }

            if (lastObject is Slider lastSlider)
            {
                computeSliderCursorPosition(lastSlider);
                TravelDistance = lastSlider.LazyTravelDistance * scalingFactor;
                TravelTime = lastSlider.LazyTravelTime / clockRate;
            }

            Vector2 lastCursorPosition = getEndCursorPosition(lastObject);

            // Don't need to jump to reach spinners
            if (!(BaseObject is Spinner))
            {
                JumpDistance = (BaseObject.StackedPosition * scalingFactor - lastCursorPosition * scalingFactor).Length;
                DistanceVector = (BaseObject.StackedPosition * scalingFactor - lastCursorPosition * scalingFactor);
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
                    progress %= 1;

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
                }
            });

            // Skip the head circle
            var scoringTimes = slider.NestedHitObjects.Skip(1).Select(t => t.StartTime);
            foreach (var time in scoringTimes)
                computeVertex(time);
        }

        private Vector2 getEndCursorPosition(OsuHitObject hitObject)
        {
            Vector2 pos = hitObject.StackedPosition;

            if (hitObject is Slider slider)
            {
                computeSliderCursorPosition(slider);
                pos = slider.LazyEndPosition ?? pos;
            }

            return pos;
        }


        private void setFlowValues()
        {
            BaseFlow = calculateBaseFlow();
            Flow = calculateFlow();
        }

        private double calculateBaseFlow()
        {
            if (previous.Count == 0 || Utils.IsRatioEqualLess(0.667, StrainTime, previous[0].StrainTime))
                return calculateSpeedFlow() * calculateDistanceFlow(); // No angle checks for the first actual note of the stream.

            if (Utils.IsRoughlyEqual(StrainTime, previous[0].StrainTime))
                return calculateSpeedFlow() * calculateDistanceFlow(calculateAngleScalingFactor(Angle));

            return 0;
        }

        private double calculateSpeedFlow()
        {
            // Sine curve transition from 0 to 1 starting at 90 BPM, reaching 1 at 90 + 30 = 120 BPM.
            return Utils.TransitionToTrue(streamBpm, 90, 30);
        }

        private double calculateDistanceFlow(double angleScalingFactor = 1)
        {
            double distanceOffset = (Math.Tanh((streamBpm - 140) / 20) + 2) * normalized_radius;
            return Utils.TransitionToFalse(JumpDistance, distanceOffset * angleScalingFactor, distanceOffset);
        }

        private double calculateAngleScalingFactor(double? angle)
        {
            if (!Utils.IsNullOrNaN(angle))
            {
                double angleScalingFactor = (-Math.Sin(Math.Cos(angle.Value) * Math.PI / 2) + 3) / 4;
                return angleScalingFactor + (1 - angleScalingFactor) * previous[0].AngleLeniency;
            }
            else
                return 0.5;
        }

        private double calculateFlow()
        {
            if (previous.Count == 0)
                return BaseFlow;

            // No angle check and a larger distance is allowed if the speed matches the previous notes, and those were flowy without a question.
            // (streamjumps, sharp turns)
            double irregularFlow = calculateIrregularFlow();

            // The next note will have lenient angle checks after a note with irregular flow.
            // (the stream section after the streamjump can take any direction too)
            AngleLeniency = (1 - BaseFlow) * irregularFlow;

            return Math.Max(BaseFlow, irregularFlow);
        }

        private double calculateIrregularFlow()
        {
            double irregularFlow = calculateExtendedDistanceFlow();
            foreach (var previousObject in previous.Take(2))
            {
                if (Utils.IsRoughlyEqual(StrainTime, previousObject.StrainTime))
                    irregularFlow *= previousObject.BaseFlow;
                else
                    irregularFlow = 0;
            }

            return irregularFlow;
        }

        private double calculateExtendedDistanceFlow()
        {
            double distanceOffset = (Math.Tanh((streamBpm - 140) / 20) * 1.75 + 2.75) * normalized_radius;
            return Utils.TransitionToFalse(JumpDistance, distanceOffset, distanceOffset);
        }

        private double streamBpm => 15000 / StrainTime;
    }
}
