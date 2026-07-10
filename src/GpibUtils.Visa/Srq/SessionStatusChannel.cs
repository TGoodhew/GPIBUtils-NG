using System;

namespace GpibUtils.Visa.Srq
{
    /// <summary>
    /// Bridges the data-driven <see cref="CompletionWaiter"/> onto a live <see cref="IInstrumentSession"/>,
    /// so any driver can drive an SRQ/serial-poll completion over the shared vendor-neutral transport
    /// (NI-VISA, Keysight, Prologix/AR488, or the simulator) with no per-device SRQ code.
    ///
    /// <para><b>HP-IB bus-extender note.</b> On the reference bench the instruments sit behind HP-IB bus
    /// extenders (HP 37204A or similar), which add longer, variable turnaround to every serial poll. The
    /// completion flow tolerates this — timeouts and <see cref="StatusModel.BusyConfirmMs"/> are the knobs;
    /// keep them generous. Do not shorten the poll interval to compensate; the latency is on the link, not
    /// the instrument.</para>
    /// </summary>
    public sealed class SessionStatusChannel : IStatusChannel
    {
        private readonly IInstrumentSession _session;

        public SessionStatusChannel(IInstrumentSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>The session this channel writes to and polls.</summary>
        public IInstrumentSession Session => _session;

        public void Send(string command) => _session.Write(command);

        public int SerialPoll() => _session.SerialPoll().Value;
    }
}
