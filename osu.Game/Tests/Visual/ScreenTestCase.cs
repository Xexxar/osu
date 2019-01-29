﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Screens;
using osu.Game.Screens;

namespace osu.Game.Tests.Visual
{
    /// <summary>
    /// A test case which can be used to test a screen (that relies on OnEntering being called to execute startup instructions).
    /// </summary>
    public abstract class ScreenTestCase : OsuTestCase
    {
        private readonly TestOsuScreen baseScreen;

        protected ScreenTestCase()
        {
            Add(baseScreen = new TestOsuScreen());
        }

        protected void LoadScreen(OsuScreen screen) => baseScreen.LoadScreen(screen);

        public class TestOsuScreen : OsuScreen
        {
            private OsuScreen nextScreen;

            public void LoadScreen(OsuScreen screen) => Schedule(() =>
            {
                nextScreen = screen;

                if (this.IsCurrentScreen())
                {
                   this.Push(screen);
                    nextScreen = null;
                }
                else
                    this.MakeCurrent();
            });

            public override void OnResuming(IScreen last)
            {
                base.OnResuming(last);
                if (nextScreen != null)
                    LoadScreen(nextScreen);
            }
        }
    }
}
