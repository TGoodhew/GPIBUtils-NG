using GpibUtils.Visa;
using GpibUtils.Visa.Simulation;
using Xunit;

namespace GpibUtils.Instruments.Counters.Tests
{
    /// <summary>The three legacy HP mnemonic microwave counters share <see cref="ILegacyFrequencyCounter"/>
    /// (issue #93) and are NOT <see cref="IFrequencyCounter"/> (that shape fits the SCPI 53131A).</summary>
    public class LegacyFrequencyCounterTests
    {
        private static IInstrumentSession Session(string resource, string reading)
        {
            var provider = new SimulatedGpibProvider();
            provider.Add(resource, new SimulatedInstrument { Responder = _ => reading });
            return provider.Open(resource);
        }

        [Fact]
        public void All_three_implement_legacy_interface_and_read_uniformly()
        {
            using (var s1 = Session(Hp5342A.DefaultResource, "1.0000000E+10"))
            using (var s2 = Session(Hp5343A.DefaultResource, " F  10000.000000 E 06"))
            using (var s3 = Session(Hp5351A.DefaultResource, "1.0000000E+10"))
            {
                ILegacyFrequencyCounter a = new Hp5342A(s1);
                ILegacyFrequencyCounter b = new Hp5343A(s2);
                ILegacyFrequencyCounter c = new Hp5351A(s3);

                foreach (var counter in new[] { a, b, c })
                {
                    Assert.False(counter is IFrequencyCounter);
                    Assert.Contains("Microwave Frequency Counter", counter.Identify());
                    Assert.Equal(1.0e10, counter.ReadFrequency(), 0);
                }
            }
        }
    }
}
