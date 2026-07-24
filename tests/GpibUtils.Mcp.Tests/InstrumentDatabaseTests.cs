using System;
using System.IO;
using System.Linq;
using GpibUtils.Mcp.Instruments;
using Xunit;

namespace GpibUtils.Mcp.Tests
{
    public class InstrumentDatabaseTests
    {
        private static InstrumentDefinition Def(string model, string matchRegex) =>
            new InstrumentDefinition { Model = model, Identity = new IdentitySpec { MatchRegex = matchRegex } };

        [Fact]
        public void MatchIdentity_logs_and_skips_a_malformed_regex_instead_of_swallowing_it()
        {
            var db = InstrumentDatabase.FromDefinitions(new[]
            {
                Def("good", "HP.*3458A"),
                Def("bad", "HP(3458A"),   // unbalanced '(' -> ArgumentException at match time
            });

            var previousErr = Console.Error;
            var captured = new StringWriter();
            Console.SetError(captured);
            try
            {
                var matches = db.MatchIdentity("HP,3458A,0,1").Select(d => d.Model).ToList();

                Assert.Contains("good", matches);          // the valid definition still matches
                Assert.DoesNotContain("bad", matches);     // the malformed one is skipped, not thrown
            }
            finally { Console.SetError(previousErr); }

            // ...and the failure is reported rather than silently turned into a non-match.
            Assert.Contains("Invalid identity MatchRegex", captured.ToString());
            Assert.Contains("bad", captured.ToString());
        }

        [Fact]
        public void MatchIdentity_returns_definitions_whose_pattern_matches()
        {
            var db = InstrumentDatabase.FromDefinitions(new[] { Def("dmm", "3458A"), Def("counter", "53131A") });
            var matches = db.MatchIdentity("HEWLETT-PACKARD,3458A,0,9").Select(d => d.Model).ToList();
            Assert.Equal(new[] { "dmm" }, matches);
        }
    }
}
