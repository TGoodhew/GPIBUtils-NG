using System;
using System.Linq;
using GpibUtils.Instruments.Plotters;
using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Plotters.Tests
{
    /// <summary>Drives the <see cref="HpPlotter"/> driver against a simulated HP pen plotter.</summary>
    public class HpPlotterTests
    {
        private static (HpPlotter driver, HpPlotterSimulatedDevice sim, IInstrumentSession session) Bench(
            HpPlotterModel model = HpPlotterModel.Hp7090A)
        {
            var provider = new SimulatedGpibProvider();
            var sim = new HpPlotterSimulatedDevice();
            provider.Add(HpPlotter.DefaultResource, sim.Instrument);
            var session = provider.Open(HpPlotter.DefaultResource);
            return (new HpPlotter(session, model), sim, session);
        }

        [Fact]
        public void Is_a_plotter()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.IsAssignableFrom<IPlotter>(driver);
        }

        [Fact]
        public void Default_address_is_six()
        {
            Assert.Equal("GPIB0::6::INSTR", HpPlotter.DefaultResource);
        }

        [Fact]
        public void Identify_uses_oi_query()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.ModelId = "7550A";
                Assert.Equal("7550A", driver.Identify());
                Assert.Contains("OI;", driver.History);
            }
        }

        [Fact]
        public void Initialize_clears_and_inits()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Initialize();
                Assert.Contains("IN;", driver.History);
                Assert.Contains("IN;", sim.Commands);
            }
        }

        [Fact]
        public void Pen_select_up_down_and_move()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.SelectPen(2);
                driver.PenDown();
                driver.MoveTo(1000, 2000);
                Assert.Equal(2, sim.SelectedPen);
                Assert.True(sim.PenDown);
                Assert.Equal(1000, sim.X);
                Assert.Equal(2000, sim.Y);
                Assert.Contains("SP2;", driver.History);
                Assert.Contains("PA1000,2000;", driver.History);
            }
        }

        [Fact]
        public void Relative_move_accumulates()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.MoveTo(100, 100);
                driver.MoveBy(50, -20);
                Assert.Equal(150, sim.X);
                Assert.Equal(80, sim.Y);
                Assert.Contains("PR50,-20;", driver.History);
            }
        }

        [Fact]
        public void Line_pens_up_and_down_around_the_stroke()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Line(0, 0, 500, 500);
                Assert.Equal(new[] { "PU;", "PA0,0;", "PD;", "PA500,500;", "PU;" }, driver.History.ToArray());
                Assert.False(sim.PenDown);   // pen left up at the end
                Assert.Equal(500, sim.X);
            }
        }

        [Fact]
        public void Label_terminates_with_etx()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.Label("HELLO");
                Assert.Equal("HELLO", sim.LastLabel);
                Assert.Equal("LB" + "HELLO" + (char)3, Assert.Single(driver.History));
            }
        }

        [Fact]
        public void Plot_hpgl_streams_instructions_individually()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                driver.PlotHpgl("IN;SP1;PA0,0;PD;PA1000,0;PU;");
                Assert.Equal(new[] { "IN;", "SP1;", "PA0,0;", "PD;", "PA1000,0;", "PU;" }, driver.History.ToArray());
                Assert.Equal(1, sim.SelectedPen);
                Assert.Equal(1000, sim.X);
            }
        }

        [Fact]
        public void Plot_hpgl_keeps_labels_whole()
        {
            var parts = HpPlotter.SplitInstructions("PA0,0;LBHi there;more" + (char)3 + "SP2;").ToArray();
            Assert.Equal("PA0,0;", parts[0]);
            Assert.Equal("LBHi there;more" + (char)3, parts[1]);   // label kept whole through the embedded ';'
            Assert.Equal("SP2;", parts[2]);
        }

        [Fact]
        public void Auto_feed_only_on_7550a()
        {
            var (a, _, sa) = Bench(HpPlotterModel.Hp7090A);
            using (sa) Assert.False(a.AutoFeed);
            var (b, _, sb) = Bench(HpPlotterModel.Hp7550A);
            using (sb) Assert.True(b.AutoFeed);
        }

        [Fact]
        public void Advance_page_on_7550a_sends_pg()
        {
            var (driver, sim, session) = Bench(HpPlotterModel.Hp7550A);
            using (session)
            {
                driver.AdvancePage();
                Assert.Equal(1, sim.PageCount);
                Assert.Contains("PG;", driver.History);
            }
        }

        [Fact]
        public void Advance_page_throws_on_manual_feed_models()
        {
            var (driver, _, session) = Bench(HpPlotterModel.Hp7090A);
            using (session)
                Assert.Throws<InvalidOperationException>(() => driver.AdvancePage());
        }

        [Fact]
        public void Output_window_and_scaling_points_parse()
        {
            var (driver, sim, session) = Bench();
            using (session)
            {
                sim.HardClipWindow = new[] { 0, 0, 10300, 7650 };
                sim.ScalingPoints = new[] { 250, 279, 10250, 7371 };
                Assert.Equal(new[] { 0, 0, 10300, 7650 }, driver.OutputWindow());
                Assert.Equal(new[] { 250, 279, 10250, 7371 }, driver.OutputScalingPoints());
            }
        }

        [Fact]
        public void Pen_out_of_range_throws()
        {
            var (driver, _, session) = Bench();
            using (session)
                Assert.Throws<ArgumentOutOfRangeException>(() => driver.SelectPen(9));
        }

        [Fact]
        public void Render_preview_produces_a_png()
        {
            byte[] png = HpPlotter.RenderPreview("IN;SP1;PU0,0;PD1000,0,1000,1000,0,1000,0,0;PU;");
            Assert.NotNull(png);
            Assert.True(png.Length > 8);
            // PNG magic number.
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray());
        }
    }
}
