namespace GpibUtils.Visa.Srq
{
    /// <summary>
    /// The minimal instrument I/O the <see cref="CompletionWaiter"/> needs: send a command, and
    /// serial-poll the status byte. Abstracted so the waiter has no dependency on a concrete transport
    /// and can be driven headlessly by a simulated instrument for testing (no hardware). The live-hardware
    /// bridge is <see cref="SessionStatusChannel"/>, which adapts an <see cref="IInstrumentSession"/>.
    /// </summary>
    public interface IStatusChannel
    {
        /// <summary>Writes one or more commands to the instrument (e.g. the enable mask, the arm string).</summary>
        void Send(string command);

        /// <summary>Serial-polls and returns the status byte (0-255). On most instruments this clears the latched bits.</summary>
        int SerialPoll();

        /// <summary>
        /// Sends a query and returns the raw textual reply. Used only when a <see cref="StatusModel"/>
        /// reads its status byte via a device command (e.g. a pre-488.2 HP analyzer's <c>STB?</c>) instead
        /// of a hardware serial poll - see <see cref="StatusModel.StatusQuery"/>. Implementations that only
        /// ever serial-poll may leave this unsupported.
        /// </summary>
        string Query(string command);
    }
}
