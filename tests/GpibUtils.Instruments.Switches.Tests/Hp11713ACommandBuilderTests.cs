using System.Collections.Generic;
using GpibUtils.Instruments.Switches;
using Xunit;

namespace GpibUtils.Instruments.Switches.Tests
{
    public class Hp11713ACommandBuilderTests
    {
        private static readonly List<Section> TwoFour = new List<Section>
        {
            new Section(1, 2), new Section(2, 4)
        };

        [Fact]
        public void Solve_returns_empty_for_zero()
        {
            var engaged = Hp11713ACommandBuilder.Solve(TwoFour, 0);
            Assert.NotNull(engaged);
            Assert.Empty(engaged);
        }

        [Fact]
        public void Solve_finds_reachable_target()
        {
            Assert.Equal(new[] { 1 }, Hp11713ACommandBuilder.Solve(TwoFour, 2));
            Assert.Equal(new[] { 2 }, Hp11713ACommandBuilder.Solve(TwoFour, 4));
            Assert.Equal(new[] { 1, 2 }, Hp11713ACommandBuilder.Solve(TwoFour, 6));
        }

        [Fact]
        public void Solve_returns_null_for_unreachable_target()
        {
            Assert.Null(Hp11713ACommandBuilder.Solve(TwoFour, 3));
            Assert.Null(Hp11713ACommandBuilder.Solve(TwoFour, 7));
        }

        [Fact]
        public void Solve_prefers_fewest_sections()
        {
            // 1 + 3 == 4 (two sections) vs a single 4 dB section — the single section wins.
            var sections = new List<Section> { new Section(1, 1), new Section(2, 3), new Section(3, 4) };
            Assert.Equal(new[] { 3 }, Hp11713ACommandBuilder.Solve(sections, 4));
        }

        [Fact]
        public void BuildString_splits_engaged_and_bypassed_in_digit_order()
        {
            var config = AttenuatorConfig.Default();
            Assert.Equal("A13B24", Hp11713ACommandBuilder.BuildString(config.X, new HashSet<int> { 1, 3 }));
            Assert.Equal("B12345678", Hp11713ACommandBuilder.BuildString(config.AllSections, new HashSet<int>()));
            Assert.Equal("A12345678",
                Hp11713ACommandBuilder.BuildString(config.AllSections, new HashSet<int> { 1, 2, 3, 4, 5, 6, 7, 8 }));
        }

        [Theory]
        [InlineData(true, "A9")]
        [InlineData(false, "B9")]
        public void Switch9_builds_expected(bool on, string expected) =>
            Assert.Equal(expected, Hp11713ACommandBuilder.Switch9(on));

        [Theory]
        [InlineData(true, "A0")]
        [InlineData(false, "B0")]
        public void Switch0_builds_expected(bool on, string expected) =>
            Assert.Equal(expected, Hp11713ACommandBuilder.Switch0(on));

        [Theory]
        [InlineData("A13B24", true)]
        [InlineData("A9 B0", true)]      // whitespace is allowed
        [InlineData("a1b2", true)]       // lowercase is allowed
        [InlineData("X1", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        public void IsValidDataString_validates_characters(string command, bool expected) =>
            Assert.Equal(expected, Hp11713ACommandBuilder.IsValidDataString(command));
    }

    public class AttenuatorConfigTests
    {
        [Fact]
        public void Default_is_8494_on_X_8496_on_Y_giving_0_to_121()
        {
            var c = AttenuatorConfig.Default();
            Assert.Equal(121, c.MaxDecibels);
            Assert.Equal(new[] { 1, 2, 4, 4 }, System.Linq.Enumerable.Select(c.X, s => s.Decibels));
            Assert.Equal(new[] { 10, 20, 40, 40 }, System.Linq.Enumerable.Select(c.Y, s => s.Decibels));
        }

        [Fact]
        public void Swapped_puts_coarse_on_X()
        {
            var c = AttenuatorConfig.Swapped();
            Assert.Equal(new[] { 10, 20, 40, 40 }, System.Linq.Enumerable.Select(c.X, s => s.Decibels));
            Assert.Equal(121, c.MaxDecibels);
        }
    }
}
