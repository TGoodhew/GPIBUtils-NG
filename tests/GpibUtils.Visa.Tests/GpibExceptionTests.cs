using System;
using GpibUtils.Visa;
using Xunit;

namespace GpibUtils.Visa.Tests
{
    public class GpibExceptionTests
    {
        [Fact]
        public void Folds_the_decoded_status_into_the_message()
        {
            var status = new GpibStatus("VI_ERROR_TMO", "Timeout expired before the operation completed.",
                unchecked((int)0xBFFF0015));
            var ex = new GpibException("NI-VISA read failed on GPIB0::14::INSTR.", status);

            // Every presentation path prints ex.Message, so the decoded detail must travel with it.
            Assert.Contains("NI-VISA read failed on GPIB0::14::INSTR.", ex.Message);
            Assert.Contains("VI_ERROR_TMO", ex.Message);
            Assert.Contains("Timeout expired", ex.Message);
        }

        [Fact]
        public void Appends_the_inner_detail_when_the_status_is_undecoded()
        {
            var ex = new GpibException("Failed to open VISA resource 'GPIB0::7::INSTR'.",
                new InvalidOperationException("no listeners on the bus"));

            Assert.Contains("Failed to open VISA resource", ex.Message);
            Assert.Contains("no listeners on the bus", ex.Message);   // vendor detail no longer discarded
        }

        [Fact]
        public void Leaves_a_plain_message_unchanged_when_there_is_no_status_or_inner()
        {
            var ex = new GpibException("plain failure");
            Assert.Equal("plain failure", ex.Message);
        }

        [Fact]
        public void Does_not_duplicate_when_inner_message_equals_base_message()
        {
            var ex = new GpibException("same", new InvalidOperationException("same"));
            Assert.Equal("same", ex.Message);
        }
    }
}
